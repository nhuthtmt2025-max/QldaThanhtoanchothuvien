using qlthuvien_vip.Models;

namespace qlthuvien_vip.Services.Interfaces
{
    public interface IJwtService
    {
        string GenerateToken(Customer customer);
    }
}
