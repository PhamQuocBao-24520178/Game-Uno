using System.Collections.Concurrent;
using UnoGame.Core.Room;
using UnoGame.Core.Models;

namespace UnoGame.API.Services;

// ════════════════════════════════════════════════════════════════════════════
// ROOM MANAGER INTERFACE
// ════════════════════════════════════════════════════════════════════════════

public interface IRoomManager
{
    // ── Room lifecycle ────────────────────────────────────────────────────
    Task<RoomRuntimeState> CreateRoomAsync(string hostId, string hostName, string hostAvatar,
        CreateRoomRequest req);
    Task<JoinRoomResult>   JoinRoomAsync(string roomId, string userId,
        string displayName, string avatarUrl, string? password = null);
    Task<JoinRoomResult>   JoinAsSpectatorAsync(string roomId, string userId,
        string displayName, string avatarUrl);
    Task                   LeaveRoomAsync(string roomId, string userId);
    Task                   CloseRoomAsync(string roomId, string requesterId);

    // ── Slot management ───────────────────────────────────────────────────
    Task ToggleReadyAsync(string roomId, string userId);
    Task KickPlayerAsync (string roomId, string hostId, string targetUserId);
    Task UpdateSettingsAsync(string roomId, string hostId, UpdateRoomSettingsRequest req);

    // ── Game lifecycle ────────────────────────────────────────────────────
    Task<bool> TryStartGameAsync(string roomId, string hostId);
    Task       OnGameEndedAsync (string roomId);

    // ── Spectator ─────────────────────────────────────────────────────────
    Task PromoteSpectatorAsync(string roomId, string userId);   // spectator → player (if slot available)
    Task DemoteToSpectatorAsync(string roomId, string userId);  // player → spectator

    // ── Disconnect / Reconnect ────────────────────────────────────────────
    Task<DisconnectResult> HandleDisconnectAsync(string userId);
    Task<ReconnectResult>  HandleReconnectAsync (string userId, string roomId);

    // ── Matchmaking ───────────────────────────────────────────────────────
    Task<MatchmakingTicket> EnqueueMatchmakingAsync(string userId, string displayName,
        string avatarUrl, int preferredPlayers = 4, bool allowBots = true);
    Task CancelMatchmakingAsync(string userId);
    Task<MatchmakingStatus> GetMatchmakingStatusAsync(string userId);

    // ── Queries ───────────────────────────────────────────────────────────
    RoomRuntimeState? GetRoomState(string roomId);
    RoomRuntimeState? GetRoomByUser(string userId);
    bool IsInRoom(string userId);
    bool IsInMatchmaking(string userId);
    IReadOnlyList<RoomRuntimeState> GetPublicWaitingRooms();
}

// ── Result types ──────────────────────────────────────────────────────────────

public enum JoinRoomOutcome
{
    Success, RoomNotFound, RoomFull, WrongPassword,
    AlreadyInRoom, GameInProgress, Banned
}

public record JoinRoomResult(JoinRoomOutcome Outcome, RoomRuntimeState? Room = null,
    string? Error = null)
{
    public bool Success => Outcome == JoinRoomOutcome.Success;
    public static JoinRoomResult Ok(RoomRuntimeState room) =>
        new(JoinRoomOutcome.Success, room);
    public static JoinRoomResult Fail(JoinRoomOutcome reason, string error) =>
        new(reason, null, error);
}

public enum DisconnectOutcome { Noted, AlreadyGone }
public record DisconnectResult(DisconnectOutcome Outcome, DisconnectRecord? Record = null);

public enum ReconnectOutcome { Success, WindowExpired, RoomGone, NotDisconnected }
public record ReconnectResult(ReconnectOutcome Outcome, RoomRuntimeState? Room = null,
    string? BotIdToRemove = null);

public enum MatchmakingStatusEnum { Queued, Matched, TimedOut, NotQueued }
public record MatchmakingStatus(MatchmakingStatusEnum Status, MatchmakingTicket? Ticket = null,
    string? MatchedRoomId = null, int QueuePosition = 0);

