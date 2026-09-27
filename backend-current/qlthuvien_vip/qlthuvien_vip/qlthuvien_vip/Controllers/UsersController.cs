using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using qlthuvien_vip.Models;
using qlthuvien_vip.Models.DTOs;
using BCrypt.Net;

namespace qlthuvien_vip.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")] // Chỉ tài khoản có quyền Admin mới được sử dụng
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        private long GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (claim != null && long.TryParse(claim.Value, out var id))
            {
                return id;
            }
            throw new UnauthorizedAccessException("Không thể xác định danh tính Admin.");
        }

        /// <summary>
        /// Xem danh sách tài khoản trong hệ thống (Hỗ trợ tìm kiếm, lọc theo quyền, trạng thái và phân trang)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<CustomerResponseDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] string? search,
            [FromQuery] string? role,
            [FromQuery] bool? isActive,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var query = _context.Customers.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u => u.Role != null && u.Role.ToLower() == role.Trim().ToLower());
            }

            if (isActive.HasValue)
            {
                query = query.Where(u => u.IsActive == isActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string kw = search.Trim().ToLower();
                query = query.Where(u => u.FullName.ToLower().Contains(kw) ||
                                         u.Email.ToLower().Contains(kw) ||
                                         (u.PhoneNumber != null && u.PhoneNumber.Contains(kw)));
            }

            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new CustomerResponseDto
                {
                    CustomerId = u.CustomerId,
                    FullName = u.FullName,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    Role = u.Role ?? "Customer",
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            var result = new PagedResult<CustomerResponseDto>
            {
                Items = users,
                TotalItems = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return Ok(ApiResponse<PagedResult<CustomerResponseDto>>.SuccessResponse(result, "Lấy danh sách tài khoản thành công."));
        }

        /// <summary>
        /// Xem chi tiết một tài khoản theo ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<CustomerResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserById(long id)
        {
            var user = await _context.Customers.FindAsync(id);
            if (user == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy tài khoản có ID = {id}."));
            }

            var responseDto = new CustomerResponseDto
            {
                CustomerId = user.CustomerId,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role ?? "Customer",
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            };

            return Ok(ApiResponse<CustomerResponseDto>.SuccessResponse(responseDto, "Lấy thông tin tài khoản thành công."));
        }

        /// <summary>
        /// Đổi quyền tài khoản (Hỗ trợ: Customer, Staff, Manager, Admin)
        /// </summary>
        [HttpPut("{id}/role")]
        [ProducesResponseType(typeof(ApiResponse<CustomerResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateRole(long id, [FromBody] UpdateUserRoleDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Dữ liệu phân quyền không hợp lệ. Quyền hợp lệ: Customer, Staff, Manager, Admin."));
            }

            var user = await _context.Customers.FindAsync(id);
            if (user == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy tài khoản có ID = {id}."));
            }

            long currentAdminId = GetCurrentUserId();

            // Chặn admin tự hạ quyền của chính mình nếu là Admin duy nhất
            if (user.CustomerId == currentAdminId && !dto.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                int adminCount = await _context.Customers.CountAsync(c => c.Role == "Admin" && c.IsActive == true);
                if (adminCount <= 1)
                {
                    return BadRequest(ApiResponse<string>.ErrorResponse("Không thể tự hạ quyền của chính bạn vì hệ thống cần ít nhất 1 tài khoản Admin đang hoạt động."));
                }
            }

            string normalizedRole = dto.Role.Trim() switch
            {
                var r when string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase) => "Admin",
                var r when string.Equals(r, "Manager", StringComparison.OrdinalIgnoreCase) => "Manager",
                var r when string.Equals(r, "Staff", StringComparison.OrdinalIgnoreCase) => "Staff",
                _ => "Customer"
            };

            user.Role = normalizedRole;
            await _context.SaveChangesAsync();

            var responseDto = new CustomerResponseDto
            {
                CustomerId = user.CustomerId,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            };

            return Ok(ApiResponse<CustomerResponseDto>.SuccessResponse(responseDto, $"Đã cập nhật quyền của tài khoản thành '{normalizedRole}' thành công. Phiên cũ của tài khoản này sẽ được cập nhật lại vào lần truy vấn kế tiếp."));
        }

        /// <summary>
        /// Khóa hoặc mở khóa tài khoản
        /// </summary>
        [HttpPut("{id}/status")]
        [ProducesResponseType(typeof(ApiResponse<CustomerResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateUserStatusDto dto)
        {
            var user = await _context.Customers.FindAsync(id);
            if (user == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy tài khoản có ID = {id}."));
            }

            long currentAdminId = GetCurrentUserId();
            if (user.CustomerId == currentAdminId && !dto.IsActive)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Bạn không thể tự khóa tài khoản của chính mình."));
            }

            user.IsActive = dto.IsActive;
            await _context.SaveChangesAsync();

            var responseDto = new CustomerResponseDto
            {
                CustomerId = user.CustomerId,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role ?? "Customer",
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            };

            string statusMsg = dto.IsActive ? "Mở khóa tài khoản thành công." : "Đã khóa tài khoản thành công! Tài khoản này sẽ bị chặn truy cập ngay lập tức.";
            return Ok(ApiResponse<CustomerResponseDto>.SuccessResponse(responseDto, statusMsg));
        }

        /// <summary>
        /// Tạo nhanh tài khoản nội bộ (Nhân viên, Quản lý, Admin) từ phía Quản trị viên
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<CustomerResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateStaffAccount([FromBody] CreateInternalUserDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Dữ liệu gửi lên không hợp lệ."));
            }

            bool isEmailExists = await _context.Customers.AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower());
            if (isEmailExists)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Email này đã được sử dụng."));
            }

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            string role = dto.Role.Trim() switch
            {
                var r when string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase) => "Admin",
                var r when string.Equals(r, "Manager", StringComparison.OrdinalIgnoreCase) => "Manager",
                var r when string.Equals(r, "Staff", StringComparison.OrdinalIgnoreCase) => "Staff",
                _ => "Customer"
            };

            var user = new Customer
            {
                FullName = dto.FullName.Trim(),
                Email = dto.Email.Trim().ToLower(),
                PhoneNumber = dto.PhoneNumber?.Trim() ?? string.Empty,
                PasswordHash = hashedPassword,
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Customers.AddAsync(user);
            await _context.SaveChangesAsync();

            var responseDto = new CustomerResponseDto
            {
                CustomerId = user.CustomerId,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            };

            return CreatedAtAction(nameof(GetUserById), new { id = user.CustomerId }, ApiResponse<CustomerResponseDto>.SuccessResponse(responseDto, $"Tạo tài khoản {role} thành công!"));
        }
    }

    public class CreateInternalUserDto
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Họ và tên không được để trống")]
        public string FullName { get; set; } = null!;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Email không được để trống")]
        [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; } = null!;

        public string PhoneNumber { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Mật khẩu không được để trống")]
        [System.ComponentModel.DataAnnotations.MinLength(6, ErrorMessage = "Mật khẩu phải chứa ít nhất 6 ký tự")]
        public string Password { get; set; } = null!;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Quyền tài khoản không được để trống")]
        [System.ComponentModel.DataAnnotations.RegularExpression("^(Customer|Staff|Manager|Admin)$", ErrorMessage = "Quyền hợp lệ: Customer, Staff, Manager, Admin")]
        public string Role { get; set; } = "Staff";
    }
}
