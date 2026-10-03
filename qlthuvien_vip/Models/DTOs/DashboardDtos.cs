using System;
using System.Collections.Generic;

namespace qlthuvien_vip.Models.DTOs
{
    public class DashboardResponseDto
    {
        public decimal TotalRevenue { get; set; }

        public int TotalOrders { get; set; }

        public int TotalCustomers { get; set; }

        public List<BestSellingProductDto> BestSellingProducts { get; set; } = new List<BestSellingProductDto>();

        public List<LowStockProductDto> LowStockProducts { get; set; } = new List<LowStockProductDto>();

        public DailyRevenueStatistic? DailyRevenue { get; set; }
    }

    public class BestSellingProductDto
    {
        public long ProductId { get; set; }

        public string Title { get; set; } = null!;

        public int QuantitySold { get; set; }

        public decimal TotalRevenue { get; set; }
    }

    public class LowStockProductDto
    {
        public long ProductId { get; set; }

        public string Title { get; set; } = null!;

        public int StockQuantity { get; set; }

        public int HoldQuantity { get; set; }
    }

    public class DailyRevenueStatistic
    {
        public DateTime Date { get; set; }

        public decimal Revenue { get; set; }

        public int OrderCount { get; set; }
    }

    public class AiChatRequestDto
    {
        public string Message { get; set; } = null!;

        public string? ApiKey { get; set; }
    }

    public class AiChatResponseDto
    {
        public string Response { get; set; } = null!;

        public DateTime? Timestamp { get; set; }

        public List<ProductResponseDto>? RelatedProducts { get; set; }
    }
}