// ════════════════════════════════════════════════════════════════════════════
// ROOM MANAGER IMPLEMENTATION
// Singleton — toàn bộ state phòng lưu trong memory.
// ════════════════════════════════════════════════════════════════════════════

public sealed class RoomManager : IRoomManager, IAsyncDisposable
{
    // ── Storage ───────────────────────────────────────────────────────────

    // roomId → RoomRuntimeState
    private readonly ConcurrentDictionary<string, RoomRuntimeState> _rooms = new();

    // userId → roomId (người chơi đang ở phòng nào)
    private readonly ConcurrentDictionary<string, string> _userRoom = new();

    // Matchmaking queue: userId → MatchmakingTicket
    private readonly ConcurrentDictionary<string, MatchmakingTicket> _matchQueue = new();

    // Disconnect records: userId → DisconnectRecord
    private readonly ConcurrentDictionary<string, DisconnectRecord> _disconnects = new();

    // ── Dependencies ──────────────────────────────────────────────────────

    private readonly IRoomService            _roomService;
    private readonly IUserService            _userService;
    private readonly IGameService            _gameService;
    private readonly ILogger<RoomManager>   _log;
    private readonly RoomManagerOptions     _options;

    // ── Background tasks ──────────────────────────────────────────────────

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _disconnectWatcher;
    private readonly Task _matchmakingWorker;

    public RoomManager(
        IRoomService         roomService,
        IUserService         userService,
        IGameService         gameService,
        ILogger<RoomManager> log,
        RoomManagerOptions?  options = null)
    {
        _roomService = roomService;
        _userService = userService;
        _gameService = gameService;
        _log         = log;
        _options     = options ?? new RoomManagerOptions();

        _disconnectWatcher = Task.Run(DisconnectWatcherLoopAsync);
        _matchmakingWorker = Task.Run(MatchmakingWorkerLoopAsync);
    }

    // ════════════════════════════════════════════════════════════════════
    // CREATE ROOM
    // ════════════════════════════════════════════════════════════════════

    public async Task<RoomRuntimeState> CreateRoomAsync(
        string hostId, string hostName, string hostAvatar, CreateRoomRequest req)
    {
        // Nếu user đang ở phòng khác → tự rời trước
        if (_userRoom.TryGetValue(hostId, out var oldRoomId))
            await LeaveRoomAsync(oldRoomId, hostId);

        // Persist vào DB
        var dto  = await _roomService.CreateAsync(hostId, req);
        var state = new RoomRuntimeState(dto.Id, hostId) { MaxPlayers = req.MaxPlayers };

        // Thêm host vào slot
        var hostSlot = new RoomSlot(hostId, hostName, hostAvatar, isHost: true);
        state.Slots.Add(hostSlot);

        // Thêm bots
        for (int i = 0; i < req.BotCount; i++)
        {
            var botId   = $"bot-{dto.Id[..6]}-{i + 1}";
            var botSlot = new RoomSlot(botId, $"Bot {i + 1}", isBot: true);
            state.Slots.Add(botSlot);
        }

        _rooms[dto.Id]   = state;
        _userRoom[hostId] = dto.Id;

        _log.LogInformation("Room created: {RoomId} ({Code}) by {Host}",
            dto.Id, dto.RoomCode, hostName);

        return state;
    }

    // ════════════════════════════════════════════════════════════════════
    // JOIN ROOM
    // ════════════════════════════════════════════════════════════════════

