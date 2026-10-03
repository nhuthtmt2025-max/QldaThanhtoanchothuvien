using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace qlthuvien_vip.Models.DTOs
{
    public class AddToCartDto
    {
        [Required(ErrorMessage = "Mã khách hàng không được để trống")]
        public long CustomerId { get; set; }

        [Required(ErrorMessage = "Mã sản phẩm không được để trống")]
        public long ProductId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
        public int Quantity { get; set; } = 1;
    }

    public class UpdateCartItemDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
        public int Quantity { get; set; }
    }

    public class CartItemResponseDto
    {
        public long CartItemId { get; set; }
        public long ProductId { get; set; }
        public string ProductTitle { get; set; } = null!;
        public string? ProductImageUrl { get; set; }
        public decimal ProductPrice { get; set; }
        public int Quantity { get; set; }
        public decimal SubTotal { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class CartResponseDto
    {
        public long CartId { get; set; }
        public long CustomerId { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalAmount { get; set; }
        public List<CartItemResponseDto> Items { get; set; } = new List<CartItemResponseDto>();
        public DateTime? UpdatedAt { get; set; }
    }
}
