namespace UnoGame.Core.Room;

// ════════════════════════════════════════════════════════════════════════════
// ROOM MANAGER — Domain Models
// Các type này sống trong memory (RoomManager singleton).
// Dữ liệu persistent vẫn dùng RoomDocument trong MongoDB.
// ════════════════════════════════════════════════════════════════════════════

// ─── Room Slot ────────────────────────────────────────────────────────────────

public sealed class RoomSlot
{
    public string  SlotId      { get; }    
    public string  DisplayName { get; set; }
    public string  AvatarUrl   { get; set; }
    public bool    IsBot       { get; }
    public bool    IsHost      { get; set; }
    public bool    IsReady     { get; set; }
    public SlotStatus Status   { get; set; } = SlotStatus.Connected;
    public DateTime JoinedAt   { get; }
    public DateTime? LastSeenAt{ get; set; }

    public RoomSlot(string slotId, string displayName, string avatarUrl = "",
        bool isBot = false, bool isHost = false)
    {
        SlotId      = slotId;
        DisplayName = displayName;
        AvatarUrl   = avatarUrl;
        IsBot       = isBot;
        IsHost      = isHost;
        IsReady     = isBot;     // bot luôn ready
        JoinedAt    = DateTime.UtcNow;
        LastSeenAt  = DateTime.UtcNow;
    }
}

public enum SlotStatus
{
    Connected    = 0,
    Disconnected = 1,   // tạm thời mất kết nối, đang trong reconnect window
    Replaced     = 2,   // bị bot thay thế sau khi hết reconnect window
    Left         = 3    // rời phòng tự nguyện
}

// ─── Spectator Slot ───────────────────────────────────────────────────────────

/// <summary>Người xem — không tham gia game, chỉ quan sát.</summary>
public sealed class SpectatorSlot
{
    public string  UserId      { get; }
    public string  DisplayName { get; }
    public string  AvatarUrl   { get; }
    public DateTime JoinedAt   { get; }
    public string? ConnectionId{ get; set; }   // SignalR connection ID

    public SpectatorSlot(string userId, string displayName, string avatarUrl = "")
    {
        UserId      = userId;
        DisplayName = displayName;
        AvatarUrl   = avatarUrl;
        JoinedAt    = DateTime.UtcNow;
    }
}

// ─── Room In-Memory State ─────────────────────────────────────────────────────

/// <summary>
/// Trạng thái phòng đang sống trong memory của RoomManager.
/// Bổ sung cho RoomDocument (persistent) — chứa state thay đổi nhanh.
/// </summary>
public sealed class RoomRuntimeState
{
    public string  RoomId         { get; }
    public string  HostId         { get; set; }
    public int     MaxPlayers     { get; set; } = 4;
    public List<RoomSlot>    Slots       { get; } = new();
    public List<SpectatorSlot> Spectators { get; } = new();
    public RoomPhase Phase        { get; set; } = RoomPhase.Waiting;

    // Matchmaking
    public bool IsMatchmaking     { get; set; }
    public DateTime? MatchmakingStartedAt { get; set; }

    // Disconnect tracking
    public Dictionary<string, DisconnectRecord> PendingDisconnects { get; } = new();

    public RoomRuntimeState(string roomId, string hostId)
    {
        RoomId = roomId;
        HostId = hostId;
    }

    public RoomSlot?      GetSlot(string userId)      => Slots.FirstOrDefault(s => s.SlotId == userId);
    public SpectatorSlot? GetSpectator(string userId) => Spectators.FirstOrDefault(s => s.UserId == userId);
    public bool IsPlayer(string userId) => Slots.Any(s => s.SlotId == userId);
    public bool IsSpectator(string userId) => Spectators.Any(s => s.UserId == userId);
    public int  HumanPlayerCount => Slots.Count(s => !s.IsBot);
    public int  ActivePlayerCount => Slots.Count(s => s.Status is SlotStatus.Connected or SlotStatus.Disconnected);
}

