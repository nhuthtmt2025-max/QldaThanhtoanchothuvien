using System;

namespace qlthuvien_vip.Models;

public partial class Notification
{
    public long NotificationId { get; set; }

    public long? CustomerId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string? Type { get; set; } // "order", "product", "payment", etc.

    public bool IsRead { get; set; } = false;

    public DateTime? CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public virtual Customer? Customer { get; set; }
}