    public async Task<JoinRoomResult> JoinRoomAsync(
        string roomId, string userId, string displayName, string avatarUrl, string? password = null)
    {
        if (!_rooms.TryGetValue(roomId, out var state))
        {
            // Thử load từ DB (room mới tạo chưa được sync vào memory)
            var dto = await _roomService.GetByIdAsync(roomId);
            if (dto is null)
                return JoinRoomResult.Fail(JoinRoomOutcome.RoomNotFound, "Room not found");
            state = await SyncRoomFromDbAsync(dto);
        }

        // Validate
        if (state.Phase is RoomPhase.Playing or RoomPhase.Closed)
            return JoinRoomResult.Fail(JoinRoomOutcome.GameInProgress, "Game is in progress");

        if (state.IsPlayer(userId))
            return JoinRoomResult.Fail(JoinRoomOutcome.AlreadyInRoom, "Already in room");

        // Full check — use in-memory MaxPlayers (avoids extra DB round-trip)
        int humanSlots = state.Slots.Count(s => !s.IsBot);
        if (humanSlots >= state.MaxPlayers)
            return JoinRoomResult.Fail(JoinRoomOutcome.RoomFull, "Room is full");

        // Password check (fetch from DB only when needed)
        if (!string.IsNullOrEmpty(password))
        {
            var roomDoc = await _roomService.GetByIdAsync(roomId);
            if (roomDoc is null)
                return JoinRoomResult.Fail(JoinRoomOutcome.RoomNotFound, "Room not found");
            if (!ValidatePassword(roomDoc, password))
                return JoinRoomResult.Fail(JoinRoomOutcome.WrongPassword, "Incorrect password");
        }

        // Nếu đang ở phòng khác → tự rời
        if (_userRoom.TryGetValue(userId, out var currentRoom) && currentRoom != roomId)
            await LeaveRoomAsync(currentRoom, userId);

        // Add slot
        var slot = new RoomSlot(userId, displayName, avatarUrl);
        state.Slots.Add(slot);
        _userRoom[userId] = roomId;

        // Persist
        await _roomService.JoinAsync(roomId, userId, password);

        _log.LogInformation("Player joined: {UserId} → room {RoomId}", userId, roomId);
        return JoinRoomResult.Ok(state);
    }

    // ════════════════════════════════════════════════════════════════════
    // JOIN AS SPECTATOR
    // ════════════════════════════════════════════════════════════════════

    public async Task<JoinRoomResult> JoinAsSpectatorAsync(
        string roomId, string userId, string displayName, string avatarUrl)
    {
        if (!_rooms.TryGetValue(roomId, out var state))
        {
            var dto = await _roomService.GetByIdAsync(roomId);
            if (dto is null)
                return JoinRoomResult.Fail(JoinRoomOutcome.RoomNotFound, "Room not found");
            state = await SyncRoomFromDbAsync(dto);
        }

        if (state.Phase == RoomPhase.Closed)
            return JoinRoomResult.Fail(JoinRoomOutcome.RoomNotFound, "Room is closed");

        // Không vào spectator nếu đã là player
        if (state.IsPlayer(userId))
            return JoinRoomResult.Fail(JoinRoomOutcome.AlreadyInRoom, "Already a player in this room");

        // Xóa spectator cũ nếu có
        state.Spectators.RemoveAll(s => s.UserId == userId);

        // Giới hạn số spectator
        if (state.Spectators.Count >= _options.MaxSpectatorsPerRoom)
            return JoinRoomResult.Fail(JoinRoomOutcome.RoomFull,
                $"Spectator limit reached ({_options.MaxSpectatorsPerRoom})");

        state.Spectators.Add(new SpectatorSlot(userId, displayName, avatarUrl));
        _userRoom[userId] = roomId;

        _log.LogInformation("Spectator joined: {UserId} → room {RoomId}", userId, roomId);
        return JoinRoomResult.Ok(state);
    }

    // ════════════════════════════════════════════════════════════════════
    // LEAVE ROOM
    // ════════════════════════════════════════════════════════════════════

