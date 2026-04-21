using System.Text.Json.Serialization;

namespace UnoGame.Core.Models;

/// <summary>
/// Toàn bộ trạng thái của một ván UNO đang diễn ra.
/// Mutable — GameEngine thay đổi trực tiếp trên object này.
///
/// Serialization: dùng System.Text.Json để persist vào MongoDB.
/// GameService serialize/deserialize trước mỗi operation.
/// </summary>
public sealed class GameState
{
    // ── Identity ──────────────────────────────────────────────────────────

    public string    RoomId      { get; set; } = null!;
    public GamePhase Phase       { get; set; } = GamePhase.Waiting;
    public DateTime  StartedAt   { get; set; }
    public DateTime  LastActionAt{ get; set; }

    // ── Players ───────────────────────────────────────────────────────────

    /// <summary>Thứ tự trong list = thứ tự ngồi. CurrentPlayerIndex trỏ vào đây.</summary>
    public List<PlayerState> Players { get; set; } = new();

    public int CurrentPlayerIndex { get; set; }

    /// <summary>1 = chiều kim đồng hồ, -1 = ngược chiều.</summary>
    public int Direction { get; set; } = 1;

    // ── Cards ─────────────────────────────────────────────────────────────

    /// <summary>Chồng bài draw. Top = cuối List (dùng RemoveAt(Count-1) để rút).</summary>
    public List<Card> DrawPile { get; set; } = new();

    /// <summary>Chồng bài discard. Top = cuối List.</summary>
    public List<Card> DiscardPile { get; set; } = new();

    // ── Current color/type ────────────────────────────────────────────────

    /// <summary>
    /// Màu hiện tại. Thường = màu của top card,
    /// nhưng Wild cho phép đổi → CurrentColor khác TopCard.Color.
    /// </summary>
    public CardColor CurrentColor { get; set; }

    // ── Pending draw stack ────────────────────────────────────────────────

    /// <summary>Tổng số lá phạt đang tích lũy do +2/+4 stack chưa được giải quyết.</summary>
    public int PendingDrawCount { get; set; }

    /// <summary>Loại card đang được stack (DrawTwo hoặc WildDrawFour). Null = không có stack.</summary>
    public CardType? PendingStackType { get; set; }

    // ── Turn tracking ─────────────────────────────────────────────────────

    public int TurnNumber { get; set; } = 1;

    // ── Result ────────────────────────────────────────────────────────────

    public string? WinnerId    { get; set; }
    public DateTime? EndedAt  { get; set; }

    // ── Multi-round ───────────────────────────────────────────────────────

    /// <summary>Điểm tích lũy qua nhiều round (nếu game Best-of-N).</summary>
    public Dictionary<string, int> CumulativeScores { get; set; } = new();

    // ── Computed shortcuts ────────────────────────────────────────────────

    [JsonIgnore]
    public PlayerState CurrentPlayer => Players[CurrentPlayerIndex];

    [JsonIgnore]
    public Card? TopCard => DiscardPile.Count > 0 ? DiscardPile[^1] : null;

    public int DrawPileCount    => DrawPile.Count;
    public int DiscardPileCount => DiscardPile.Count;

    // ── Helpers ────────────────────────────────────────────────────────────

    public PlayerState? GetPlayer(string playerId) =>
        Players.FirstOrDefault(p => p.PlayerId == playerId);

    public bool IsPlayerTurn(string playerId) =>
        Phase == GamePhase.Playing && CurrentPlayer.PlayerId == playerId;
}
