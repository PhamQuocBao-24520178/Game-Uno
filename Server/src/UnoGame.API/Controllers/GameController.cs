using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UnoGame.API.Services;
using UnoGame.API.Hubs;
using UnoGame.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace UnoGame.API.Controllers;

/// <summary>
/// Quản lý trạng thái và hành động trong ván chơi UNO.
///
/// Lưu ý thiết kế:
///   - Hành động game thời gian thực (PlayCard, DrawCard) → ưu tiên dùng SignalR
///   - REST endpoints này là fallback (khi SignalR mất kết nối) + lấy trạng thái
///   - GET /state     → public state (card count, current player, top card)
///   - GET /hand      → private hand (chỉ gửi cho chính player đó)
///   - GET /history   → lịch sử các ván đã chơi trong phòng
/// </summary>
[ApiController]
[Route("api/games")]
[Authorize]
public class GameController : BaseController
{
    private readonly IGameService _games;
    private readonly IRoomService _rooms;
    private readonly IHubContext<GameHub> _hub;
    private readonly ILogger<GameController> _log;

    public GameController(
        IGameService games,
        IRoomService rooms,
        IHubContext<GameHub> hub,
        ILogger<GameController> log)
    {
        _games = games;
        _rooms = rooms;
        _hub   = hub;
        _log   = log;
    }

