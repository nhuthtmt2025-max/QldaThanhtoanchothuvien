using System;
using System.ComponentModel.DataAnnotations;

namespace qlthuvien_vip.Models.DTOs
{
    public class InitiatePaymentDto
    {
        [Required(ErrorMessage = "Mã đơn hàng không được để trống")]
        public long OrderId { get; set; }

        [Required(ErrorMessage = "Phương thức thanh toán không được để trống")]
        [RegularExpression("^(COD|QR_TRANSFER|ONLINE|VNPAY)$", ErrorMessage = "Phương thức thanh toán hợp lệ: COD, QR_TRANSFER, ONLINE, VNPAY")]
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
        public string? QrCodeUrl { get; set; }
        public string? BankInfo { get; set; }
        public string? Note { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class PaymentWebhookDto
    {
        [Required(ErrorMessage = "TransactionCode không được để trống")]
        public string TransactionCode { get; set; } = null!;

        [Required(ErrorMessage = "Mã đơn hàng không được để trống")]
        public long OrderId { get; set; }

        [Required(ErrorMessage = "Số tiền giao dịch không được để trống")]
        [Range(0, double.MaxValue, ErrorMessage = "Số tiền giao dịch không hợp lệ")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Trạng thái giao dịch không được để trống (SUCCESS/FAILED)")]
        public string Status { get; set; } = null!; // SUCCESS hoặc FAILED

        public string? ResponseCode { get; set; } = "00";
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Chữ ký HMAC-SHA256 để xác minh tính toàn vẹn và nguồn gửi của Webhook
        /// </summary>
        [Required(ErrorMessage = "Chữ ký số (Signature) không được để trống")]
        public string Signature { get; set; } = null!;
    }

    public class GenerateSignatureRequestDto
    {
        [Required]
        public long OrderId { get; set; }
        [Required]
        public string TransactionCode { get; set; } = null!;
        [Required]
        public decimal Amount { get; set; }
        [Required]
        public string Status { get; set; } = "SUCCESS";
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
