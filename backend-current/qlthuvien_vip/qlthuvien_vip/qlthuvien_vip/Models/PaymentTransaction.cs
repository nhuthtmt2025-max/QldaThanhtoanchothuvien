using System;
using System.Collections.Generic;

namespace qlthuvien_vip.Models;

public partial class PaymentTransaction
{
    public long PaymentId { get; set; }

    public long OrderId { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string? TransactionCode { get; set; }

    public decimal Amount { get; set; }

    public string PaymentStatus { get; set; } = null!;

    public string? ResponseCode { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual Order Order { get; set; } = null!;
}