    public async Task LeaveRoomAsync(string roomId, string userId)
    {
        if (!_rooms.TryGetValue(roomId, out var state)) return;

        _userRoom.TryRemove(userId, out _);
        _disconnects.TryRemove(userId, out _);

        var isSpectator = state.IsSpectator(userId);

        if (isSpectator)
        {
            state.Spectators.RemoveAll(s => s.UserId == userId);
            _log.LogInformation("Spectator left: {UserId} from room {RoomId}", userId, roomId);
            return;
        }

        var slot = state.GetSlot(userId);
        if (slot is null) return;

        bool wasHost = slot.IsHost;
        state.Slots.Remove(slot);

        // Chuyển host nếu cần
        string? newHostId = null;
        if (wasHost && state.Slots.Any(s => !s.IsBot))
        {
            var nextHost = state.Slots.First(s => !s.IsBot);
            nextHost.IsHost = true;
            state.HostId    = nextHost.SlotId;
            newHostId        = nextHost.SlotId;
        }

        // Phòng trống → đóng
        if (!state.Slots.Any(s => !s.IsBot))
        {
            state.Phase = RoomPhase.Closed;
            _rooms.TryRemove(roomId, out _);
            await _roomService.CloseRoomAsync(roomId, userId);
            _log.LogInformation("Room auto-closed (empty): {RoomId}", roomId);
            return;
        }

        // Nếu game đang chạy và còn < 2 người → kết thúc game
        if (state.Phase == RoomPhase.Playing && state.HumanPlayerCount < 1)
        {
            state.Phase = RoomPhase.PostGame;
            _log.LogWarning("Game ended due to too few players: {RoomId}", roomId);
        }

        await _roomService.LeaveAsync(roomId, userId);
        _log.LogInformation("Player left: {UserId} from room {RoomId} (newHost={Host})",
            userId, roomId, newHostId ?? "none");
    }

    // ════════════════════════════════════════════════════════════════════
    // CLOSE ROOM
    // ════════════════════════════════════════════════════════════════════

    public async Task CloseRoomAsync(string roomId, string requesterId)
    {
        if (!_rooms.TryGetValue(roomId, out var state)) return;
        if (state.HostId != requesterId)
            throw new InvalidOperationException("Only the host can close the room");

        state.Phase = RoomPhase.Closed;

        // Xóa tất cả user khỏi mapping
        foreach (var slot in state.Slots)
            _userRoom.TryRemove(slot.SlotId, out _);
        foreach (var spec in state.Spectators)
            _userRoom.TryRemove(spec.UserId, out _);

        _rooms.TryRemove(roomId, out _);
        await _roomService.CloseRoomAsync(roomId, requesterId);
        _log.LogInformation("Room closed by host: {RoomId}", roomId);
    }

    // ════════════════════════════════════════════════════════════════════
    // SLOT MANAGEMENT
    // ════════════════════════════════════════════════════════════════════

    public async Task ToggleReadyAsync(string roomId, string userId)
    {
        if (!_rooms.TryGetValue(roomId, out var state)) return;
        var slot = state.GetSlot(userId);
        if (slot is null || slot.IsBot) return;

        slot.IsReady = !slot.IsReady;
        await _roomService.MarkReadyAsync(roomId, userId);
    }

    public async Task KickPlayerAsync(string roomId, string hostId, string targetUserId)
    {
        if (!_rooms.TryGetValue(roomId, out var state)) return;
        if (state.HostId != hostId)
            throw new InvalidOperationException("Only the host can kick players");
        if (state.Phase != RoomPhase.Waiting)
            throw new InvalidOperationException("Cannot kick during game");

        await LeaveRoomAsync(roomId, targetUserId);
        await _roomService.KickPlayerAsync(roomId, hostId, targetUserId);
    }

    public async Task UpdateSettingsAsync(string roomId, string hostId, UpdateRoomSettingsRequest req)
    {
        if (!_rooms.TryGetValue(roomId, out var state)) return;
        if (state.HostId != hostId)
            throw new InvalidOperationException("Only the host can update settings");

        // Update bot count in memory
        if (req.BotCount.HasValue)
        {
            int current = state.Slots.Count(s => s.IsBot);
            int target  = req.BotCount.Value;

            if (target > current) // thêm bot
            {
                for (int i = current; i < target; i++)
                {
                    var botId   = $"bot-{roomId[..6]}-{i + 1}";
                    state.Slots.Add(new RoomSlot(botId, $"Bot {i + 1}", isBot: true));
                }
            }
            else if (target < current) // bớt bot
            {
                var botsToRemove = state.Slots.Where(s => s.IsBot).TakeLast(current - target).ToList();
                foreach (var b in botsToRemove) state.Slots.Remove(b);
            }
        }

        await _roomService.UpdateSettingsAsync(roomId, hostId, req);
    }

    // ════════════════════════════════════════════════════════════════════
    // GAME LIFECYCLE
    // ════════════════════════════════════════════════════════════════════

