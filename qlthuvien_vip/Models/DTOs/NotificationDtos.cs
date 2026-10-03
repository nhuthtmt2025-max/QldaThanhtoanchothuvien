using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace qlthuvien_vip.Models.DTOs
{
    public class NotificationResponseDto
    {
        public long NotificationId { get; set; }

        public string Title { get; set; } = null!;

        public string Message { get; set; } = null!;

        public string? Type { get; set; }

        public bool IsRead { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? ReadAt { get; set; }
    }

    public class CreateNotificationDto
    {
        [Required(ErrorMessage = "Tiêu đề không được để trống")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Nội dung không được để trống")]
        public string Message { get; set; } = null!;

        public string? Type { get; set; }

        public long? CustomerId { get; set; }
    }
}
