using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace UnoGame.API.Services;

// ════════════════════════════════════════════════════════════════
// Redis-backed implementation (production, multi-server)
// ════════════════════════════════════════════════════════════════

/// <summary>
/// Lưu danh sách JWT ID (jti) của token đã bị thu hồi trong Redis.
/// TTL của mỗi entry = thời gian còn lại đến khi token tự hết hạn.
/// Sau khi token hết hạn tự nhiên, entry cũng tự xoá → không cần cleanup job.
/// </summary>
public class RedisTokenBlacklistService : ITokenBlacklistService
{
    private readonly IConnectionMultiplexer _redis;
    private const string Prefix = "uno:blacklist:";

    public RedisTokenBlacklistService(IConnectionMultiplexer redis) => _redis = redis;

    public async Task BlacklistAsync(string jti, DateTime expiresAt)
    {
        if (string.IsNullOrEmpty(jti)) return;

        var db  = _redis.GetDatabase();
        var key = $"{Prefix}{jti}";
        var ttl = expiresAt - DateTime.UtcNow;

        if (ttl <= TimeSpan.Zero) return; // đã hết hạn — không cần blacklist

        await db.StringSetAsync(key, "1", ttl);
    }

    public async Task<bool> IsBlacklistedAsync(string jti)
    {
        if (string.IsNullOrEmpty(jti)) return false;

        var db  = _redis.GetDatabase();
        var key = $"{Prefix}{jti}";
        return await db.KeyExistsAsync(key);
    }
}

// ════════════════════════════════════════════════════════════════
// MemoryCache fallback (dev / single-server)
// ════════════════════════════════════════════════════════════════

/// <summary>
/// Fallback khi không có Redis.
/// Dùng IMemoryCache với SizeLimit để tránh rò bộ nhớ.
/// Không phù hợp cho multi-server deployment.
/// </summary>
public class MemoryTokenBlacklistService : ITokenBlacklistService
{
    private readonly IMemoryCache _cache;

    public MemoryTokenBlacklistService(IMemoryCache cache) => _cache = cache;

    public Task BlacklistAsync(string jti, DateTime expiresAt)
    {
        if (string.IsNullOrEmpty(jti)) return Task.CompletedTask;

        var ttl = expiresAt - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero) return Task.CompletedTask;

        _cache.Set($"bl:{jti}", true, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = expiresAt,
            Size               = 1
        });

        return Task.CompletedTask;
    }

    public Task<bool> IsBlacklistedAsync(string jti)
    {
        if (string.IsNullOrEmpty(jti)) return Task.FromResult(false);
        return Task.FromResult(_cache.TryGetValue($"bl:{jti}", out _));
    }
}
