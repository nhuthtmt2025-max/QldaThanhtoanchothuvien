using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using qlthuvien_vip.Models;
using qlthuvien_vip.Models.DTOs;
using qlthuvien_vip.Services.Interfaces;

namespace qlthuvien_vip.Services.Implementations
{
    public class AiService : IAiService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiService> _logger;

        public AiService(
            AppDbContext context,
            IConfiguration configuration,
            HttpClient httpClient,
            ILogger<AiService> logger)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<AiChatResponseDto> ChatAsync(string message)
        {
            var cleanMessage = message.Trim();
            var lowerMessage = cleanMessage.ToLower();

            // 1. Tìm kiếm sách liên quan từ cơ sở dữ liệu
            var query = _context.Products
                .Where(p => p.IsActive == true)
                .AsNoTracking();

            // Tìm chính xác theo tên, tác giả, thể loại, mô tả
            var matchedProducts = await query
                .Where(p => p.Title.ToLower().Contains(lowerMessage) ||
                           (p.Author != null && p.Author.ToLower().Contains(lowerMessage)) ||
                           (p.Category != null && p.Category.ToLower().Contains(lowerMessage)) ||
                           (p.Description != null && p.Description.ToLower().Contains(lowerMessage)))
                .Take(6)
                .ToListAsync();

            // Nếu chưa thấy, tách từ khóa để tìm mở rộng
            if (!matchedProducts.Any())
            {
                var keywords = lowerMessage
                    .Split(new[] { ' ', ',', '.', ';', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 2)
                    .Distinct()
                    .Take(5)
                    .ToList();

                if (keywords.Any())
                {
                    matchedProducts = await query
                        .Where(p => keywords.Any(k => p.Title.ToLower().Contains(k) ||
                                                      (p.Author != null && p.Author.ToLower().Contains(k)) ||
                                                      (p.Category != null && p.Category.ToLower().Contains(k))))
                        .Take(6)
                        .ToListAsync();
                }
            }

            // Nếu vẫn không có, lấy 5 cuốn sách nổi bật đang có hàng để tư vấn
            if (!matchedProducts.Any())
            {
                matchedProducts = await query
                    .OrderByDescending(p => p.StockQuantity)
                    .Take(5)
                    .ToListAsync();
            }

            var productDtos = matchedProducts.Select(p => new ProductResponseDto
            {
                ProductId = p.ProductId,
                Title = p.Title,
                Author = p.Author,
                Publisher = p.Publisher,
                Isbn = p.Isbn,
                Category = p.Category,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                Description = p.Description,
                ImageUrl = p.ImageUrl,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList();

            // 2. Gọi AI Service sử dụng API Key lưu tập trung tại BE (Gemini hoặc OpenAI)
            string aiResponseText = await CallAiApiAsync(cleanMessage, matchedProducts);

            return new AiChatResponseDto
            {
                Response = aiResponseText,
                Timestamp = DateTime.UtcNow,
                RelatedProducts = productDtos
            };
        }

        private async Task<string> CallAiApiAsync(string userMessage, List<Product> availableBooks)
        {
            // Lấy API key từ cấu hình backend (không để lộ ra client)
            var apiKey = _configuration["Ai:ApiKey"] 
                         ?? _configuration["Ai:GeminiApiKey"] 
                         ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            var provider = _configuration["Ai:Provider"] ?? "Gemini";
            var model = _configuration["Ai:Model"] ?? "gemini-1.5-flash";

            // Xây dựng danh sách sách ngữ cảnh
            var booksContext = string.Join("\n", availableBooks.Select(b =>
                $"- [Mã: {b.ProductId}] '{b.Title}' | Tác giả: {b.Author ?? "Chưa rõ"} | Thể loại: {b.Category ?? "Chung"} | Giá: {b.Price:N0}đ | Tồn kho: {b.StockQuantity} | Tóm tắt: {b.Description}"));

            var prompt = $@"Bạn là trợ lý AI chuyên viên tư vấn sách tận tâm và am hiểu của Nhà sách Bookstore.
Dưới đây là danh sách sách hiện có tại nhà sách:
{booksContext}

Yêu cầu của khách hàng: ""{userMessage}""

Nhiệm vụ của bạn:
1. Trả lời thân thiện, lịch sự, nhiệt tình bằng tiếng Việt.
2. Gợi ý cụ thể các cuốn sách phù hợp nhất từ danh sách trên (nêu rõ tên sách, tác giả, giá bán và lý do cuốn sách này phù hợp với sở thích hoặc nhu cầu của khách hàng).
3. Nếu không có cuốn sách nào khớp hoàn toàn, hãy giới thiệu các cuốn sách nổi bật hiện có trong danh sách trên và đưa ra lời khuyên hữu ích cho người đọc.";

            // Nếu có API key hợp lệ của Gemini
            if (!string.IsNullOrWhiteSpace(apiKey) && apiKey.Length > 15 && provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                    var requestBody = new
                    {
                        contents = new[]
                        {
                            new
                            {
                                parts = new[]
                                {
                                    new { text = prompt }
                                }
                            }
                        },
                        generationConfig = new
                        {
                            temperature = 0.7,
                            maxOutputTokens = 800
                        }
                    };

                    var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                    var httpResponse = await _httpClient.PostAsync(geminiUrl, content);

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var responseJson = await httpResponse.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(responseJson);
                        var text = doc.RootElement
                            .GetProperty("candidates")[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString();

                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text.Trim();
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Gemini API call failed with status code: {StatusCode}", httpResponse.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi gọi external AI API, chuyển sang chế độ tư vấn tích hợp.");
                }
            }

            // Fallback: Chế độ tư vấn thông minh tích hợp (khi chưa cấu hình khóa ngoài hoặc offline)
            return GenerateSmartFallbackResponse(userMessage, availableBooks);
        }

        private string GenerateSmartFallbackResponse(string userMessage, List<Product> books)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Xin chào quý khách! Cảm ơn bạn đã quan tâm đến kho sách của Bookstore về chủ đề \"{userMessage}\".");
            sb.AppendLine();

            if (books.Any())
            {
                sb.AppendLine("Dựa trên tìm kiếm và sở thích của bạn, tôi xin gợi ý những tựa sách rất phù hợp đang có sẵn tại cửa hàng:");
                sb.AppendLine();

                int idx = 1;
                foreach (var book in books)
                {
                    sb.AppendLine($"{idx}. **{book.Title}**");
                    if (!string.IsNullOrWhiteSpace(book.Author))
                        sb.AppendLine($"   - Tác giả: {book.Author}");
                    if (!string.IsNullOrWhiteSpace(book.Category))
                        sb.AppendLine($"   - Thể loại: {book.Category}");
                    sb.AppendLine($"   - Giá ưu đãi: {book.Price:N0} VNĐ (Còn {book.StockQuantity} cuốn)");
                    if (!string.IsNullOrWhiteSpace(book.Description))
                    {
                        var shortDesc = book.Description.Length > 120 
                            ? book.Description.Substring(0, 120) + "..." 
                            : book.Description;
                        sb.AppendLine($"   - Giới thiệu: {shortDesc}");
                    }
                    sb.AppendLine();
                    idx++;
                }

                sb.AppendLine("Bạn có thể thêm trực tiếp các tựa sách trên vào Giỏ hàng hoặc đặt mua ngay hôm nay để nhận ưu đãi vận chuyển tốt nhất nhé!");
            }
            else
            {
                sb.AppendLine("Hiện tại cửa hàng chưa có tựa sách hoàn toàn trùng khớp với từ khóa này. Bạn có thể thử tìm kiếm với tên tác giả, thể loại khác hoặc liên hệ bộ phận hỗ trợ để chúng tôi đặt thêm sách cho bạn!");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
