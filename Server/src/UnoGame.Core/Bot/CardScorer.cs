using UnoGame.Core.Models;

namespace UnoGame.Core.Bot;

/// <summary>
/// Card Scorer — tính điểm cho từng lá bài có thể đánh.
///
/// Score = BaseTypeScore
///       + AttackBonus         (action card chống đối thủ nguy hiểm)
///       + HandThinningBonus   (giảm điểm tay nhanh → thắng sớm hơn)
///       + ColorEconomyBonus   (tiết kiệm Wild)
///       + ComboBonus          (mở đường cho lá tiếp theo)
///       + StackDefenseBonus   (counter attack đang đến)
///       - WildConservationPenalty (tránh lãng phí Wild khi không cần)
///       - SetupPenalty        (tránh đánh bài giúp đối thủ)
///
/// Điểm càng cao → bot chọn lá đó.
/// </summary>
public static class CardScorer
{
    // ── Score weights (tunable) ────────────────────────────────────────────

    private const double W_BaseAction      = 25;
    private const double W_BaseDraw        = 30;
    private const double W_BaseWild        = 12;   // thấp → tiết kiệm
    private const double W_BaseWD4         = 18;   // cao hơn Wild vì +4
    private const double W_NumberHighValue = 10;   // đánh số lớn trước (giảm điểm tay)
    private const double W_SameColorBonus  = 8;    // cùng màu → giữ chiều tấn công
    private const double W_ColorCountBonus = 3;    // mỗi lá cùng màu trong tay
    private const double W_WildPenalty     = 20;   // không cần Wild → penalize
    private const double W_WD4Penalty      = 15;   // không cần WD4 → penalize
    private const double W_UnoOpportunity  = 50;   // đánh lá này sẽ còn 1 bài
    private const double W_AttackUrgency   = 1.5;  // multiplier khi đối thủ critical

    // ── Public interface ────────────────────────────────────────────────────

    /// <summary>
    /// Tính điểm cho tất cả lá playable và trả về list đã sort (cao nhất trước).
    /// </summary>
    public static List<ScoredCard> ScoreAll(
        IEnumerable<Card> playable,
        List<Card>        myHand,
        GameState         state,
        string            botId,
        BotMemory         memory,
        ThreatAnalysis    threat)
    {
        return playable
            .Select(c => new ScoredCard(c, Score(c, myHand, state, botId, memory, threat)))
            .OrderByDescending(sc => sc.Score)
            .ToList();
    }

    /// <summary>Tính điểm cho một lá bài.</summary>
    public static double Score(
        Card           card,
        List<Card>     myHand,
        GameState      state,
        string         botId,
        BotMemory      memory,
        ThreatAnalysis threat)
    {
        double score = 0;

        // ── 1. Base type score ─────────────────────────────────────────────
        score += BaseScore(card);

        // ── 2. Stack defense (ưu tiên cao nhất khi đang bị tấn công) ────────
        if (threat.UnderStackAttack)
        {
            score += StackDefenseScore(card, state, threat);
            // Nếu không phải stack card → heavily penalize (đã validate ngoài)
        }

        // ── 3. Attack bonus khi đối thủ nguy hiểm ─────────────────────────
        score += ThreatAnalyzer.AttackValue(card, threat)
              * (threat.AnyOpponentCritical ? W_AttackUrgency : 1.0);

        // ── 4. Wild conservation penalty ──────────────────────────────────
        if (card.IsWild)
            score -= WildConservationPenalty(card, myHand, threat);

        // ── 5. Color economy — giữ Wild cho khi thực sự cần ──────────────
        if (!card.IsWild)
        {
            // Bonus nếu cùng màu với current → không phá chiều tấn công
            if (card.Color == state.CurrentColor)
                score += W_SameColorBonus;

            // Bonus tỷ lệ với số lá cùng màu trong tay
            int sameColorCount = myHand.Count(c => c.Color == card.Color && !c.IsWild);
            score += sameColorCount * W_ColorCountBonus;
        }

        // ── 6. Hand thinning — đánh lá có value lớn trước ─────────────────
        if (card.Type == CardType.Number)
            score += card.ScoreValue * 0.5; // giảm điểm hand score → thắng lợi hơn

        // ── 7. UNO opportunity bonus ──────────────────────────────────────
        if (myHand.Count == 2) // sau khi đánh sẽ còn 1 lá = UNO
            score += W_UnoOpportunity;

        // ── 8. Combo bonus — đánh lá tạo màu tốt cho lượt sau ────────────
        score += ComboBonus(card, myHand, state, threat);

        // ── 9. Setup penalty — tránh đánh Reverse khi đối thủ ngay sau mình ở chiều đó
        score -= SetupPenalty(card, state, threat);

        return score;
    }

