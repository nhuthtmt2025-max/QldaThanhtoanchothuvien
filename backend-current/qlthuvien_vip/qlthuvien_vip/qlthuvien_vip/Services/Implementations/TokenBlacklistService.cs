using System;
using Microsoft.Extensions.Caching.Memory;
using qlthuvien_vip.Services.Interfaces;

namespace qlthuvien_vip.Services.Implementations
{
    public class TokenBlacklistService : ITokenBlacklistService
    {
        private readonly IMemoryCache _cache;
        private const string BlacklistPrefix = "revoked_jwt_";

        public TokenBlacklistService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public void BlacklistToken(string token, TimeSpan? expiry = null)
        {
            if (string.IsNullOrWhiteSpace(token)) return;

            // Mặc định lưu trữ trong danh sách đen 24 giờ (khớp thời gian sống tối đa của JWT)
            var cacheExpiry = expiry ?? TimeSpan.FromHours(24);
            _cache.Set(BlacklistPrefix + token, true, cacheExpiry);
        }

        public bool IsTokenBlacklisted(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;

            return _cache.TryGetValue(BlacklistPrefix + token, out _);
        }
    }
}
