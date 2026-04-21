using System.Collections.Concurrent;

namespace UnoGame.API.Middleware;

/// <summary>
/// Rate limiting đơn giản dựa trên IP — bảo vệ auth endpoints khỏi brute force.
///
/// Cấu hình mặc định:
///   - Auth endpoints (/api/auth/*): 10 request / 1 phút / IP
///   - Forgot password: 3 request / 15 phút / IP (nghiêm ngặt hơn)
///   - Endpoints khác: không giới hạn (dùng middleware này)
///
/// Production: thay bằng ASP.NET Core Rate Limiting (AddRateLimiter) hoặc
/// Redis-backed solution để hỗ trợ multi-server.
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _log;

    // IP → (windowStart, requestCount)
    private static readonly ConcurrentDictionary<string, RateLimitEntry> _store = new();

    // Các rule theo path prefix
    private static readonly List<RateLimitRule> _rules = new()
    {
        new("/api/auth/forgot-password", MaxRequests: 3,  WindowMinutes: 15),
        new("/api/auth/login",           MaxRequests: 10, WindowMinutes: 1),
        new("/api/auth/change-password", MaxRequests: 5,  WindowMinutes: 5),
        new("/api/auth/",               MaxRequests: 20,  WindowMinutes: 1),  // catch-all auth
    };

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> log)
    {
        _next = next;
        _log  = log;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var rule = FindRule(ctx.Request.Path);
        if (rule is not null)
        {
            var ip  = GetClientIp(ctx);
            var key = $"{ip}:{rule.PathPrefix}";

            if (IsRateLimited(key, rule))
            {
                _log.LogWarning("Rate limit hit: IP={IP} Path={Path}", ip, ctx.Request.Path);

                ctx.Response.StatusCode = 429;
                ctx.Response.Headers["Retry-After"] = (rule.WindowMinutes * 60).ToString();
                ctx.Response.ContentType = "application/json";

                await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
                {
                    success   = false,
                    error     = $"Too many requests. Try again in {rule.WindowMinutes} minute(s).",
                    code      = "RATE_LIMITED",
                    retryAfterSeconds = rule.WindowMinutes * 60,
                    timestamp = DateTime.UtcNow
                }, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                }));
                return;
            }
        }

        await _next(ctx);
    }

    private static bool IsRateLimited(string key, RateLimitRule rule)
    {
        var now    = DateTime.UtcNow;
        var window = TimeSpan.FromMinutes(rule.WindowMinutes);

        var entry = _store.GetOrAdd(key, _ => new RateLimitEntry(now, 0));

        lock (entry)
        {
            // Reset window nếu đã hết thời gian
            if (now - entry.WindowStart > window)
            {
                entry.WindowStart = now;
                entry.Count       = 0;
            }

            entry.Count++;

            if (entry.Count > rule.MaxRequests)
                return true;
        }

        // Dọn dẹp entries cũ định kỳ (giới hạn memory)
        if (_store.Count > 10_000)
            CleanupOldEntries(now, window);

        return false;
    }

    private static void CleanupOldEntries(DateTime now, TimeSpan window)
    {
        var toRemove = _store
            .Where(kv => now - kv.Value.WindowStart > window * 2)
            .Select(kv => kv.Key)
            .Take(1000)
            .ToList();

        foreach (var k in toRemove)
            _store.TryRemove(k, out _);
    }

    private static RateLimitRule? FindRule(PathString path) =>
        _rules.FirstOrDefault(r =>
            path.StartsWithSegments(r.PathPrefix, StringComparison.OrdinalIgnoreCase));

    private static string GetClientIp(HttpContext ctx)
    {
        // X-Forwarded-For nếu có reverse proxy (nginx, Cloudflare)
        var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',')[0].Trim();

        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private record RateLimitRule(string PathPrefix, int MaxRequests, int WindowMinutes);

    private class RateLimitEntry
    {
        public DateTime WindowStart;
        public int      Count;
        public RateLimitEntry(DateTime start, int count) { WindowStart = start; Count = count; }
    }
}
