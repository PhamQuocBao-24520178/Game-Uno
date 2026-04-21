using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UnoGame.API.Services;

namespace UnoGame.API.Controllers;

/// <summary>
/// Auth Controller — quản lý vòng đời xác thực.
///
/// Flow chuẩn:
///   [Unity] Firebase SDK login → idToken
///   POST /api/auth/login      → user profile
///   ...game play...
///   POST /api/auth/logout     → token blacklisted
///
/// Forgot password (không cần auth):
///   POST /api/auth/forgot-password → Firebase gửi email reset
///
/// Change password (cần auth):
///   POST /api/auth/change-password → cập nhật qua Firebase Admin → yêu cầu re-login
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : BaseController
{
    private readonly IAuthService _auth;
    private readonly ILogger<AuthController> _log;

    public AuthController(IAuthService auth, ILogger<AuthController> log)
    {
        _auth = auth;
        _log  = log;
    }

    // ─── POST /api/auth/login ─────────────────────────────────────────────
    /// <summary>
    /// Xác minh Firebase ID Token và lấy user profile.
    /// Client gọi sau mỗi lần đăng nhập thành công phía Firebase.
    /// Nếu là user mới → tự động tạo document trong MongoDB.
    ///
    /// Cách lấy idToken trong Unity:
    ///   var result = await auth.SignInWithEmailAndPasswordAsync(email, password);
    ///   var idToken = await result.User.TokenAsync(forceRefresh: false);
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest("idToken is required");

        var result = await _auth.LoginAsync(req.IdToken);

        _log.LogInformation(
            "Login: {UserId} ({Email}) isNew={IsNew}",
            result.User.Id, result.User.Stats, result.IsNewUser);

        return Ok(result, result.IsNewUser ? "Welcome! Account created." : "Login successful");
    }

    // ─── POST /api/auth/logout ────────────────────────────────────────────
    /// <summary>
    /// Đăng xuất: blacklist token hiện tại khỏi hệ thống.
    ///
    /// logoutAll = true → revoke toàn bộ refresh token của user
    ///   → các thiết bị khác bị kick ngay khi token hiện tại hết hạn.
    ///
    /// Client phải xoá token khỏi local storage sau khi gọi API này.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? req = null)
    {
        var rawToken = ExtractRawToken();
        await _auth.LogoutAsync(CurrentUserId, rawToken ?? "", req?.LogoutAll ?? false);

        _log.LogInformation("Logout: {UserId} (allDevices={All})",
            CurrentUserId, req?.LogoutAll ?? false);

        return Ok<object>(null!, req?.LogoutAll == true
            ? "Logged out from all devices"
            : "Logged out successfully");
    }

    // ─── POST /api/auth/forgot-password ──────────────────────────────────
    /// <summary>
    /// Gửi email đặt lại mật khẩu.
    /// Không cần auth — user có thể quên mật khẩu trước khi đăng nhập.
    ///
    /// Luôn trả về 200 kể cả email không tồn tại.
    /// (Tránh user enumeration attack — attacker không biết email có đăng ký chưa)
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest("Valid email address is required");

        await _auth.SendPasswordResetEmailAsync(req.Email);

        // Luôn trả về cùng message — không tiết lộ email có tồn tại hay không
        return Ok<object>(null!,
            "If this email is registered, a password reset link has been sent.");
    }

    // ─── POST /api/auth/change-password ──────────────────────────────────
    /// <summary>
    /// Đổi mật khẩu (user đã đăng nhập).
    ///
    /// Sau khi đổi thành công:
    ///   - Tất cả refresh token bị revoke
    ///   - Client PHẢI đăng nhập lại
    ///   - Response có RequiresRelogin = true
    ///
    /// Lưu ý: Firebase không yêu cầu old password khi đổi qua Admin SDK.
    /// Để bảo mật cao hơn: yêu cầu user re-authenticate trước (client-side)
    ///   bằng EmailAuthProvider.credential(email, currentPassword) trước khi gọi API này.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ChangePasswordResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState.Values
                .SelectMany(v => v.Errors)
                .FirstOrDefault()?.ErrorMessage ?? "Validation failed");

        var result = await _auth.ChangePasswordAsync(CurrentUserId, req.NewPassword);

        _log.LogInformation("Password changed for {UserId}", CurrentUserId);

        return Ok(result, result.Message);
    }

    // ─── GET /api/auth/verify ─────────────────────────────────────────────
    /// <summary>
    /// Kiểm tra token hiện tại có còn hợp lệ không.
    /// Dùng bởi Unity client khi khởi động app để quyết định
    /// có cần đăng nhập lại hay không.
    ///
    /// Trả về 200 + thông tin token nếu valid.
    /// Trả về 401 nếu token hết hạn hoặc bị revoke (qua FirebaseAuthMiddleware).
    /// </summary>
    [HttpGet("verify")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuthStatusResponse>), 200)]
    [ProducesResponseType(401)]
    public IActionResult Verify()
    {
        // Nếu đến được đây = middleware đã verify thành công
        var emailVerified = User.FindFirstValue("email_verified") == "true";
        var expiresStr    = User.FindFirstValue("token_expires");

        return Ok(new AuthStatusResponse
        {
            IsValid       = true,
            UserId        = CurrentUserId,
            Email         = CurrentUserEmail ?? "",
            EmailVerified = emailVerified,
        });
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    /// <summary>Lấy raw Bearer token từ Authorization header (dùng cho logout).</summary>
    private string? ExtractRawToken()
    {
        var header = Request.Headers.Authorization.FirstOrDefault();
        return header?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? header["Bearer ".Length..].Trim()
            : null;
    }
}
