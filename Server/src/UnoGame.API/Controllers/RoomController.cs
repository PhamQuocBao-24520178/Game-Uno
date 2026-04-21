using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using UnoGame.API.Hubs;
using UnoGame.API.Services;
using UnoGame.Core.Models;

namespace UnoGame.API.Controllers;

/// <summary>
/// Quản lý phòng chơi UNO.
///
/// Flow:
///   POST   /api/rooms              → tạo phòng (host)
///   POST   /api/rooms/{id}/join    → vào phòng
///   PUT    /api/rooms/{id}/ready   → báo sẵn sàng
///   POST   /api/rooms/{id}/start   → bắt đầu game (host only)
///   DELETE /api/rooms/{id}/leave   → rời phòng
/// </summary>
[ApiController]
[Route("api/rooms")]
[Authorize]
public class RoomController : BaseController
{
    private readonly IRoomService         _rooms;
    private readonly IGameService         _games;
    private readonly IHubContext<GameHub> _hub;
    private readonly IConnectionManager   _connections;
    private readonly ILogger<RoomController> _log;

    public RoomController(
        IRoomService         rooms,
        IGameService         games,
        IHubContext<GameHub> hub,
        IConnectionManager   connections,
        ILogger<RoomController> log)
    {
        _rooms       = rooms;
        _games       = games;
        _hub         = hub;
        _connections = connections;
        _log         = log;
    }

