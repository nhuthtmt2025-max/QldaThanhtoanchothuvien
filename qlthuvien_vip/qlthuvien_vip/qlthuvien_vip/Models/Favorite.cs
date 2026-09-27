using System;

namespace qlthuvien_vip.Models
{
    public partial class Favorite
    {
        public long FavoriteId { get; set; }

        public long CustomerId { get; set; }

        public long ProductId { get; set; }

        public DateTime? CreatedAt { get; set; }

        public virtual Customer Customer { get; set; } = null!;

        public virtual Product Product { get; set; } = null!;
    }
}
