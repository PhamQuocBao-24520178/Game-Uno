using System.ComponentModel.DataAnnotations;

namespace UnoGame.Core.DTOs;

// ════════════════════════════════════════════════════════════════
// AUTH REQUEST DTOs
// ════════════════════════════════════════════════════════════════

/// <summary>
/// Client gửi sau khi Firebase signin thành công.
/// Server verify token → trả về profile.
/// </summary>
public record LoginRequest
{
    /// <summary>Firebase ID Token lấy từ FirebaseAuth.getIdToken()</summary>
    [Required]
    public string IdToken { get; init; } = null!;
}

public record ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = null!;
}

public record ChangePasswordRequest
{
    [Required, MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string NewPassword { get; init; } = null!;

    [Required]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; init; } = null!;
}

public record LogoutRequest
{
    /// <summary>
    /// LogoutAll = true → revoke tất cả refresh tokens của user (đăng xuất tất cả thiết bị).
    /// LogoutAll = false → chỉ blacklist token hiện tại.
    /// </summary>
    public bool LogoutAll { get; init; } = false;
}

// ════════════════════════════════════════════════════════════════
// AUTH RESPONSE DTOs
// ════════════════════════════════════════════════════════════════

public record LoginResponse
{
    public UserDto      User         { get; init; } = null!;
    public bool         IsNewUser    { get; init; }  // true nếu vừa tạo doc lần đầu
    public DateTime     TokenExpires { get; init; }  // khi nào ID token hết hạn
}

public record AuthStatusResponse
{
    public bool   IsValid       { get; init; }
    public string UserId        { get; init; } = null!;
    public string Email         { get; init; } = null!;
    public bool   EmailVerified { get; init; }
}

public record ChangePasswordResponse
{
    public string  Message           { get; init; } = null!;
    public bool    RequiresRelogin   { get; init; } = true;
    public DateTime RevokedAt        { get; init; }
}
