using System.ComponentModel.DataAnnotations;

namespace qlthuvien_vip.Models.DTOs
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "Họ và tên không được để trống")]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; } = null!;

        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải chứa ít nhất 6 ký tự")]
        public string Password { get; set; } = null!;
    }

    public class LoginDto
    {
        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        public string Password { get; set; } = null!;
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static ApiResponse<T> SuccessResponse(T data, string message = "Thành công")
        {
            return new ApiResponse<T> { Success = true, Message = message, Data = data };
        }

        public static ApiResponse<T> ErrorResponse(string message)
        {
            return new ApiResponse<T> { Success = false, Message = message, Data = default };
        }
    }

    public class CustomerResponseDto
    {
        public long CustomerId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string Role { get; set; } = "Customer";
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class LoginResponseDto
    {
        public string Token { get; set; } = null!;
        public string TokenType { get; set; } = "Bearer";
        public CustomerResponseDto UserInfo { get; set; } = null!;
    }

    public class UpdateUserRoleDto
    {
        [Required(ErrorMessage = "Quyền tài khoản không được để trống")]
        [RegularExpression("^(Customer|Staff|Manager|Admin)$", ErrorMessage = "Quyền tài khoản hợp lệ: Customer, Staff, Manager, Admin")]
        public string Role { get; set; } = null!;
    }

    public class UpdateUserStatusDto
    {
        [Required(ErrorMessage = "Trạng thái tài khoản không được để trống")]
        public bool IsActive { get; set; }
    }

    public class UpdateProfileDto
    {
        [Required(ErrorMessage = "Họ và tên không được để trống")]
        public string FullName { get; set; } = null!;

        public string PhoneNumber { get; set; } = string.Empty;
    }
}
