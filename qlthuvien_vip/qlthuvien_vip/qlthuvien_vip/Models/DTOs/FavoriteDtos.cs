using System;

namespace qlthuvien_vip.Models.DTOs
{
    public class FavoriteResponseDto
    {
        public long FavoriteId { get; set; }
        public long ProductId { get; set; }
        public string Title { get; set; } = null!;
        public string? Author { get; set; }
        public string? Category { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string? ImageUrl { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class FavoriteCheckDto
    {
        public long ProductId { get; set; }
        public bool IsFavorite { get; set; }
    }
}
