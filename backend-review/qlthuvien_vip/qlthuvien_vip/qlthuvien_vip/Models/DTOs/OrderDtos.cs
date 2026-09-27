using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace qlthuvien_vip.Models.DTOs
{
    public class CreateOrderDto
    {
        [Required(ErrorMessage = "Mã địa chỉ giao hàng không được để trống")]
        public long AddressId { get; set; }
    }

    public class OrderItemResponseDto
    {
        public long OrderItemId { get; set; }
        public long ProductId { get; set; }
        public string ProductTitle { get; set; } = null!;
        public string? ProductImageUrl { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class OrderResponseDto
    {
        public long OrderId { get; set; }
        public long CustomerId { get; set; }
        public string CustomerName { get; set; } = null!;
        public string RecipientName { get; set; } = null!;
        public string RecipientPhone { get; set; } = null!;
        public string ShippingAddress { get; set; } = null!;
        public decimal TotalAmount { get; set; }
        public string OrderStatus { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public List<OrderItemResponseDto> Items { get; set; } = new List<OrderItemResponseDto>();
    }
}
