using System.Collections.Generic;
using System.Threading.Tasks;
using qlthuvien_vip.Models.DTOs;

namespace qlthuvien_vip.Services.Interfaces
{
    public interface INotificationService
    {
        /// <summary>
        /// Lấy danh sách thông báo của khách hàng
        /// </summary>
        Task<List<NotificationResponseDto>> GetCustomerNotificationsAsync(long customerId, bool? isRead = null);

        /// <summary>
        /// Đánh dấu một thông báo là đã đọc
        /// </summary>
        Task<NotificationResponseDto?> MarkAsReadAsync(long notificationId, long customerId);

        /// <summary>
        /// Đánh dấu tất cả thông báo của khách hàng là đã đọc
        /// </summary>
        Task<int> MarkAllAsReadAsync(long customerId);

        /// <summary>
        /// Xóa thông báo
        /// </summary>
        Task<bool> DeleteNotificationAsync(long notificationId, long customerId);

        /// <summary>
        /// Tự động tạo thông báo khi có sự kiện (đặt hàng, cập nhật đơn hàng, thanh toán, v.v.)
        /// </summary>
        Task<NotificationResponseDto> CreateNotificationAsync(long? customerId, string title, string message, string? type = null);
    }
}
