using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using qlthuvien_vip.Models;
using qlthuvien_vip.Models.DTOs;

namespace qlthuvien_vip.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FavoritesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FavoritesController(AppDbContext context)
        {
            _context = context;
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
        /// Lấy danh sách sách yêu thích (Wishlist) của tài khoản đang đăng nhập
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<FavoriteResponseDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyFavorites()
        {
            long customerId = GetCurrentCustomerId();

            var favorites = await _context.Favorites
                .Include(f => f.Product)
                .Where(f => f.CustomerId == customerId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new FavoriteResponseDto
                {
                    FavoriteId = f.FavoriteId,
                    ProductId = f.ProductId,
                    Title = f.Product.Title,
                    Author = f.Product.Author,
                    Category = f.Product.Category,
                    Price = f.Product.Price,
                    StockQuantity = f.Product.StockQuantity,
                    ImageUrl = f.Product.ImageUrl,
                    IsActive = f.Product.IsActive,
                    CreatedAt = f.CreatedAt
                })
                .ToListAsync();

            return Ok(ApiResponse<IEnumerable<FavoriteResponseDto>>.SuccessResponse(favorites, "Lấy danh sách sách yêu thích thành công."));
        }

        /// <summary>
        /// Kiểm tra xem sản phẩm cụ thể đã nằm trong danh sách yêu thích hay chưa
        /// </summary>
        [HttpGet("check/{productId}")]
        [ProducesResponseType(typeof(ApiResponse<FavoriteCheckDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckFavorite(long productId)
        {
            long customerId = GetCurrentCustomerId();

            bool isFav = await _context.Favorites
                .AnyAsync(f => f.CustomerId == customerId && f.ProductId == productId);

            var result = new FavoriteCheckDto
            {
                ProductId = productId,
                IsFavorite = isFav
            };

            return Ok(ApiResponse<FavoriteCheckDto>.SuccessResponse(result));
        }

        /// <summary>
        /// Thêm sách vào danh sách yêu thích
        /// </summary>
        [HttpPost("{productId}")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddFavorite(long productId)
        {
            long customerId = GetCurrentCustomerId();

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy sản phẩm có ID = {productId}."));
            }

            bool alreadyFavorited = await _context.Favorites
                .AnyAsync(f => f.CustomerId == customerId && f.ProductId == productId);

            if (alreadyFavorited)
            {
                return Ok(ApiResponse<string>.SuccessResponse("Sách này đã có trong danh sách yêu thích của bạn từ trước."));
            }

            var favorite = new Favorite
            {
                CustomerId = customerId,
                ProductId = productId,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Favorites.AddAsync(favorite);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Đã thêm sách vào danh sách yêu thích thành công!"));
        }

        /// <summary>
        /// Xóa sách khỏi danh sách yêu thích
        /// </summary>
        [HttpDelete("{productId}")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveFavorite(long productId)
        {
            long customerId = GetCurrentCustomerId();

            var favorite = await _context.Favorites
                .FirstOrDefaultAsync(f => f.CustomerId == customerId && f.ProductId == productId);

            if (favorite == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse("Sách này không có trong danh sách yêu thích của bạn."));
            }

            _context.Favorites.Remove(favorite);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Đã xóa sách khỏi danh sách yêu thích thành công!"));
        }
    }
}