    // ─── GET /api/games/{roomId}/state ───────────────────────────────────
    /// <summary>
    /// Trạng thái public của game:
    ///   - Top card, current color, direction
    ///   - Lượt chơi hiện tại
    ///   - Số bài mỗi người (không lộ nội dung bài)
    ///   - Draw pile count
    /// Dùng để sync lại khi reconnect.
    /// </summary>
    [HttpGet("{roomId}/state")]
    [ProducesResponseType(typeof(ApiResponse<GameStateDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetGameState([FromRoute] string roomId)
    {
        var room = await _rooms.GetByIdAsync(roomId);
        if (room is null) return NotFound("Room not found");

        if (room.Status == RoomStatus.Waiting)
            return BadRequest("Game has not started yet");

        var state = await _games.GetPublicStateAsync(roomId, CurrentUserId);
        if (state is null) return NotFound("Game state not found");

        return Ok(state);
    }

    // ─── GET /api/games/{roomId}/hand ────────────────────────────────────
    /// <summary>
    /// Lá bài trên tay của player hiện tại (bí mật, chỉ gửi cho chính họ).
    /// Response bao gồm:
    ///   - Danh sách bài đang giữ
    ///   - Các bài có thể đánh (pre-computed)
    ///   - Có phải rút bài không (mustDraw)
    /// </summary>
    [HttpGet("{roomId}/hand")]
    [ProducesResponseType(typeof(ApiResponse<MyHandDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> GetMyHand([FromRoute] string roomId)
    {
        // Xác nhận player có trong phòng
        if (!await _rooms.IsPlayerInRoomAsync(roomId, CurrentUserId))
            return Forbidden("You are not in this room");

        var hand = await _games.GetMyHandAsync(roomId, CurrentUserId);
        if (hand is null) return NotFound("Game not active");

        return Ok(hand);
    }

    // ─── POST /api/games/{roomId}/play ───────────────────────────────────
    /// <summary>
    /// Đánh một lá bài (REST fallback).
    /// Khuyến khích dùng SignalR GameHub.PlayCard() thay thế.
    ///
    /// Request body:
    ///   { "card": { "color": "Red", "type": "DrawTwo" },
    ///     "chosenColor": null }
    ///
    /// Với Wild/WildDrawFour phải có chosenColor: "Red"|"Green"|"Blue"|"Yellow"
    /// </summary>
    [HttpPost("{roomId}/play")]
    [ProducesResponseType(typeof(ApiResponse<GameActionResult>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    [ProducesResponseType(typeof(ApiResponse), 403)]
    public async Task<IActionResult> PlayCard(
        [FromRoute] string roomId,
        [FromBody]  PlayCardRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest("Invalid card data");

        if (!await _rooms.IsPlayerInRoomAsync(roomId, CurrentUserId))
            return Forbidden("You are not in this room");

        // Validate Wild color requirement
        bool isWild = req.Card.Type is "Wild" or "WildDrawFour";
        if (isWild && string.IsNullOrEmpty(req.ChosenColor))
            return BadRequest("Must specify chosenColor when playing Wild or WildDrawFour");

        if (!string.IsNullOrEmpty(req.ChosenColor) &&
            !new[] { "Red", "Green", "Blue", "Yellow" }.Contains(req.ChosenColor))
            return BadRequest("chosenColor must be: Red | Green | Blue | Yellow");

        var result = await _games.PlayCardAsync(roomId, CurrentUserId, req);

        if (!result.Success)
            return BadRequest(result.Error!);

        // Broadcast via SignalR (REST cũng phải notify real-time)
        await _hub.Clients.Group(roomId).SendAsync("CardPlayed", new
        {
            PlayerId     = CurrentUserId,
            Card         = req.Card,
            ChosenColor  = req.ChosenColor,
            NextPlayerId = result.State?.CurrentPlayerId,
            Effects      = new { result.IsGameOver, result.WinnerId }
        });

        if (result.IsGameOver)
        {
            await _hub.Clients.Group(roomId).SendAsync("GameOver", new
            {
                WinnerId = result.WinnerId
            });
        }

        return Ok(result);
    }

    // ─── POST /api/games/{roomId}/draw ───────────────────────────────────
    /// <summary>
    /// Rút bài.
    /// - Nếu đang bị +2/+4 stack: rút đúng số lá bị phạt
    /// - Nếu lá rút có thể đánh ngay: response có canPlayDrawn = true
    ///   → client chờ player quyết định đánh hay bỏ
    ///
    /// Lá rút chỉ gửi cho player rút (private).
    /// Các player khác nhận "PlayerDrewCards" event qua SignalR.
    /// </summary>
    [HttpPost("{roomId}/draw")]
    [ProducesResponseType(typeof(ApiResponse<GameActionResult>), 200)]
    public async Task<IActionResult> DrawCard([FromRoute] string roomId)
    {
        if (!await _rooms.IsPlayerInRoomAsync(roomId, CurrentUserId))
            return Forbidden("You are not in this room");

        var result = await _games.DrawCardAsync(roomId, CurrentUserId);

        if (!result.Success)
            return BadRequest(result.Error!);

        // Notify others (không lộ lá bài)
        await _hub.Clients.GroupExcept(roomId, new[] { Context_GetConnectionId() }).SendAsync(
            "PlayerDrewCards",
            new {
                PlayerId  = CurrentUserId,
                CardCount = result.DrawnCards?.Count ?? 0
            });

        return Ok(result);
    }

    // ─── POST /api/games/{roomId}/uno ────────────────────────────────────
    /// <summary>
    /// Gọi UNO hoặc bắt người khác quên gọi UNO.
    ///
    /// Có 2 trường hợp:
    ///   1. Tự gọi UNO (targetId = mình): khi còn 1 bài, gọi trước khi đánh
    ///   2. Bắt UNO (targetId = người khác): người kia còn 1 bài mà chưa gọi
    ///      → họ bị phạt rút 2 lá
    /// </summary>
    [HttpPost("{roomId}/uno")]
    [ProducesResponseType(typeof(ApiResponse<GameActionResult>), 200)]
    public async Task<IActionResult> CallUno(
        [FromRoute] string roomId,
        [FromQuery] string targetId)
    {
        if (string.IsNullOrEmpty(targetId))
            return BadRequest("targetId is required");

        if (!await _rooms.IsPlayerInRoomAsync(roomId, CurrentUserId))
            return Forbidden("You are not in this room");

        var result = await _games.CallUnoAsync(roomId, CurrentUserId, targetId);

        await _hub.Clients.Group(roomId).SendAsync("UnoCalled", new
        {
            CallerId = CurrentUserId,
            TargetId = targetId,
            result.Success,
            result.Error
        });

        return Ok(result);
    }

    // ─── GET /api/games/{roomId}/history ─────────────────────────────────
    /// <summary>
    /// Lịch sử các ván đã hoàn thành trong phòng này.
    /// Dùng hiển thị kết quả các round trước (multi-round game).
    /// </summary>
    [HttpGet("{roomId}/history")]
    [ProducesResponseType(typeof(ApiResponse<List<GameHistoryDto>>), 200)]
    public async Task<IActionResult> GetRoomHistory(
        [FromRoute] string roomId,
        [FromQuery] int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 50);

        var room = await _rooms.GetByIdAsync(roomId);
        if (room is null) return NotFound("Room not found");

        var history = await _games.GetRoomHistoryAsync(roomId, limit);
        return Ok(history);
    }

    // ─── GET /api/games/my-history ───────────────────────────────────────
    /// <summary>
    /// Lịch sử ván chơi của user hiện tại (tất cả các phòng).
    /// Dùng trên trang profile.
    /// </summary>
    [HttpGet("my-history")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<GameHistoryDto>>), 200)]
    public async Task<IActionResult> GetMyHistory(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        page     = Math.Max(page, 1);

        var items = await _games.GetUserHistoryAsync(CurrentUserId, page, pageSize);
        // Giả sử service trả về total count — ở đây simplify
        return Paginated(items, items.Count, page, pageSize);
    }

    // ─── GET /api/games/{gameId} ─────────────────────────────────────────
    /// <summary>
    /// Chi tiết kết quả một ván cụ thể (post-game summary screen).
    /// </summary>
    [HttpGet("results/{gameId}")]
    [ProducesResponseType(typeof(ApiResponse<GameHistoryDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetGameResult([FromRoute] string gameId)
    {
        var game = await _games.GetGameByIdAsync(gameId);
        if (game is null) return NotFound("Game record not found");
        return Ok(game);
    }

    // Helper: Trong thực tế, lấy connectionId từ IHttpContextAccessor hoặc mapping
    private static string Context_GetConnectionId() => string.Empty;
}
