using System;
using System.Threading.Tasks;
using qlthuvien_vip.Models.DTOs;

namespace qlthuvien_vip.Services.Interfaces
{
    public interface IDashboardService
    {
        /// <summary>
        /// Thống kê dữ liệu Dashboard gồm doanh thu, đơn hàng, khách hàng, sách bán chạy, tồn kho thấp theo khoảng ngày.
        /// </summary>
        Task<DashboardResponseDto> GetDashboardStatsAsync(DateTime? fromDate, DateTime? toDate, int topProducts = 5, int lowStockThreshold = 10);
    }
}
