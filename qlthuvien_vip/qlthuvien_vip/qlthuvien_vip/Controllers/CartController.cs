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
    public class CartController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CartController(AppDbContext context)
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
        /// Xem giỏ hàng của người dùng hiện tại (Tính toán Thành tiền và Tổng tiền)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<CartResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCart()
        {
            long customerId = GetCurrentCustomerId();

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null || !cart.CartItems.Any())
            {
                var emptyCartDto = new CartResponseDto
                {
                    CartId = cart?.CartId ?? 0,
                    CustomerId = customerId,
                    TotalQuantity = 0,
                    TotalAmount = 0,
                    Items = new List<CartItemResponseDto>(),
                    UpdatedAt = cart?.UpdatedAt
                };
                return Ok(ApiResponse<CartResponseDto>.SuccessResponse(emptyCartDto, "Giỏ hàng hiện đang trống."));
            }

            var cartDto = MapToCartResponseDto(cart);
            return Ok(ApiResponse<CartResponseDto>.SuccessResponse(cartDto, "Lấy thông tin giỏ hàng thành công."));
        }

        /// <summary>
        /// Thêm sản phẩm vào giỏ hàng (Cộng dồn số lượng nếu có, kiểm tra lỗi hết hàng/vượt tồn kho)
        /// </summary>
        [HttpPost("add")]
        [ProducesResponseType(typeof(ApiResponse<CartResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Dữ liệu gửi lên không hợp lệ."));
            }

            long customerId = GetCurrentCustomerId();

            var product = await _context.Products.FindAsync(dto.ProductId);
            if (product == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy sản phẩm có ID = {dto.ProductId}."));
            }

            if (product.IsActive == false)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Sản phẩm này hiện đang ngưng kinh doanh."));
            }

            // Tính tồn kho khả dụng thực tế (Tồn kho - Khóa tạm)
            int availableStock = product.StockQuantity - product.HoldQuantity;
            if (availableStock <= 0)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Sản phẩm hết hàng."));
            }

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null)
            {
                cart = new Cart
                {
                    CustomerId = customerId,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.Carts.AddAsync(cart);
                await _context.SaveChangesAsync();
            }

            var cartItem = cart.CartItems.FirstOrDefault(ci => ci.ProductId == dto.ProductId);
            int newQuantity = dto.Quantity;
            if (cartItem != null)
            {
                newQuantity += cartItem.Quantity;
            }

            if (newQuantity > availableStock)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse($"Số lượng vượt tồn kho. Tồn kho khả dụng hiện tại: {availableStock}."));
            }

            if (cartItem != null)
            {
                cartItem.Quantity = newQuantity;
            }
            else
            {
                var newCartItem = new CartItem
                {
                    CartId = cart.CartId,
                    ProductId = dto.ProductId,
                    Quantity = dto.Quantity,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.CartItems.AddAsync(newCartItem);
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var updatedCart = await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .FirstAsync(c => c.CartId == cart.CartId);

            return Ok(ApiResponse<CartResponseDto>.SuccessResponse(MapToCartResponseDto(updatedCart), "Đã thêm sản phẩm vào giỏ hàng thành công!"));
        }

        /// <summary>
        /// Cập nhật số lượng của mục sản phẩm trong giỏ hàng
        /// </summary>
        [HttpPut("item/{cartItemId}")]
        [ProducesResponseType(typeof(ApiResponse<CartResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateCartItem(long cartItemId, [FromBody] UpdateCartItemDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Dữ liệu gửi lên không hợp lệ."));
            }

            long customerId = GetCurrentCustomerId();

            var cartItem = await _context.CartItems
                .Include(ci => ci.Product)
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId && ci.Cart.CustomerId == customerId);

            if (cartItem == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy mục giỏ hàng có ID = {cartItemId} của người dùng."));
            }

            int availableStock = cartItem.Product.StockQuantity - cartItem.Product.HoldQuantity;
            if (dto.Quantity > availableStock)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse($"Số lượng yêu cầu ({dto.Quantity}) vượt quá tồn kho khả dụng ({availableStock})."));
            }

            cartItem.Quantity = dto.Quantity;
            cartItem.Cart.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var updatedCart = await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .FirstAsync(c => c.CartId == cartItem.CartId);

            return Ok(ApiResponse<CartResponseDto>.SuccessResponse(MapToCartResponseDto(updatedCart), "Cập nhật số lượng sản phẩm thành công!"));
        }

        /// <summary>
        /// Xóa sản phẩm khỏi giỏ hàng
        /// </summary>
        [HttpDelete("item/{cartItemId}")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveCartItem(long cartItemId)
        {
            long customerId = GetCurrentCustomerId();

            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId && ci.Cart.CustomerId == customerId);

            if (cartItem == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy mục giỏ hàng có ID = {cartItemId}."));
            }

            long cartId = cartItem.CartId;
            _context.CartItems.Remove(cartItem);

            var cart = await _context.Carts.FindAsync(cartId);
            if (cart != null)
            {
                cart.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Đã xóa sản phẩm khỏi giỏ hàng thành công!"));
        }

        /// <summary>
        /// Xóa toàn bộ giỏ hàng
        /// </summary>
        [HttpDelete("clear")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ClearCart()
        {
            long customerId = GetCurrentCustomerId();

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null || !cart.CartItems.Any())
            {
                return Ok(ApiResponse<string>.SuccessResponse("Giỏ hàng của bạn hiện đã trống sẵn."));
            }

            _context.CartItems.RemoveRange(cart.CartItems);
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Đã xóa toàn bộ sản phẩm trong giỏ hàng thành công!"));
        }

        private static CartResponseDto MapToCartResponseDto(Cart cart)
        {
            var items = cart.CartItems.Select(ci => new CartItemResponseDto
            {
                CartItemId = ci.CartItemId,
                ProductId = ci.ProductId,
                ProductTitle = ci.Product?.Title ?? string.Empty,
                ProductImageUrl = ci.Product?.ImageUrl,
                ProductPrice = ci.Product?.Price ?? 0,
                Quantity = ci.Quantity,
                SubTotal = (ci.Product?.Price ?? 0) * ci.Quantity,
                CreatedAt = ci.CreatedAt
            }).ToList();

            return new CartResponseDto
            {
                CartId = cart.CartId,
                CustomerId = cart.CustomerId,
                TotalQuantity = items.Sum(i => i.Quantity),
                TotalAmount = items.Sum(i => i.SubTotal),
                Items = items,
                UpdatedAt = cart.UpdatedAt
            };
        }
    }
}