public enum RoomPhase
{
    Waiting      = 0,   // lobby, chờ người vào
    Matchmaking  = 1,   // đang tìm người ghép
    CountingDown = 2,   // đếm ngược trước khi bắt đầu
    Playing      = 3,   // game đang diễn ra
    PostGame     = 4,   // kết quả, chờ replay/leave
    Closed       = 5
}

// ─── Disconnect Record ────────────────────────────────────────────────────────

/// <summary>
/// Ghi nhận khi player disconnect — theo dõi reconnect window.
/// Sau khi hết ReconnectDeadline, bot sẽ thay thế slot.
/// </summary>
public sealed class DisconnectRecord
{
    public string    UserId            { get; }
    public string    RoomId            { get; }
    public DateTime  DisconnectedAt    { get; }
    public DateTime  ReconnectDeadline { get; }   // = DisconnectedAt + 30s
    public bool      WasInGame         { get; }   // disconnect khi đang trong ván
    public string?   BotReplacementId  { get; set; }  // bot tạm thay thế

    public bool IsExpired => DateTime.UtcNow > ReconnectDeadline;

    public DisconnectRecord(string userId, string roomId, bool wasInGame,
        int reconnectWindowSeconds = 30)
    {
        UserId            = userId;
        RoomId            = roomId;
        WasInGame         = wasInGame;
        DisconnectedAt    = DateTime.UtcNow;
        ReconnectDeadline = DateTime.UtcNow.AddSeconds(reconnectWindowSeconds);
    }
}

// ─── Matchmaking Ticket ────────────────────────────────────────────────────────

/// <summary>
/// Ticket của player đang tìm kiếm phòng tự động (matchmaking).
/// RoomManager dùng để ghép các player cùng range và preferences.
/// </summary>
public sealed class MatchmakingTicket
{
    public string   TicketId     { get; } = Guid.NewGuid().ToString("N")[..8];
    public string   UserId       { get; }
    public string   DisplayName  { get; }
    public string   AvatarUrl    { get; }
    public int      PreferredPlayers { get; }   // 2, 3 hoặc 4
    public bool     AllowBots    { get; }
    public DateTime CreatedAt    { get; } = DateTime.UtcNow;
    public DateTime ExpiresAt    { get; }       // = CreatedAt + 60s

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public TimeSpan WaitTime => DateTime.UtcNow - CreatedAt;

    public MatchmakingTicket(string userId, string displayName, string avatarUrl = "",
        int preferredPlayers = 4, bool allowBots = true, int timeoutSeconds = 60)
    {
        UserId           = userId;
        DisplayName      = displayName;
        AvatarUrl        = avatarUrl;
        PreferredPlayers = preferredPlayers;
        AllowBots        = allowBots;
        ExpiresAt        = DateTime.UtcNow.AddSeconds(timeoutSeconds);
    }
}

// ─── Room Manager Events ──────────────────────────────────────────────────────

/// <summary>Events mà RoomManager phát ra để các handler (Hub, etc.) xử lý.</summary>
public record RoomEvent(string RoomId)
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public record PlayerJoinedRoomEvent      (string RoomId, string UserId, bool AsSpectator)  : RoomEvent(RoomId);
public record PlayerLeftRoomEvent        (string RoomId, string UserId, string? NewHostId) : RoomEvent(RoomId);
public record PlayerDisconnectedRoomEvent(string RoomId, string UserId, int WindowSeconds) : RoomEvent(RoomId);
public record PlayerReconnectedRoomEvent (string RoomId, string UserId)                    : RoomEvent(RoomId);
public record BotReplacedPlayerEvent     (string RoomId, string UserId, string BotId)      : RoomEvent(RoomId);
public record RoomClosedEvent            (string RoomId, string Reason)                    : RoomEvent(RoomId);
public record MatchFoundEvent            (string RoomId, List<string> MatchedUserIds)      : RoomEvent(RoomId);
public record MatchmakingTimeoutEvent    (string UserId)                                   : RoomEvent(string.Empty);