    public async Task<bool> TryStartGameAsync(string roomId, string hostId)
    {
        if (!_rooms.TryGetValue(roomId, out var state)) return false;
        if (state.HostId != hostId) return false;
        if (state.Phase != RoomPhase.Waiting) return false;

        // Validate: ≥2 total (human + bot), non-host humans all ready
        int total = state.Slots.Count;
        if (total < 2) return false;

        bool allReady = state.Slots
            .Where(s => !s.IsHost && !s.IsBot)
            .All(s => s.IsReady);
        if (!allReady) return false;

        state.Phase = RoomPhase.CountingDown;
        await _roomService.StartGameAsync(roomId, hostId);

        // Initialize game engine
        var players = state.Slots.Select(s => new RoomPlayerDto
        {
            UserId      = s.SlotId,
            DisplayName = s.DisplayName,
            AvatarUrl   = s.AvatarUrl,
            IsBot       = s.IsBot,
            IsHost      = s.IsHost,
            IsReady     = s.IsReady,
            IsConnected = s.Status == SlotStatus.Connected
        }).ToList();

        await _gameService.InitializeGameAsync(roomId, players);
        state.Phase = RoomPhase.Playing;

        return true;
    }

    public Task OnGameEndedAsync(string roomId)
    {
        if (_rooms.TryGetValue(roomId, out var state))
        {
            state.Phase = RoomPhase.PostGame;

            // Reset ready status cho round tiếp theo
            foreach (var slot in state.Slots.Where(s => !s.IsBot))
                slot.IsReady = false;
        }
        return Task.CompletedTask;
    }

    // ════════════════════════════════════════════════════════════════════
    // SPECTATOR PROMOTION / DEMOTION
    // ════════════════════════════════════════════════════════════════════

    public async Task PromoteSpectatorAsync(string roomId, string userId)
    {
        if (!_rooms.TryGetValue(roomId, out var state)) return;
        var spec = state.GetSpectator(userId);
        if (spec is null) throw new InvalidOperationException("User is not a spectator");

        int humanSlots = state.Slots.Count(s => !s.IsBot);
        if (humanSlots >= state.MaxPlayers)
            throw new InvalidOperationException("Room is full — cannot promote spectator");

        if (state.Phase != RoomPhase.Waiting)
            throw new InvalidOperationException("Cannot join as player while game is in progress");

        // Chuyển spectator → player slot
        state.Spectators.Remove(spec);
        state.Slots.Add(new RoomSlot(userId, spec.DisplayName, spec.AvatarUrl));
        await _roomService.JoinAsync(roomId, userId, null);

        _log.LogInformation("Spectator promoted to player: {UserId} in room {RoomId}", userId, roomId);
    }

    public Task DemoteToSpectatorAsync(string roomId, string userId)
    {
        if (!_rooms.TryGetValue(roomId, out var state)) return Task.CompletedTask;
        var slot = state.GetSlot(userId);
        if (slot is null || slot.IsBot) return Task.CompletedTask;
        if (slot.IsHost) throw new InvalidOperationException("Host cannot demote themselves");
        if (state.Phase == RoomPhase.Playing)
            throw new InvalidOperationException("Cannot leave game as player (use disconnect handling)");

        state.Slots.Remove(slot);
        state.Spectators.Add(new SpectatorSlot(userId, slot.DisplayName, slot.AvatarUrl));

        _log.LogInformation("Player demoted to spectator: {UserId} in room {RoomId}", userId, roomId);
        return Task.CompletedTask;
    }

