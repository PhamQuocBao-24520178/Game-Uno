using UnoGame.Core.Models;

namespace UnoGame.Core.Bot;

// ════════════════════════════════════════════════════════════════════════════
// BOT DECISION — kết quả Decide() trả về cho BotOrchestrator
// ════════════════════════════════════════════════════════════════════════════

public enum BotActionType
{
    PlayCard,   // đánh bài
    DrawCard,   // rút bài
    CallUno,    // tự gọi UNO (trước khi đánh lá cuối)
    CatchUno,   // bắt đối thủ quên gọi UNO
    PassTurn,   // không đánh lá vừa rút
}

/// <summary>
/// Quyết định của bot sau khi phân tích toàn bộ game state.
/// BotOrchestrator đọc và thực thi theo thứ tự: PreActions → MainAction.
/// </summary>
public sealed record BotDecision
{
    // ── Main action ───────────────────────────────────────────────────────

    public BotActionType Action { get; init; }

    /// <summary>Lá bài muốn đánh (chỉ có khi Action = PlayCard).</summary>
    public Card? CardToPlay { get; init; }

    /// <summary>Màu chọn sau khi đánh Wild/WildDrawFour.</summary>
    public CardColor? ChosenColor { get; init; }

    /// <summary>Target khi bắt UNO (chỉ có khi Action = CatchUno).</summary>
    public string? UnoTargetId { get; init; }

    // ── Pre-actions (thực hiện trước main action) ─────────────────────────

    /// <summary>
    /// Bot nên gọi UNO cho chính mình trước khi đánh lá áp cuối.
    /// Chỉ có khi Action = PlayCard và sau khi đánh còn đúng 1 lá.
    /// </summary>
    public bool ShouldSelfCallUno { get; init; }

    // ── Debug / explanation ────────────────────────────────────────────────

    /// <summary>Lý do chọn action này (cho logging/debug).</summary>
    public string Reason { get; init; } = "";

    /// <summary>Điểm của lá bài được chọn (cao nhất trong danh sách).</summary>
    public double CardScore { get; init; }

    // ── Factory methods ────────────────────────────────────────────────────

    public static BotDecision Play(Card card, CardColor? color, string reason, double score = 0,
        bool selfCallUno = false) => new()
    {
        Action          = BotActionType.PlayCard,
        CardToPlay      = card,
        ChosenColor     = color,
        Reason          = reason,
        CardScore       = score,
        ShouldSelfCallUno = selfCallUno
    };

    public static BotDecision Draw(string reason) => new()
    {
        Action = BotActionType.DrawCard,
        Reason = reason
    };

    public static BotDecision SelfCallUno() => new()
    {
        Action = BotActionType.CallUno,
        Reason = "Self UNO call"
    };

    public static BotDecision CatchUno(string targetId) => new()
    {
        Action       = BotActionType.CatchUno,
        UnoTargetId  = targetId,
        Reason       = $"Catch UNO: {targetId}"
    };

    public static BotDecision Pass(string reason) => new()
    {
        Action = BotActionType.PassTurn,
        Reason = reason
    };
}