    // ── Component calculations ─────────────────────────────────────────────

    private static double BaseScore(Card card) => card.Type switch
    {
        CardType.Skip         => W_BaseAction,
        CardType.Reverse      => W_BaseAction - 5,  // reverse ít hiệu quả hơn skip
        CardType.DrawTwo      => W_BaseDraw,
        CardType.Wild         => W_BaseWild,
        CardType.WildDrawFour => W_BaseWD4,
        CardType.Number       => W_NumberHighValue - (card.Value ?? 0) * 0.3,
        _                     => 0
    };

    private static double StackDefenseScore(Card card, GameState state, ThreatAnalysis threat)
    {
        if (!card.IsDrawCard) return -999; // không thể counter → rất thấp

        // WD4 > DrawTwo khi counter-stacking (4 > 2 penalty)
        double bonus = card.Type switch
        {
            CardType.WildDrawFour => 80 + threat.CurrentPenalty * 2,
            CardType.DrawTwo      => 60 + threat.CurrentPenalty,
            _                     => 0
        };

        // Bonus thêm nếu escalate được (DrawTwo → WD4)
        if (state.PendingStackType == CardType.DrawTwo && card.Type == CardType.WildDrawFour)
            bonus += 20; // escalation là lợi thế lớn

        return bonus;
    }

    private static double WildConservationPenalty(Card card, List<Card> hand, ThreatAnalysis threat)
    {
        // Đếm bài thường (non-wild) có thể đánh được
        // Nếu có nhiều lá thường playable → không cần Wild
        int nonWildPlayable = hand.Count(c => !c.IsWild);

        double penalty = card.Type switch
        {
            CardType.WildDrawFour => nonWildPlayable >= 2 ? W_WD4Penalty : 0,
            CardType.Wild         => nonWildPlayable >= 2 ? W_WildPenalty : 0,
            _                     => 0
        };

        // Không penalize Wild khi gần thắng (2 bài còn lại)
        if (hand.Count <= 2) penalty *= 0.2;

        // Không penalize WD4 khi đang tấn công đối thủ critical
        if (threat.AnyOpponentCritical && card.Type == CardType.WildDrawFour)
            penalty *= 0.1;

        return penalty;
    }

    private static double ComboBonus(
        Card card, List<Card> hand, GameState state, ThreatAnalysis threat)
    {
        double bonus = 0;

        if (card.IsWild) return 0; // Wild không combo theo màu

        // Nếu đánh card này → màu mới = card.Color
        // Kiểm tra còn bao nhiêu lá cùng màu đó trong tay
        int nextTurnOptions = hand.Count(c =>
            (c.Color == card.Color || c.IsWild) && !c.Equals(card));

        bonus += nextTurnOptions * 1.5; // mỗi option tiếp theo = +1.5

        // Bonus nếu có action card cùng màu (chain attack)
        bool hasActionOfSameColor = hand.Any(c =>
            c.Color == card.Color && c.IsAction && !c.IsWild && !c.Equals(card));
        if (hasActionOfSameColor && threat.AnyOpponentDangerous)
            bonus += 8;

        // Bonus nếu có lá cùng số (số đỏ 5 → sau đó đánh xanh 5)
        if (card.Type == CardType.Number)
        {
            bool hasSameNumber = hand.Any(c =>
                c.Type == CardType.Number && c.Value == card.Value && !c.Equals(card));
            if (hasSameNumber) bonus += 5;
        }

        return bonus;
    }

    private static double SetupPenalty(Card card, GameState state, ThreatAnalysis threat)
    {
        // Reverse penalty: nếu đảo chiều thì đối thủ nguy hiểm đi tiếp (không phải bị skip)
        if (card.Type == CardType.Reverse && state.Players.Count > 2)
        {
            // Sau reverse, chiều đổi → player "phía sau" mình sẽ đi
            // Nếu player phía sau nguy hiểm → không nên Reverse
            int myIdx     = state.Players.FindIndex(p => p.PlayerId ==
                state.CurrentPlayer.PlayerId);
            int n         = state.Players.Count;
            int behindIdx = ((myIdx - state.Direction) % n + n) % n;
            var behind    = state.Players[behindIdx];

            if (behind.Hand.Count <= 3)
                return 12; // penalize Reverse vì nó sẽ cho người nguy hiểm đi sớm hơn
        }

        return 0;
    }
}

/// <summary>Lá bài kèm điểm số.</summary>
public readonly record struct ScoredCard(Card Card, double Score);
