using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace qlthuvien_vip.Models.DTOs
{
    public class CheckoutItemDto
    {
        [Required(ErrorMessage = "Mã sản phẩm không được để trống")]
        public long ProductId { get; set; }

        [Required(ErrorMessage = "Số lượng không được để trống")]
        [Range(1, int.MaxValue, ErrorMessage = "Số lượng mua phải lớn hơn 0")]
        public int Quantity { get; set; }
    }

    public class CreateOrderDto
    {
        [Required(ErrorMessage = "Mã địa chỉ giao hàng không được để trống")]
        public long AddressId { get; set; }

        public string PaymentMethod { get; set; } = "COD"; // COD, QR_TRANSFER, ONLINE

        public string? DiscountCode { get; set; }

        /// <summary>
        /// Danh sách sản phẩm được chọn để mua. Nếu để trống hoặc null, hệ thống sẽ mua toàn bộ giỏ hàng.
        /// </summary>
        public List<CheckoutItemDto>? Items { get; set; }
    }

    public class OrderCostPreviewRequestDto
    {
        public long? AddressId { get; set; }
        public string? DiscountCode { get; set; }
        public List<CheckoutItemDto> Items { get; set; } = new List<CheckoutItemDto>();
    }

    public class OrderCostPreviewResponseDto
    {
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? DiscountCode { get; set; }
        public string? DiscountMessage { get; set; }
        public int TotalItems { get; set; }
    }

    public class UpdateOrderStatusDto
    {
        [Required(ErrorMessage = "Trạng thái đơn hàng không được để trống")]
        [RegularExpression("^(PENDING_PAYMENT|PENDING_CONFIRMATION|PAID|CONFIRMED|PROCESSING|SHIPPING|DELIVERED|CANCELLED|RETURNED)$", 
            ErrorMessage = "Trạng thái hợp lệ: PENDING_PAYMENT, PENDING_CONFIRMATION, PAID, CONFIRMED, PROCESSING, SHIPPING, DELIVERED, CANCELLED, RETURNED")]
        public string Status { get; set; } = null!;

        public string? Note { get; set; }
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
        
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? DiscountCode { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = "COD";

        public string OrderStatus { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public List<OrderItemResponseDto> Items { get; set; } = new List<OrderItemResponseDto>();
    }
}
