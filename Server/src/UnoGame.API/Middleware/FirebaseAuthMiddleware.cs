using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using UnoGame.API.Services;

namespace UnoGame.API.Middleware;

/// <summary>
/// Firebase Authentication Middleware.
///
/// Pipeline vị trí: SAU UseAuthentication() / UseAuthorization().
/// Mục đích bổ sung so với JWT Bearer handler mặc định:
///   1. Kiểm tra token blacklist (logout)
///   2. Cache kết quả verify để tránh gọi Firebase mỗi request
///   3. Enrich ClaimsPrincipal với claims từ Firebase (displayName, photoUrl, emailVerified)
///   4. Xử lý token hết hạn và revoked gracefully
///
/// Request path:
///   Authorization: Bearer {firebase_id_token}
///                          ↓
///   JwtBearer handler validate JWT signature + issuer + audience
///                          ↓
///   FirebaseAuthMiddleware verify full Firebase token + blacklist
///                          ↓
///   [Authorize] attribute check ClaimsPrincipal
/// </summary>
public class FirebaseAuthMiddleware
{
    private readonly RequestDelegate    _next;
    private readonly ILogger<FirebaseAuthMiddleware> _log;

    // Các path không cần auth — bỏ qua middleware để tránh overhead
    private static readonly HashSet<string> _anonymousPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/swagger",
        "/swagger/v1/swagger.json",
        "/api/auth/forgot-password",
    };

    public FirebaseAuthMiddleware(RequestDelegate next, ILogger<FirebaseAuthMiddleware> log)
    {
        _next = next;
        _log  = log;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Bỏ qua các path public
        if (IsAnonymousPath(ctx.Request.Path))
        {
            await _next(ctx);
            return;
        }

        var token = ExtractToken(ctx);

        if (!string.IsNullOrEmpty(token))
        {
            await ProcessTokenAsync(ctx, token);
        }

        await _next(ctx);
    }

    // ─── Token extraction ─────────────────────────────────────────────────────

    private static string? ExtractToken(HttpContext ctx)
    {
        // 1. Authorization: Bearer <token>
        var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
        if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
            return authHeader["Bearer ".Length..].Trim();

        // 2. SignalR query string: ?access_token=<token>
        var queryToken = ctx.Request.Query["access_token"].FirstOrDefault();
        if (!string.IsNullOrEmpty(queryToken) && ctx.Request.Path.StartsWithSegments("/hubs"))
            return queryToken;

        return null;
    }

    // ─── Token processing ─────────────────────────────────────────────────────

    private async Task ProcessTokenAsync(HttpContext ctx, string token)
    {
        var authService      = ctx.RequestServices.GetRequiredService<IAuthService>();
        var blacklistService = ctx.RequestServices.GetRequiredService<ITokenBlacklistService>();

        FirebaseTokenInfo? info;
        try
        {
            info = await authService.VerifyTokenAsync(token);
        }
        catch (AuthException ex)
        {
            _log.LogDebug("Token verification failed: {Code} — {Message}", ex.Code, ex.Message);
            await WriteUnauthorizedAsync(ctx, ex.Code.ToString(), ex.Message);
            return;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Unexpected error verifying Firebase token");
            await WriteUnauthorizedAsync(ctx, "VERIFY_ERROR", "Token verification failed");
            return;
        }

        if (info is null) return;

        // Kiểm tra blacklist (user đã logout)
        if (await blacklistService.IsBlacklistedAsync(info.Jti))
        {
            _log.LogInformation("Blacklisted token used by {UserId}", info.Uid);
            await WriteUnauthorizedAsync(ctx, "TOKEN_REVOKED",
                "This session has been logged out. Please sign in again.");
            return;
        }

        // Enrich ClaimsPrincipal với full Firebase claims
        EnrichClaims(ctx, info);
    }

    // ─── Claims enrichment ────────────────────────────────────────────────────

    private static void EnrichClaims(HttpContext ctx, FirebaseTokenInfo info)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, info.Uid),
            new(ClaimTypes.Email,          info.Email),
            new("email_verified",          info.EmailVerified.ToString().ToLower()),
            new("jti",                     info.Jti),
            new("token_expires",           info.ExpiresAt.ToString("O")),
        };

        if (!string.IsNullOrEmpty(info.DisplayName))
            claims.Add(new Claim(ClaimTypes.Name, info.DisplayName));

        if (!string.IsNullOrEmpty(info.PhotoUrl))
            claims.Add(new Claim("photo_url", info.PhotoUrl));

        // Ghi đè/bổ sung lên identity đã có từ JwtBearer handler
        var identity  = new ClaimsIdentity(claims, "Firebase");
        var principal = new ClaimsPrincipal(identity);
        ctx.User = principal;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static bool IsAnonymousPath(PathString path) =>
        _anonymousPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));

    private static async Task WriteUnauthorizedAsync(HttpContext ctx, string code, string message)
    {
        // Chỉ ghi response nếu chưa bắt đầu
        if (ctx.Response.HasStarted) return;

        ctx.Response.StatusCode  = 401;
        ctx.Response.ContentType = "application/json";

        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            success = false,
            error   = message,
            code    = code,
            timestamp = DateTime.UtcNow
        }, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        // Dừng pipeline — không gọi _next
        await ctx.Response.WriteAsync(body);
    }
}
