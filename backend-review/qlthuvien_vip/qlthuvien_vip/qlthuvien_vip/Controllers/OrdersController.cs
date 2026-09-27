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
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OrdersController(AppDbContext context)
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

        private bool IsAdmin()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            return roleClaim != null && string.Equals(roleClaim.Value, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Xác nhận đặt hàng (Tạo đơn hàng, khóa tạm tồn kho, chặn tạo đơn nếu thiếu địa chỉ giao hàng)
        /// </summary>
        [HttpPost("checkout")]
        [ProducesResponseType(typeof(ApiResponse<OrderResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Checkout([FromBody] CreateOrderDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Dữ liệu gửi lên không hợp lệ. Vui lòng cung cấp mã địa chỉ giao hàng."));
            }

            long customerId = GetCurrentCustomerId();

            // 1. Kiểm tra địa chỉ giao hàng của khách hàng
            var address = await _context.CustomerAddresses
                .FirstOrDefaultAsync(a => a.AddressId == dto.AddressId && a.CustomerId == customerId);

            if (address == null)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Chưa có địa chỉ giao hàng hợp lệ. Vui lòng thêm hoặc chọn địa chỉ giao hàng trước khi đặt hàng."));
            }

            // 2. Lấy giỏ hàng của Khách hàng
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (cart == null || !cart.CartItems.Any())
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Giỏ hàng của bạn đang trống, không thể tiến hành đặt hàng."));
            }

            // 3. Kiểm tra số lượng tồn kho khả dụng cho từng sản phẩm trong giỏ hàng
            foreach (var item in cart.CartItems)
            {
                if (item.Product == null || item.Product.IsActive == false)
                {
                    return BadRequest(ApiResponse<string>.ErrorResponse($"Sản phẩm (ID = {item.ProductId}) hiện ngưng kinh doanh."));
                }

                int availableStock = item.Product.StockQuantity - item.Product.HoldQuantity;
                if (item.Quantity > availableStock)
                {
                    return BadRequest(ApiResponse<string>.ErrorResponse($"Sản phẩm '{item.Product.Title}' không đủ tồn kho khả dụng (Yêu cầu: {item.Quantity}, Khả dụng: {availableStock})."));
                }
            }

            // 4. Bắt đầu Transaction tạo Đơn hàng và Khóa tạm tồn kho
            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var fullShippingAddress = $"{address.DetailedAddress}, {address.Ward}, {address.District}, {address.Province}".TrimStart(',', ' ');
                decimal totalAmount = cart.CartItems.Sum(ci => ci.Product.Price * ci.Quantity);

                var order = new Order
                {
                    CustomerId = customerId,
                    RecipientName = address.RecipientName,
                    RecipientPhone = address.RecipientPhone,
                    ShippingAddress = fullShippingAddress,
                    TotalAmount = totalAmount,
                    OrderStatus = "PENDING_PAYMENT", // Trạng thái 'Chờ thanh toán'
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(15) // Hết hạn thanh toán sau 15 phút
                };

                await _context.Orders.AddAsync(order);
                await _context.SaveChangesAsync(); // Sinh OrderId

                // Khóa tạm tồn kho & Tạo các OrderItem
                foreach (var cartItem in cart.CartItems)
                {
                    var orderItem = new OrderItem
                    {
                        OrderId = order.OrderId,
                        ProductId = cartItem.ProductId,
                        UnitPrice = cartItem.Product.Price,
                        Quantity = cartItem.Quantity,
                        Subtotal = cartItem.Product.Price * cartItem.Quantity
                    };
                    await _context.OrderItems.AddAsync(orderItem);

                    // Tăng số lượng khóa tạm
                    cartItem.Product.HoldQuantity += cartItem.Quantity;
                    cartItem.Product.UpdatedAt = DateTime.UtcNow;
                }

                // Xóa giỏ hàng sau khi tạo đơn hàng thành công
                _context.CartItems.RemoveRange(cart.CartItems);
                cart.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                var createdOrder = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .FirstAsync(o => o.OrderId == order.OrderId);

                return CreatedAtAction(nameof(GetById), new { id = order.OrderId }, ApiResponse<OrderResponseDto>.SuccessResponse(MapToResponseDto(createdOrder), "Khởi tạo đơn hàng thành công! Đã tạm khóa tồn kho. Vui lòng thanh toán trong vòng 15 phút."));
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                return BadRequest(ApiResponse<string>.ErrorResponse($"Lỗi trong quá trình tạo đơn hàng: {ex.Message}"));
            }
        }

        /// <summary>
        /// Xem danh sách đơn hàng (Khách hàng xem đơn hàng của mình, Admin xem tất cả)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<OrderResponseDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOrders([FromQuery] string? status)
        {
            long customerId = GetCurrentCustomerId();
            bool isAdmin = IsAdmin();

            var query = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(o => o.CustomerId == customerId);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(o => o.OrderStatus.ToLower() == status.Trim().ToLower());
            }

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => MapToResponseDto(o))
                .ToListAsync();

            return Ok(ApiResponse<IEnumerable<OrderResponseDto>>.SuccessResponse(orders, "Lấy danh sách đơn hàng thành công."));
        }

        /// <summary>
        /// Xem chi tiết một đơn hàng theo ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<OrderResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            long customerId = GetCurrentCustomerId();
            bool isAdmin = IsAdmin();

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy đơn hàng có ID = {id}."));
            }

            if (!isAdmin && order.CustomerId != customerId)
            {
                return Forbid();
            }

            return Ok(ApiResponse<OrderResponseDto>.SuccessResponse(MapToResponseDto(order), "Lấy chi tiết đơn hàng thành công."));
        }

        /// <summary>
        /// Hủy đơn hàng đang chờ thanh toán (Mở khóa tồn kho tạm thời)
        /// </summary>
        [HttpPost("{id}/cancel")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelOrder(long id)
        {
            long customerId = GetCurrentCustomerId();
            bool isAdmin = IsAdmin();

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy đơn hàng có ID = {id}."));
            }

            if (!isAdmin && order.CustomerId != customerId)
            {
                return Forbid();
            }

            if (order.OrderStatus != "PENDING_PAYMENT")
            {
                return BadRequest(ApiResponse<string>.ErrorResponse($"Chỉ có thể hủy đơn hàng ở trạng thái 'Chờ thanh toán'. Trạng thái hiện tại: {order.OrderStatus}."));
            }

            order.OrderStatus = "CANCELLED";
            order.UpdatedAt = DateTime.UtcNow;

            // Giải phóng tồn kho tạm khóa
            foreach (var item in order.OrderItems)
            {
                if (item.Product != null)
                {
                    item.Product.HoldQuantity = Math.Max(0, item.Product.HoldQuantity - item.Quantity);
                    item.Product.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Đã hủy đơn hàng và giải phóng tồn kho tạm khóa thành công."));
        }

        private static OrderResponseDto MapToResponseDto(Order o)
        {
            var items = o.OrderItems.Select(oi => new OrderItemResponseDto
            {
                OrderItemId = oi.OrderItemId,
                ProductId = oi.ProductId,
                ProductTitle = oi.Product?.Title ?? string.Empty,
                ProductImageUrl = oi.Product?.ImageUrl,
                UnitPrice = oi.UnitPrice,
                Quantity = oi.Quantity,
                Subtotal = oi.Subtotal
            }).ToList();

            return new OrderResponseDto
            {
                OrderId = o.OrderId,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer?.FullName ?? string.Empty,
                RecipientName = o.RecipientName,
                RecipientPhone = o.RecipientPhone,
                ShippingAddress = o.ShippingAddress,
                TotalAmount = o.TotalAmount,
                OrderStatus = o.OrderStatus,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                ExpiresAt = o.ExpiresAt,
                Items = items
            };
        }
    }
}