    // ════════════════════════════════════════════════════════════════════
    // DISCONNECT HANDLING
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gọi khi SignalR OnDisconnectedAsync fires.
    /// Phân biệt lobby disconnect (rời phòng hẳn) vs in-game (giữ slot 30s).
    /// </summary>
    public Task<DisconnectResult> HandleDisconnectAsync(string userId)
    {
        if (!_userRoom.TryGetValue(userId, out var roomId))
            return Task.FromResult(new DisconnectResult(DisconnectOutcome.AlreadyGone));

        if (!_rooms.TryGetValue(roomId, out var state))
            return Task.FromResult(new DisconnectResult(DisconnectOutcome.AlreadyGone));

        var slot = state.GetSlot(userId);
        if (slot is null)
        {
            // Spectator disconnect → remove immediately
            state.Spectators.RemoveAll(s => s.UserId == userId);
            _userRoom.TryRemove(userId, out _);
            return Task.FromResult(new DisconnectResult(DisconnectOutcome.AlreadyGone));
        }

        bool isInGame = state.Phase == RoomPhase.Playing;

        if (!isInGame)
        {
            // Lobby: rời phòng ngay
            _ = LeaveRoomAsync(roomId, userId);
            return Task.FromResult(new DisconnectResult(DisconnectOutcome.Noted));
        }

        // In-game: đánh dấu disconnected, chờ reconnect
        slot.Status      = SlotStatus.Disconnected;
        slot.LastSeenAt  = DateTime.UtcNow;

        var record = new DisconnectRecord(userId, roomId, wasInGame: true,
            reconnectWindowSeconds: _options.ReconnectWindowSeconds);

        _disconnects[userId]                = record;
        state.PendingDisconnects[userId]    = record;

        _log.LogInformation("Player disconnected (in-game): {UserId} from room {RoomId}, window={W}s",
            userId, roomId, _options.ReconnectWindowSeconds);

        return Task.FromResult(new DisconnectResult(DisconnectOutcome.Noted, record));
    }

    /// <summary>
    /// Gọi khi player kết nối lại thành công trong reconnect window.
    /// Nếu bot đã thay thế → đánh dấu để remove bot.
    /// </summary>
    public Task<ReconnectResult> HandleReconnectAsync(string userId, string roomId)
    {
        if (!_disconnects.TryGetValue(userId, out var record))
            return Task.FromResult(new ReconnectResult(ReconnectOutcome.NotDisconnected));

        if (record.IsExpired)
        {
            _disconnects.TryRemove(userId, out _);
            return Task.FromResult(new ReconnectResult(ReconnectOutcome.WindowExpired));
        }

        if (!_rooms.TryGetValue(roomId, out var state))
        {
            _disconnects.TryRemove(userId, out _);
            return Task.FromResult(new ReconnectResult(ReconnectOutcome.RoomGone));
        }

        // Restore slot
        var slot = state.GetSlot(userId);
        if (slot is not null)
        {
            slot.Status     = SlotStatus.Connected;
            slot.LastSeenAt = DateTime.UtcNow;
        }

        _disconnects.TryRemove(userId, out _);
        state.PendingDisconnects.Remove(userId);

        var botToRemove = record.BotReplacementId;

        _log.LogInformation("Player reconnected: {UserId} to room {RoomId} (bot to remove: {Bot})",
            userId, roomId, botToRemove ?? "none");

        return Task.FromResult(new ReconnectResult(ReconnectOutcome.Success, state, botToRemove));
    }

    // ════════════════════════════════════════════════════════════════════
    // MATCHMAKING
    // ════════════════════════════════════════════════════════════════════

    public async Task<MatchmakingTicket> EnqueueMatchmakingAsync(
        string userId, string displayName, string avatarUrl,
        int preferredPlayers = 4, bool allowBots = true)
    {
        // Rời phòng cũ nếu đang ở đâu đó
        if (_userRoom.TryGetValue(userId, out var currentRoom))
            await LeaveRoomAsync(currentRoom, userId);

        // Hủy ticket cũ nếu có
        _matchQueue.TryRemove(userId, out _);

        var ticket = new MatchmakingTicket(userId, displayName, avatarUrl,
            preferredPlayers, allowBots, _options.MatchmakingTimeoutSeconds);
        _matchQueue[userId] = ticket;

        _log.LogInformation("Matchmaking enqueued: {UserId} (pref={Pref}, bots={Bots})",
            userId, preferredPlayers, allowBots);

        return ticket;
    }

    public Task CancelMatchmakingAsync(string userId)
    {
        _matchQueue.TryRemove(userId, out _);
        _log.LogInformation("Matchmaking cancelled: {UserId}", userId);
        return Task.CompletedTask;
    }

