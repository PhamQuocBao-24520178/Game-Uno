using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UnoGame.API.Services;

namespace UnoGame.API.Controllers;

/// <summary>
/// Bảng xếp hạng người chơi UNO.
///
///   GET /api/leaderboard          → global all-time
///   GET /api/leaderboard/weekly   → reset mỗi thứ Hai 00:00 UTC
///   GET /api/leaderboard/me/rank  → thứ hạng của bản thân
/// </summary>
[ApiController]
[Route("api/leaderboard")]
[Authorize]
public class LeaderboardController : BaseController
{
    private readonly ILeaderboardService _leaderboard;

    public LeaderboardController(ILeaderboardService leaderboard)
    {
        _leaderboard = leaderboard;
    }

    // ─── GET /api/leaderboard ────────────────────────────────────────────
    /// <summary>
    /// Bảng xếp hạng toàn thời gian — sắp xếp theo TotalScore giảm dần.
    /// Trả về top N players, hỗ trợ phân trang.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<LeaderboardEntryDto>>), 200)]
    public async Task<IActionResult> GetGlobal(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(page, 1);

        var result = await _leaderboard.GetGlobalAsync(page, pageSize);
        return Paginated(result.Items, result.TotalCount, page, pageSize);
    }

    // ─── GET /api/leaderboard/weekly ─────────────────────────────────────
    /// <summary>
    /// Bảng xếp hạng tuần này (từ thứ Hai đến hiện tại).
    /// Reset tự động mỗi đầu tuần.
    /// </summary>
    [HttpGet("weekly")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<LeaderboardEntryDto>>), 200)]
    public async Task<IActionResult> GetWeekly(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(page, 1);

        var result = await _leaderboard.GetWeeklyAsync(page, pageSize);
        return Paginated(result.Items, result.TotalCount, page, pageSize);
    }

    // ─── GET /api/leaderboard/me/rank ────────────────────────────────────
    /// <summary>
    /// Xếp hạng của người dùng hiện tại:
    ///   - Global rank + weekly rank
    ///   - Tổng số người chơi
    ///   - Phần trăm top (ví dụ: top 5%)
    /// </summary>
    [HttpGet("me/rank")]
    [ProducesResponseType(typeof(ApiResponse<MyRankDto>), 200)]
    public async Task<IActionResult> GetMyRank()
    {
        var rank = await _leaderboard.GetMyRankAsync(CurrentUserId);
        return Ok(rank);
    }
}
