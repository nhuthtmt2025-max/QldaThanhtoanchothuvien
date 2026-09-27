using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using qlthuvien_vip.Models;
using qlthuvien_vip.Services.Interfaces;

namespace qlthuvien_vip.Services.Implementations
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(Customer customer)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var secretKey = jwtSettings["Secret"] ?? "BookstoreSuperSecretKeyForJWTAuth2026_MustBeLongEnough!";
            var issuer = jwtSettings["Issuer"] ?? "BookstoreAPI";
            var audience = jwtSettings["Audience"] ?? "BookstoreClients";
            var expiryInMinutes = int.TryParse(jwtSettings["ExpiryInMinutes"], out var exp) ? exp : 1440; // 24 giờ

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, customer.CustomerId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, customer.CustomerId.ToString()),
                new Claim(ClaimTypes.Email, customer.Email),
                new Claim(ClaimTypes.Name, customer.FullName),
                new Claim(ClaimTypes.Role, customer.Role ?? "Customer")
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryInMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
