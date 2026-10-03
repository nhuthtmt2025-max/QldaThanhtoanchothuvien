using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using qlthuvien_vip.Models;
using qlthuvien_vip.Models.DTOs;
using qlthuvien_vip.Services.Interfaces;

namespace qlthuvien_vip.Services.Implementations
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<NotificationResponseDto>> GetCustomerNotificationsAsync(long customerId, bool? isRead = null)
        {
            var query = _context.Notifications
                .Where(n => n.CustomerId == customerId)
                .AsNoTracking();

            if (isRead.HasValue)
            {
                query = query.Where(n => n.IsRead == isRead.Value);
            }

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NotificationResponseDto
                {
                    NotificationId = n.NotificationId,
                    Title = n.Title,
                    Message = n.Message,
                    Type = n.Type,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt,
                    ReadAt = n.ReadAt
                })
                .ToListAsync();
        }

        public async Task<NotificationResponseDto?> MarkAsReadAsync(long notificationId, long customerId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.CustomerId == customerId);

            if (notification == null)
            {
                return null;
            }

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;

            _context.Notifications.Update(notification);
            await _context.SaveChangesAsync();

            return new NotificationResponseDto
            {
                NotificationId = notification.NotificationId,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt,
                ReadAt = notification.ReadAt
            };
        }

        public async Task<int> MarkAllAsReadAsync(long customerId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.CustomerId == customerId && !n.IsRead)
                .ToListAsync();

            if (!notifications.Any())
            {
                return 0;
            }

            var now = DateTime.UtcNow;
            foreach (var n in notifications)
            {
                n.IsRead = true;
                n.ReadAt = now;
            }

            _context.Notifications.UpdateRange(notifications);
            await _context.SaveChangesAsync();

            return notifications.Count;
        }

        public async Task<bool> DeleteNotificationAsync(long notificationId, long customerId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.CustomerId == customerId);

            if (notification == null)
            {
                return false;
            }

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<NotificationResponseDto> CreateNotificationAsync(long? customerId, string title, string message, string? type = null)
        {
            var notification = new Notification
            {
                CustomerId = customerId,
                Title = title,
                Message = message,
                Type = type,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Notifications.AddAsync(notification);
            await _context.SaveChangesAsync();

            return new NotificationResponseDto
            {
                NotificationId = notification.NotificationId,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt,
                ReadAt = notification.ReadAt
            };
        }
    }
}
