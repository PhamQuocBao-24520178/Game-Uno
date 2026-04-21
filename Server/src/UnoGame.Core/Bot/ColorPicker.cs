using UnoGame.Core.Models;

namespace UnoGame.Core.Bot;

/// <summary>
/// Color Picker — chọn màu tối ưu sau khi đánh Wild hoặc WildDrawFour.
///
/// Thuật toán tính điểm cho từng màu:
///   + Số lá màu đó trong tay bot        (×3) — tối đa hóa combo turn tiếp
///   + Số lá action của màu đó           (×2) — action cards có giá trị cao
///   + Màu ít xuất hiện trên discard     (×1) — có thể đối thủ thiếu màu này
///   - Xác suất đối thủ kế có màu đó    (×20) — không muốn đối thủ chơi tiếp
///   - Màu của đối thủ nguy hiểm nhất   (-5) — tránh pick màu giúp họ
/// </summary>
public static class ColorPicker
{
    private static readonly CardColor[] PlayColors =
        { CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow };

    /// <summary>
    /// Chọn màu tốt nhất cho bot sau khi đánh Wild/WildDrawFour.
    /// </summary>
    public static CardColor Pick(
        List<Card>      myHand,
        GameState       state,
        string          botId,
        BotMemory       memory,
        ThreatAnalysis  threat)
    {
        double bestScore = double.MinValue;
        var    bestColor = CardColor.Red;

        foreach (var color in PlayColors)
        {
            double score = ScoreColor(color, myHand, state, botId, memory, threat);
            if (score > bestScore)
            {
                bestScore = score;
                bestColor = color;
            }
        }

        return bestColor;
    }

    // ── Scoring ────────────────────────────────────────────────────────────

    private static double ScoreColor(
        CardColor      color,
        List<Card>     myHand,
        GameState      state,
        string         botId,
        BotMemory      memory,
        ThreatAnalysis threat)
    {
        double score = 0;

        // ── My hand composition ──────────────────────────────────────────
        var myCards    = myHand.Where(c => c.Color == color).ToList();
        var myActions  = myCards.Where(c => c.IsAction).ToList();

        score += myCards.Count    * 3.0;   // cơ hội đánh tiếp
        score += myActions.Count  * 2.0;   // action cards có giá trị hơn

        // Nếu picking màu này giúp tôi thắng ngay lượt sau
        if (myCards.Count == 1 && myHand.Count == 2)
            score += 15; // bonus "có thể thắng"

        // ── Opponent handicap ────────────────────────────────────────────
        // Xác suất đối thủ kế có bài màu này
        if (threat.NextPlayer is not null)
        {
            double nextProb = memory.ProbabilityOpponentHasColor(
                color, threat.NextPlayer.Hand.Count);
            score -= nextProb * 20; // không muốn chọn màu đối thủ dễ đánh
        }

        // Tránh màu của đối thủ nguy hiểm nhất
        if (threat.MostDangerousOpponent is not null && threat.AnyOpponentCritical)
        {
            double dangerProb = memory.ProbabilityOpponentHasColor(
                color, threat.MostDangerousOpponent.Hand.Count);
            score -= dangerProb * 25; // penalty cao hơn khi đối thủ sắp thắng
        }

        // ── Discard rarity bonus ─────────────────────────────────────────
        // Màu ít xuất hiện trên discard pile → đối thủ có thể thiếu
        int remaining = memory.RemainingOfColor(color);
        // Ít còn lại = hiếm = đối thủ ít có
        score += Math.Max(0, (10 - remaining)) * 0.5;

        // ── Consistency bonus ─────────────────────────────────────────────
        // Nếu top card vừa là màu này (trước khi đánh Wild) → đối thủ có thể đã đánh hết màu đó
        // → pick màu khác để tạo áp lực
        if (state.CurrentColor == color && myCards.Count == 0)
            score -= 5; // tránh pick màu mình không có gì

        return score;
    }

    /// <summary>
    /// Fast-path: chỉ nhìn vào tay bài, không cần memory/threat.
    /// Dùng khi cần quyết định nhanh hoặc trong game phase Early.
    /// </summary>
    public static CardColor PickSimple(List<Card> myHand)
    {
        var counts = PlayColors.ToDictionary(
            c => c,
            c => myHand.Count(card => card.Color == c));

        return counts.MaxBy(kv => kv.Value).Key;
    }
}
