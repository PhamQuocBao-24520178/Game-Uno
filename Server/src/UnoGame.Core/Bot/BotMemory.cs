using UnoGame.Core.Models;

namespace UnoGame.Core.Bot;

/// <summary>
/// Card Counting Engine — theo dõi toàn bộ bài đã ra, ước tính phân phối còn lại.
///
/// Vì bot chạy server-side, nó có quyền truy cập vào:
///   - Tay bài của chính nó (đầy đủ)
///   - Discard pile (toàn bộ, công khai)
///   - DrawPile count (không biết thứ tự)
///   - Hand size của mỗi đối thủ (không biết nội dung)
///
/// Từ những thông tin này, bot tính:
///   - Số lá mỗi màu/loại còn lại trong game (draw pile + opponent hands)
///   - Xác suất đối thủ tiếp theo có lá cùng màu
///   - Xác suất bị stack counter (+2 hoặc +4)
///   - "Danger score" của từng đối thủ
/// </summary>
public sealed class BotMemory
{
    // Tổng số mỗi loại card trong bộ bài hoàn chỉnh — dùng CardKey nhất quán
    private static readonly Dictionary<CardKey, int> FullDeckCounts
        = BuildFullDeckCounts();

    // ── Fields ─────────────────────────────────────────────────────────────

    /// <summary>Số lần mỗi (color, type, value) đã xuất hiện trong discard pile.</summary>
    public IReadOnlyDictionary<CardKey, int> DiscardedCounts { get; }

    /// <summary>Số lá mỗi (color, type, value) ước tính còn lại trong game.</summary>
    public IReadOnlyDictionary<CardKey, int> RemainingCounts { get; }

    /// <summary>Tổng số lá còn lại ngoài tay bot (draw pile + opponent hands).</summary>
    public int TotalUnknownCards { get; }

    /// <summary>Phân phối màu ước tính trong các lá chưa biết.</summary>
    public IReadOnlyDictionary<CardColor, double> UnknownColorProbability { get; }

    // ── Constructor ────────────────────────────────────────────────────────

    public BotMemory(GameState state, string botId)
    {
        var botPlayer = state.GetPlayer(botId)!;

        // Đếm những gì đã biết
        var discarded = CountCards(state.DiscardPile);
        var myHand = CountCards(botPlayer.Hand);

        DiscardedCounts = discarded;

        // Tính remaining: full deck - discard - myHand
        var remaining = new Dictionary<CardKey, int>();
        foreach (var (key, total) in FullDeckCounts)
        {
            int dCount = discarded.TryGetValue(key, out int d) ? d : 0;
            int mCount = myHand.TryGetValue(key, out int m) ? m : 0;
            int rem = Math.Max(0, total - dCount - mCount);
            if (rem > 0) remaining[key] = rem;
        }
        RemainingCounts = remaining;

        // Unknown = draw pile + opponent hands
        int knownCards = state.DiscardPile.Count + botPlayer.Hand.Count;
        TotalUnknownCards = Math.Max(0, 108 - knownCards);

        // Color probability trong unknown cards
        var colorProb = new Dictionary<CardColor, double>();
        int totalRemaining = remaining.Values.Sum();
        foreach (var color in new[] { CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow })
        {
            int count = remaining
                .Where(kv => kv.Key.Color == color)
                .Sum(kv => kv.Value);
            colorProb[color] = totalRemaining > 0 ? (double)count / totalRemaining : 0.25;
        }
        UnknownColorProbability = colorProb;
    }

    // ── Probability queries ────────────────────────────────────────────────

    /// <summary>
    /// Xác suất đối thủ có lá cùng màu với currentColor trong tay.
    /// Dựa trên số lá màu đó còn lại / tổng lá chưa biết.
    /// </summary>
    public double ProbabilityOpponentHasColor(CardColor color, int opponentHandSize)
    {
        if (TotalUnknownCards == 0 || opponentHandSize == 0) return 0;

        int remaining = RemainingCounts
            .Where(kv => kv.Key.Color == color)
            .Sum(kv => kv.Value);

        // P(opponent has at least 1 of color) = 1 - P(none of their cards is color)
        // Using hypergeometric approximation
        double p = (double)remaining / TotalUnknownCards;
        double probNone = Math.Pow(1 - p, opponentHandSize);
        return 1 - probNone;
    }

    /// <summary>
    /// Xác suất đối thủ có DrawTwo card (để counter stack +2).
    /// </summary>
    public double ProbabilityOpponentHasDrawTwo(int opponentHandSize)
    {
        if (TotalUnknownCards == 0) return 0;
        int drawTwos = RemainingCounts
            .Where(kv => kv.Key.Type == CardType.DrawTwo)
            .Sum(kv => kv.Value);
        double p = (double)drawTwos / TotalUnknownCards;
        return 1 - Math.Pow(1 - p, opponentHandSize);
    }

    /// <summary>
    /// Xác suất đối thủ có WildDrawFour.
    /// </summary>
    public double ProbabilityOpponentHasWD4(int opponentHandSize)
    {
        if (TotalUnknownCards == 0) return 0;
        int wd4s = RemainingCounts
            .Where(kv => kv.Key.Type == CardType.WildDrawFour)
            .Sum(kv => kv.Value);
        double p = (double)wd4s / TotalUnknownCards;
        return 1 - Math.Pow(1 - p, opponentHandSize);
    }

    /// <summary>
    /// Số lá màu color còn lại trong game (unknown).
    /// </summary>
    public int RemainingOfColor(CardColor color) =>
        RemainingCounts.Where(kv => kv.Key.Color == color).Sum(kv => kv.Value);

    /// <summary>
    /// Số lá cùng number còn lại (để biết đánh số có ích không).
    /// </summary>
    public int RemainingOfNumber(int value) =>
        RemainingCounts.Where(kv => kv.Key.Type == CardType.Number && kv.Key.Value == value)
                       .Sum(kv => kv.Value);

    // ── Helpers ────────────────────────────────────────────────────────────

    private static Dictionary<CardKey, int> CountCards(IEnumerable<Card> cards)
    {
        var dict = new Dictionary<CardKey, int>();
        foreach (var card in cards)
        {
            var key = new CardKey(card.Color, card.Type, card.Value);
            dict[key] = dict.TryGetValue(key, out int existing) ? existing + 1 : 1;
        }
        return dict;
    }

    private static Dictionary<CardKey, int> BuildFullDeckCounts()
    {
        var d = new Dictionary<CardKey, int>();
        var colors = new[] { CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow };

        foreach (var c in colors)
        {
            Set(d, c, CardType.Number, 0, 1);
            for (int n = 1; n <= 9; n++) Set(d, c, CardType.Number, n, 2);
            Set(d, c, CardType.Skip, null, 2);
            Set(d, c, CardType.Reverse, null, 2);
            Set(d, c, CardType.DrawTwo, null, 2);
        }
        Set(d, CardColor.Wild, CardType.Wild, null, 4);
        Set(d, CardColor.Wild, CardType.WildDrawFour, null, 4);
        return d;
    }

    private static void Set(Dictionary<CardKey, int> d,
        CardColor c, CardType t, int? v, int count) =>
        d[new CardKey(c, t, v)] = count;
}

/// <summary>Composite key để tra cứu card counts.</summary>
public readonly record struct CardKey(CardColor Color, CardType Type, int? Value);