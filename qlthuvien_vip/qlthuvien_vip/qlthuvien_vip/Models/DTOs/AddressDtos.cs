using System.ComponentModel.DataAnnotations;

namespace qlthuvien_vip.Models.DTOs
{
    public class CreateAddressDto
    {
        [Required(ErrorMessage = "Tên người nhận không được để trống")]
        [MaxLength(255, ErrorMessage = "Tên người nhận tối đa 255 ký tự")]
        public string RecipientName { get; set; } = null!;

        [Required(ErrorMessage = "Số điện thoại người nhận không được để trống")]
        [Phone(ErrorMessage = "Số điện thoại không đúng định dạng")]
        [MaxLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự")]
        public string RecipientPhone { get; set; } = null!;

        [Required(ErrorMessage = "Tỉnh/Thành phố không được để trống")]
        [MaxLength(100, ErrorMessage = "Tỉnh/Thành tối đa 100 ký tự")]
        public string Province { get; set; } = null!;

        [Required(ErrorMessage = "Quận/Huyện không được để trống")]
        [MaxLength(100, ErrorMessage = "Quận/Huyện tối đa 100 ký tự")]
        public string District { get; set; } = null!;

        [Required(ErrorMessage = "Phường/Xã không được để trống")]
        [MaxLength(100, ErrorMessage = "Phường/Xã tối đa 100 ký tự")]
        public string Ward { get; set; } = null!;

        [MaxLength(500, ErrorMessage = "Địa chỉ chi tiết tối đa 500 ký tự")]
        public string? DetailedAddress { get; set; }

        public bool IsDefault { get; set; } = false;
    }

    public class UpdateAddressDto : CreateAddressDto
    {
    }

    public class AddressResponseDto
    {
        public long AddressId { get; set; }
        public long CustomerId { get; set; }
        public string RecipientName { get; set; } = null!;
        public string RecipientPhone { get; set; } = null!;
        public string Province { get; set; } = null!;
        public string District { get; set; } = null!;
        public string Ward { get; set; } = null!;
        public string? DetailedAddress { get; set; }
        public string FullAddressString => $"{DetailedAddress}, {Ward}, {District}, {Province}".TrimStart(',', ' ');
        public bool? IsDefault { get; set; }
    }
}
