using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UnoGame.API.Services;

namespace UnoGame.API.Controllers;

/// <summary>
/// Quản lý hồ sơ người dùng (profile, stats, lookup).
///
/// Auth actions (login/logout/forgot-password/change-password) → AuthController
/// User profile actions (view/edit/stats) → UserController (this file)
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UserController : BaseController
{
    private readonly IUserService _users;
    private readonly ILogger<UserController> _log;

    public UserController(IUserService users, ILogger<UserController> log)
    {
        _users = users;
        _log   = log;
    }

    // ─── GET /api/users/me ────────────────────────────────────────────────
    /// <summary>
    /// Lấy thông tin đầy đủ user hiện tại (profile + stats).
    /// Auto-creates user document nếu chưa có (phòng trường hợp skip /login).
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), 200)]
    public async Task<IActionResult> GetMe()
    {
        var user = await _users.GetByIdAsync(CurrentUserId);
        if (user is null)
            return NotFound("User not found. Please call POST /api/auth/login first.");
        return Ok(user);
    }

    // ─── PUT /api/users/me ────────────────────────────────────────────────
    /// <summary>
    /// Cập nhật displayName hoặc avatarUrl.
    /// Cả hai field đều optional — gửi ít nhất một.
    /// </summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        if (!ModelState.IsValid) return BadRequest("Validation failed");

        if (req.DisplayName is null && req.AvatarUrl is null)
            return BadRequest("Provide at least one field to update (displayName or avatarUrl)");

        if (!await _users.ExistsAsync(CurrentUserId))
            return NotFound("User not found");

        var updated = await _users.UpdateProfileAsync(CurrentUserId, req);
        return Ok(updated, "Profile updated");
    }

    // ─── GET /api/users/me/stats ──────────────────────────────────────────
    /// <summary>
    /// Thống kê chi tiết: số ván, win rate, tổng điểm.
    /// </summary>
    [HttpGet("me/stats")]
    [ProducesResponseType(typeof(ApiResponse<UserStatsDto>), 200)]
    public async Task<IActionResult> GetMyStats()
    {
        var stats = await _users.GetStatsAsync(CurrentUserId);
        return Ok(stats);
    }

    // ─── GET /api/users/{id} ──────────────────────────────────────────────
    /// <summary>
    /// Public profile của user bất kỳ (xem thông tin đối thủ).
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetById([FromRoute] string id)
    {
        var user = await _users.GetByIdAsync(id);
        if (user is null) return NotFound("User not found");
        return Ok(user);
    }
}
