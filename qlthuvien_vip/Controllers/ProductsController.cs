using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using qlthuvien_vip.Models;
using qlthuvien_vip.Models.DTOs;

namespace qlthuvien_vip.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductsController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Tìm kiếm sách theo từ khóa (Tên sách, Tác giả, ISBN), lọc theo giá, thể loại và phân trang đầy đủ (trả về totalCount, totalPages cho FE).
        /// Public cho phép Khách hàng xem.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<ProductResponseDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? category,
            [FromQuery] decimal? minPrice,
            [FromQuery] decimal? maxPrice,
            [FromQuery] bool? isActive = true,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var query = _context.Products.AsNoTracking().AsQueryable();

            if (isActive.HasValue)
            {
                query = query.Where(p => p.IsActive == isActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(p => p.Category != null && p.Category.ToLower() == category.Trim().ToLower());
            }

            if (minPrice.HasValue)
            {
                query = query.Where(p => p.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string keyword = search.Trim().ToLower();
                query = query.Where(p => p.Title.ToLower().Contains(keyword) ||
                                         (p.Author != null && p.Author.ToLower().Contains(keyword)) ||
                                         (p.Isbn != null && p.Isbn.ToLower().Contains(keyword)));
            }

            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => MapToResponseDto(p))
                .ToListAsync();

            var pagedResult = new PagedResult<ProductResponseDto>
            {
                Items = products,
                TotalItems = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return Ok(ApiResponse<PagedResult<ProductResponseDto>>.SuccessResponse(pagedResult, "Lấy danh sách sách thành công."));
        }

        /// <summary>
        /// Xem chi tiết sách gồm: Tên, Tác giả, NXB, Giá bán, Mô tả, Số lượng tồn kho, Hình ảnh.
        /// Public cho phép Khách hàng xem.
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<ProductResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(long id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy sản phẩm có ID = {id}."));
            }

            return Ok(ApiResponse<ProductResponseDto>.SuccessResponse(MapToResponseDto(product), "Lấy thông tin chi tiết sách thành công."));
        }

        /// <summary>
        /// Thêm sản phẩm sách mới (Admin hoặc Quản lý - Manager)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        [ProducesResponseType(typeof(ApiResponse<ProductResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Dữ liệu gửi lên không hợp lệ."));
            }

            if (!string.IsNullOrWhiteSpace(dto.Isbn))
            {
                bool isbnExists = await _context.Products.AnyAsync(p => p.Isbn == dto.Isbn.Trim());
                if (isbnExists)
                {
                    return BadRequest(ApiResponse<string>.ErrorResponse("Mã ISBN này đã tồn tại trong hệ thống."));
                }
            }

            var product = new Product
            {
                Title = dto.Title.Trim(),
                Author = dto.Author?.Trim(),
                Publisher = dto.Publisher?.Trim(),
                Isbn = dto.Isbn?.Trim(),
                Category = dto.Category?.Trim(),
                Price = dto.Price,
                StockQuantity = dto.StockQuantity,
                HoldQuantity = 0,
                Description = dto.Description?.Trim(),
                ImageUrl = dto.ImageUrl?.Trim(),
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();

            var responseDto = MapToResponseDto(product);
            return CreatedAtAction(nameof(GetById), new { id = product.ProductId }, ApiResponse<ProductResponseDto>.SuccessResponse(responseDto, "Thêm sản phẩm sách mới thành công!"));
        }

        /// <summary>
        /// Cập nhật thông tin sách (Admin hoặc Quản lý - Manager)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Manager")]
        [ProducesResponseType(typeof(ApiResponse<ProductResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateProductDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<string>.ErrorResponse("Dữ liệu gửi lên không hợp lệ."));
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy sản phẩm có ID = {id}."));
            }

            if (!string.IsNullOrWhiteSpace(dto.Isbn) && dto.Isbn.Trim() != product.Isbn)
            {
                bool isbnExists = await _context.Products.AnyAsync(p => p.Isbn == dto.Isbn.Trim() && p.ProductId != id);
                if (isbnExists)
                {
                    return BadRequest(ApiResponse<string>.ErrorResponse("Mã ISBN này đã được sử dụng bởi sản phẩm khác."));
                }
            }

            product.Title = dto.Title.Trim();
            product.Author = dto.Author?.Trim();
            product.Publisher = dto.Publisher?.Trim();
            product.Isbn = dto.Isbn?.Trim();
            product.Category = dto.Category?.Trim();
            product.Price = dto.Price;
            product.StockQuantity = dto.StockQuantity;
            product.Description = dto.Description?.Trim();
            product.ImageUrl = dto.ImageUrl?.Trim();
            product.IsActive = dto.IsActive;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<ProductResponseDto>.SuccessResponse(MapToResponseDto(product), "Cập nhật sản phẩm sách thành công!"));
        }

        /// <summary>
        /// Xóa sản phẩm sách (Admin hoặc Quản lý - Manager)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Manager")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(long id, [FromQuery] bool hardDelete = false)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound(ApiResponse<string>.ErrorResponse($"Không tìm thấy sản phẩm có ID = {id}."));
            }

            if (hardDelete)
            {
                bool hasOrderItems = await _context.OrderItems.AnyAsync(oi => oi.ProductId == id);
                if (hasOrderItems)
                {
                    product.IsActive = false;
                    product.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return Ok(ApiResponse<string>.SuccessResponse("Sản phẩm đã phát sinh đơn hàng nên không thể xóa vĩnh viễn. Đã tự động chuyển sang trạng thái ngưng kinh doanh."));
                }

                var cartItems = await _context.CartItems.Where(ci => ci.ProductId == id).ToListAsync();
                if (cartItems.Any())
                {
                    _context.CartItems.RemoveRange(cartItems);
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                return Ok(ApiResponse<string>.SuccessResponse("Đã xóa vĩnh viễn sản phẩm khỏi hệ thống."));
            }

            product.IsActive = false;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(ApiResponse<string>.SuccessResponse("Đã ngưng kinh doanh sản phẩm này (xóa mềm thành công)."));
        }

        private static ProductResponseDto MapToResponseDto(Product p)
        {
            return new ProductResponseDto
            {
                ProductId = p.ProductId,
                Title = p.Title,
                Author = p.Author,
                Publisher = p.Publisher,
                Isbn = p.Isbn,
                Category = p.Category,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                Description = p.Description,
                ImageUrl = p.ImageUrl,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            };
        }
    }
}
