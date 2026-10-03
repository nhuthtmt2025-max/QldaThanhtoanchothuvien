using System.Threading.Tasks;
using qlthuvien_vip.Models.DTOs;

namespace qlthuvien_vip.Services.Interfaces
{
    public interface IAiService
    {
        /// <summary>
        /// Xử lý tin nhắn tư vấn sách của khách hàng và tìm sách liên quan từ cơ sở dữ liệu.
        /// API key được cấu hình và quản lý tại Backend.
        /// </summary>
        Task<AiChatResponseDto> ChatAsync(string message);
    }
}
