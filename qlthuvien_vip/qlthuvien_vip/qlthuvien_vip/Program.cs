using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using qlthuvien_vip.Models;
using qlthuvien_vip.Models.DTOs;
using qlthuvien_vip.Services.Implementations;
using qlthuvien_vip.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 1. Cấu hình CORS kết nối Frontend (Cho phép headers Authorization, methods, origins)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { "http://localhost:3000", "http://localhost:5173", "http://localhost:4200", "http://127.0.0.1:3000", "http://127.0.0.1:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("Authorization", "Content-Disposition");
    });

    options.AddPolicy("AllowAny", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("Authorization", "Content-Disposition");
    });
});

// 2. Cấu hình Swagger kèm nút xác thực JWT Bearer Token
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Bookstore RESTful API",
        Version = "v1",
        Description = "Hệ thống RESTful API Cửa hàng bán sách với JWT Auth, RBAC (Admin, Manager, Staff, Customer), Tồn kho an toàn, Webhook IPN & Order Timeout."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập Token JWT vào ô bên dưới (Ví dụ: Bearer eyJhbGciOi...)"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 3. Cấu hình EF Core DbContext SQL Server (Lấy tập trung từ appsettings.json)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// 4. Đăng ký Services & Background HostedService
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddHostedService<OrderTimeoutHostedService>();

// 5. Cấu hình JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Secret"] ?? "BookstoreSuperSecretKeyForJWTAuth2026_MustBeLongEnoughKeyForHMACSHA256!";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "BookstoreAPI",
        ValidAudience = jwtSettings["Audience"] ?? "BookstoreClients",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var blacklistService = context.HttpContext.RequestServices.GetRequiredService<ITokenBlacklistService>();
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                if (blacklistService.IsTokenBlacklisted(token))
                {
                    context.Fail("Token đã bị vô hiệu hóa do tài khoản đã đăng xuất. Vui lòng đăng nhập lại.");
                    return;
                }
            }

            // Xử lý kiểm tra tức thì khi tài khoản bị khóa hoặc đổi quyền
            var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? context.Principal?.FindFirst("sub")?.Value;

            if (long.TryParse(userIdClaim, out var userId))
            {
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var customer = await dbContext.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == userId);
                if (customer == null || customer.IsActive == false)
                {
                    context.Fail("Tài khoản của bạn đã bị khóa hoặc không còn tồn tại. Phiên đăng nhập đã bị vô hiệu hóa.");
                    return;
                }

                var tokenRole = context.Principal?.FindFirst(ClaimTypes.Role)?.Value;
                if (!string.Equals(tokenRole, customer.Role, StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail("Quyền hạn của tài khoản đã thay đổi. Vui lòng đăng nhập lại để cập nhật phiên làm việc.");
                    return;
                }
            }
        },
        OnChallenge = async context =>
        {
            if (!context.Response.HasStarted)
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var errorMessage = context.AuthenticateFailure != null
                    ? context.AuthenticateFailure.Message
                    : "Yêu cầu cần có JWT Bearer Token hợp lệ để xác thực.";

                var response = ApiResponse<string>.ErrorResponse(errorMessage);
                await context.Response.WriteAsJsonAsync(response);
            }
        }
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Tự động kiểm tra và thêm các bảng, cột schema mới vào SQL Server nếu chưa có
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[products]') AND name = 'hold_quantity')
            BEGIN
                ALTER TABLE [products] ADD [hold_quantity] INT NOT NULL DEFAULT 0;
            END;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[orders]') AND name = 'expires_at')
            BEGIN
                ALTER TABLE [orders] ADD [expires_at] DATETIME NULL;
            END;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[orders]') AND name = 'subtotal')
            BEGIN
                ALTER TABLE [orders] ADD [subtotal] DECIMAL(15,2) NULL;
            END;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[orders]') AND name = 'shipping_fee')
            BEGIN
                ALTER TABLE [orders] ADD [shipping_fee] DECIMAL(15,2) NULL DEFAULT 0;
            END;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[orders]') AND name = 'discount_amount')
            BEGIN
                ALTER TABLE [orders] ADD [discount_amount] DECIMAL(15,2) NULL DEFAULT 0;
            END;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[orders]') AND name = 'discount_code')
            BEGIN
                ALTER TABLE [orders] ADD [discount_code] VARCHAR(50) NULL;
            END;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[orders]') AND name = 'payment_method')
            BEGIN
                ALTER TABLE [orders] ADD [payment_method] VARCHAR(50) NULL DEFAULT 'COD';
            END;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[customers]') AND name = 'role')
            BEGIN
                ALTER TABLE [customers] ADD [role] VARCHAR(50) NOT NULL DEFAULT 'Customer';
            END;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[customers]') AND name = 'password_hash')
            BEGIN
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[customers]') AND name = 'password')
                BEGIN
                    EXEC sp_rename 'customers.password', 'password_hash', 'COLUMN';
                END
                ELSE
                BEGIN
                    ALTER TABLE [customers] ADD [password_hash] VARCHAR(255) NOT NULL DEFAULT '';
                END;
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'favorites')
            BEGIN
                CREATE TABLE [favorites] (
                    [favorite_id] BIGINT IDENTITY(1,1) PRIMARY KEY,
                    [customer_id] BIGINT NOT NULL,
                    [product_id] BIGINT NOT NULL,
                    [created_at] DATETIME DEFAULT GETDATE(),
                    CONSTRAINT FK_favorites_customers FOREIGN KEY ([customer_id]) REFERENCES [customers]([customer_id]) ON DELETE CASCADE,
                    CONSTRAINT FK_favorites_products FOREIGN KEY ([product_id]) REFERENCES [products]([product_id]) ON DELETE CASCADE,
                    CONSTRAINT UQ_favorites_customer_product UNIQUE ([customer_id], [product_id])
                );
            END;
        ");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Database Init Warning] {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bookstore API v1");
    c.RoutePrefix = "swagger"; // Phục vụ Swagger UI tại /swagger và /swagger/index.html
});

app.MapGet("/", () => Results.Redirect("/swagger"));

app.UseHttpsRedirection();

// Kích hoạt CORS (Bắt buộc đặt trước UseAuthentication và UseAuthorization)
app.UseCors("AllowAny");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();