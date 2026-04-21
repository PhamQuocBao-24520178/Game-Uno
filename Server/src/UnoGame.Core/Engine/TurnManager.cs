namespace UnoGame.Core.Engine;

using UnoGame.Core.Models;

/// <summary>
/// Quản lý thứ tự lượt chơi UNO.
///
/// Luật quan trọng:
///   - Reverse với 2 người chơi = Skip (luật chính thức UNO)
///   - Skip tốn 1 lượt của người kế
///   - DrawTwo/WildDrawFour tốn lượt của người kế (phải rút/stack)
///   - WildDrawFour thay đổi chiều không, chỉ bắt người kế rút
/// </summary>
public static class TurnManager
{
    // ════════════════════════════════════════════════════════════════
    // CORE ADVANCE
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Áp dụng effect của lá vừa đánh và chuyển lượt.
    ///
    /// Trả về effects đã xảy ra (để broadcast cho client).
    /// </summary>
    public static TurnEffects ApplyCardAndAdvance(GameState state, Card playedCard)
    {
        var effects = new TurnEffects();

        switch (playedCard.Type)
        {
            case CardType.Skip:
                effects.SkippedPlayerId = PeekNextPlayer(state).PlayerId;
                Advance(state, steps: 2); // bỏ qua 1 người → đi 2 bước
                break;

            case CardType.Reverse:
                if (state.Players.Count == 2)
                {
                    // 2 người: Reverse = Skip
                    effects.Reversed           = true;
                    effects.SkippedPlayerId    = PeekNextPlayer(state).PlayerId;
                    state.Direction           *= -1;
                    Advance(state, steps: 2);
                }
                else
                {
                    effects.Reversed  = true;
                    state.Direction  *= -1;
                    Advance(state, steps: 1);
                }
                break;

            case CardType.DrawTwo:
                // Người kế bị stack penalty — lượt chuyển sang họ để rút/stack
                state.PendingDrawCount += 2;
                state.PendingStackType  = CardType.DrawTwo;
                effects.PendingDraw     = state.PendingDrawCount;
                effects.SkippedPlayerId = PeekNextPlayer(state).PlayerId;
                Advance(state, steps: 1); // chuyển sang người kế (để họ rút hoặc stack)
                break;

            case CardType.WildDrawFour:
                state.PendingDrawCount += 4;
                state.PendingStackType  = CardType.WildDrawFour;
                effects.PendingDraw     = state.PendingDrawCount;
                effects.SkippedPlayerId = PeekNextPlayer(state).PlayerId;
                Advance(state, steps: 1);
                break;

            case CardType.Wild:
            case CardType.Number:
            default:
                Advance(state, steps: 1);
                break;
        }

        state.TurnNumber++;
        state.LastActionAt = DateTime.UtcNow;
        state.CurrentPlayer.TurnsPlayed++;

        return effects;
    }

    /// <summary>
    /// Chuyển lượt sau khi player rút bài (không đánh được).
    /// Nếu vừa rút mà lá rút đánh được → KHÔNG advance (player quyết định tiếp).
    /// </summary>
    public static void AdvanceAfterDraw(GameState state)
    {
        Advance(state, steps: 1);
        state.TurnNumber++;
        state.LastActionAt = DateTime.UtcNow;
        state.CurrentPlayer.TurnsPlayed++;
    }

    // ════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tính index của player tiếp theo theo Direction, với bước tùy chỉnh.
    /// </summary>
    public static int NextIndex(GameState state, int steps = 1)
    {
        int n   = state.Players.Count;
        int idx = (state.CurrentPlayerIndex + state.Direction * steps % n + n * 2) % n;
        return idx;
    }

    /// <summary>Xem player tiếp theo mà không thay đổi state.</summary>
    public static PlayerState PeekNextPlayer(GameState state, int steps = 1) =>
        state.Players[NextIndex(state, steps)];

    /// <summary>Di chuyển CurrentPlayerIndex về phía trước steps bước.</summary>
    private static void Advance(GameState state, int steps)
    {
        state.CurrentPlayerIndex = NextIndex(state, steps);

        // Reset HasCalledUno khi lượt bắt đầu (không dùng trong turn trước)
        state.CurrentPlayer.HasCalledUno = false;
    }
}

/// <summary>Các hiệu ứng xảy ra từ lá bài vừa đánh — trả về để broadcast.</summary>
public sealed class TurnEffects
{
    /// <summary>Có đảo chiều không.</summary>
    public bool    Reversed        { get; set; }

    /// <summary>PlayerId bị mất lượt (Skip / DrawTwo / WD4).</summary>
    public string? SkippedPlayerId { get; set; }

    /// <summary>Tổng penalty đang tích lũy (> 0 khi có stack).</summary>
    public int     PendingDraw     { get; set; }
}
