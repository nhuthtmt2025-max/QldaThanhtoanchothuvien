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

        private bool IsStaffOrHigher()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            return roleClaim != null && (
                string.Equals(roleClaim, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(roleClaim, "Manager", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(roleClaim, "Staff", StringComparison.OrdinalIgnoreCase)
            );
        }

        /// <summary>
        /// API tính trước tiền đơn hàng (Preview Cost): Tự tính giá sách, phí ship, mã giảm giá và tổng tiền cho FE hiển thị trước khi đặt hàng.
        /// </summary>
        [HttpPost("preview-cost")]
        [ProducesResponseType(typeof(ApiResponse<OrderCostPreviewResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PreviewCost([FromBody] OrderCostPreviewRequestDto dto)
        {
            long customerId = GetCurrentCustomerId();

            List<CheckoutItemDto> itemsToCalculate = dto.Items ?? new List<CheckoutItemDto>();

            // Nếu không truyền items, lấy toàn bộ giỏ hàng
            if (!itemsToCalculate.Any())
            {
                var cart = await _context.Carts
                    .Include(c => c.CartItems)
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId);

                if (cart != null && cart.CartItems.Any())
                {
                    itemsToCalculate = cart.CartItems.Select(ci => new CheckoutItemDto
                    {
                        ProductId = ci.ProductId,
                        Quantity = ci.Quantity
                    }).ToList();
                }
            }

            if (!itemsToCalculate.Any())
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Danh sách sản phẩm tính tiền đang trống."));
            }

            var productIds = itemsToCalculate.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.ProductId) && p.IsActive == true)
                .ToDictionaryAsync(p => p.ProductId);

            decimal subtotal = 0;
            int totalItems = 0;

            foreach (var item in itemsToCalculate)
            {
                if (products.TryGetValue(item.ProductId, out var product))
                {
                    subtotal += product.Price * item.Quantity;
                    totalItems += item.Quantity;
                }
            }

            var (shippingFee, discountAmount, discountMessage) = CalculateFeesAndDiscounts(subtotal, dto.DiscountCode);
            decimal totalAmount = Math.Max(0, subtotal + shippingFee - discountAmount);

            var preview = new OrderCostPreviewResponseDto
            {
                Subtotal = subtotal,
                ShippingFee = shippingFee,
                DiscountAmount = discountAmount,
                TotalAmount = totalAmount,
                DiscountCode = dto.DiscountCode?.Trim().ToUpper(),
                DiscountMessage = discountMessage,
                TotalItems = totalItems
            };

            return Ok(ApiResponse<OrderCostPreviewResponseDto>.SuccessResponse(preview, "Tính toán chi phí đơn hàng thành công."));
        }

        /// <summary>
        /// Xác nhận đặt hàng (Checkout):
        /// - Nhận danh sách sản phẩm và số lượng cần mua (chọn lọc từ giỏ)
        /// - Chỉ xóa các dòng đã mua khỏi giỏ hàng
        /// - Tự tính giá sách, phí ship, mã giảm giá và tổng tiền trên Backend
        /// - Chống đặt vượt tồn khi nhiều người mua cùng lúc
        /// - Đơn COD chuyển sang chờ xác nhận/giao hàng, không bị hủy sau 15 phút
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

            // 2. Xác định danh sách sản phẩm cần checkout
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            List<CheckoutItemDto> itemsToCheckout = new List<CheckoutItemDto>();

            if (dto.Items != null && dto.Items.Any())
            {
                itemsToCheckout = dto.Items.Where(i => i.Quantity > 0).ToList();
            }
            else
            {
                if (cart == null || !cart.CartItems.Any())
                {
                    return BadRequest(ApiResponse<string>.ErrorResponse("Giỏ hàng của bạn đang trống, không thể tiến hành đặt hàng."));
                }
                itemsToCheckout = cart.CartItems.Select(ci => new CheckoutItemDto
                {
                    ProductId = ci.ProductId,
                    Quantity = ci.Quantity
                }).ToList();
            }

            if (!itemsToCheckout.Any())
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Không có sản phẩm nào được chọn để mua."));
            }

            // 3. Sử dụng Database Transaction với mức cô lập Serializable để chống Race Condition vượt tồn kho
            using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var productIds = itemsToCheckout.Select(i => i.ProductId).Distinct().ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.ProductId))
                    .ToDictionaryAsync(p => p.ProductId);

                decimal subtotal = 0;
                var orderItems = new List<OrderItem>();

                foreach (var item in itemsToCheckout)
                {
                    if (!products.TryGetValue(item.ProductId, out var product) || product.IsActive == false)
                    {
                        await dbTransaction.RollbackAsync();
                        return BadRequest(ApiResponse<string>.ErrorResponse($"Sản phẩm (ID = {item.ProductId}) hiện ngưng kinh doanh hoặc không tồn tại."));
                    }

                    // Kiểm tra tồn kho khả dụng (Tồn kho thực tế - Khóa tạm)
                    int availableStock = product.StockQuantity - product.HoldQuantity;
                    if (item.Quantity > availableStock)
                    {
                        await dbTransaction.RollbackAsync();
                        return BadRequest(ApiResponse<string>.ErrorResponse($"Sản phẩm '{product.Title}' không đủ tồn kho khả dụng (Yêu cầu: {item.Quantity}, Khả dụng: {availableStock}). Vui lòng giảm số lượng hoặc chọn sản phẩm khác."));
                    }

                    // Tăng số lượng khóa tạm (HoldQuantity) ngay lập tức
                    product.HoldQuantity += item.Quantity;
                    product.UpdatedAt = DateTime.UtcNow;

                    decimal lineSubtotal = product.Price * item.Quantity;
                    subtotal += lineSubtotal;

                    orderItems.Add(new OrderItem
                    {
                        ProductId = product.ProductId,
                        UnitPrice = product.Price,
                        Quantity = item.Quantity,
                        Subtotal = lineSubtotal
                    });
                }

                // 4. Backend tự tính toán phí ship, mã giảm giá và tổng tiền
                var (shippingFee, discountAmount, _) = CalculateFeesAndDiscounts(subtotal, dto.DiscountCode);
                decimal totalAmount = Math.Max(0, subtotal + shippingFee - discountAmount);

                // 5. Xác định trạng thái đơn hàng và thời gian hết hạn theo phương thức thanh toán
                string paymentMethod = (dto.PaymentMethod ?? "COD").Trim().ToUpper();
                bool isCod = paymentMethod == "COD";

                // Đơn COD: PENDING_CONFIRMATION (Chờ xác nhận giao hàng), KHÔNG có hạn 15 phút (ExpiresAt = null)
                // Đơn Online/QR: PENDING_PAYMENT (Chờ thanh toán), hạn 15 phút
                string initialStatus = isCod ? "PENDING_CONFIRMATION" : "PENDING_PAYMENT";
                DateTime? expiresAt = isCod ? null : DateTime.UtcNow.AddMinutes(15);

                var fullShippingAddress = $"{address.DetailedAddress}, {address.Ward}, {address.District}, {address.Province}".TrimStart(',', ' ');

                var order = new Order
                {
                    CustomerId = customerId,
                    RecipientName = address.RecipientName,
                    RecipientPhone = address.RecipientPhone,
                    ShippingAddress = fullShippingAddress,
                    Subtotal = subtotal,
                    ShippingFee = shippingFee,
                    DiscountAmount = discountAmount,
                    DiscountCode = dto.DiscountCode?.Trim().ToUpper(),
                    TotalAmount = totalAmount,
                    PaymentMethod = paymentMethod,
                    OrderStatus = initialStatus,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt
                };

                await _context.Orders.AddAsync(order);
                await _context.SaveChangesAsync(); // Sinh OrderId

                // Gán OrderId vào các OrderItem
                foreach (var oi in orderItems)
                {
                    oi.OrderId = order.OrderId;
                    await _context.OrderItems.AddAsync(oi);
                }

                // Nếu là COD, tự động tạo sẵn giao dịch thanh toán PENDING để ghi nhận
                if (isCod)
                {
                    var codPayment = new PaymentTransaction
                    {
                        OrderId = order.OrderId,
                        PaymentMethod = "COD",
                        Amount = totalAmount,
                        PaymentStatus = "PENDING",
                        TransactionCode = $"COD-{order.OrderId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                        CreatedAt = DateTime.UtcNow
                    };
                    await _context.PaymentTransactions.AddAsync(codPayment);
                }

                // 6. CHỈ XÓA CÁC DÒNG ĐÃ MUA KHỎI GIỎ HÀNG (Sản phẩm chưa mua vẫn giữ nguyên trong giỏ)
                if (cart != null && cart.CartItems.Any())
                {
                    var purchasedProductIds = itemsToCheckout.Select(i => i.ProductId).ToHashSet();
                    var cartItemsToRemove = cart.CartItems
                        .Where(ci => purchasedProductIds.Contains(ci.ProductId))
                        .ToList();

                    if (cartItemsToRemove.Any())
                    {
                        _context.CartItems.RemoveRange(cartItemsToRemove);
                        cart.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                var createdOrder = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .FirstAsync(o => o.OrderId == order.OrderId);

                string message = isCod
                    ? "Đặt hàng thành công! Đơn hàng COD đã chuyển sang trạng thái chờ xác nhận/giao hàng."
                    : "Khởi tạo đơn hàng thành công! Đã tạm khóa tồn kho. Vui lòng thanh toán trực tuyến trong vòng 15 phút.";

                return CreatedAtAction(nameof(GetById), new { id = order.OrderId }, 
                    ApiResponse<OrderResponseDto>.SuccessResponse(MapToResponseDto(createdOrder), message));
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                return BadRequest(ApiResponse<string>.ErrorResponse($"Lỗi trong quá trình tạo đơn hàng: {ex.Message}"));
            }
        }

        /// <summary>
        /// Xem danh sách đơn hàng (Khách hàng xem đơn của mình; Nhân viên/Quản lý/Admin xem toàn bộ đơn)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<OrderResponseDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOrders([FromQuery] string? status)
        {
            long customerId = GetCurrentCustomerId();
            bool isStaffOrAdmin = IsStaffOrHigher();

            var query = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .AsQueryable();

            if (!isStaffOrAdmin)
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
        /// Xem chi tiết một đơn hàng theo ID (Khách hàng chỉ xem đơn của mình; Nhân viên/Quản trị viên xem tất cả)
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<OrderResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            long customerId = GetCurrentCustomerId();
            bool isStaffOrAdmin = IsStaffOrHigher();

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy đơn hàng có ID = {id}."));
            }

            if (!isStaffOrAdmin && order.CustomerId != customerId)
            {
                return Forbid();
            }

            return Ok(ApiResponse<OrderResponseDto>.SuccessResponse(MapToResponseDto(order), "Lấy chi tiết đơn hàng thành công."));
        }

        /// <summary>
        /// Cập nhật trạng thái đơn hàng (Dành riêng cho Nhân viên, Quản lý và Admin)
        /// Kiểm tra chuyển trạng thái hợp lệ và xử lý tồn kho chống trừ/hoàn trùng lặp.
        /// </summary>
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin,Manager,Staff")]
        [ProducesResponseType(typeof(ApiResponse<OrderResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateOrderStatus(long id, [FromBody] UpdateOrderStatusDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Dữ liệu trạng thái không hợp lệ."));
            }

            using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.OrderId == id);

                if (order == null)
                {
                    return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy đơn hàng có ID = {id}."));
                }

                string currentStatus = order.OrderStatus.ToUpper();
                string targetStatus = dto.Status.Trim().ToUpper();

                if (currentStatus == targetStatus)
                {
                    return Ok(ApiResponse<OrderResponseDto>.SuccessResponse(MapToResponseDto(order), "Đơn hàng đã ở trạng thái này."));
                }

                // Kiểm tra tính hợp lệ của việc chuyển đổi trạng thái (State Machine)
                bool isValidTransition = ValidateStatusTransition(currentStatus, targetStatus);
                if (!isValidTransition)
                {
                    return BadRequest(ApiResponse<string>.ErrorResponse($"Không thể chuyển đổi trạng thái từ '{currentStatus}' sang '{targetStatus}'. Quy trình chuyển trạng thái không hợp lệ."));
                }

                // Xử lý tồn kho tương ứng với trạng thái mới:
                // 1. Nếu chuyển sang CANCELLED hoặc RETURNED:
                if (targetStatus == "CANCELLED" || targetStatus == "RETURNED")
                {
                    foreach (var item in order.OrderItems)
                    {
                        if (item.Product != null)
                        {
                            // Nếu đơn trước đó chỉ mới giữ tạm tồn (PENDING_PAYMENT / PENDING_CONFIRMATION)
                            if (currentStatus == "PENDING_PAYMENT" || currentStatus == "PENDING_CONFIRMATION")
                            {
                                item.Product.HoldQuantity = Math.Max(0, item.Product.HoldQuantity - item.Quantity);
                            }
                            // Nếu đơn đã được xác nhận/thanh toán/giao (đã trừ tồn kho thật) -> Hoàn lại tồn kho
                            else if (currentStatus == "PAID" || currentStatus == "CONFIRMED" || currentStatus == "PROCESSING" || currentStatus == "SHIPPING")
                            {
                                item.Product.StockQuantity += item.Quantity;
                            }
                            item.Product.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }
                // 2. Nếu chuyển từ PENDING_CONFIRMATION (COD) sang CONFIRMED hoặc SHIPPING: Trừ tồn kho thật và giải phóng HoldQuantity
                else if (currentStatus == "PENDING_CONFIRMATION" && (targetStatus == "CONFIRMED" || targetStatus == "PROCESSING" || targetStatus == "SHIPPING"))
                {
                    foreach (var item in order.OrderItems)
                    {
                        if (item.Product != null)
                        {
                            item.Product.StockQuantity = Math.Max(0, item.Product.StockQuantity - item.Quantity);
                            item.Product.HoldQuantity = Math.Max(0, item.Product.HoldQuantity - item.Quantity);
                            item.Product.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }

                order.OrderStatus = targetStatus;
                order.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                return Ok(ApiResponse<OrderResponseDto>.SuccessResponse(MapToResponseDto(order), $"Cập nhật trạng thái đơn hàng thành '{targetStatus}' thành công."));
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                return BadRequest(ApiResponse<string>.ErrorResponse($"Lỗi cập nhật trạng thái đơn hàng: {ex.Message}"));
            }
        }

        /// <summary>
        /// Khách hàng hủy đơn hàng đang ở trạng thái 'Chờ thanh toán' hoặc 'Chờ xác nhận'
        /// </summary>
        [HttpPost("{id}/cancel")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelOrder(long id)
        {
            long customerId = GetCurrentCustomerId();
            bool isStaffOrAdmin = IsStaffOrHigher();

            using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.OrderId == id);

                if (order == null)
                {
                    return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy đơn hàng có ID = {id}."));
                }

                if (!isStaffOrAdmin && order.CustomerId != customerId)
                {
                    return Forbid();
                }

                if (order.OrderStatus != "PENDING_PAYMENT" && order.OrderStatus != "PENDING_CONFIRMATION")
                {
                    return BadRequest(ApiResponse<string>.ErrorResponse($"Chỉ có thể hủy đơn hàng khi đang ở trạng thái 'Chờ thanh toán' hoặc 'Chờ xác nhận'. Trạng thái hiện tại: {order.OrderStatus}."));
                }

                order.OrderStatus = "CANCELLED";
                order.UpdatedAt = DateTime.UtcNow;

                // Giải phóng tồn kho tạm khóa một cách an toàn
                foreach (var item in order.OrderItems)
                {
                    if (item.Product != null)
                    {
                        item.Product.HoldQuantity = Math.Max(0, item.Product.HoldQuantity - item.Quantity);
                        item.Product.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                return Ok(ApiResponse<string>.SuccessResponse("Đã hủy đơn hàng và giải phóng tồn kho tạm khóa thành công."));
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                return BadRequest(ApiResponse<string>.ErrorResponse($"Lỗi hủy đơn hàng: {ex.Message}"));
            }
        }

        private static bool ValidateStatusTransition(string current, string target)
        {
            return (current, target) switch
            {
                ("PENDING_PAYMENT", "PAID") => true,
                ("PENDING_PAYMENT", "CANCELLED") => true,
                ("PENDING_PAYMENT", "EXPIRED") => true,

                ("PENDING_CONFIRMATION", "CONFIRMED") => true,
                ("PENDING_CONFIRMATION", "PROCESSING") => true,
                ("PENDING_CONFIRMATION", "CANCELLED") => true,

                ("PAID", "PROCESSING") => true,
                ("PAID", "CONFIRMED") => true,
                ("PAID", "CANCELLED") => true,

                ("CONFIRMED", "PROCESSING") => true,
                ("CONFIRMED", "SHIPPING") => true,
                ("CONFIRMED", "CANCELLED") => true,

                ("PROCESSING", "SHIPPING") => true,
                ("PROCESSING", "CANCELLED") => true,

                ("SHIPPING", "DELIVERED") => true,
                ("SHIPPING", "RETURNED") => true,

                ("DELIVERED", "RETURNED") => true,

                _ => false
            };
        }

        private static (decimal shippingFee, decimal discountAmount, string? message) CalculateFeesAndDiscounts(decimal subtotal, string? discountCode)
        {
            // Quy tắc tính phí ship: Miễn phí nếu sách >= 300,000 VNĐ; ngược lại 30,000 VNĐ
            decimal shippingFee = subtotal >= 300000m ? 0m : 30000m;
            decimal discountAmount = 0m;
            string? message = null;

            if (!string.IsNullOrWhiteSpace(discountCode))
            {
                string code = discountCode.Trim().ToUpper();
                switch (code)
                {
                    case "GIAM10":
                    case "SALE10":
                        discountAmount = Math.Round(subtotal * 0.10m, 0); // Giảm 10%
                        message = "Áp dụng mã giảm giá 10% thành công!";
                        break;
                    case "FREESHIP":
                        discountAmount = shippingFee; // Miễn phí vận chuyển
                        shippingFee = 0;
                        message = "Áp dụng mã miễn phí vận chuyển thành công!";
                        break;
                    case "VIP50K":
                        if (subtotal >= 200000m)
                        {
                            discountAmount = 50000m;
                            message = "Áp dụng mã giảm giá 50.000 VNĐ thành công!";
                        }
                        else
                        {
                            message = "Mã VIP50K chỉ áp dụng cho đơn hàng từ 200.000 VNĐ trở lên.";
                        }
                        break;
                    default:
                        message = "Mã giảm giá không hợp lệ hoặc đã hết hạn.";
                        break;
                }
            }

            return (shippingFee, discountAmount, message);
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
                Subtotal = o.Subtotal ?? items.Sum(i => i.Subtotal),
                ShippingFee = o.ShippingFee ?? 0,
                DiscountAmount = o.DiscountAmount ?? 0,
                DiscountCode = o.DiscountCode,
                TotalAmount = o.TotalAmount,
                PaymentMethod = o.PaymentMethod ?? "COD",
                OrderStatus = o.OrderStatus,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                ExpiresAt = o.ExpiresAt,
                Items = items
            };
        }
    }
}
