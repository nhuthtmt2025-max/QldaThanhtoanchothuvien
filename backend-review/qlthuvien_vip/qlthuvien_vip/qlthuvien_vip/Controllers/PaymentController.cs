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
    public class PaymentController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PaymentController(AppDbContext context)
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
        /// Khởi tạo thanh toán: Hiển thị tổng tiền cuối cùng và chọn phương thức (COD, QR_TRANSFER, ONLINE)
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
                return BadRequest(ApiResponse<string>.ErrorResponse("Dữ liệu gửi lên không hợp lệ. Các phương thức hỗ trợ: COD, QR_TRANSFER, ONLINE."));
            }

            long customerId = GetCurrentCustomerId();

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId && o.CustomerId == customerId);

            if (order == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy đơn hàng có ID = {dto.OrderId}."));
            }

            if (order.OrderStatus != "PENDING_PAYMENT")
            {
                return BadRequest(ApiResponse<string>.ErrorResponse($"Đơn hàng này không ở trạng thái chờ thanh toán. Trạng thái hiện tại: {order.OrderStatus}."));
            }

            // Sinh mã giao dịch duy nhất (TransactionCode)
            string transactionCode = $"TXN-{order.OrderId}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

            var payment = new PaymentTransaction
            {
                OrderId = order.OrderId,
                PaymentMethod = dto.PaymentMethod.ToUpper(),
                Amount = order.TotalAmount,
                PaymentStatus = "PENDING",
                TransactionCode = transactionCode,
                CreatedAt = DateTime.UtcNow
            };

            await _context.PaymentTransactions.AddAsync(payment);
            await _context.SaveChangesAsync();

            string paymentUrl = dto.PaymentMethod.ToUpper() switch
            {
                "QR_TRANSFER" => $"https://img.vietqr.io/image/970436-123456789-qr_only.png?amount={(long)order.TotalAmount}&addInfo=THANHTOAN_DONHANG_{order.OrderId}",
                "ONLINE" => $"https://paymentgateway.mock.vn/pay?txnid={transactionCode}&amount={order.TotalAmount}",
                _ => $"N/A (Thanh toán tiền mặt khi nhận hàng - COD)"
            };

            var responseDto = new PaymentResponseDto
            {
                PaymentId = payment.PaymentId,
                OrderId = payment.OrderId,
                PaymentMethod = payment.PaymentMethod,
                Amount = payment.Amount,
                PaymentStatus = payment.PaymentStatus,
                TransactionCode = payment.TransactionCode,
                PaymentUrl = paymentUrl,
                CreatedAt = payment.CreatedAt
            };

            return Ok(ApiResponse<PaymentResponseDto>.SuccessResponse(responseDto, $"Khởi tạo thanh toán đơn hàng #{order.OrderId} thành công! Tổng tiền thanh toán: {order.TotalAmount:#,##0} VNĐ."));
        }

        /// <summary>
        /// Webhook / IPN Callback nhận kết quả thanh toán từ Cổng thanh toán (Hỗ trợ Idempotency chống thanh toán trùng)
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

            // 1. Tìm bản ghi giao dịch theo TransactionCode hoặc OrderId
            var payment = await _context.PaymentTransactions
                .FirstOrDefaultAsync(p => p.TransactionCode == dto.TransactionCode || p.OrderId == dto.OrderId);

            if (payment == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy thông tin giao dịch thanh toán cho mã '{dto.TransactionCode}'."));
            }

            // 2. Kiếm tra Idempotency (Chống xử lý trùng khi Webhook gửi 2 lần IPN)
            if (payment.PaymentStatus == "SUCCESS")
            {
                var existingInvoice = await _context.Invoices.FirstOrDefaultAsync(i => i.OrderId == payment.OrderId);
                var invoiceDto = existingInvoice != null ? MapToInvoiceDto(existingInvoice) : null!;
                return Ok(ApiResponse<InvoiceResponseDto?>.SuccessResponse(invoiceDto, "Thông báo: Giao dịch này đã được ghi nhận thanh toán thành công từ trước (Idempotent call)."));
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == payment.OrderId);

            if (order == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy đơn hàng ID = {payment.OrderId}."));
            }

            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (string.Equals(dto.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                {
                    // 3. Giao dịch THÀNH CÔNG
                    payment.PaymentStatus = "SUCCESS";
                    payment.ResponseCode = dto.ResponseCode ?? "00";
                    payment.CompletedAt = DateTime.UtcNow;

                    order.OrderStatus = "PAID"; // Đã thanh toán
                    order.UpdatedAt = DateTime.UtcNow;

                    // Trừ tồn kho chính thức và giảm khóa tạm
                    foreach (var item in order.OrderItems)
                    {
                        if (item.Product != null)
                        {
                            item.Product.StockQuantity = Math.Max(0, item.Product.StockQuantity - item.Quantity);
                            item.Product.HoldQuantity = Math.Max(0, item.Product.HoldQuantity - item.Quantity);
                            item.Product.UpdatedAt = DateTime.UtcNow;
                        }
                    }

                    // Tạo Hóa đơn điện tử (e-Invoice)
                    string invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{order.OrderId:D6}";
                    decimal taxAmount = Math.Round(order.TotalAmount * 0.08m, 2); // VAT 8%

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

                    return Ok(ApiResponse<InvoiceResponseDto>.SuccessResponse(MapToInvoiceDto(invoice), "Thanh toán thành công! Đơn hàng đã được xác nhận, tồn kho chính thức đã được cập nhật và Hóa đơn điện tử đã khởi tạo."));
                }
                else
                {
                    // 4. Giao dịch THẤT BẠI
                    payment.PaymentStatus = "FAILED";
                    payment.ResponseCode = dto.ResponseCode ?? "99";
                    payment.ErrorMessage = dto.ErrorMessage ?? "Giao dịch bị từ chối hoặc khách hàng hủy thanh toán.";
                    payment.CompletedAt = DateTime.UtcNow;

                    // Đơn hàng giữ nguyên 'PENDING_PAYMENT' để cho phép chọn lại phương thức thanh toán
                    order.OrderStatus = "PENDING_PAYMENT";
                    order.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await dbTransaction.CommitAsync();

                    return Ok(ApiResponse<string>.SuccessResponse("Giao dịch thanh toán thất bại. Đơn hàng của bạn giữ nguyên trạng thái 'Chờ thanh toán'. Bạn có thể khởi tạo thanh toán lại bằng phương thức khác."));
                }
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                return BadRequest(ApiResponse<string>.ErrorResponse($"Lỗi trong quá trình xử lý kết quả thanh toán: {ex.Message}"));
            }
        }

        /// <summary>
        /// Nút "Tôi đã thanh toán" - Tự động re-check kết quả giao dịch thanh toán nếu Webhook bị trễ
        /// </summary>
        [HttpPost("recheck/{orderId}")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RecheckPayment(long orderId)
        {
            long customerId = GetCurrentCustomerId();

            var order = await _context.Orders
                .Include(o => o.PaymentTransactions)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customerId);

            if (order == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy đơn hàng có ID = {orderId}."));
            }

            if (order.OrderStatus == "PAID")
            {
                return Ok(ApiResponse<string>.SuccessResponse("Đơn hàng này đã được xác nhận thanh toán thành công trước đó!"));
            }

            var latestPayment = order.PaymentTransactions.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
            if (latestPayment == null)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Đơn hàng này chưa thực hiện khởi tạo thanh toán. Vui lòng chọn phương thức thanh toán trước."));
            }

            // Simulating API Call to Payment Gateway to recheck status
            bool simulatedGateWaySuccess = true; // Giả lập Gateway trả về đã nhận tiền

            if (simulatedGateWaySuccess)
            {
                var webhookDto = new PaymentWebhookDto
                {
                    OrderId = order.OrderId,
                    TransactionCode = latestPayment.TransactionCode ?? string.Empty,
                    Status = "SUCCESS",
                    ResponseCode = "00"
                };

                return await ProcessWebhook(webhookDto);
            }

            return Ok(ApiResponse<string>.SuccessResponse("Hệ thống đã tự động kiểm tra với Cổng thanh toán. Giao dịch của bạn hiện vẫn đang chờ ghi nhận từ Ngân hàng/Cổng thanh toán."));
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
