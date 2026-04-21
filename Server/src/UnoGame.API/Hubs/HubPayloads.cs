
namespace UnoGame.API.Hubs;

// ════════════════════════════════════════════════════════════════════════════
// Hub Payloads — strongly-typed objects gửi từ server → client qua SignalR.
// Mọi Clients.Group(...).SendAsync(event, payload) đều dùng record này.
// Unity client deserialize thành C# class tương ứng (cùng tên field).
// ════════════════════════════════════════════════════════════════════════════

// ─── Lobby ──────────────────────────────────────────────────────────────────

public record PlayerJoinedPayload(
    string   UserId,
    string   DisplayName,
    string   AvatarUrl,
    int      PlayerCount,
    int      MaxPlayers,
    DateTime Timestamp);

public record PlayerLeftPayload(
    string   UserId,
    string   DisplayName,
    string?  NewHostId,      // null nếu phòng không cần chuyển host
    int      PlayerCount,
    DateTime Timestamp);

public record PlayerKickedPayload(
    string   KickedUserId,
    string   Reason,
    DateTime Timestamp);

public record PlayerReadyChangedPayload(
    string   UserId,
    bool     IsReady,
    bool     AllReady,       // true khi mọi non-host đều ready
    DateTime Timestamp);

public record RoomSettingsUpdatedPayload(
    int     MaxPlayers,
    int     BotCount,
    string  BotDifficulty,
    bool    IsPrivate);

public record RoomClosedPayload(
    string  Reason,
    DateTime Timestamp);

// ─── Game Lifecycle ─────────────────────────────────────────────────────────

public record GameStartingPayload(
    string   RoomId,
    int      CountdownSeconds, // client hiển thị đếm ngược
    DateTime StartsAt);

public record GameStartedPayload(
    string        RoomId,
    GameStateDto  State,
    DateTime      StartedAt);

/// <summary>Private: gửi riêng cho từng player ngay sau GameStarted.</summary>
public record HandDealtPayload(
    List<CardDto> Cards,
    List<CardDto> Playable,    // bài có thể đánh ngay ở turn đầu
    bool          IsMyTurn,
    bool          MustDraw);

public record TurnChangedPayload(
    string   CurrentPlayerId,
    string   CurrentPlayerName,
    bool     IsBot,
    int      TurnNumber,
    int      PendingDrawCount,  // > 0 nếu đang bị stack +2/+4
    int      TimeoutSeconds,    // giây trước khi auto-draw (nếu AFK)
    DateTime TurnStartedAt);

public record GameOverPayload(
    string              WinnerId,
    string              WinnerName,
    List<PlayerResultDto> Results,
    int                 TotalTurns,
    string              Duration,   // "mm:ss"
    DateTime            EndedAt);

// ─── In-Game Actions ────────────────────────────────────────────────────────

public record CardPlayedPayload(
    string   PlayerId,
    string   PlayerName,
    CardDto  Card,
    string   CurrentColor,   // màu sau khi đánh (quan trọng với Wild)
    int      RemainingCards, // số bài còn lại của player đó
    bool     HasCalledUno,
    string   NextPlayerId,
    string   NextPlayerName,
    int      PendingDrawCount,
    bool     IsGameOver,
    DateTime Timestamp);

/// <summary>Private: chỉ người rút nhận được nội dung lá bài.</summary>
public record CardsDrawnPayload(
    List<CardDto> Cards,
    bool          CanPlayDrawn,  // lá rút 1 có thể đánh ngay không?
    int           PendingCleared, // draw penalty đã được xử lý
    string        NextPlayerId,  // null nếu vẫn là turn của người này
    DateTime      Timestamp);

/// <summary>Public: các player khác thấy ai rút bao nhiêu lá (không thấy nội dung).</summary>
public record PlayerDrewCardsPayload(
    string   PlayerId,
    string   PlayerName,
    int      CardCount,
    bool     WasPenalty,    // true nếu do +2/+4 stack
    string   NextPlayerId,
    DateTime Timestamp);

public record UnoCalledPayload(
    string   CallerId,
    string   CallerName,
    string   TargetId,
    string   TargetName,
    bool     IsSelfCall,    // true: tự gọi | false: bắt người khác
    DateTime Timestamp);

public record UnoCaughtPayload(
    string   VictimId,
    string   VictimName,
    int      PenaltyCards,  // số lá phạt (thường = 2)
    string   CaughtById,
    DateTime Timestamp);

// ─── Bot ────────────────────────────────────────────────────────────────────

public record BotThinkingPayload(
    string BotId,
    string BotName,
    int    ThinkingMs); // client dùng để animate spinner đúng thời gian

// ─── Connection ─────────────────────────────────────────────────────────────

public record PlayerDisconnectedPayload(
    string   UserId,
    string   DisplayName,
    int      ReconnectWindowSeconds, // client hiển thị countdown trước khi forfeit
    DateTime DisconnectedAt);

public record PlayerReconnectedPayload(
    string   UserId,
    string   DisplayName,
    DateTime ReconnectedAt);

// ─── Chat ───────────────────────────────────────────────────────────────────

public record ChatMessagePayload(
    string   SenderId,
    string   SenderName,
    string   Message,
    bool     IsSystem,      // true: server-generated event message
    DateTime Timestamp);

// ─── Error ──────────────────────────────────────────────────────────────────

public record ActionErrorPayload(
    string  Code,           // NOT_YOUR_TURN | INVALID_CARD | MUST_DRAW | GAME_NOT_ACTIVE | ...
    string  Message,
    string? Detail);

public static class ErrorCodes
{
    public const string NotYourTurn     = "NOT_YOUR_TURN";
    public const string InvalidCard     = "INVALID_CARD";
    public const string MustDrawFirst   = "MUST_DRAW_FIRST";
    public const string MustStack       = "MUST_STACK";
    public const string GameNotActive   = "GAME_NOT_ACTIVE";
    public const string NotInRoom       = "NOT_IN_ROOM";
    public const string RoomFull        = "ROOM_FULL";
    public const string NotHost         = "NOT_HOST";
    public const string CardNotInHand   = "CARD_NOT_IN_HAND";
    public const string MissingColor    = "MISSING_CHOSEN_COLOR";
    public const string InvalidColor    = "INVALID_COLOR";
    public const string AlreadyInRoom   = "ALREADY_IN_ROOM";
}
