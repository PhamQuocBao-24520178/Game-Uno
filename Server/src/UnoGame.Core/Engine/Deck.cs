namespace UnoGame.Core.Engine;

using UnoGame.Core.Models;

/// <summary>
/// Tạo và quản lý bộ bài UNO chuẩn 108 lá.
///
/// Cấu trúc bộ bài:
///   4 màu (Red/Green/Blue/Yellow):
///     - 1 lá số 0
///     - 2 lá mỗi số 1–9 = 18 lá
///     - 2 lá Skip, 2 lá Reverse, 2 lá DrawTwo = 6 lá
///     - Mỗi màu: 25 lá × 4 màu = 100 lá
///   Wild cards:
///     - 4 lá Wild + 4 lá WildDrawFour = 8 lá
///   Tổng: 108 lá ✓
/// </summary>
public static class Deck
{
    private static readonly CardColor[] Colors =
        { CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow };

    // ── Build ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Tạo bộ bài đầy đủ 108 lá đã được xáo trộn.
    /// </summary>
    public static List<Card> BuildShuffled(Random? rng = null)
    {
        var deck = Build();
        Shuffle(deck, rng ?? Random.Shared);
        return deck;
    }

    /// <summary>
    /// Tạo bộ bài theo thứ tự cố định (dùng cho testing).
    /// </summary>
    public static List<Card> Build()
    {
        var cards = new List<Card>(108);

        foreach (var color in Colors)
        {
            // 1 lá số 0
            cards.Add(new Card(color, CardType.Number, 0));

            // 2 lá mỗi số 1–9
            for (int n = 1; n <= 9; n++)
            {
                cards.Add(new Card(color, CardType.Number, n));
                cards.Add(new Card(color, CardType.Number, n));
            }

            // 2 lá mỗi action card
            cards.Add(new Card(color, CardType.Skip));
            cards.Add(new Card(color, CardType.Skip));
            cards.Add(new Card(color, CardType.Reverse));
            cards.Add(new Card(color, CardType.Reverse));
            cards.Add(new Card(color, CardType.DrawTwo));
            cards.Add(new Card(color, CardType.DrawTwo));
        }

        // 4 Wild + 4 WildDrawFour
        for (int i = 0; i < 4; i++)
        {
            cards.Add(new Card(CardColor.Wild, CardType.Wild));
            cards.Add(new Card(CardColor.Wild, CardType.WildDrawFour));
        }

        return cards; // 108 lá
    }

    // ── Shuffle ────────────────────────────────────────────────────────────

    /// <summary>Fisher-Yates in-place shuffle cho bất kỳ List.</summary>
    public static void Shuffle<T>(List<T> items, Random? rng = null)
    {
        rng ??= Random.Shared;
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    // ── Deal ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Chia bài cho players từ draw pile.
    /// Mỗi player nhận initialHandSize lá (mặc định 7).
    /// Sau khi chia, lật lá đầu tiên hợp lệ làm top card.
    /// </summary>
    public static Card DealInitialHands(
        List<Card> drawPile,
        List<PlayerState> players,
        int initialHandSize = 7)
    {
        if (drawPile.Count < players.Count * initialHandSize + 1)
            throw new InvalidOperationException(
                $"Not enough cards to deal {initialHandSize} to {players.Count} players");

        // Chia theo vòng (round-robin) giống UNO thật
        for (int round = 0; round < initialHandSize; round++)
            foreach (var player in players)
                player.AddCard(DrawOne(drawPile));

        // Lật lá top card — không được là WildDrawFour
        Card firstCard;
        int attempts = 0;
        do
        {
            firstCard = DrawOne(drawPile);
            if (firstCard.Type == CardType.WildDrawFour)
            {
                // Bỏ vào cuối draw pile và tiếp tục
                drawPile.Insert(0, firstCard);
            }
            else
            {
                break;
            }
            attempts++;
        } while (attempts < drawPile.Count);

        if (firstCard.Type == CardType.WildDrawFour)
            throw new InvalidOperationException("All remaining cards are WildDrawFour");

        return firstCard;
    }

    // ── Draw helpers ───────────────────────────────────────────────────────

    /// <summary>Rút 1 lá từ top của draw pile (cuối List).</summary>
    public static Card DrawOne(List<Card> drawPile)
    {
        if (drawPile.Count == 0)
            throw new InvalidOperationException("Draw pile is empty — call Replenish first");

        var card = drawPile[^1];
        drawPile.RemoveAt(drawPile.Count - 1);
        return card;
    }

    /// <summary>Rút nhiều lá.</summary>
    public static List<Card> DrawMany(List<Card> drawPile, int count)
    {
        var drawn = new List<Card>(count);
        for (int i = 0; i < count; i++)
            drawn.Add(DrawOne(drawPile));
        return drawn;
    }

    // ── Replenish ──────────────────────────────────────────────────────────

    /// <summary>
    /// Khi draw pile sắp hết: xáo trộn discard pile (trừ lá trên cùng) vào draw pile.
    /// Trả về số lá được thêm vào.
    /// </summary>
    public static int Replenish(List<Card> drawPile, List<Card> discardPile, Random? rng = null)
    {
        if (discardPile.Count <= 1) return 0;

        // Giữ lại lá top của discard
        var topCard = discardPile[^1];

        // Chuyển tất cả trừ top vào draw pile
        var cardsToRecycle = discardPile.Take(discardPile.Count - 1).ToList();
        discardPile.RemoveRange(0, discardPile.Count - 1);

        // Wild cards trở về trạng thái "chưa chọn màu"
        // (không cần làm gì vì color = Wild khi màu được chọn không được lưu vào card object —
        //  màu được chọn lưu trong GameState.CurrentColor)

        Shuffle(cardsToRecycle, rng ?? Random.Shared);
        drawPile.AddRange(cardsToRecycle);

        return cardsToRecycle.Count;
    }

    /// <summary>
    /// Đảm bảo draw pile có đủ lá để rút.
    /// Tự động replenish nếu cần.
    /// Trả về true nếu đủ bài để rút count lá.
    /// </summary>
    public static bool EnsureAvailable(
        List<Card> drawPile,
        List<Card> discardPile,
        int count,
        Random? rng = null)
    {
        if (drawPile.Count >= count) return true;

        Replenish(drawPile, discardPile, rng);

        return drawPile.Count >= count;
    }
}