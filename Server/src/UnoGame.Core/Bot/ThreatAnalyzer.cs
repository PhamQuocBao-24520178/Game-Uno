using UnoGame.Core.Models;

namespace UnoGame.Core.Bot;

/// <summary>
/// Threat Analyzer — đánh giá mức độ nguy hiểm của từng đối thủ và tình thế game.
///
/// Kết quả được dùng bởi CardScorer để điều chỉnh độ ưu tiên của từng lá bài.
/// </summary>
public sealed class ThreatAnalysis
{
    // ── Opponent threats ──────────────────────────────────────────────────

    /// <summary>Đối thủ nguy hiểm nhất (ít bài nhất). Null nếu không ai đặc biệt nguy hiểm.</summary>
    public PlayerState? MostDangerousOpponent { get; init; }

    /// <summary>Số bài của đối thủ nguy hiểm nhất.</summary>
    public int MostDangerousCardCount { get; init; } = 99;

    /// <summary>Có đối thủ nào đang ở UNO (1 bài, chưa gọi) không → phải bắt hoặc phòng thủ.</summary>
    public List<PlayerState> ImminentUnoThreats { get; init; } = new();

    /// <summary>Đối thủ ngồi kế tiếp theo chiều hiện tại.</summary>
    public PlayerState? NextPlayer { get; init; }

    /// <summary>Đối thủ ngồi kế tiếp có bài ít hơn ngưỡng nguy hiểm không.</summary>
    public bool NextPlayerIsDangerous { get; init; }

    // ── Stack attack ──────────────────────────────────────────────────────

    /// <summary>Hiện đang bị tấn công bằng +2/+4 stack.</summary>
    public bool UnderStackAttack { get; init; }

    /// <summary>Số lá bị phạt hiện tại.</summary>
    public int CurrentPenalty { get; init; }

    // ── My win distance ───────────────────────────────────────────────────

    /// <summary>Số lá tôi còn trên tay.</summary>
    public int MyCardCount { get; init; }

    /// <summary>Tôi đang ở UNO không (1 bài).</summary>
    public bool AmAtUno { get; init; }

    /// <summary>Tôi có thể thắng trong vòng 1 lượt không (có lá playable và còn 1 bài).</summary>
    public bool CanWinThisTurn { get; init; }

    // ── Game phase ────────────────────────────────────────────────────────

    /// <summary>Giai đoạn game: Early (nhiều bài) / Mid / Late (ít bài).</summary>
    public GamePhaseEstimate Phase { get; init; }

    // ── Danger thresholds ─────────────────────────────────────────────────

    public bool AnyOpponentCritical  => MostDangerousCardCount <= 2;
    public bool AnyOpponentDangerous => MostDangerousCardCount <= 4;
    public bool AnyUnoThreat         => ImminentUnoThreats.Count > 0;
}

public enum GamePhaseEstimate { Early, Mid, Late }

public static class ThreatAnalyzer
{
    private const int DangerThreshold  = 3;  // ≤ 3 bài = nguy hiểm
    private const int CriticalThreshold = 1; // 1 bài = critical

    /// <summary>
    /// Phân tích toàn bộ tình thế game từ góc nhìn của botId.
    /// </summary>
    public static ThreatAnalysis Analyze(GameState state, string botId)
    {
        var me         = state.GetPlayer(botId)!;
        var opponents  = state.Players.Where(p => p.PlayerId != botId).ToList();
        var nextPlayer = GetNextPlayer(state, botId);

        // Đối thủ nguy hiểm nhất
        var mostDangerous = opponents.MinBy(p => p.Hand.Count);

        // UNO threats: đối thủ có 1 bài, chưa gọi UNO
        var unoThreats = opponents
            .Where(p => p.Hand.Count == 1 && !p.HasCalledUno)
            .ToList();

        // Game phase ước tính từ tổng số bài còn lại
        int avgCards = opponents.Count > 0
            ? (int)opponents.Average(p => p.Hand.Count) : 7;
        var phase = avgCards >= 6 ? GamePhaseEstimate.Early
                  : avgCards >= 3 ? GamePhaseEstimate.Mid
                  :                  GamePhaseEstimate.Late;

        // Tôi có thể thắng lượt này không
        bool canWin = me.Hand.Count == 1 && state.IsPlayerTurn(botId);

        return new ThreatAnalysis
        {
            MostDangerousOpponent   = mostDangerous,
            MostDangerousCardCount  = mostDangerous?.Hand.Count ?? 99,
            ImminentUnoThreats      = unoThreats,
            NextPlayer              = nextPlayer,
            NextPlayerIsDangerous   = (nextPlayer?.Hand.Count ?? 99) <= DangerThreshold,
            UnderStackAttack        = state.PendingDrawCount > 0,
            CurrentPenalty          = state.PendingDrawCount,
            MyCardCount             = me.Hand.Count,
            AmAtUno                 = me.Hand.Count == 1,
            CanWinThisTurn          = canWin,
            Phase                   = phase
        };
    }

    /// <summary>Lấy player kế tiếp theo chiều hiện tại.</summary>
    private static PlayerState? GetNextPlayer(GameState state, string botId)
    {
        int myIdx  = state.Players.FindIndex(p => p.PlayerId == botId);
        if (myIdx < 0) return null;
        int n      = state.Players.Count;
        int nextIdx = ((myIdx + state.Direction) % n + n) % n;
        return state.Players[nextIdx];
    }

    /// <summary>
    /// Đánh giá mức độ "tấn công" hiệu quả của một action card lên đối thủ.
    /// Dùng bởi CardScorer để bonus điểm khi tình hình cấp bách.
    /// </summary>
    public static int AttackValue(Card card, ThreatAnalysis threat)
    {
        // Không có điểm tấn công nếu đối thủ không nguy hiểm
        if (!threat.AnyOpponentDangerous && !threat.AnyUnoThreat)
            return 0;

        return card.Type switch
        {
            CardType.WildDrawFour => threat.AnyOpponentCritical ? 60 : 30,
            CardType.DrawTwo      => threat.AnyOpponentCritical ? 45 : 20,
            CardType.Skip         => threat.NextPlayerIsDangerous ? 35 : 15,
            CardType.Reverse      =>
                // Reverse hữu ích khi player ngay trước mình nguy hiểm (đảo chiều để skip họ)
                threat.AnyOpponentDangerous ? 20 : 5,
            _ => 0
        };
    }
}
