using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using qlthuvien_vip.Models;

namespace qlthuvien_vip.Services.Implementations
{
    public class OrderTimeoutHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrderTimeoutHostedService> _logger;

        public OrderTimeoutHostedService(IServiceProvider serviceProvider, ILogger<OrderTimeoutHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OrderTimeoutHostedService đang khởi chạy...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndCancelExpiredOrdersAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình kiểm tra Order Timeout.");
                }

                // Chạy định kỳ mỗi 1 phút
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task CheckAndCancelExpiredOrdersAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow;

            var expiredOrderIds = await dbContext.Orders
                .Where(o => o.OrderStatus == "PENDING_PAYMENT" && o.ExpiresAt != null && o.ExpiresAt <= now)
                .Select(o => o.OrderId)
                .ToListAsync();

            if (expiredOrderIds.Any())
            {
                _logger.LogInformation("Tìm thấy {Count} đơn hàng hết hạn chờ thanh toán. Đang xử lý mở khóa tồn kho và hủy đơn an toàn...", expiredOrderIds.Count);

                foreach (var orderId in expiredOrderIds)
                {
                    using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                    try
                    {
                        var order = await dbContext.Orders
                            .Include(o => o.OrderItems)
                            .ThenInclude(oi => oi.Product)
                            .FirstOrDefaultAsync(o => o.OrderId == orderId);

                        // Chỉ hủy nếu trạng thái vẫn còn là PENDING_PAYMENT (tránh đè lên Webhook hoặc Thao tác hủy của user)
                        if (order != null && order.OrderStatus == "PENDING_PAYMENT")
                        {
                            order.OrderStatus = "EXPIRED";
                            order.UpdatedAt = DateTime.UtcNow;

                            foreach (var item in order.OrderItems)
                            {
                                if (item.Product != null)
                                {
                                    item.Product.HoldQuantity = Math.Max(0, item.Product.HoldQuantity - item.Quantity);
                                    item.Product.UpdatedAt = DateTime.UtcNow;
                                }
                            }

                            await dbContext.SaveChangesAsync();
                            await transaction.CommitAsync();
                        }
                        else
                        {
                            await transaction.RollbackAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Lỗi khi xử lý hủy đơn hết hạn OrderId = {OrderId}", orderId);
                    }
                }

                _logger.LogInformation("Đã hoàn tất tự động hủy các đơn hàng hết hạn và giải phóng tồn kho tạm khóa.");
            }
        }
    }
}
