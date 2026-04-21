namespace UnoGame.Core.Models;

/// <summary>
/// Trạng thái của một người chơi trong ván game.
/// Mutable — thay đổi theo từng lượt đi.
/// </summary>
public sealed class PlayerState
{
    // ── Identity ──────────────────────────────────────────────────────────

    public string PlayerId    { get; }
    public string DisplayName { get; }
    public string AvatarUrl   { get; }
    public bool   IsBot       { get; }

    // ── Hand ──────────────────────────────────────────────────────────────

    /// <summary>Bài trên tay. Thứ tự không quan trọng về luật, nhưng giữ stable cho UI.</summary>
    public List<Card> Hand { get; } = new();

    // ── UNO state ─────────────────────────────────────────────────────────

    /// <summary>
    /// True khi player đã tự gọi "UNO" sau khi đánh lá áp cuối.
    /// Reset về false sau mỗi lượt mà player rút hoặc đánh bài.
    /// </summary>
    public bool HasCalledUno { get; set; }

    /// <summary>
    /// Timestamp khi player đánh lá áp cuối (còn 1 lá).
    /// Dùng để tính thời gian cửa sổ bắt UNO (~2 giây).
    /// </summary>
    public DateTime? LastPlayedDownToOneAt { get; set; }

    // ── Connection ────────────────────────────────────────────────────────

    public bool     IsConnected       { get; set; } = true;
    public DateTime? DisconnectedAt   { get; set; }

    // ── Session stats ─────────────────────────────────────────────────────

    /// <summary>Số lượt đã đi trong ván này.</summary>
    public int TurnsPlayed  { get; set; }

    /// <summary>Số lá đã rút do bị phạt (+2/+4).</summary>
    public int PenaltyDraws { get; set; }

    // ── Constructor ────────────────────────────────────────────────────────

    public PlayerState(string playerId, string displayName, string avatarUrl = "", bool isBot = false)
    {
        PlayerId    = playerId;
        DisplayName = displayName;
        AvatarUrl   = avatarUrl;
        IsBot       = isBot;
    }

    // ── Hand operations ────────────────────────────────────────────────────

    public void AddCard(Card card) => Hand.Add(card);

    public void AddCards(IEnumerable<Card> cards) => Hand.AddRange(cards);

    public bool RemoveCard(Card card) => Hand.Remove(card);

    /// <summary>Tìm lá bài trong tay khớp với CardDto từ client request.</summary>
    public Card? FindCard(string color, string type, int? value)
    {
        return Hand.FirstOrDefault(c =>
            c.Color.ToString() == color &&
            c.Type.ToString()  == type  &&
            c.Value            == value);
    }

    /// <summary>Tính tổng điểm các lá còn lại trên tay (dùng khi thua).</summary>
    public int HandScore => Hand.Sum(c => c.ScoreValue);

    public override string ToString() =>
        $"{DisplayName} [{Hand.Count} cards]{(IsBot ? " [BOT]" : "")}";
}
