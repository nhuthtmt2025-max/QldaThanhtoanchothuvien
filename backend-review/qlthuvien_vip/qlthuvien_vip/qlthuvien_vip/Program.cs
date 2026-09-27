using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using qlthuvien_vip.Models;
using qlthuvien_vip.Services.Implementations;
using qlthuvien_vip.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 1. Cấu hình Swagger kèm nút xác thực JWT Bearer Token
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Bookstore RESTful API",
        Version = "v1",
        Description = "Hệ thống RESTful API Cửa hàng bán sách với JWT Auth, Khóa tồn kho tạm thời, Webhook IPN & Order Timeout BackgroundService."
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

// 2. Cấu hình EF Core DbContext SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// 3. Đăng ký Services & Background HostedService
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddHostedService<OrderTimeoutHostedService>();

// 4. Cấu hình JWT Authentication
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
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Tự động kiểm tra và thêm các cột schema mới vào SQL Server nếu chưa có
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

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[customers]') AND name = 'role')
            BEGIN
                ALTER TABLE [customers] ADD [role] VARCHAR(50) NOT NULL DEFAULT 'Customer';
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
    c.RoutePrefix = string.Empty; // Hiển thị Swagger trực tiếp tại trang chủ http://localhost:port/
});

app.UseHttpsRedirection();

app.UseAuthentication(); // Bắt buộc đặt trước UseAuthorization
app.UseAuthorization();

app.MapControllers();

app.Run();