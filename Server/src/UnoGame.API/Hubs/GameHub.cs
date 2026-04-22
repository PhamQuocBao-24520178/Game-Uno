using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UnoGame.API.Services;
using UnoGame.Core.Models;

namespace UnoGame.API.Hubs;

[Authorize]
public sealed class GameHub : Hub
{
    private readonly IGameService        _games;
    private readonly IRoomService        _rooms;
    private readonly IUserService        _users;
    private readonly IConnectionManager  _connections;
    private readonly IBotOrchestrator    _bots;
    private readonly ILogger<GameHub>    _log;

    public GameHub(
        IGameService       games,
        IRoomService       rooms,
        IUserService       users,
        IConnectionManager connections,
        IBotOrchestrator   bots,
        ILogger<GameHub>   log)
    {
        _games       = games;
        _rooms       = rooms;
        _users       = users;
        _connections = connections;
        _bots        = bots;
        _log         = log;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private string UserId =>
        Context.UserIdentifier
        ?? throw new HubException("Unauthenticated");

    private Task SendError(string code, string message, string? detail = null) =>
        Clients.Caller.SendAsync(HubEvents.ActionError,
            new ActionErrorPayload(code, message, detail));

    // ════════════════════════════════════════════════════════════════════
    // CONNECTION LIFECYCLE
    // ════════════════════════════════════════════════════════════════════

    public override async Task OnConnectedAsync()
    {
        var userId = UserId;
        _connections.Register(Context.ConnectionId, userId);
        _log.LogDebug("Connected: {UserId} / {ConnId}", userId, Context.ConnectionId);

        // Nếu user đang ở phòng nào → tự động re-join SignalR group
        var existingRoomId = _connections.GetRoomIdByUser(userId);
        if (existingRoomId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, existingRoomId);
            _connections.JoinRoom(Context.ConnectionId, existingRoomId);

            // Notify room: player quay lại
            await Clients.OthersInGroup(existingRoomId).SendAsync(
                HubEvents.PlayerReconnected,
                new PlayerReconnectedPayload(userId, await GetDisplayNameAsync(userId),
                    DateTime.UtcNow));

            // Gửi sync state ngay khi reconnect
            await SyncStateToCallerAsync(existingRoomId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = UserId;
        var roomId = _connections.GetRoomId(Context.ConnectionId);

        _connections.Unregister(Context.ConnectionId);
        _log.LogDebug("Disconnected: {UserId} / {ConnId} | room={RoomId}",
            userId, Context.ConnectionId, roomId);

        if (roomId is not null)
        {
            var room = await _rooms.GetByIdAsync(roomId);
            if (room?.Status == RoomStatus.Playing)
            {
                // Trong game: thông báo disconnect, cho 30s reconnect window
                await Clients.OthersInGroup(roomId).SendAsync(
                    HubEvents.PlayerDisconnected,
                    new PlayerDisconnectedPayload(
                        userId,
                        await GetDisplayNameAsync(userId),
                        ReconnectWindowSeconds: 30,
                        DateTime.UtcNow));
            }
            else if (room?.Status == RoomStatus.Waiting)
            {
                // Trong lobby: rời phòng hẳn
                await _rooms.LeaveAsync(roomId, userId);
                var updatedRoom = await _rooms.GetByIdAsync(roomId);

                await Clients.OthersInGroup(roomId).SendAsync(
                    HubEvents.PlayerLeft,
                    new PlayerLeftPayload(
                        userId,
                        await GetDisplayNameAsync(userId),
                        NewHostId    : updatedRoom?.HostId,
                        PlayerCount  : updatedRoom?.Players.Count ?? 0,
                        Timestamp    : DateTime.UtcNow));
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ════════════════════════════════════════════════════════════════════
    // LOBBY METHODS
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Vào SignalR group của phòng.
    /// Phải gọi sau khi REST POST /rooms/{id}/join thành công.
    /// </summary>
    public async Task JoinRoom(string roomId, string? password = null)
    {
        var userId = UserId;

        var room = await _rooms.GetByIdAsync(roomId);
        if (room is null)
        {
            await SendError(ErrorCodes.NotInRoom, "Room not found");
            return;
        }

        // Nếu chưa join room qua REST → join luôn ở đây (tiện cho testing)
        if (!await _rooms.IsPlayerInRoomAsync(roomId, userId))
        {
            if (room.Players.Count >= room.MaxPlayers)
            {
                await SendError(ErrorCodes.RoomFull, "Room is full");
                return;
            }
            await _rooms.JoinAsync(roomId, userId, password);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        _connections.JoinRoom(Context.ConnectionId, roomId);

        var user = await _users.GetByIdAsync(userId);

        // Notify others
        await Clients.OthersInGroup(roomId).SendAsync(
            HubEvents.PlayerJoined,
            new PlayerJoinedPayload(
                userId,
                user?.DisplayName ?? "Unknown",
                user?.AvatarUrl   ?? "",
                PlayerCount : room.Players.Count + 1,
                MaxPlayers  : room.MaxPlayers,
                Timestamp   : DateTime.UtcNow));

        // Sync full room state to joiner
        var updatedRoom = await _rooms.GetByIdAsync(roomId);
        await Clients.Caller.SendAsync("RoomJoined", updatedRoom);

        _log.LogInformation("Hub JoinRoom: {UserId} → room {RoomId}", userId, roomId);
    }

    /// <summary>
    /// Rời SignalR group. Gọi trước khi đóng kết nối hoặc về lobby.
    /// </summary>
    public async Task LeaveRoom(string roomId)
    {
        var userId = UserId;

        if (!await _rooms.IsPlayerInRoomAsync(roomId, userId))
        {
            await SendError(ErrorCodes.NotInRoom, "You are not in this room");
            return;
        }

        await _rooms.LeaveAsync(roomId, userId);
        _connections.LeaveRoom(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

        var updatedRoom = await _rooms.GetByIdAsync(roomId);
        var user        = await _users.GetByIdAsync(userId);

        await Clients.OthersInGroup(roomId).SendAsync(
            HubEvents.PlayerLeft,
            new PlayerLeftPayload(
                userId,
                user?.DisplayName ?? "Unknown",
                NewHostId   : updatedRoom?.HostId,
                PlayerCount : updatedRoom?.Players.Count ?? 0,
                Timestamp   : DateTime.UtcNow));
    }

    /// <summary>
    /// Toggle trạng thái Ready của player trong lobby.
    /// </summary>
    public async Task ToggleReady(string roomId)
    {
        var userId = UserId;

        var room = await _rooms.GetByIdAsync(roomId);
        if (room is null) { await SendError(ErrorCodes.NotInRoom, "Room not found"); return; }
        if (room.Status != RoomStatus.Waiting)
        {
            await SendError(ErrorCodes.GameNotActive, "Game already started");
            return;
        }

        await _rooms.MarkReadyAsync(roomId, userId);
        var updated = await _rooms.GetByIdAsync(roomId);

        bool allReady = updated?.Players
            .Where(p => !p.IsHost && !p.IsBot)
            .All(p => p.IsReady) ?? false;

        var me = updated?.Players.FirstOrDefault(p => p.UserId == userId);

        await Clients.Group(roomId).SendAsync(
            HubEvents.PlayerReadyChanged,
            new PlayerReadyChangedPayload(
                userId,
                IsReady  : me?.IsReady ?? false,
                AllReady : allReady,
                Timestamp: DateTime.UtcNow));
    }

    // ════════════════════════════════════════════════════════════════════
    // GAME ACTIONS
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Đánh bài. Bắt buộc có chosenColor nếu đánh Wild hoặc WildDrawFour.
    ///
    /// Broadcast sau khi đánh thành công:
    ///   → Group: CardPlayed (public)
    ///   → Group: TurnChanged
    ///   → Group: GameOver (nếu thắng)
    /// </summary>
    public async Task PlayCard(string roomId, CardDto card, string? chosenColor = null)
    {
        var userId = UserId;

        // ── Validation ────────────────────────────────────────────────
        if (!await _rooms.IsPlayerInRoomAsync(roomId, userId))
        {
            await SendError(ErrorCodes.NotInRoom, "You are not in this room");
            return;
        }

        bool isWild = card.Type is "Wild" or "WildDrawFour";
        if (isWild && string.IsNullOrEmpty(chosenColor))
        {
            await SendError(ErrorCodes.MissingColor,
                "Must specify chosenColor when playing Wild or WildDrawFour");
            return;
        }

        if (!string.IsNullOrEmpty(chosenColor) &&
            chosenColor is not ("Red" or "Green" or "Blue" or "Yellow"))
        {
            await SendError(ErrorCodes.InvalidColor,
                "chosenColor must be: Red | Green | Blue | Yellow");
            return;
        }

        // ── Execute ────────────────────────────────────────────────────
        var req    = new PlayCardRequest { Card = card, ChosenColor = chosenColor };
        var result = await _games.PlayCardAsync(roomId, userId, req);

        if (!result.Success)
        {
            await SendError(MapGameError(result.Error), result.Error ?? "Action failed");
            return;
        }

        var state      = result.State!;
        var user       = await _users.GetByIdAsync(userId);
        var myStats    = state.Players.FirstOrDefault(p => p.PlayerId == userId);
        var nextPlayer = state.Players.FirstOrDefault(p => p.PlayerId == state.CurrentPlayerId);

        // ── Broadcast: CardPlayed ──────────────────────────────────────
        await Clients.Group(roomId).SendAsync(
            HubEvents.CardPlayed,
            new CardPlayedPayload(
                PlayerId        : userId,
                PlayerName      : user?.DisplayName ?? "Unknown",
                Card            : state.TopCard,
                CurrentColor    : state.CurrentColor,
                RemainingCards  : myStats?.CardCount ?? 0,
                HasCalledUno    : myStats?.HasCalledUno ?? false,
                NextPlayerId    : state.CurrentPlayerId,
                NextPlayerName  : nextPlayer?.DisplayName ?? "",
                PendingDrawCount: state.PendingDrawCount,
                IsGameOver      : result.IsGameOver,
                Timestamp       : DateTime.UtcNow));

        // ── Broadcast: TurnChanged (nếu game chưa kết thúc) ───────────
        if (!result.IsGameOver)
        {
            await Clients.Group(roomId).SendAsync(
                HubEvents.TurnChanged,
                new TurnChangedPayload(
                    CurrentPlayerId   : state.CurrentPlayerId,
                    CurrentPlayerName : nextPlayer?.DisplayName ?? "",
                    IsBot             : nextPlayer?.IsBot ?? false,
                    TurnNumber        : state.TurnNumber,
                    PendingDrawCount  : state.PendingDrawCount,
                    TimeoutSeconds    : 30,
                    TurnStartedAt     : DateTime.UtcNow));

            // Trigger bot nếu đến lượt bot
            _bots.TriggerIfBotTurn(roomId);
        }
        else
        {
            await BroadcastGameOverAsync(roomId);
        }
    }

    /// <summary>
    /// Rút bài (hoặc nhận bài phạt do +2/+4 stack).
    ///
    /// Broadcast sau khi rút:
    ///   → Caller only : CardsDrawn (nội dung bài — private)
    ///   → Others      : PlayerDrewCards (chỉ số lượng)
    ///   → Group       : TurnChanged
    /// </summary>
    public async Task DrawCard(string roomId)
    {
        var userId = UserId;

        if (!await _rooms.IsPlayerInRoomAsync(roomId, userId))
        {
            await SendError(ErrorCodes.NotInRoom, "You are not in this room");
            return;
        }

        var result = await _games.DrawCardAsync(roomId, userId);

        if (!result.Success)
        {
            await SendError(MapGameError(result.Error), result.Error ?? "Cannot draw card");
            return;
        }

        var state      = result.State!;
        var user       = await _users.GetByIdAsync(userId);
        var nextPlayer = state.Players.FirstOrDefault(p => p.PlayerId == state.CurrentPlayerId);

        // ── Private: lá bài thực tế chỉ gửi cho người rút ────────────
        await Clients.Caller.SendAsync(
            HubEvents.CardsDrawn,
            new CardsDrawnPayload(
                Cards          : result.DrawnCards ?? new(),
                CanPlayDrawn   : result.DrawnCards?.Count == 1 &&
                                  (result.State?.Players
                                      .FirstOrDefault(p => p.PlayerId == userId)
                                      ?.CardCount > 0),
                PendingCleared : result.DrawnCards?.Count > 1 ? result.DrawnCards.Count : 0,
                NextPlayerId   : state.CurrentPlayerId,
                Timestamp      : DateTime.UtcNow));

        // ── Public: chỉ báo số lá rút ─────────────────────────────────
        await Clients.OthersInGroup(roomId).SendAsync(
            HubEvents.PlayerDrewCards,
            new PlayerDrewCardsPayload(
                PlayerId    : userId,
                PlayerName  : user?.DisplayName ?? "Unknown",
                CardCount   : result.DrawnCards?.Count ?? 1,
                WasPenalty  : (result.DrawnCards?.Count ?? 0) > 1,
                NextPlayerId: state.CurrentPlayerId,
                Timestamp   : DateTime.UtcNow));

        // ── TurnChanged ───────────────────────────────────────────────
        await Clients.Group(roomId).SendAsync(
            HubEvents.TurnChanged,
            new TurnChangedPayload(
                CurrentPlayerId   : state.CurrentPlayerId,
                CurrentPlayerName : nextPlayer?.DisplayName ?? "",
                IsBot             : nextPlayer?.IsBot ?? false,
                TurnNumber        : state.TurnNumber,
                PendingDrawCount  : state.PendingDrawCount,
                TimeoutSeconds    : 30,
                TurnStartedAt     : DateTime.UtcNow));

        _bots.TriggerIfBotTurn(roomId);
    }

    /// <summary>
    /// Gọi UNO (tự gọi khi còn 1 lá) hoặc bắt UNO (targetId ≠ mình).
    ///
    /// Tự gọi → UnoCalled broadcast (thông báo player đã gọi UNO).
    /// Bắt UNO thành công → UnoCaught (target bị phạt 2 lá).
    /// Bắt UNO thất bại → ActionError (target đã gọi hoặc > 1 lá).
    /// </summary>
    public async Task CallUno(string roomId, string targetId)
    {
        var callerId = UserId;

        if (!await _rooms.IsPlayerInRoomAsync(roomId, callerId))
        {
            await SendError(ErrorCodes.NotInRoom, "You are not in this room");
            return;
        }

        var result = await _games.CallUnoAsync(roomId, callerId, targetId);

        if (!result.Success)
        {
            await SendError("INVALID_UNO_CALL", result.Error ?? "Invalid UNO call");
            return;
        }

        var caller     = await _users.GetByIdAsync(callerId);
        var target     = await _users.GetByIdAsync(targetId);
        bool isSelf    = callerId == targetId;
        bool caughtUno = !isSelf && (result.DrawnCards?.Count ?? 0) > 0;

        if (isSelf)
        {
            // Tự gọi UNO
            await Clients.Group(roomId).SendAsync(
                HubEvents.UnoCalled,
                new UnoCalledPayload(
                    callerId,
                    caller?.DisplayName ?? "Unknown",
                    targetId,
                    target?.DisplayName ?? "Unknown",
                    IsSelfCall: true,
                    Timestamp : DateTime.UtcNow));
        }
        else if (caughtUno)
        {
            // Bắt được người quên gọi UNO
            await Clients.Group(roomId).SendAsync(
                HubEvents.UnoCaught,
                new UnoCaughtPayload(
                    VictimId    : targetId,
                    VictimName  : target?.DisplayName ?? "Unknown",
                    PenaltyCards: result.DrawnCards!.Count,
                    CaughtById  : callerId,
                    Timestamp   : DateTime.UtcNow));

            // Notify target: nhận bài phạt (private)
            var targetConnId = _connections.GetConnectionId(targetId);
            if (targetConnId is not null)
            {
                await Clients.Client(targetConnId).SendAsync(
                    HubEvents.CardsDrawn,
                    new CardsDrawnPayload(
                        Cards          : result.DrawnCards,
                        CanPlayDrawn   : false,
                        PendingCleared : 0,
                        NextPlayerId   : string.Empty,
                        Timestamp      : DateTime.UtcNow));
            }
        }
    }

    /// <summary>
    /// Gửi chat message trong phòng.
    /// Message bị trim và giới hạn 200 ký tự.
    /// </summary>
    public async Task SendChatMessage(string roomId, string message)
    {
        var userId = UserId;

        if (!await _rooms.IsPlayerInRoomAsync(roomId, userId))
        {
            await SendError(ErrorCodes.NotInRoom, "You are not in this room");
            return;
        }

        var cleaned = (message ?? "").Trim();
        if (cleaned.Length == 0) return;
        if (cleaned.Length > 200) cleaned = cleaned[..200];

        var user = await _users.GetByIdAsync(userId);

        await Clients.Group(roomId).SendAsync(
            HubEvents.ChatMessage,
            new ChatMessagePayload(
                SenderId  : userId,
                SenderName: user?.DisplayName ?? "Unknown",
                Message   : cleaned,
                IsSystem  : false,
                Timestamp : DateTime.UtcNow));
    }

    // ════════════════════════════════════════════════════════════════════
    // RECONNECT SYNC
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Client gọi sau khi reconnect để lấy lại trạng thái hiện tại.
    /// Gửi GameStateSynced + MyHandSynced chỉ cho caller.
    /// </summary>
    public async Task RequestSync(string roomId)
    {
        var userId = UserId;

        if (!await _rooms.IsPlayerInRoomAsync(roomId, userId))
        {
            await SendError(ErrorCodes.NotInRoom, "You are not in this room");
            return;
        }

        await SyncStateToCallerAsync(roomId);
    }

    // ════════════════════════════════════════════════════════════════════
    // INTERNAL HELPERS
    // ════════════════════════════════════════════════════════════════════

    private async Task SyncStateToCallerAsync(string roomId)
    {
        var userId = UserId;
        var room   = await _rooms.GetByIdAsync(roomId);
        if (room is null) return;

        if (room.Status == RoomStatus.Playing)
        {
            var state = await _games.GetPublicStateAsync(roomId, userId);
            if (state is not null)
                await Clients.Caller.SendAsync(HubEvents.GameStateSynced, state);

            var hand = await _games.GetMyHandAsync(roomId, userId);
            if (hand is not null)
                await Clients.Caller.SendAsync(HubEvents.MyHandSynced, hand);
        }
        else
        {
            // Lobby state sync
            await Clients.Caller.SendAsync("RoomJoined", room);
        }
    }

    internal async Task BroadcastGameStartedAsync(string roomId)
    {
        var state = await _games.GetPublicStateAsync(roomId, "__system__");
        if (state is null) return;

        // Countdown
        await Clients.Group(roomId).SendAsync(
            HubEvents.GameStarting,
            new GameStartingPayload(roomId, CountdownSeconds: 3,
                StartsAt: DateTime.UtcNow.AddSeconds(3)));

        await Task.Delay(3000);

        // Game started — public state
        await Clients.Group(roomId).SendAsync(
            HubEvents.GameStarted,
            new GameStartedPayload(roomId, state, DateTime.UtcNow));

        // Private hand cho từng player
        var connections = _connections.GetConnectionsInRoom(roomId);
        foreach (var connId in connections)
        {
            var uid = _connections.GetUserId(connId);
            if (uid is null) continue;

            var hand = await _games.GetMyHandAsync(roomId, uid);
            if (hand is null) continue;

            var myState = state.Players.FirstOrDefault(p => p.PlayerId == uid);

            await Clients.Client(connId).SendAsync(
                HubEvents.HandDealt,
                new HandDealtPayload(
                    Cards    : hand.Cards,
                    Playable : hand.Playable,
                    IsMyTurn : state.CurrentPlayerId == uid,
                    MustDraw : hand.MustDraw));
        }

        // Initial TurnChanged
        var firstPlayer = state.Players.FirstOrDefault(p => p.PlayerId == state.CurrentPlayerId);
        await Clients.Group(roomId).SendAsync(
            HubEvents.TurnChanged,
            new TurnChangedPayload(
                CurrentPlayerId   : state.CurrentPlayerId,
                CurrentPlayerName : firstPlayer?.DisplayName ?? "",
                IsBot             : firstPlayer?.IsBot ?? false,
                TurnNumber        : 1,
                PendingDrawCount  : 0,
                TimeoutSeconds    : 30,
                TurnStartedAt     : DateTime.UtcNow));

        // Nếu lượt đầu là bot → trigger ngay
        if (firstPlayer?.IsBot == true)
            _bots.TriggerIfBotTurn(roomId);
    }

    private async Task BroadcastGameOverAsync(string roomId)
    {
        var history = await _games.GetRoomHistoryAsync(roomId, 1);
        var latest  = history.FirstOrDefault();
        if (latest is null) return;

        await Clients.Group(roomId).SendAsync(
            HubEvents.GameOver,
            new GameOverPayload(
                WinnerId  : latest.WinnerId,
                WinnerName: latest.WinnerName,
                Results   : latest.Results,
                TotalTurns: latest.TotalTurns,
                Duration  : latest.Duration,
                EndedAt   : DateTime.UtcNow));

        // System chat message
        await Clients.Group(roomId).SendAsync(
            HubEvents.ChatMessage,
            new ChatMessagePayload(
                SenderId  : "__system__",
                SenderName: "UNO",
                Message   : $"🏆 {latest.WinnerName} won the game!",
                IsSystem  : true,
                Timestamp : DateTime.UtcNow));
    }

    private async Task<string> GetDisplayNameAsync(string userId)
    {
        var user = await _users.GetByIdAsync(userId);
        return user?.DisplayName ?? "Unknown";
    }

    private static string MapGameError(string? error) => error switch
    {
        "Not your turn"   => ErrorCodes.NotYourTurn,
        "Card not in hand"=> ErrorCodes.CardNotInHand,
        "Invalid card"    => ErrorCodes.InvalidCard,
        "Must draw or stack" => ErrorCodes.MustStack,
        "Game not active" => ErrorCodes.GameNotActive,
        _                 => "GAME_ERROR"
    };
}
