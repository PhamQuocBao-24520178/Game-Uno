using UnoGame.Core.Engine;
using UnoGame.Core.Models;

namespace UnoGame.Core.Bot;

/// <summary>
/// UNO Bot AI — Hard Mode.
///
/// Entry point duy nhất: <see cref="Decide"/>.
/// Nhận GameState thật (server-side), trả về BotDecision.
///
/// ┌─────────────────────────────────────────────────────────────────────┐
/// │  Decision tree                                                      │
/// ├─────────────────────────────────────────────────────────────────────┤
/// │  1. Kiểm tra bắt UNO đối thủ (cao nhất ưu tiên)                   │
/// │  2. Đang bị stack attack?                                           │
/// │     ├── Có stack card → PLAY stack card                             │
/// │     └── Không có → DRAW (nhận phạt)                                │
/// │  3. Có lá playable không?                                           │
/// │     ├── Không → DRAW                                                │
/// │     └── Có → Score tất cả → PLAY lá điểm cao nhất                 │
/// │         └── Trước khi đánh lá cuối → SELF_CALL_UNO                 │
/// │  4. Sau draw (canPlayDrawn = true):                                 │
/// │     └── Đánh luôn nếu score đủ cao (aggressive)                    │
/// └─────────────────────────────────────────────────────────────────────┘
/// </summary>
public sealed class UnoBot
{
    private readonly GameState      _state;
    private readonly string         _botId;
    private readonly BotMemory      _memory;
    private readonly ThreatAnalysis _threat;
    private readonly PlayerState    _me;

    public UnoBot(GameState state, string botId)
    {
        _state  = state;
        _botId  = botId;
        _me     = state.GetPlayer(botId)
                  ?? throw new ArgumentException($"Bot {botId} not found in game");
        _memory = new BotMemory(state, botId);
        _threat = ThreatAnalyzer.Analyze(state, botId);
    }

    // ════════════════════════════════════════════════════════════════
    // MAIN DECISION
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Phân tích game state và trả về quyết định tối ưu.
    /// Không có side effect — chỉ đọc state.
    /// </summary>
    public BotDecision Decide()
    {
        // ── Step 1: Bắt UNO đối thủ ngay lập tức ─────────────────────────
        var unoTarget = FindUnoTarget();
        if (unoTarget is not null)
            return BotDecision.CatchUno(unoTarget.PlayerId);

        // ── Step 2: Xử lý stack attack ────────────────────────────────────
        if (_threat.UnderStackAttack)
            return DecideUnderAttack();

        // ── Step 3: Chọn lá đánh tốt nhất ────────────────────────────────
        var playable  = RuleValidator.GetPlayableCards(_state, _botId);
        if (playable.Count == 0)
            return BotDecision.Draw("No playable cards");

        return DecidePlay(playable);
    }

    /// <summary>
    /// Gọi sau khi DrawCard trả về canPlayDrawn = true.
    /// Bot quyết định có đánh lá vừa rút không.
    /// </summary>
    public BotDecision DecideAfterDraw(Card drawnCard)
    {
        var topCard = _state.TopCard!;
        if (!drawnCard.CanPlayOn(topCard, _state.CurrentColor))
            return BotDecision.Pass("Drawn card not playable");

        // Score lá vừa rút
        var scored = CardScorer.Score(drawnCard, _me.Hand, _state, _botId, _memory, _threat);

        // Bot aggressive: luôn đánh nếu đánh được
        // Nhưng nếu score âm rõ ràng (ví dụ Wild khi có nhiều lá thường)
        // → pass để tiết kiệm Wild
        if (scored < -10)
            return BotDecision.Pass($"Drawn Wild not worth playing (score={scored:F1})");

        var color = drawnCard.IsWild
            ? ColorPicker.Pick(_me.Hand, _state, _botId, _memory, _threat)
            : (CardColor?)null;

        bool selfUno = _me.Hand.Count == 2; // sau khi đánh còn 1 lá

        return BotDecision.Play(drawnCard, color,
            $"Play drawn card (score={scored:F1})", scored, selfUno);
    }

    // ════════════════════════════════════════════════════════════════
    // PRIVATE DECISION BRANCHES
    // ════════════════════════════════════════════════════════════════

