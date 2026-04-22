using System.Net.Http.Json;
using FirebaseAdmin.Auth;
using Microsoft.Extensions.Caching.Memory;
using UnoGame.Core.DTOs;

namespace UnoGame.Infrastructure.Services;

/// <summary>
/// AuthService — xử lý toàn bộ luồng xác thực Firebase.
///
/// Caching strategy:
///   Token verify kết quả được cache 4 phút (Firebase token valid 1 giờ).
///   Cache key = SHA256(token) để tránh lưu raw token trong memory.
///
/// Password operations dùng Firebase Admin SDK (server-side) —
///   không cần raw password, chỉ cần uid.
/// Forgot password dùng Firebase Auth REST API (Identity Toolkit).
/// </summary>
public class AuthService : IAuthService
{
    private readonly IMemoryCache            _cache;
    private readonly IUserService            _users;
    private readonly ITokenBlacklistService  _blacklist;
    private readonly IHttpClientFactory      _http;
    private readonly IConfiguration          _cfg;
    private readonly ILogger<AuthService>    _log;

    private static readonly TimeSpan TokenCacheTtl = TimeSpan.FromMinutes(4);

    public AuthService(
        IMemoryCache           cache,
        IUserService           users,
        ITokenBlacklistService blacklist,
        IHttpClientFactory     http,
        IConfiguration         cfg,
        ILogger<AuthService>   log)
    {
        _cache     = cache;
        _users     = users;
        _blacklist = blacklist;
        _http      = http;
        _cfg       = cfg;
        _log       = log;
    }

    // ════════════════════════════════════════════════════════════
    // LOGIN
    // ════════════════════════════════════════════════════════════

    public async Task<LoginResponse> LoginAsync(string idToken)
    {
        var info = await VerifyTokenAsync(idToken)
            ?? throw new AuthException(AppAuthErrorCode.InvalidToken, "Invalid Firebase token");

        if (await _blacklist.IsBlacklistedAsync(info.Jti))
            throw new AuthException(AppAuthErrorCode.TokenBlacklisted,
                "This session has been revoked. Please sign in again.");

        // Auto-create user doc nếu là lần đầu đăng nhập
        bool isNewUser = false;
        var  user      = await _users.GetByIdAsync(info.Uid);

        if (user is null)
        {
            isNewUser = true;
            var displayName = info.DisplayName
                ?? info.Email.Split('@')[0]; // fallback: phần trước @

            user = await _users.RegisterAsync(info.Uid, info.Email, new RegisterUserRequest
            {
                DisplayName = displayName,
                AvatarUrl   = info.PhotoUrl
            });

            _log.LogInformation("Auto-created user on first login: {UserId} ({Email})",
                info.Uid, info.Email);
        }

        return new LoginResponse
        {
            User         = user,
            IsNewUser    = isNewUser,
            TokenExpires = info.ExpiresAt
        };
    }

    // ════════════════════════════════════════════════════════════
    // LOGOUT
    // ════════════════════════════════════════════════════════════

    public async Task LogoutAsync(string userId, string idToken, bool logoutAll)
    {
        // Verify token để lấy jti + expiresAt
        FirebaseTokenInfo? info;
        try
        {
            info = await VerifyTokenAsync(idToken);
        }
        catch
        {
            // Token đã hết hạn — không cần làm gì thêm
            _log.LogDebug("Logout called with expired/invalid token for {UserId}", userId);
            return;
        }

        if (info is not null)
        {
            // Blacklist token hiện tại
            await _blacklist.BlacklistAsync(info.Jti, info.ExpiresAt);
            // Xoá khỏi verify cache
            _cache.Remove(CacheKey(idToken));
        }

        if (logoutAll)
        {
            // Revoke TẤT CẢ refresh tokens của user → các thiết bị khác bị kick
            await FirebaseAuth.DefaultInstance.RevokeRefreshTokensAsync(userId);
            _log.LogInformation("Revoked all refresh tokens for {UserId}", userId);
        }

        _log.LogInformation("User logged out: {UserId} (allDevices={All})", userId, logoutAll);
    }

    // ════════════════════════════════════════════════════════════
    // FORGOT PASSWORD
    // ════════════════════════════════════════════════════════════

