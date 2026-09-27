using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using qlthuvien_vip.Models;
using qlthuvien_vip.Models.DTOs;

namespace qlthuvien_vip.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AddressesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AddressesController(AppDbContext context)
        {
            _context = context;
        }

        private long GetCurrentCustomerId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (claim != null && long.TryParse(claim.Value, out var customerId))
            {
                return customerId;
            }
            throw new UnauthorizedAccessException("Không thể xác định thông tin khách hàng từ JWT Token.");
        }

        /// <summary>
        /// Lấy danh sách địa chỉ giao hàng của Khách hàng hiện tại
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<AddressResponseDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyAddresses()
        {
            long customerId = GetCurrentCustomerId();

            var addresses = await _context.CustomerAddresses
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.AddressId)
                .Select(a => MapToResponseDto(a))
                .ToListAsync();

            return Ok(ApiResponse<IEnumerable<AddressResponseDto>>.SuccessResponse(addresses, "Lấy danh sách địa chỉ giao hàng thành công."));
        }

        /// <summary>
        /// Lấy thông tin chi tiết một địa chỉ theo ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<AddressResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            long customerId = GetCurrentCustomerId();

            var address = await _context.CustomerAddresses
                .FirstOrDefaultAsync(a => a.AddressId == id && a.CustomerId == customerId);

            if (address == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy địa chỉ có ID = {id}."));
            }

            return Ok(ApiResponse<AddressResponseDto>.SuccessResponse(MapToResponseDto(address), "Lấy thông tin địa chỉ thành công."));
        }

        /// <summary>
        /// Thêm địa chỉ giao hàng mới (Bắt buộc validate: Tên, SĐT, Tỉnh/Thành, Quận/Huyện, Phường/Xã)
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<AddressResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateAddressDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Dữ liệu gửi lên không hợp lệ. Vui lòng nhập đầy đủ Tên, SĐT, Tỉnh/Thành, Quận/Huyện, Phường/Xã."));
            }

            long customerId = GetCurrentCustomerId();

            if (dto.IsDefault)
            {
                var existingDefaults = await _context.CustomerAddresses
                    .Where(a => a.CustomerId == customerId && a.IsDefault == true)
                    .ToListAsync();

                foreach (var addr in existingDefaults)
                {
                    addr.IsDefault = false;
                }
            }

            var address = new CustomerAddress
            {
                CustomerId = customerId,
                RecipientName = dto.RecipientName.Trim(),
                RecipientPhone = dto.RecipientPhone.Trim(),
                Province = dto.Province.Trim(),
                District = dto.District.Trim(),
                Ward = dto.Ward.Trim(),
                DetailedAddress = dto.DetailedAddress?.Trim(),
                IsDefault = dto.IsDefault
            };

            await _context.CustomerAddresses.AddAsync(address);
            await _context.SaveChangesAsync();

            var responseDto = MapToResponseDto(address);
            return CreatedAtAction(nameof(GetById), new { id = address.AddressId }, ApiResponse<AddressResponseDto>.SuccessResponse(responseDto, "Thêm địa chỉ giao hàng mới thành công!"));
        }

        /// <summary>
        /// Cập nhật thông tin địa chỉ giao hàng
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponse<AddressResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateAddressDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Dữ liệu gửi lên không hợp lệ. Vui lòng nhập đầy đủ Tên, SĐT, Tỉnh/Thành, Quận/Huyện, Phường/Xã."));
            }

            long customerId = GetCurrentCustomerId();

            var address = await _context.CustomerAddresses
                .FirstOrDefaultAsync(a => a.AddressId == id && a.CustomerId == customerId);

            if (address == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy địa chỉ có ID = {id}."));
            }

            if (dto.IsDefault && address.IsDefault != true)
            {
                var existingDefaults = await _context.CustomerAddresses
                    .Where(a => a.CustomerId == customerId && a.IsDefault == true && a.AddressId != id)
                    .ToListAsync();

                foreach (var addr in existingDefaults)
                {
                    addr.IsDefault = false;
                }
            }

            address.RecipientName = dto.RecipientName.Trim();
            address.RecipientPhone = dto.RecipientPhone.Trim();
            address.Province = dto.Province.Trim();
            address.District = dto.District.Trim();
            address.Ward = dto.Ward.Trim();
            address.DetailedAddress = dto.DetailedAddress?.Trim();
            address.IsDefault = dto.IsDefault;

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<AddressResponseDto>.SuccessResponse(MapToResponseDto(address), "Cập nhật địa chỉ giao hàng thành công!"));
        }

        /// <summary>
        /// Xóa địa chỉ giao hàng
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(long id)
        {
            long customerId = GetCurrentCustomerId();

            var address = await _context.CustomerAddresses
                .FirstOrDefaultAsync(a => a.AddressId == id && a.CustomerId == customerId);

            if (address == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy địa chỉ có ID = {id}."));
            }

            _context.CustomerAddresses.Remove(address);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Đã xóa địa chỉ giao hàng thành công!"));
        }

        private static AddressResponseDto MapToResponseDto(CustomerAddress a)
        {
            return new AddressResponseDto
            {
                AddressId = a.AddressId,
                CustomerId = a.CustomerId,
                RecipientName = a.RecipientName,
                RecipientPhone = a.RecipientPhone,
                Province = a.Province,
                District = a.District,
                Ward = a.Ward,
                DetailedAddress = a.DetailedAddress,
                IsDefault = a.IsDefault
            };
        }
    }
}