    public Task<MatchmakingStatus> GetMatchmakingStatusAsync(string userId)
    {
        if (!_matchQueue.TryGetValue(userId, out var ticket))
            return Task.FromResult(new MatchmakingStatus(MatchmakingStatusEnum.NotQueued));

        if (ticket.IsExpired)
        {
            _matchQueue.TryRemove(userId, out _);
            return Task.FromResult(new MatchmakingStatus(MatchmakingStatusEnum.TimedOut, ticket));
        }

        int position = _matchQueue.Values
            .OrderBy(t => t.CreatedAt)
            .TakeWhile(t => t.UserId != userId)
            .Count() + 1;

        return Task.FromResult(new MatchmakingStatus(MatchmakingStatusEnum.Queued, ticket,
            QueuePosition: position));
    }

    // ════════════════════════════════════════════════════════════════════
    // QUERIES
    // ════════════════════════════════════════════════════════════════════

    public RoomRuntimeState? GetRoomState(string roomId) =>
        _rooms.GetValueOrDefault(roomId);

    public RoomRuntimeState? GetRoomByUser(string userId) =>
        _userRoom.TryGetValue(userId, out var roomId)
            ? _rooms.GetValueOrDefault(roomId)
            : null;

    public bool IsInRoom(string userId)        => _userRoom.ContainsKey(userId);
    public bool IsInMatchmaking(string userId) => _matchQueue.ContainsKey(userId);

    public IReadOnlyList<RoomRuntimeState> GetPublicWaitingRooms() =>
        _rooms.Values
            .Where(r => r.Phase == RoomPhase.Waiting)
            .OrderByDescending(r => r.Slots.Count)
            .ToList();

    // ════════════════════════════════════════════════════════════════════
    // BACKGROUND: DISCONNECT WATCHER
    // Kiểm tra reconnect window mỗi 3 giây
    // ════════════════════════════════════════════════════════════════════

