using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using qlthuvien_vip.Models;
using qlthuvien_vip.Models.DTOs;

namespace qlthuvien_vip.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public PaymentController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private long GetCurrentCustomerId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (claim != null && long.TryParse(claim.Value, out var customerId))
            {
                return customerId;
            }
            throw new UnauthorizedAccessException("Không thể xác định thông tin khách hàng từ JWT Token.");
        }

        /// <summary>
        /// Khởi tạo thanh toán:
        /// - COD: Chuyển trạng thái sang PENDING_CONFIRMATION, xóa hạn 15 phút
        /// - QR_TRANSFER: Sinh link mã VietQR thực tế chuẩn Napas 24/7 kèm số tài khoản, số tiền và nội dung đơn
        /// - ONLINE / VNPAY: Sinh URL cổng thanh toán thực tế kèm chữ ký số bảo mật HMAC
        /// </summary>
        [HttpPost("initiate")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<PaymentResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> InitiatePayment([FromBody] InitiatePaymentDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Dữ liệu gửi lên không hợp lệ. Các phương thức hỗ trợ: COD, QR_TRANSFER, ONLINE, VNPAY."));
            }

            long customerId = GetCurrentCustomerId();

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId && o.CustomerId == customerId);

            if (order == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy đơn hàng có ID = {dto.OrderId}."));
            }

            string method = dto.PaymentMethod.Trim().ToUpper();

            // Nếu là COD: Đơn chuyển sang PENDING_CONFIRMATION và xóa hạn ExpiresAt (không bị hủy sau 15 phút)
            if (method == "COD")
            {
                order.OrderStatus = "PENDING_CONFIRMATION";
                order.ExpiresAt = null;
                order.PaymentMethod = "COD";
                order.UpdatedAt = DateTime.UtcNow;

                var existingCodPayment = await _context.PaymentTransactions
                    .FirstOrDefaultAsync(p => p.OrderId == order.OrderId && p.PaymentMethod == "COD");

                if (existingCodPayment == null)
                {
                    existingCodPayment = new PaymentTransaction
                    {
                        OrderId = order.OrderId,
                        PaymentMethod = "COD",
                        Amount = order.TotalAmount,
                        PaymentStatus = "PENDING",
                        TransactionCode = $"COD-{order.OrderId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                        CreatedAt = DateTime.UtcNow
                    };
                    await _context.PaymentTransactions.AddAsync(existingCodPayment);
                }

                await _context.SaveChangesAsync();

                var codResponse = new PaymentResponseDto
                {
                    PaymentId = existingCodPayment.PaymentId,
                    OrderId = order.OrderId,
                    PaymentMethod = "COD",
                    Amount = order.TotalAmount,
                    PaymentStatus = "PENDING",
                    TransactionCode = existingCodPayment.TransactionCode ?? string.Empty,
                    PaymentUrl = string.Empty,
                    Note = "Đơn hàng COD đã được xác nhận. Quý khách thanh toán tiền mặt khi shipper giao hàng.",
                    CreatedAt = existingCodPayment.CreatedAt
                };

                return Ok(ApiResponse<PaymentResponseDto>.SuccessResponse(codResponse, "Đã chọn thanh toán tiền mặt khi nhận hàng (COD). Đơn hàng không bị giới hạn thời gian 15 phút."));
            }

            if (order.OrderStatus != "PENDING_PAYMENT")
            {
                return BadRequest(ApiResponse<string>.ErrorResponse($"Đơn hàng này không ở trạng thái chờ thanh toán. Trạng thái hiện tại: {order.OrderStatus}."));
            }

            // Sinh mã giao dịch duy nhất
            string transactionCode = $"TXN-{order.OrderId}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

            var payment = new PaymentTransaction
            {
                OrderId = order.OrderId,
                PaymentMethod = method,
                Amount = order.TotalAmount,
                PaymentStatus = "PENDING",
                TransactionCode = transactionCode,
                CreatedAt = DateTime.UtcNow
            };

            await _context.PaymentTransactions.AddAsync(payment);
            await _context.SaveChangesAsync();

            string paymentUrl = string.Empty;
            string qrUrl = string.Empty;
            string bankInfo = string.Empty;

            var paymentConfig = _configuration.GetSection("Payment");
            var vietQrBankBin = paymentConfig["VietQr:BankBin"] ?? "970436";
            var vietQrAccount = paymentConfig["VietQr:AccountNumber"] ?? "123456789";
            var vietQrName = paymentConfig["VietQr:AccountName"] ?? "BOOKSTORE STORE";

            if (method == "QR_TRANSFER")
            {
                // Chuẩn VietQR tĩnh/động: https://img.vietqr.io/image/<BANK_BIN>-<ACCOUNT_NO>-compact2.png?amount=<AMOUNT>&addInfo=<MEMO>&accountName=<NAME>
                string memo = Uri.EscapeDataString($"THANH TOAN DH{order.OrderId}");
                qrUrl = $"https://img.vietqr.io/image/{vietQrBankBin}-{vietQrAccount}-compact2.png?amount={(long)order.TotalAmount}&addInfo={memo}&accountName={Uri.EscapeDataString(vietQrName)}";
                paymentUrl = qrUrl;
                bankInfo = $"Ngân hàng thụ hưởng: Vietcombank (Mã BIN: {vietQrBankBin}) | STK: {vietQrAccount} | Chủ TK: {vietQrName} | Nội dung CK: THANH TOAN DH{order.OrderId}";
            }
            else if (method == "ONLINE" || method == "VNPAY")
            {
                // Tạo URL thanh toán VNPAY thực tế kèm chữ ký số HMAC-SHA512
                paymentUrl = CreateVnpayPaymentUrl(order, transactionCode);
                qrUrl = paymentUrl;
            }

            var responseDto = new PaymentResponseDto
            {
                PaymentId = payment.PaymentId,
                OrderId = payment.OrderId,
                PaymentMethod = payment.PaymentMethod,
                Amount = payment.Amount,
                PaymentStatus = payment.PaymentStatus,
                TransactionCode = payment.TransactionCode,
                PaymentUrl = paymentUrl,
                QrCodeUrl = qrUrl,
                BankInfo = bankInfo,
                Note = "Vui lòng hoàn tất thanh toán trước khi đơn hàng hết hạn 15 phút.",
                CreatedAt = payment.CreatedAt
            };

            return Ok(ApiResponse<PaymentResponseDto>.SuccessResponse(responseDto, $"Khởi tạo thanh toán đơn hàng #{order.OrderId} thành công! Tổng tiền: {order.TotalAmount:#,##0} VNĐ."));
        }

        /// <summary>
        /// Webhook / IPN Callback nhận kết quả thanh toán từ Cổng thanh toán
        /// - Bắt buộc xác minh nguồn gửi và Chữ ký số (HMAC-SHA256)
        /// - Kiểm tra tính toàn vẹn thông tin giao dịch (Mã đơn, Số tiền, Trạng thái)
        /// - Hỗ trợ Idempotency chống trừ/hoàn tồn nhiều lần
        /// </summary>
        [HttpPost("webhook")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<InvoiceResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ProcessWebhook([FromBody] PaymentWebhookDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Dữ liệu Webhook/IPN không hợp lệ."));
            }

            // 1. Xác minh chữ ký số HMAC-SHA256
            string secretKey = _configuration["Payment:WebhookSecret"] 
                ?? "BookstorePaymentWebhookSecretKey2026_SecureHMACSHA256";

            // Định dạng chuỗi dữ liệu ký: OrderId|TransactionCode|Amount|Status
            string rawData = $"{dto.OrderId}|{dto.TransactionCode}|{dto.Amount.ToString("0.00", CultureInfo.InvariantCulture)}|{dto.Status.ToUpper()}";
            string computedSignature = ComputeHmacSha256(rawData, secretKey);

            if (!string.Equals(computedSignature, dto.Signature, StringComparison.OrdinalIgnoreCase))
            {
                // Thử thêm định dạng số nguyên không có số thập phân phòng khi cổng gửi số nguyên
                string rawDataInt = $"{dto.OrderId}|{dto.TransactionCode}|{(long)dto.Amount}|{dto.Status.ToUpper()}";
                string computedSignatureInt = ComputeHmacSha256(rawDataInt, secretKey);

                if (!string.Equals(computedSignatureInt, dto.Signature, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(ApiResponse<string>.ErrorResponse("Xác minh chữ ký số thất bại! Nguồn gửi hoặc dữ liệu giao dịch không hợp lệ."));
                }
            }

            // 2. Tìm bản ghi giao dịch thanh toán
            var payment = await _context.PaymentTransactions
                .FirstOrDefaultAsync(p => p.TransactionCode == dto.TransactionCode && p.OrderId == dto.OrderId);

            if (payment == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy bản ghi giao dịch phù hợp với OrderId = {dto.OrderId} và TransactionCode = {dto.TransactionCode}."));
            }

            // 3. Kiểm tra Idempotency (Chống xử lý trùng lặp khi Webhook gửi nhiều lần)
            if (payment.PaymentStatus == "SUCCESS")
            {
                var existingInvoice = await _context.Invoices.FirstOrDefaultAsync(i => i.OrderId == payment.OrderId);
                var invoiceDto = existingInvoice != null ? MapToInvoiceDto(existingInvoice) : null!;
                return Ok(ApiResponse<InvoiceResponseDto?>.SuccessResponse(invoiceDto, "Thông báo: Giao dịch này đã được ghi nhận thanh toán thành công trước đó (Idempotent call)."));
            }

            // 4. Tìm đơn hàng tương ứng
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == payment.OrderId);

            if (order == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy đơn hàng ID = {payment.OrderId}."));
            }

            // Xác minh số tiền giao dịch phải khớp chính xác với số tiền đơn hàng
            if (Math.Abs(order.TotalAmount - dto.Amount) > 0.01m)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse($"Số tiền thanh toán ({dto.Amount:#,##0}) không khớp với tổng tiền đơn hàng ({order.TotalAmount:#,##0})."));
            }

            // 5. Bắt đầu DB Transaction với mức cô lập Serializable để đảm bảo toàn vẹn tồn kho
            using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                if (string.Equals(dto.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                {
                    // Nếu đơn hàng đã bị hủy hoặc hết hạn trước khi webhook tới
                    if (order.OrderStatus == "CANCELLED" || order.OrderStatus == "EXPIRED")
                    {
                        payment.PaymentStatus = "PAYMENT_RECEIVED_ON_CANCELLED_ORDER";
                        payment.ResponseCode = dto.ResponseCode ?? "00";
                        payment.CompletedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        await dbTransaction.CommitAsync();

                        return Ok(ApiResponse<string>.SuccessResponse("Thanh toán ghi nhận thành công nhưng đơn hàng đã bị hủy hoặc hết hạn trước đó. Hệ thống đã đánh dấu cần nhân viên đối soát và hoàn tiền."));
                    }

                    // Giao dịch THÀNH CÔNG
                    payment.PaymentStatus = "SUCCESS";
                    payment.ResponseCode = dto.ResponseCode ?? "00";
                    payment.CompletedAt = DateTime.UtcNow;

                    order.OrderStatus = "PAID";
                    order.ExpiresAt = null; // Xóa hạn chờ thanh toán
                    order.UpdatedAt = DateTime.UtcNow;

                    // Trừ tồn kho chính thức và giải phóng khóa tạm
                    foreach (var item in order.OrderItems)
                    {
                        if (item.Product != null)
                        {
                            item.Product.StockQuantity = Math.Max(0, item.Product.StockQuantity - item.Quantity);
                            item.Product.HoldQuantity = Math.Max(0, item.Product.HoldQuantity - item.Quantity);
                            item.Product.UpdatedAt = DateTime.UtcNow;
                        }
                    }

                    // Khởi tạo Hóa đơn điện tử (e-Invoice)
                    string invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{order.OrderId:D6}";
                    decimal taxAmount = Math.Round(order.TotalAmount * 0.08m, 2); // Thuế VAT 8%

                    var invoice = new Invoice
                    {
                        OrderId = order.OrderId,
                        PaymentId = payment.PaymentId,
                        InvoiceNumber = invoiceNumber,
                        TotalAmount = order.TotalAmount,
                        TaxAmount = taxAmount,
                        Status = "ISSUED",
                        PdfUrl = $"https://bookstore.vn/invoices/download/{invoiceNumber}.pdf?txnid={payment.TransactionCode}",
                        InvoiceDate = DateTime.UtcNow
                    };

                    await _context.Invoices.AddAsync(invoice);
                    await _context.SaveChangesAsync();
                    await dbTransaction.CommitAsync();

                    return Ok(ApiResponse<InvoiceResponseDto>.SuccessResponse(MapToInvoiceDto(invoice), "Xác thực chữ ký hợp lệ! Đã xác nhận thanh toán thành công, trừ tồn kho và xuất hóa đơn điện tử."));
                }
                else
                {
                    // Giao dịch THẤT BẠI
                    payment.PaymentStatus = "FAILED";
                    payment.ResponseCode = dto.ResponseCode ?? "99";
                    payment.ErrorMessage = dto.ErrorMessage ?? "Giao dịch thanh toán bị từ chối hoặc khách hàng hủy.";
                    payment.CompletedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await dbTransaction.CommitAsync();

                    return Ok(ApiResponse<string>.SuccessResponse("Đã ghi nhận giao dịch thanh toán thất bại."));
                }
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                return BadRequest(ApiResponse<string>.ErrorResponse($"Lỗi trong quá trình xử lý Webhook: {ex.Message}"));
            }
        }

        /// <summary>
        /// API kiểm tra trạng thái thanh toán của đơn hàng (Không giả lập tự động thành công)
        /// </summary>
        [HttpGet("status/{orderId}")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<PaymentResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CheckPaymentStatus(long orderId)
        {
            long customerId = GetCurrentCustomerId();

            var order = await _context.Orders
                .Include(o => o.PaymentTransactions)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customerId);

            if (order == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy đơn hàng có ID = {orderId}."));
            }

            var latestPayment = order.PaymentTransactions.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
            if (latestPayment == null)
            {
                return Ok(ApiResponse<string>.SuccessResponse("Đơn hàng chưa có giao dịch thanh toán nào được khởi tạo."));
            }

            var responseDto = new PaymentResponseDto
            {
                PaymentId = latestPayment.PaymentId,
                OrderId = latestPayment.OrderId,
                PaymentMethod = latestPayment.PaymentMethod,
                Amount = latestPayment.Amount,
                PaymentStatus = latestPayment.PaymentStatus,
                TransactionCode = latestPayment.TransactionCode ?? string.Empty,
                CreatedAt = latestPayment.CreatedAt,
                Note = order.OrderStatus == "PAID" 
                    ? "Đơn hàng đã được thanh toán thành công!" 
                    : $"Trạng thái thanh toán hiện tại: {latestPayment.PaymentStatus}"
            };

            return Ok(ApiResponse<PaymentResponseDto>.SuccessResponse(responseDto, "Lấy thông tin trạng thái thanh toán thành công."));
        }

        /// <summary>
        /// Endpoint hỗ trợ sinh chữ ký HMAC-SHA256 phục vụ kiểm thử Webhook và tích hợp Cổng thanh toán
        /// </summary>
        [HttpPost("generate-test-signature")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public IActionResult GenerateWebhookSignature([FromBody] GenerateSignatureRequestDto dto)
        {
            string secretKey = _configuration["Payment:WebhookSecret"] 
                ?? "BookstorePaymentWebhookSecretKey2026_SecureHMACSHA256";

            string rawData = $"{dto.OrderId}|{dto.TransactionCode}|{dto.Amount.ToString("0.00", CultureInfo.InvariantCulture)}|{dto.Status.ToUpper()}";
            string signature = ComputeHmacSha256(rawData, secretKey);

            var result = new
            {
                RawData = rawData,
                Signature = signature,
                SampleWebhookPayload = new
                {
                    OrderId = dto.OrderId,
                    TransactionCode = dto.TransactionCode,
                    Amount = dto.Amount,
                    Status = dto.Status.ToUpper(),
                    ResponseCode = "00",
                    Signature = signature
                }
            };

            return Ok(ApiResponse<object>.SuccessResponse(result, "Đã tạo chữ ký HMAC-SHA256 thành công phục vụ tích hợp Webhook!"));
        }

        private string CreateVnpayPaymentUrl(Order order, string txnRef)
        {
            var vnpConfig = _configuration.GetSection("Payment:Vnpay");
            string tmnCode = vnpConfig["TmnCode"] ?? "BOOKSTORE01";
            string hashSecret = vnpConfig["HashSecret"] ?? "VNPAYSECRETKEY2026SECUREHMAC512XYZABC1234567890";
            string baseUrl = vnpConfig["BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
            string returnUrl = vnpConfig["ReturnUrl"] ?? "http://localhost:5173/payment-result";

            long amountInVndUnits = (long)(order.TotalAmount * 100); // VNPAY nhân 100

            var queryParams = new SortedDictionary<string, string>
            {
                { "vnp_Version", "2.1.0" },
                { "vnp_Command", "pay" },
                { "vnp_TmnCode", tmnCode },
                { "vnp_Amount", amountInVndUnits.ToString() },
                { "vnp_CreateDate", DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss") },
                { "vnp_CurrCode", "VND" },
                { "vnp_IpAddr", "127.0.0.1" },
                { "vnp_Locale", "vn" },
                { "vnp_OrderInfo", $"Thanh toan don hang #{order.OrderId}" },
                { "vnp_OrderType", "other" },
                { "vnp_ReturnUrl", returnUrl },
                { "vnp_TxnRef", txnRef }
            };

            var signData = new StringBuilder();
            var queryString = new StringBuilder();
            foreach (var kv in queryParams)
            {
                if (signData.Length > 0)
                {
                    signData.Append('&');
                    queryString.Append('&');
                }
                signData.Append(WebUtility.UrlEncode(kv.Key)).Append('=').Append(WebUtility.UrlEncode(kv.Value));
                queryString.Append(WebUtility.UrlEncode(kv.Key)).Append('=').Append(WebUtility.UrlEncode(kv.Value));
            }

            string secureHash = ComputeHmacSha512(signData.ToString(), hashSecret);
            queryString.Append("&vnp_SecureHash=").Append(secureHash);

            return $"{baseUrl}?{queryString}";
        }

        private static string ComputeHmacSha256(string data, string key)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        private static string ComputeHmacSha512(string data, string key)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        private static InvoiceResponseDto MapToInvoiceDto(Invoice inv)
        {
            return new InvoiceResponseDto
            {
                InvoiceId = inv.InvoiceId,
                OrderId = inv.OrderId,
                PaymentId = inv.PaymentId,
                InvoiceNumber = inv.InvoiceNumber,
                TotalAmount = inv.TotalAmount,
                TaxAmount = inv.TaxAmount ?? 0,
                Status = inv.Status ?? "ISSUED",
                PdfUrl = inv.PdfUrl ?? string.Empty,
                InvoiceDate = inv.InvoiceDate
            };
        }
    }
}
