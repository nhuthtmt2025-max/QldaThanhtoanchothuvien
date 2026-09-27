using System;
using System.ComponentModel.DataAnnotations;

namespace qlthuvien_vip.Models.DTOs
{
    public class InitiatePaymentDto
    {
        [Required(ErrorMessage = "Mã đơn hàng không được để trống")]
        public long OrderId { get; set; }

        [Required(ErrorMessage = "Phương thức thanh toán không được để trống")]
        [RegularExpression("^(COD|QR_TRANSFER|ONLINE)$", ErrorMessage = "Phương thức thanh toán hợp lệ: COD, QR_TRANSFER, ONLINE")]
        public string PaymentMethod { get; set; } = null!;
    }

    public class PaymentResponseDto
    {
        public long PaymentId { get; set; }
        public long OrderId { get; set; }
        public string PaymentMethod { get; set; } = null!;
        public decimal Amount { get; set; }
        public string PaymentStatus { get; set; } = null!;
        public string TransactionCode { get; set; } = null!;
        public string PaymentUrl { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
    }

    public class PaymentWebhookDto
    {
        [Required(ErrorMessage = "TransactionCode không được để trống")]
        public string TransactionCode { get; set; } = null!;

        [Required(ErrorMessage = "Mã đơn hàng không được để trống")]
        public long OrderId { get; set; }

        [Required(ErrorMessage = "Trạng thái giao dịch không được để trống (SUCCESS/FAILED)")]
        public string Status { get; set; } = null!; // SUCCESS hoặc FAILED

        public string? ResponseCode { get; set; } = "00";
        public string? ErrorMessage { get; set; }
    }

    public class InvoiceResponseDto
    {
        public long InvoiceId { get; set; }
        public long OrderId { get; set; }
        public long PaymentId { get; set; }
        public string InvoiceNumber { get; set; } = null!;
        public decimal TotalAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public string Status { get; set; } = null!;
        public string PdfUrl { get; set; } = null!;
        public DateTime? InvoiceDate { get; set; }
    }
}
