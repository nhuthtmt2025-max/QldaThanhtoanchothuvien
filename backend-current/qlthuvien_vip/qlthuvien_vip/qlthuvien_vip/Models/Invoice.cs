using System;
using System.Collections.Generic;

namespace qlthuvien_vip.Models;

public partial class Invoice
{
    public long InvoiceId { get; set; }

    public string InvoiceNumber { get; set; } = null!;

    public long OrderId { get; set; }

    public long PaymentId { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal? TaxAmount { get; set; }

    public DateTime? InvoiceDate { get; set; }

    public string? PdfUrl { get; set; }

    public string? Status { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual PaymentTransaction Payment { get; set; } = null!;
}