    public async Task SendPasswordResetEmailAsync(string email)
    {
        // Dùng Firebase Auth REST API (Identity Toolkit v3)
        // Firebase Admin SDK không có method gửi reset email trực tiếp
        var apiKey    = _cfg["Firebase:WebApiKey"]
            ?? throw new InvalidOperationException("Firebase:WebApiKey not configured");

        var client = _http.CreateClient("firebase-rest");
        var url    = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={apiKey}";

        var payload = new
        {
            requestType = "PASSWORD_RESET",
            email       = email
        };

        // Không throw nếu email không tồn tại — tránh user enumeration attack
        try
        {
            var response = await client.PostAsJsonAsync(url, payload);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _log.LogDebug("Firebase reset email: {Status} {Body}", response.StatusCode, body);
                // Không throw — luôn trả về success cho client
            }
            else
            {
                _log.LogInformation("Password reset email sent to {Email}", email);
            }
        }
        catch (Exception ex)
        {
            // Log lỗi nhưng không expose cho client
            _log.LogWarning(ex, "Failed to send password reset email to {Email}", email);
        }
    }

    // ════════════════════════════════════════════════════════════
    // CHANGE PASSWORD
    // ════════════════════════════════════════════════════════════

    public async Task<ChangePasswordResponse> ChangePasswordAsync(string userId, string newPassword)
    {
        if (newPassword.Length < 6)
            throw new AuthException(AppAuthErrorCode.WeakPassword,
                "Password must be at least 6 characters");

        // Firebase Admin: cập nhật password (không cần old password — user đã authenticated)
        var updateArgs = new UserRecordArgs
        {
            Uid      = userId,
            Password = newPassword
        };

        try
        {
            await FirebaseAuth.DefaultInstance.UpdateUserAsync(updateArgs);
        }
        catch (FirebaseAuthException ex)
        {
            _log.LogError(ex, "Firebase failed to update password for {UserId}", userId);
            throw new AuthException(AppAuthErrorCode.FirebaseError,
                "Failed to update password. Please try again.");
        }

        // Revoke tất cả refresh tokens → buộc re-login trên mọi thiết bị
        await FirebaseAuth.DefaultInstance.RevokeRefreshTokensAsync(userId);

        var revokedAt = DateTime.UtcNow;
        _log.LogInformation(
            "Password changed for {UserId}. All sessions revoked at {Time}", userId, revokedAt);

        return new ChangePasswordResponse
        {
            Message         = "Password changed successfully. Please sign in again.",
            RequiresRelogin = true,
            RevokedAt       = revokedAt
        };
    }

    // ════════════════════════════════════════════════════════════
    // VERIFY TOKEN (with cache)
    // ════════════════════════════════════════════════════════════

    public async Task<FirebaseTokenInfo?> VerifyTokenAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken)) return null;

        var cacheKey = CacheKey(idToken);

        // Cache hit
        if (_cache.TryGetValue(cacheKey, out FirebaseTokenInfo? cached))
            return cached;

        // Firebase Admin verify
        FirebaseToken decoded;
        try
        {
            decoded = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(
                idToken, checkRevoked: false); // checkRevoked là expensive gRPC call
        }
        catch (FirebaseAuthException ex)
        {
            _log.LogDebug("Firebase token verify failed: {Code} {Message}",
                ex.AuthErrorCode, ex.Message);

            throw ex.AuthErrorCode switch
            {
                FirebaseAdmin.Auth.AuthErrorCode.ExpiredIdToken =>
                    new AuthException(AppAuthErrorCode.TokenExpired, "Token has expired"),
                FirebaseAdmin.Auth.AuthErrorCode.RevokedIdToken =>
                    new AuthException(AppAuthErrorCode.TokenRevoked,
                        "Token has been revoked. Please sign in again."),
                FirebaseAdmin.Auth.AuthErrorCode.InvalidIdToken or
                FirebaseAdmin.Auth.AuthErrorCode.CertificateFetchFailed =>
                    new AuthException(AppAuthErrorCode.InvalidToken, "Invalid token"),
                _ => new AuthException(AppAuthErrorCode.FirebaseError, ex.Message)
            };
        }

        var info = new FirebaseTokenInfo
        {
            Uid           = decoded.Uid,
            Email         = decoded.Claims.GetValueOrDefault("email")?.ToString() ?? "",
            EmailVerified = decoded.Claims.TryGetValue("email_verified", out var ev)
                            && ev is bool b && b,
            Jti           = decoded.Claims.TryGetValue("jti", out var jti)
                            ? jti?.ToString() ?? decoded.Uid
                            : decoded.Uid,
            ExpiresAt     = decoded.Claims.TryGetValue("exp", out var exp)
                            ? DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(exp)).UtcDateTime
                            : DateTime.UtcNow.AddHours(1),
            DisplayName   = decoded.Claims.GetValueOrDefault("name")?.ToString(),
            PhotoUrl      = decoded.Claims.GetValueOrDefault("picture")?.ToString(),
        };

        // Cache với TTL = min(4 phút, thời gian còn lại của token)
        var remaining = info.ExpiresAt - DateTime.UtcNow;
        var cacheTtl  = remaining > TokenCacheTtl ? TokenCacheTtl : remaining;

        if (cacheTtl > TimeSpan.Zero)
        {
            _cache.Set(cacheKey, info, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheTtl,
                Size = 1
            });
        }

        return info;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    // Dùng hash thay vì raw token để tiết kiệm memory và tránh lưu sensitive data
    private static string CacheKey(string token)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(token));
        return $"fbt:{Convert.ToHexString(bytes)[..16]}"; // 16 chars đủ để định danh
    }
}
