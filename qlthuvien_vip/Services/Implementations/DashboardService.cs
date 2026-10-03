using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using qlthuvien_vip.Models;
using qlthuvien_vip.Models.DTOs;
using qlthuvien_vip.Services.Interfaces;

namespace qlthuvien_vip.Services.Implementations
{
    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext _context;

        public DashboardService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardResponseDto> GetDashboardStatsAsync(
            DateTime? fromDate,
            DateTime? toDate,
            int topProducts = 5,
            int lowStockThreshold = 10)
        {
            // Mặc định 30 ngày gần nhất nếu không truyền
            if (!fromDate.HasValue)
                fromDate = DateTime.UtcNow.AddDays(-30);
            if (!toDate.HasValue)
                toDate = DateTime.UtcNow;

            var validStatuses = new[] { "DELIVERED", "COMPLETED", "PAID" };

            // 1. Thống kê tổng doanh thu
            var totalRevenue = await _context.Orders
                .Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate && validStatuses.Contains(o.OrderStatus))
                .SumAsync(o => o.TotalAmount);

            // 2. Thống kê tổng số đơn hàng
            var totalOrders = await _context.Orders
                .Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
                .CountAsync();

            // 3. Thống kê tổng số khách hàng
            var totalCustomers = await _context.Customers.CountAsync();

            // 4. Danh sách sách bán chạy nhất
            var bestSellingProducts = await _context.OrderItems
                .Where(oi => oi.Order.CreatedAt >= fromDate && oi.Order.CreatedAt <= toDate)
                .GroupBy(oi => new { oi.ProductId, oi.Product.Title })
                .Select(g => new BestSellingProductDto
                {
                    ProductId = g.Key.ProductId,
                    Title = g.Key.Title,
                    QuantitySold = g.Sum(oi => oi.Quantity),
                    TotalRevenue = g.Sum(oi => oi.Subtotal)
                })
                .OrderByDescending(p => p.QuantitySold)
                .Take(topProducts)
                .ToListAsync();

            // 5. Danh sách sách tồn kho thấp
            var lowStockProducts = await _context.Products
                .Where(p => p.StockQuantity <= lowStockThreshold && p.IsActive == true)
                .Select(p => new LowStockProductDto
                {
                    ProductId = p.ProductId,
                    Title = p.Title,
                    StockQuantity = p.StockQuantity,
                    HoldQuantity = p.HoldQuantity
                })
                .OrderBy(p => p.StockQuantity)
                .ToListAsync();

            // 6. Doanh thu theo ngày hôm nay / ngày gần nhất trong khoảng
            var lastDate = toDate.Value.Date;
            var dailyRevenue = await _context.Orders
                .Where(o => o.CreatedAt.HasValue &&
                            o.CreatedAt.Value.Date == lastDate &&
                            validStatuses.Contains(o.OrderStatus))
                .AsNoTracking()
                .GroupBy(g => g.CreatedAt!.Value.Date)
                .Select(g => new DailyRevenueStatistic
                {
                    Date = g.Key,
                    Revenue = g.Sum(o => o.TotalAmount),
                    OrderCount = g.Count()
                })
                .FirstOrDefaultAsync();

            return new DashboardResponseDto
            {
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrders,
                TotalCustomers = totalCustomers,
                BestSellingProducts = bestSellingProducts,
                LowStockProducts = lowStockProducts,
                DailyRevenue = dailyRevenue
            };
        }
    }
}
