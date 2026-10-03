using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using qlthuvien_vip.Models.DTOs;
using qlthuvien_vip.Services.Interfaces;

namespace qlthuvien_vip.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        /// <summary>
        /// Lấy dữ liệu dashboard thống kê doanh thu, đơn hàng, khách hàng, sách bán chạy, sách tồn kho thấp.
        /// Chỉ cho phép Manager/Admin truy cập. Có thể lọc theo khoảng ngày (fromDate, toDate).
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        [ProducesResponseType(typeof(ApiResponse<DashboardResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetDashboard(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int topProducts = 5,
            [FromQuery] int lowStockThreshold = 10)
        {
            // Kiểm tra ngày bắt đầu không lớn hơn ngày kết thúc
            if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc."));
            }

            var dashboard = await _dashboardService.GetDashboardStatsAsync(
                fromDate,
                toDate,
                topProducts,
                lowStockThreshold);

            return Ok(ApiResponse<DashboardResponseDto>.SuccessResponse(dashboard, "Lấy dữ liệu dashboard thành công."));
        }
    }
}
