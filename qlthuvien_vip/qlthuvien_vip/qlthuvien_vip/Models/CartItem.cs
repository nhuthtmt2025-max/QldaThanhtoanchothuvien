using System;
using System.Collections.Generic;

namespace qlthuvien_vip.Models;

public partial class CartItem
{
    public long CartItemId { get; set; }

    public long CartId { get; set; }

    public long ProductId { get; set; }

    public int Quantity { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Cart Cart { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