    private async Task DisconnectWatcherLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(3_000, _cts.Token);
                await ProcessExpiredDisconnectsAsync();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "DisconnectWatcher error");
            }
        }
    }

    private async Task ProcessExpiredDisconnectsAsync()
    {
        var expired = _disconnects.Values
            .Where(r => r.IsExpired)
            .ToList();

        foreach (var record in expired)
        {
            _disconnects.TryRemove(record.UserId, out _);

            if (!_rooms.TryGetValue(record.RoomId, out var state)) continue;

            state.PendingDisconnects.Remove(record.UserId);
            var slot = state.GetSlot(record.UserId);
            if (slot is null) continue;

            if (state.Phase == RoomPhase.Playing)
            {
                // In-game: thay thế bằng bot tạm thời
                await ReplacewithBotAsync(state, slot, record);
            }
            else
            {
                // Lobby: xóa khỏi phòng
                state.Slots.Remove(slot);
                _userRoom.TryRemove(record.UserId, out _);
                await _roomService.LeaveAsync(record.RoomId, record.UserId);

                _log.LogInformation(
                    "Disconnect expired (lobby): {UserId} removed from room {RoomId}",
                    record.UserId, record.RoomId);
            }
        }
    }

    private async Task ReplacewithBotAsync(RoomRuntimeState state, RoomSlot slot, DisconnectRecord record)
    {
        var botId   = $"bot-dc-{record.UserId[..Math.Min(6, record.UserId.Length)]}";
        var botSlot = new RoomSlot(botId, $"{slot.DisplayName} (Bot)", isBot: true);

        state.Slots[state.Slots.IndexOf(slot)] = botSlot;
        record.BotReplacementId = botId;

        _log.LogInformation(
            "Disconnect expired (in-game): {UserId} → replaced by bot {BotId} in room {RoomId}",
            record.UserId, botId, state.RoomId);

        // Notify game service về sự thay đổi (optional — game sẽ treat botId như normal bot)
        await Task.CompletedTask;
    }

    // ════════════════════════════════════════════════════════════════════
    // BACKGROUND: MATCHMAKING WORKER
    // Chạy mỗi 5 giây, ghép queue thành phòng
    // ════════════════════════════════════════════════════════════════════

    private async Task MatchmakingWorkerLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5_000, _cts.Token);
                await RunMatchmakingCycleAsync();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "MatchmakingWorker error");
            }
        }
    }

    private async Task RunMatchmakingCycleAsync()
    {
        // Xóa tickets hết hạn
        var expired = _matchQueue.Values.Where(t => t.IsExpired).ToList();
        foreach (var t in expired)
            _matchQueue.TryRemove(t.UserId, out _);

        var queue = _matchQueue.Values
            .OrderBy(t => t.CreatedAt)
            .ToList();

        if (queue.Count < 2) return;

        // Nhóm theo preferredPlayers (4 → 3 → 2 fallback)
        foreach (int targetSize in new[] { 4, 3, 2 })
        {
            var group = queue
                .Where(t => t.PreferredPlayers >= targetSize || t.WaitTime.TotalSeconds >= 20)
                .Take(targetSize)
                .ToList();

            if (group.Count < 2) continue;

            bool needBots = group.Count < targetSize && group.Any(t => t.AllowBots);

            // Tạo phòng và ghép người vào
            var hostTicket = group[0];
            var host       = await _userService.GetByIdAsync(hostTicket.UserId);
            if (host is null) continue;

            var createReq = new CreateRoomRequest
            {
                RoomName      = "Matched Room",
                MaxPlayers    = targetSize,
                BotCount      = needBots ? targetSize - group.Count : 0,
                BotDifficulty = "hard",
                IsPrivate     = false
            };

            var roomState = await CreateRoomAsync(
                hostTicket.UserId, hostTicket.DisplayName, hostTicket.AvatarUrl, createReq);

            // Xóa host khỏi queue (đã xử lý trong CreateRoomAsync)
            _matchQueue.TryRemove(hostTicket.UserId, out _);

            // Thêm các player còn lại
            foreach (var ticket in group.Skip(1))
            {
                _matchQueue.TryRemove(ticket.UserId, out _);
                await JoinRoomAsync(roomState.RoomId, ticket.UserId,
                    ticket.DisplayName, ticket.AvatarUrl, password: null);
            }

            _log.LogInformation(
                "Matchmaking: room {RoomId} created for {Count} players (bots={Bots})",
                roomState.RoomId, group.Count, createReq.BotCount);

            break; // Xử lý từng nhóm một per cycle
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════════════════════════════

    private async Task<RoomRuntimeState> SyncRoomFromDbAsync(RoomDto dto)
    {
        var state = new RoomRuntimeState(dto.Id, dto.HostId) { MaxPlayers = dto.MaxPlayers };
        foreach (var p in dto.Players)
        {
            state.Slots.Add(new RoomSlot(p.UserId, p.DisplayName, p.AvatarUrl,
                p.IsBot, p.IsHost) { IsReady = p.IsReady });
        }
        state.Phase = dto.Status switch
        {
            RoomStatus.Playing => RoomPhase.Playing,
            RoomStatus.Closed  => RoomPhase.Closed,
            _                  => RoomPhase.Waiting
        };
        _rooms[dto.Id] = state;

        foreach (var p in dto.Players.Where(p => !p.IsBot))
            _userRoom.TryAdd(p.UserId, dto.Id);

        await Task.CompletedTask;
        return state;
    }

    private static bool ValidatePassword(RoomDto room, string? supplied)
    {
        // Nếu phòng không có password → luôn pass
        return true; // TODO: implement BCrypt.Verify(supplied, room.PasswordHash)
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        await Task.WhenAll(_disconnectWatcher, _matchmakingWorker);
        _cts.Dispose();
    }
}

// ─── Options ──────────────────────────────────────────────────────────────────

public sealed class RoomManagerOptions
{
    /// <summary>Giây chờ reconnect trước khi bị thay bởi bot.</summary>
    public int ReconnectWindowSeconds    { get; set; } = 30;

    /// <summary>Giây timeout matchmaking trước khi huỷ.</summary>
    public int MatchmakingTimeoutSeconds { get; set; } = 60;

    /// <summary>Số spectator tối đa mỗi phòng.</summary>
    public int MaxSpectatorsPerRoom      { get; set; } = 10;
}
