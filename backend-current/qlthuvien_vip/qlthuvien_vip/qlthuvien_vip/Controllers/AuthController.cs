using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using qlthuvien_vip.Models;
using qlthuvien_vip.Models.DTOs;
using qlthuvien_vip.Services.Interfaces;
using BCrypt.Net;

namespace qlthuvien_vip.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly ITokenBlacklistService _blacklistService;

        public AuthController(AppDbContext context, IJwtService jwtService, ITokenBlacklistService blacklistService)
        {
            _context = context;
            _jwtService = jwtService;
            _blacklistService = blacklistService;
        }

        /// <summary>
        /// Endpoint đăng ký tài khoản Khách hàng mới (Chỉ tạo quyền Customer, chặn tự gửi quyền Admin)
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(typeof(ApiResponse<CustomerResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Dữ liệu gửi lên không hợp lệ."));
            }

            bool isEmailExists = await _context.Customers
                .AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower());

            if (isEmailExists)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Email này đã được sử dụng để đăng ký tài khoản."));
            }

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            
            // Bắt buộc chỉ tạo quyền Customer, tuyệt đối không cho phép tự đăng ký quyền Admin/Manager/Staff
            const string role = "Customer";

            var newCustomer = new Customer
            {
                FullName = dto.FullName.Trim(),
                Email = dto.Email.Trim().ToLower(),
                PhoneNumber = dto.PhoneNumber?.Trim() ?? string.Empty,
                PasswordHash = hashedPassword,
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Customers.AddAsync(newCustomer);
            await _context.SaveChangesAsync();

            var responseDto = new CustomerResponseDto
            {
                CustomerId = newCustomer.CustomerId,
                FullName = newCustomer.FullName,
                Email = newCustomer.Email,
                PhoneNumber = newCustomer.PhoneNumber,
                Role = newCustomer.Role,
                IsActive = newCustomer.IsActive,
                CreatedAt = newCustomer.CreatedAt
            };

            return Ok(ApiResponse<CustomerResponseDto>.SuccessResponse(responseDto, "Đăng ký tài khoản thành công!"));
        }

        /// <summary>
        /// Endpoint đăng nhập sinh JWT Bearer Token
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Dữ liệu gửi lên không hợp lệ."));
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email.ToLower() == dto.Email.ToLower());

            if (customer == null || !BCrypt.Net.BCrypt.Verify(dto.Password, customer.PasswordHash))
            {
                return Unauthorized(ApiResponse<string>.ErrorResponse("Email hoặc mật khẩu không chính xác."));
            }

            if (customer.IsActive == false)
            {
                return Unauthorized(ApiResponse<string>.ErrorResponse("Tài khoản của bạn hiện đang bị khóa."));
            }

            string token = _jwtService.GenerateToken(customer);

            var loginResponse = new LoginResponseDto
            {
                Token = token,
                TokenType = "Bearer",
                UserInfo = new CustomerResponseDto
                {
                    CustomerId = customer.CustomerId,
                    FullName = customer.FullName,
                    Email = customer.Email,
                    PhoneNumber = customer.PhoneNumber,
                    Role = customer.Role ?? "Customer",
                    IsActive = customer.IsActive,
                    CreatedAt = customer.CreatedAt
                }
            };

            return Ok(ApiResponse<LoginResponseDto>.SuccessResponse(loginResponse, "Đăng nhập thành công!"));
        }

        /// <summary>
        /// Lấy thông tin người dùng đang đăng nhập (/api/auth/me)
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<CustomerResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var customerId))
            {
                return Unauthorized(ApiResponse<string>.ErrorResponse("Không thể xác định thông tin người dùng từ phiên đăng nhập."));
            }

            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null || customer.IsActive == false)
            {
                return Unauthorized(ApiResponse<string>.ErrorResponse("Tài khoản của bạn đã bị khóa hoặc không tồn tại."));
            }

            var responseDto = new CustomerResponseDto
            {
                CustomerId = customer.CustomerId,
                FullName = customer.FullName,
                Email = customer.Email,
                PhoneNumber = customer.PhoneNumber,
                Role = customer.Role ?? "Customer",
                IsActive = customer.IsActive,
                CreatedAt = customer.CreatedAt
            };

            return Ok(ApiResponse<CustomerResponseDto>.SuccessResponse(responseDto, "Lấy thông tin phiên đăng nhập thành công."));
        }

        /// <summary>
        /// Cập nhật thông tin cá nhân của người dùng đang đăng nhập
        /// </summary>
        [HttpPut("profile")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<CustomerResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Dữ liệu gửi lên không hợp lệ."));
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var customerId))
            {
                return Unauthorized(ApiResponse<string>.ErrorResponse("Không thể xác định thông tin người dùng."));
            }

            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null || customer.IsActive == false)
            {
                return Unauthorized(ApiResponse<string>.ErrorResponse("Tài khoản không tồn tại hoặc đã bị khóa."));
            }

            customer.FullName = dto.FullName.Trim();
            customer.PhoneNumber = dto.PhoneNumber?.Trim() ?? string.Empty;
            await _context.SaveChangesAsync();

            var responseDto = new CustomerResponseDto
            {
                CustomerId = customer.CustomerId,
                FullName = customer.FullName,
                Email = customer.Email,
                PhoneNumber = customer.PhoneNumber,
                Role = customer.Role ?? "Customer",
                IsActive = customer.IsActive,
                CreatedAt = customer.CreatedAt
            };

            return Ok(ApiResponse<CustomerResponseDto>.SuccessResponse(responseDto, "Cập nhật thông tin cá nhân thành công!"));
        }

        /// <summary>
        /// Endpoint đăng xuất tài khoản trên toàn hệ thống (Hủy bỏ phiên làm việc và thu hồi JWT Bearer Token)
        /// </summary>
        [Authorize]
        [HttpPost("logout")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status401Unauthorized)]
        public IActionResult Logout()
        {
            var authHeader = Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                _blacklistService.BlacklistToken(token);
            }

            return Ok(ApiResponse<string>.SuccessResponse(string.Empty, "Đăng xuất thành công! Phiên đăng nhập đã được hủy."));
        }
    }
}
