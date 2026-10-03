using System;
using System.Collections.Generic;

namespace qlthuvien_vip.Models;

public partial class Customer
{
    public long CustomerId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PhoneNumber { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = "Customer";

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Cart? Cart { get; set; }

    public virtual ICollection<CustomerAddress> CustomerAddresses { get; set; } = new List<CustomerAddress>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
