using System;
using System.Collections.Generic;

namespace qlthuvien_vip.Models;

public partial class Cart
{
    public long CartId { get; set; }

    public long CustomerId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual Customer Customer { get; set; } = null!;
}