    private BotDecision DecideUnderAttack()
    {
        // Lấy tất cả stack cards (DrawTwo hoặc WD4)
        var stackCards = _me.Hand
            .Where(c => c.CanPlayOn(_state.TopCard!, _state.CurrentColor, _state.PendingStackType))
            .Where(c => c.IsDrawCard)
            .ToList();

        if (stackCards.Count == 0)
            return BotDecision.Draw(
                $"Cannot counter stack (+{_threat.CurrentPenalty}), accepting penalty");

        // Ưu tiên escalate: DrawTwo → WildDrawFour (nếu có thể)
        var escalation = stackCards
            .FirstOrDefault(c => c.Type == CardType.WildDrawFour
                              && _state.PendingStackType == CardType.DrawTwo);
        if (escalation is not null)
        {
            var escalColor = ColorPicker.Pick(_me.Hand, _state, _botId, _memory, _threat);
            bool selfUno   = _me.Hand.Count == 2;
            return BotDecision.Play(escalation, escalColor,
                $"Escalate +2 to +{_threat.CurrentPenalty + 4} with WD4", 999, selfUno);
        }

        // Chọn stack card tốt nhất
        var best = stackCards
            .OrderByDescending(c => c.Type == CardType.WildDrawFour ? 1 : 0)
            .First();

        var chosenColor = best.IsWild
            ? ColorPicker.Pick(_me.Hand, _state, _botId, _memory, _threat)
            : (CardColor?)null;

        bool callUno = _me.Hand.Count == 2;

        return BotDecision.Play(best, chosenColor,
            $"Counter stack with {best.ShortCode}, total={_threat.CurrentPenalty + best.DrawPenalty}",
            900, callUno);
    }

    private BotDecision DecidePlay(List<Card> playable)
    {
        // Tính điểm tất cả lá playable
        var scored = CardScorer.ScoreAll(
            playable, _me.Hand, _state, _botId, _memory, _threat);

        // ── Special: nếu UNO threat ngay lập tức và tôi có +4 → dùng ngay ──
        if (_threat.AnyUnoThreat)
        {
            var wd4 = scored.FirstOrDefault(s => s.Card.Type == CardType.WildDrawFour);
            if (wd4.Card is not null)
            {
                var attackColor = ColorPicker.Pick(_me.Hand, _state, _botId, _memory, _threat);
                bool selfUno    = _me.Hand.Count == 2;
                return BotDecision.Play(wd4.Card, attackColor,
                    $"WD4 to block UNO threat (score={wd4.Score:F1})", wd4.Score, selfUno);
            }

            // Dùng +2 nếu có
            var d2 = scored.FirstOrDefault(s => s.Card.Type == CardType.DrawTwo);
            if (d2.Card is not null)
            {
                bool selfUno = _me.Hand.Count == 2;
                return BotDecision.Play(d2.Card, null,
                    $"+2 to block UNO threat (score={d2.Score:F1})", d2.Score, selfUno);
            }
        }

        // ── Lấy lá điểm cao nhất ─────────────────────────────────────────
        var best = scored[0];

        // ── Màu cho Wild cards ────────────────────────────────────────────
        CardColor? chosenColor = null;
        if (best.Card.IsWild)
            chosenColor = ColorPicker.Pick(_me.Hand, _state, _botId, _memory, _threat);

        // ── Tự gọi UNO nếu đây là lá áp cuối ─────────────────────────────
        bool shouldCallUno = _me.Hand.Count == 2; // sau khi đánh còn 1 lá

        return BotDecision.Play(
            card: best.Card,
            color: chosenColor,
            reason: BuildReason(best, chosenColor),
            score: best.Score,
            selfCallUno: shouldCallUno);
    }

    // ── UNO catch detection ────────────────────────────────────────────────

    private PlayerState? FindUnoTarget()
    {
        return _state.Players.FirstOrDefault(p =>
            p.PlayerId != _botId &&
            p.Hand.Count == 1    &&
            !p.HasCalledUno      &&
            p.LastPlayedDownToOneAt.HasValue &&
            (DateTime.UtcNow - p.LastPlayedDownToOneAt.Value).TotalSeconds <= 5);
    }

    // ── Debug reason builder ───────────────────────────────────────────────

    private string BuildReason(ScoredCard best, CardColor? color)
    {
        var parts = new List<string> { $"Best card: {best.Card.ShortCode} (score={best.Score:F1})" };

        if (color.HasValue)
            parts.Add($"→ color={color}");

        if (_threat.AnyOpponentCritical)
            parts.Add($"[opponent critical: {_threat.MostDangerousCardCount} cards]");
        else if (_threat.AnyOpponentDangerous)
            parts.Add($"[opponent dangerous: {_threat.MostDangerousCardCount} cards]");

        if (_threat.UnderStackAttack)
            parts.Add($"[under attack: +{_threat.CurrentPenalty}]");

        return string.Join(" ", parts);
    }
}
