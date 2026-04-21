namespace UnoGame.API.Hubs;

/// <summary>
/// Hằng số tên event cho SignalR.
/// Dùng ở cả server (hub broadcast) và client (Unity đăng ký lắng nghe).
/// Mọi thay đổi tên event phải cập nhật ở đây — không hardcode chuỗi ở nơi khác.
/// </summary>
public static class HubEvents
{
    // ── Lobby / Room ─────────────────────────────────────────────────────
    /// <summary>Một player (người hoặc bot) vừa vào phòng.</summary>
    public const string PlayerJoined         = "PlayerJoined";

    /// <summary>Một player rời phòng tự nguyện.</summary>
    public const string PlayerLeft           = "PlayerLeft";

    /// <summary>Host kick player. Target client phải disconnect ngay.</summary>
    public const string PlayerKicked         = "PlayerKicked";

    /// <summary>Player thay đổi trạng thái Ready.</summary>
    public const string PlayerReadyChanged   = "PlayerReadyChanged";

    /// <summary>Host thay đổi cài đặt phòng (maxPlayers, botCount, ...).</summary>
    public const string RoomSettingsUpdated  = "RoomSettingsUpdated";

    /// <summary>Host đóng phòng — tất cả client phải về màn hình Lobby.</summary>
    public const string RoomClosed           = "RoomClosed";

    // ── Game lifecycle ────────────────────────────────────────────────────
    /// <summary>Host bấm Start — đếm ngược trước khi chia bài.</summary>
    public const string GameStarting         = "GameStarting";

    /// <summary>Bài đã được chia, game bắt đầu. Broadcast public GameStateDto.</summary>
    public const string GameStarted          = "GameStarted";

    /// <summary>Private: gửi riêng MyHandDto cho từng player sau khi chia bài.</summary>
    public const string HandDealt            = "HandDealt";

    /// <summary>Lượt đi chuyển sang player tiếp theo.</summary>
    public const string TurnChanged          = "TurnChanged";

    /// <summary>Game kết thúc — có người thắng.</summary>
    public const string GameOver             = "GameOver";

    // ── In-game actions ───────────────────────────────────────────────────
    /// <summary>Public: ai đó đánh lá gì, màu hiện tại là gì, ai đi tiếp.</summary>
    public const string CardPlayed           = "CardPlayed";

    /// <summary>Private: lá bài thực tế mà player vừa rút (chỉ gửi cho họ).</summary>
    public const string CardsDrawn           = "CardsDrawn";

    /// <summary>Public: ai đó rút N lá (không lộ nội dung bài).</summary>
    public const string PlayerDrewCards      = "PlayerDrewCards";

    /// <summary>Ai đó tự gọi UNO hoặc bắt được người quên gọi.</summary>
    public const string UnoCalled            = "UnoCalled";

    /// <summary>Player bị bắt quên gọi UNO và phải rút thêm bài.</summary>
    public const string UnoCaught            = "UnoCaught";

    // ── Bot ──────────────────────────────────────────────────────────────
    /// <summary>Bot đang "suy nghĩ" — hiển thị spinner trên client.</summary>
    public const string BotThinking          = "BotThinking";

    // ── Connection ────────────────────────────────────────────────────────
    /// <summary>Player mất kết nối (network drop).</summary>
    public const string PlayerDisconnected   = "PlayerDisconnected";

    /// <summary>Player kết nối lại thành công.</summary>
    public const string PlayerReconnected    = "PlayerReconnected";

    // ── Sync (reconnect) ─────────────────────────────────────────────────
    /// <summary>Private: toàn bộ public GameStateDto gửi khi reconnect.</summary>
    public const string GameStateSynced      = "GameStateSynced";

    /// <summary>Private: MyHandDto gửi lại khi reconnect.</summary>
    public const string MyHandSynced         = "MyHandSynced";

    // ── Chat ─────────────────────────────────────────────────────────────
    /// <summary>Tin nhắn chat trong phòng.</summary>
    public const string ChatMessage          = "ChatMessage";

    // ── Error ────────────────────────────────────────────────────────────
    /// <summary>Private: lỗi action (không phải lượt của bạn, bài không hợp lệ...).</summary>
    public const string ActionError          = "ActionError";
}

/// <summary>
/// Tên method mà CLIENT phải implement để SERVER có thể invoke.
/// Không dùng trong hub — chỉ dùng để document contract cho Unity developer.
/// </summary>
public static class ClientMethods
{
    // Danh sách event client cần đăng ký:
    // connection.On<PlayerJoinedPayload>(HubEvents.PlayerJoined, ...)
    // connection.On<CardPlayedPayload>(HubEvents.CardPlayed, ...)
    // ... (xem HubPayloads.cs để biết kiểu tham số)
}
