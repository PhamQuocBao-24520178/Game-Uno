namespace UnoGame.Core.Models;

/// <summary>
/// Một lá bài UNO — immutable record.
///
/// Bộ bài chuẩn 108 lá:
///   4 màu × (1×"0" + 2×"1–9" + 2×Skip + 2×Reverse + 2×DrawTwo) = 100 lá
///   4×Wild + 4×WildDrawFour = 8 lá
///   Tổng = 108 lá
/// </summary>
public sealed record Card
{
    // ── Properties ────────────────────────────────────────────────────────

    public CardColor Color { get; }
    public CardType  Type  { get; }

    /// <summary>0–9 cho Number cards; null cho action/wild cards.</summary>
    public int? Value { get; }

    // ── Constructor ────────────────────────────────────────────────────────

    public Card(CardColor color, CardType type, int? value = null)
    {
        if (type == CardType.Number && (value is null or < 0 or > 9))
            throw new ArgumentException($"Number card must have value 0–9, got {value}");

        if (type != CardType.Number && value is not null)
            throw new ArgumentException($"Non-number card must have null value");

        if (type is CardType.Wild or CardType.WildDrawFour && color != CardColor.Wild)
            throw new ArgumentException($"Wild cards must have Color=Wild");

        if (type is not (CardType.Wild or CardType.WildDrawFour) && color == CardColor.Wild)
            throw new ArgumentException($"Only Wild cards can have Color=Wild");

        Color = color;
        Type  = type;
        Value = value;
    }

    // ── Derived properties ─────────────────────────────────────────────────

    public bool IsWild      => Type is CardType.Wild or CardType.WildDrawFour;
    public bool IsAction    => Type is CardType.Skip or CardType.Reverse
                                    or CardType.DrawTwo or CardType.Wild
                                    or CardType.WildDrawFour;
    public bool IsDrawCard  => Type is CardType.DrawTwo or CardType.WildDrawFour;

    /// <summary>
    /// Số lá rút mà card này gây ra (0 nếu không phải draw card).
    /// Dùng để tính stack penalty.
    /// </summary>
    public int DrawPenalty => Type switch
    {
        CardType.DrawTwo      => 2,
        CardType.WildDrawFour => 4,
        _                     => 0
    };

    // ── Play validation ────────────────────────────────────────────────────

    /// <summary>
    /// Kiểm tra lá bài này có thể đánh lên topCard không.
    ///
    /// Luật chuẩn:
    ///   Wild/WildDrawFour → luôn đánh được (trừ khi đang bị stack penalty)
    ///   Cùng màu với currentColor → đánh được
    ///   Cùng type (action) với topCard → đánh được (ví dụ Skip đánh lên Skip)
    ///   Cùng value (number) với topCard → đánh được (ví dụ 7 đánh lên 7)
    ///
    /// pendingStackType: nếu đang bị stack +2/+4, chỉ được đánh cùng loại.
    /// </summary>
    public bool CanPlayOn(Card topCard, CardColor currentColor, CardType? pendingStackType = null)
    {
        // Đang bị stack penalty: chỉ được counter-stack hoặc chịu rút
        if (pendingStackType.HasValue)
        {
            return Type == pendingStackType.Value ||
                   // WildDrawFour escalates lên DrawTwo penalty (house rule phổ biến)
                   (pendingStackType == CardType.DrawTwo && Type == CardType.WildDrawFour);
        }

        // Wild luôn đánh được (khi không bị stack)
        if (IsWild) return true;

        // Match màu hiện tại
        if (Color == currentColor) return true;

        // Match type (action card đánh lên cùng loại action)
        if (Type == topCard.Type && Type != CardType.Number) return true;

        // Match số
        if (Type == CardType.Number && topCard.Type == CardType.Number && Value == topCard.Value)
            return true;

        return false;
    }

    // ── Scoring ────────────────────────────────────────────────────────────

    /// <summary>
    /// Điểm của lá bài (tính cho tay người thua).
    /// Number = face value, Action = 20, Wild/* = 50.
    /// </summary>
    public int ScoreValue => Type switch
    {
        CardType.Number       => Value ?? 0,
        CardType.Skip         => 20,
        CardType.Reverse      => 20,
        CardType.DrawTwo      => 20,
        CardType.Wild         => 50,
        CardType.WildDrawFour => 50,
        _                     => 0
    };

    // ── Display / Serialization ────────────────────────────────────────────

    public override string ToString() => Type switch
    {
        CardType.Number       => $"{Color} {Value}",
        CardType.Wild         => "Wild",
        CardType.WildDrawFour => "Wild Draw Four",
        _                     => $"{Color} {Type}"
    };

    /// <summary>Mã ngắn để log/debug: R5, GS, BS, YR, W, WD4, ...</summary>
    public string ShortCode => Type switch
    {
        CardType.Number       => $"{Color.ToString()[0]}{Value}",
        CardType.Skip         => $"{Color.ToString()[0]}S",
        CardType.Reverse      => $"{Color.ToString()[0]}R",
        CardType.DrawTwo      => $"{Color.ToString()[0]}+2",
        CardType.Wild         => "W",
        CardType.WildDrawFour => "W+4",
        _                     => "?"
    };
}
