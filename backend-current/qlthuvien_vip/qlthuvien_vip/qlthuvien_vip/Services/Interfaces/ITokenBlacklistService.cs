using System;

namespace qlthuvien_vip.Services.Interfaces
{
    public interface ITokenBlacklistService
    {
        void BlacklistToken(string token, TimeSpan? expiry = null);
        bool IsTokenBlacklisted(string token);
    }
}