    // ─── GET /api/rooms ───────────────────────────────────────────────────────
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<RoomSummaryDto>>), 200)]
    public async Task<IActionResult> ListRooms(
        [FromQuery] int    page     = 1,
        [FromQuery] int    pageSize = 20,
        [FromQuery] string? search  = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        var result = await _rooms.ListPublicRoomsAsync(Math.Max(page, 1), pageSize, search);
        return Paginated(result.Items, result.TotalCount, page, pageSize);
    }

    // ─── GET /api/rooms/{id} ──────────────────────────────────────────────────
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<RoomDto>), 200)]
    public async Task<IActionResult> GetRoom([FromRoute] string id)
    {
        var room = await _rooms.GetByIdAsync(id);
        if (room is null) return NotFound("Room not found");
        return Ok(room);
    }

    // ─── GET /api/rooms/code/{code} ───────────────────────────────────────────
    [HttpGet("code/{code}")]
    [ProducesResponseType(typeof(ApiResponse<RoomDto>), 200)]
    public async Task<IActionResult> GetRoomByCode([FromRoute] string code)
    {
        var room = await _rooms.GetByCodeAsync(code.ToUpperInvariant());
        if (room is null) return NotFound("Room not found");
        return Ok(room);
    }

    // ─── POST /api/rooms ──────────────────────────────────────────────────────
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RoomDto>), 201)]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest req)
    {
        if (!ModelState.IsValid) return BadRequest("Invalid room settings");
        if (req.BotCount >= req.MaxPlayers)
            return BadRequest("Bot count must be less than max players");

        var room = await _rooms.CreateAsync(CurrentUserId, req);
        _log.LogInformation("Room created: {Code} by {UserId}", room.RoomCode, CurrentUserId);
        return Created($"/api/rooms/{room.Id}", room);
    }

    // ─── POST /api/rooms/{id}/join ────────────────────────────────────────────
    [HttpPost("{id}/join")]
    [ProducesResponseType(typeof(ApiResponse<RoomDto>), 200)]
    public async Task<IActionResult> JoinRoom(
        [FromRoute] string id,
        [FromBody] JoinRoomRequest? req = null)
    {
        var room = await _rooms.GetByIdAsync(id);
        if (room is null) return NotFound("Room not found");
        if (room.Status != RoomStatus.Waiting) return Conflict("Room is not accepting players");
        if (room.Players.Count >= room.MaxPlayers) return Conflict("Room is full");
        if (await _rooms.IsPlayerInRoomAsync(id, CurrentUserId)) return Conflict("Already in this room");

        var updated = await _rooms.JoinAsync(id, CurrentUserId, req?.Password);
        await _hub.Clients.Group(id).SendAsync("PlayerJoined", new {
            UserId = CurrentUserId, PlayerCount = updated.Players.Count
        });
        return Ok(updated, "Joined room");
    }

    // ─── POST /api/rooms/code/{code}/join ────────────────────────────────────
    [HttpPost("code/{code}/join")]
    [ProducesResponseType(typeof(ApiResponse<RoomDto>), 200)]
    public async Task<IActionResult> JoinByCode(
        [FromRoute] string code,
        [FromBody] JoinRoomRequest? req = null)
    {
        var room = await _rooms.GetByCodeAsync(code.ToUpperInvariant());
        if (room is null) return NotFound("Invalid room code");
        if (room.Status != RoomStatus.Waiting) return Conflict("Room is not accepting players");
        if (room.Players.Count >= room.MaxPlayers) return Conflict("Room is full");

        var updated = await _rooms.JoinAsync(room.Id, CurrentUserId, req?.Password);
        await _hub.Clients.Group(room.Id).SendAsync("PlayerJoined", new {
            UserId = CurrentUserId, PlayerCount = updated.Players.Count
        });
        return Ok(updated, "Joined room");
    }

    // ─── DELETE /api/rooms/{id}/leave ────────────────────────────────────────
    [HttpDelete("{id}/leave")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> LeaveRoom([FromRoute] string id)
    {
        if (!await _rooms.IsPlayerInRoomAsync(id, CurrentUserId))
            return BadRequest("You are not in this room");

        await _rooms.LeaveAsync(id, CurrentUserId);
        var updated = await _rooms.GetByIdAsync(id);
        await _hub.Clients.Group(id).SendAsync("PlayerLeft", new {
            UserId = CurrentUserId, NewHostId = updated?.HostId, PlayerCount = updated?.Players.Count ?? 0
        });
        return NoContent();
    }

    // ─── PUT /api/rooms/{id}/ready ────────────────────────────────────────────
    [HttpPut("{id}/ready")]
    [ProducesResponseType(typeof(ApiResponse<RoomDto>), 200)]
    public async Task<IActionResult> ToggleReady([FromRoute] string id)
    {
        var room = await _rooms.GetByIdAsync(id);
        if (room is null) return NotFound("Room not found");
        if (room.Status != RoomStatus.Waiting) return Conflict("Game already started");
        if (!await _rooms.IsPlayerInRoomAsync(id, CurrentUserId))
            return BadRequest("You are not in this room");

        await _rooms.MarkReadyAsync(id, CurrentUserId);
        var updated = await _rooms.GetByIdAsync(id);
        bool allReady = updated?.Players.Where(p => !p.IsHost && !p.IsBot).All(p => p.IsReady) ?? false;

        await _hub.Clients.Group(id).SendAsync(HubEvents.PlayerReadyChanged, new {
            UserId  = CurrentUserId, AllReady = allReady
        });
        return Ok(updated!);
    }

    // ─── POST /api/rooms/{id}/start ───────────────────────────────────────────
    [HttpPost("{id}/start")]
    [ProducesResponseType(typeof(ApiResponse<RoomDto>), 200)]
    public async Task<IActionResult> StartGame([FromRoute] string id)
    {
        var room = await _rooms.GetByIdAsync(id);
        if (room is null) return NotFound("Room not found");
        if (room.HostId != CurrentUserId) return Forbidden("Only the host can start the game");
        if (room.Status != RoomStatus.Waiting) return Conflict("Game already started");

        int total = room.Players.Count + room.BotCount;
        if (total < 2) return BadRequest("Need at least 2 players (including bots)");

        var nonHostNotReady = room.Players.Where(p => !p.IsHost && !p.IsBot && !p.IsReady).ToList();
        if (nonHostNotReady.Any()) return BadRequest("All players must be ready");

        var updated = await _rooms.StartGameAsync(id, CurrentUserId);

        // Initialize game engine (non-blocking broadcast)
        _ = Task.Run(async () =>
        {
            try
            {
                await _games.InitializeGameAsync(id, updated.Players);
                await HubBroadcaster.BroadcastGameStartedAsync(_hub, _connections, _games, id);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to start game in room {RoomId}", id);
            }
        });

        _log.LogInformation("Game starting: room={RoomId}", id);
        return Ok(updated, "Game starting — countdown beginning");
    }

    // ─── PUT /api/rooms/{id}/settings ────────────────────────────────────────
    [HttpPut("{id}/settings")]
    [ProducesResponseType(typeof(ApiResponse<RoomDto>), 200)]
    public async Task<IActionResult> UpdateSettings(
        [FromRoute] string id,
        [FromBody] UpdateRoomSettingsRequest req)
    {
        var room = await _rooms.GetByIdAsync(id);
        if (room is null) return NotFound("Room not found");
        if (room.HostId != CurrentUserId) return Forbidden("Only the host can change settings");
        if (room.Status != RoomStatus.Waiting) return Conflict("Cannot change settings after game started");

        var updated = await _rooms.UpdateSettingsAsync(id, CurrentUserId, req);
        await _hub.Clients.Group(id).SendAsync(HubEvents.RoomSettingsUpdated, new {
            updated.MaxPlayers, updated.BotCount, updated.BotDifficulty
        });
        return Ok(updated, "Settings updated");
    }

    // ─── DELETE /api/rooms/{id}/players/{userId} ──────────────────────────────
    [HttpDelete("{id}/players/{userId}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> KickPlayer(
        [FromRoute] string id, [FromRoute] string userId)
    {
        var room = await _rooms.GetByIdAsync(id);
        if (room is null) return NotFound("Room not found");
        if (room.HostId != CurrentUserId) return Forbidden("Only the host can kick players");
        if (userId == CurrentUserId) return BadRequest("Cannot kick yourself");
        if (room.Status != RoomStatus.Waiting) return Conflict("Cannot kick after game started");

        await _rooms.KickPlayerAsync(id, CurrentUserId, userId);
        await _hub.Clients.Group(id).SendAsync(HubEvents.PlayerKicked, new { UserId = userId });
        return NoContent();
    }

    // ─── GET /api/rooms/{id}/players ─────────────────────────────────────────
    [HttpGet("{id}/players")]
    [ProducesResponseType(typeof(ApiResponse<List<RoomPlayerDto>>), 200)]
    public async Task<IActionResult> GetPlayers([FromRoute] string id)
    {
        var room = await _rooms.GetByIdAsync(id);
        if (room is null) return NotFound("Room not found");
        return Ok(await _rooms.GetPlayersAsync(id));
    }

    // ─── DELETE /api/rooms/{id} ───────────────────────────────────────────────
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> CloseRoom([FromRoute] string id)
    {
        var room = await _rooms.GetByIdAsync(id);
        if (room is null) return NotFound("Room not found");
        if (room.HostId != CurrentUserId) return Forbidden("Only the host can close the room");
        if (room.Status == RoomStatus.Playing) return Conflict("Cannot close during game");

        await _rooms.CloseRoomAsync(id, CurrentUserId);
        await _hub.Clients.Group(id).SendAsync(HubEvents.RoomClosed, new { Reason = "Host closed the room" });
        return NoContent();
    }
}

// ─── Hub Broadcaster ──────────────────────────────────────────────────────────

/// <summary>
/// Static helper: broadcast game start events qua IHubContext (không cần Hub instance).
/// Dùng được từ Controller, Background Service, etc.
/// </summary>
public static class HubBroadcaster
{
    public static async Task BroadcastGameStartedAsync(
        IHubContext<GameHub> hub,
        IConnectionManager   connections,
        IGameService         gameService,
        string               roomId)
    {
        // Countdown 3 giây
        await hub.Clients.Group(roomId).SendAsync(
            HubEvents.GameStarting,
            new GameStartingPayload(roomId, CountdownSeconds: 3,
                StartsAt: DateTime.UtcNow.AddSeconds(3)));
        await Task.Delay(3000);

        var state = await gameService.GetPublicStateAsync(roomId, "__system__");
        if (state is null) return;

        // GameStarted (public state)
        await hub.Clients.Group(roomId).SendAsync(
            HubEvents.GameStarted,
            new GameStartedPayload(roomId, state, DateTime.UtcNow));

        // HandDealt (private per player)
        foreach (var connId in connections.GetConnectionsInRoom(roomId))
        {
            var uid = connections.GetUserId(connId);
            if (uid is null) continue;
            var hand = await gameService.GetMyHandAsync(roomId, uid);
            if (hand is null) continue;

            await hub.Clients.Client(connId).SendAsync(
                HubEvents.HandDealt,
                new HandDealtPayload(
                    Cards    : hand.Cards,
                    Playable : hand.Playable,
                    IsMyTurn : state.CurrentPlayerId == uid,
                    MustDraw : hand.MustDraw));
        }

        // Initial TurnChanged
        var first = state.Players.FirstOrDefault(p => p.PlayerId == state.CurrentPlayerId);
        await hub.Clients.Group(roomId).SendAsync(
            HubEvents.TurnChanged,
            new TurnChangedPayload(
                state.CurrentPlayerId, first?.DisplayName ?? "", first?.IsBot ?? false,
                TurnNumber: 1, PendingDrawCount: 0, TimeoutSeconds: 30,
                TurnStartedAt: DateTime.UtcNow));
    }
}
