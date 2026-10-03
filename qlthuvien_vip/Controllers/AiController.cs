using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using qlthuvien_vip.Models.DTOs;
using qlthuvien_vip.Services.Interfaces;

namespace qlthuvien_vip.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AiController : ControllerBase
    {
        private readonly IAiService _aiService;

        public AiController(IAiService aiService)
        {
            _aiService = aiService;
        }

        /// <summary>
        /// API AI tư vấn sách dựa trên nhu cầu, sở thích của khách hàng và tìm sách liên quan từ cơ sở dữ liệu.
        /// API Key được bảo mật và cấu hình duy nhất tại Backend (Client không cần truyền).
        /// </summary>
        [HttpPost("chat")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<AiChatResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Chat([FromBody] AiChatRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Nội dung tin nhắn tư vấn không được để trống."));
            }

            var result = await _aiService.ChatAsync(request.Message);

            return Ok(ApiResponse<AiChatResponseDto>.SuccessResponse(result, "Tư vấn sách thành công."));
        }
    }
}
