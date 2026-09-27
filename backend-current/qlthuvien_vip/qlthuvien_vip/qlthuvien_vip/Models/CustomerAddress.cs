using System;
using System.Collections.Generic;

namespace qlthuvien_vip.Models;

public partial class CustomerAddress
{
    public long AddressId { get; set; }

    public long CustomerId { get; set; }

    public string RecipientName { get; set; } = null!;

    public string RecipientPhone { get; set; } = null!;

    public string Province { get; set; } = null!;

    public string District { get; set; } = null!;

    public string Ward { get; set; } = null!;

    public string DetailedAddress { get; set; } = null!;

    public bool? IsDefault { get; set; }

    public virtual Customer Customer { get; set; } = null!;
}
