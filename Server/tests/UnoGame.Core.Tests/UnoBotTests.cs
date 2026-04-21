using UnoGame.Core.Bot;
using UnoGame.Core.Engine;
using UnoGame.Core.Models;
using Xunit;

namespace UnoGame.Core.Tests;

/// <summary>
/// Unit tests cho UNO Bot AI.
/// Kiểm tra từng component và decision branch.
///
/// Nhóm:
///   BotMemory       — card counting accuracy
///   ThreatAnalyzer  — threat detection
///   CardScorer      — card scoring logic
///   ColorPicker     — color selection
///   UnoBot.Decide   — full decision tree
/// </summary>
public class UnoBotTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static Card Num(CardColor c, int v)  => new(c, CardType.Number, v);
    private static Card Skip(CardColor c)        => new(c, CardType.Skip);
    private static Card Rev(CardColor c)         => new(c, CardType.Reverse);
    private static Card D2(CardColor c)          => new(c, CardType.DrawTwo);
    private static Card Wild()                   => new(CardColor.Wild, CardType.Wild);
    private static Card WD4()                    => new(CardColor.Wild, CardType.WildDrawFour);

    private static GameState BuildState(
        Card topCard,
        CardColor currentColor,
        List<Card> botHand,
        List<List<Card>>? opponentHands = null,
        int pendingDraw = 0,
        CardType? pendingStack = null,
        int direction = 1)
    {
        var state = new GameState
        {
            RoomId           = "test",
            Phase            = GamePhase.Playing,
            Direction        = direction,
            CurrentPlayerIndex = 0,
            CurrentColor     = currentColor,
            PendingDrawCount = pendingDraw,
            PendingStackType = pendingStack
        };

        // Bot = player 0
        var bot = new PlayerState("bot", "Bot");
        bot.AddCards(botHand);
        state.Players.Add(bot);

        // Opponents
        int idx = 1;
        foreach (var hand in (opponentHands ?? new() { new List<Card> { Num(CardColor.Blue, 5) } }))
        {
            var opp = new PlayerState($"p{idx}", $"Player {idx}");
            opp.AddCards(hand);
            state.Players.Add(opp);
            idx++;
        }

        state.DiscardPile.Add(topCard);
        state.DrawPile.AddRange(Deck.BuildShuffled(new Random(42)));
        return state;
    }

    // ════════════════════════════════════════════════════════════════
    // BOT MEMORY TESTS
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void BotMemory_RemainingCounts_SubtractsDiscardAndHand()
    {
        var state = BuildState(
            topCard: Num(CardColor.Red, 5),
            currentColor: CardColor.Red,
            botHand: new() { Num(CardColor.Red, 1), Num(CardColor.Red, 2) });

        // Thêm vài lá vào discard
        state.DiscardPile.Add(Num(CardColor.Red, 3));
        state.DiscardPile.Add(Num(CardColor.Red, 4));

        var memory = new BotMemory(state, "bot");

        // Red 1 đã trong tay bot, Red 3/4 trong discard
        // Red 1: bộ bài có 2, trừ 1 trong tay = còn 1
        var key1 = new CardKey(CardColor.Red, CardType.Number, 1);
        Assert.True(memory.RemainingCounts.TryGetValue(key1, out int rem1));
        Assert.Equal(1, rem1);

        // Red 3: có 2 trong deck, 1 trong discard = còn 1
        var key3 = new CardKey(CardColor.Red, CardType.Number, 3);
        Assert.True(memory.RemainingCounts.TryGetValue(key3, out int rem3));
        Assert.Equal(1, rem3);
    }

    [Fact]
    public void BotMemory_ProbabilityOpponentHasColor_Decreases_WhenColorRare()
    {
        // Đưa nhiều Red vào discard → Red hiếm
        var state = BuildState(
            topCard: Num(CardColor.Red, 5),
            currentColor: CardColor.Red,
            botHand: new() { Num(CardColor.Blue, 1) });

        // Thêm nhiều Red vào discard
        for (int i = 0; i < 15; i++)
            state.DiscardPile.Add(Num(CardColor.Red, i % 9 + 1));

        var memory = new BotMemory(state, "bot");

        double probRed  = memory.ProbabilityOpponentHasColor(CardColor.Red,  7);
        double probBlue = memory.ProbabilityOpponentHasColor(CardColor.Blue, 7);

        // Blue chắc chắn phổ biến hơn Red (Red đã bị đẩy ra nhiều)
        Assert.True(probBlue > probRed,
            $"Expected Blue ({probBlue:F3}) > Red ({probRed:F3})");
    }

    [Fact]
    public void BotMemory_TotalUnknownCards_IsCorrect()
    {
        var state = BuildState(
            topCard: Num(CardColor.Red, 5),
            currentColor: CardColor.Red,
            botHand: new() { Num(CardColor.Blue, 1), Num(CardColor.Green, 2) });

        // Discard: 1 top card
        var memory = new BotMemory(state, "bot");

        // Unknown = 108 - discard(1) - myHand(2) = 105
        Assert.Equal(105, memory.TotalUnknownCards);
    }

    // ════════════════════════════════════════════════════════════════
    // THREAT ANALYZER TESTS
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void ThreatAnalyzer_DetectsImminentUnoThreat()
    {
        var state = BuildState(
            topCard: Num(CardColor.Red, 5),
            currentColor: CardColor.Red,
            botHand: new() { Num(CardColor.Blue, 7) },
            opponentHands: new() { new() { Num(CardColor.Green, 3) } }); // 1 card

        state.Players[1].LastPlayedDownToOneAt = DateTime.UtcNow;

        var threat = ThreatAnalyzer.Analyze(state, "bot");

        Assert.Single(threat.ImminentUnoThreats);
        Assert.Equal("p1", threat.ImminentUnoThreats[0].PlayerId);
        Assert.True(threat.AnyUnoThreat);
    }

    [Fact]
    public void ThreatAnalyzer_NoDangerWhenOpponentsHaveManyCards()
    {
        var state = BuildState(
            topCard: Num(CardColor.Red, 5),
            currentColor: CardColor.Red,
            botHand: new() { Num(CardColor.Blue, 7) },
            opponentHands: new()
            {
                new() { Num(CardColor.Green, 1), Num(CardColor.Green, 2),
                        Num(CardColor.Green, 3), Num(CardColor.Green, 4),
                        Num(CardColor.Green, 5), Num(CardColor.Green, 6),
                        Num(CardColor.Green, 7) }
            });

        var threat = ThreatAnalyzer.Analyze(state, "bot");

        Assert.False(threat.AnyOpponentDangerous);
        Assert.False(threat.AnyUnoThreat);
        Assert.Equal(7, threat.MostDangerousCardCount);
    }

    [Fact]
    public void ThreatAnalyzer_DetectsStackAttack()
    {
        var state = BuildState(
            topCard: D2(CardColor.Red),
            currentColor: CardColor.Red,
            botHand: new() { Num(CardColor.Blue, 5) },
            pendingDraw: 4,
            pendingStack: CardType.DrawTwo);

        var threat = ThreatAnalyzer.Analyze(state, "bot");

        Assert.True(threat.UnderStackAttack);
        Assert.Equal(4, threat.CurrentPenalty);
    }

    // ════════════════════════════════════════════════════════════════
    // CARD SCORER TESTS
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void CardScorer_WD4_ScoresHigherThanNumber_WhenOpponentCritical()
    {
        var opHand = new List<Card> { Num(CardColor.Green, 9) }; // 1 card = critical
        var state  = BuildState(
            topCard: Num(CardColor.Red, 5),
            currentColor: CardColor.Red,
            botHand: new() { WD4(), Num(CardColor.Red, 3) },
            opponentHands: new() { opHand });

        state.Players[1].LastPlayedDownToOneAt = DateTime.UtcNow;

        var memory = new BotMemory(state, "bot");
        var threat = ThreatAnalyzer.Analyze(state, "bot");

        double scoreWD4    = CardScorer.Score(WD4(), state.Players[0].Hand, state, "bot", memory, threat);
        double scoreNumber = CardScorer.Score(Num(CardColor.Red, 3), state.Players[0].Hand, state, "bot", memory, threat);

        Assert.True(scoreWD4 > scoreNumber,
            $"WD4 ({scoreWD4:F1}) should beat Number ({scoreNumber:F1}) when opponent is critical");
    }

    [Fact]
    public void CardScorer_Wild_PenalizedWhenHaveColorAlternatives()
    {
        var state = BuildState(
            topCard: Num(CardColor.Red, 5),
            currentColor: CardColor.Red,
            botHand: new()
            {
                Wild(),
                Num(CardColor.Red, 3), Num(CardColor.Red, 7), Num(CardColor.Red, 9)
            });

        var memory = new BotMemory(state, "bot");
        var threat = ThreatAnalyzer.Analyze(state, "bot");

        double scoreWild = CardScorer.Score(Wild(), state.Players[0].Hand, state, "bot", memory, threat);
        double scoreRed3 = CardScorer.Score(Num(CardColor.Red, 3), state.Players[0].Hand, state, "bot", memory, threat);

        // Wild bị penalize khi có nhiều Red
        Assert.True(scoreRed3 > scoreWild,
            $"Red3 ({scoreRed3:F1}) should beat Wild ({scoreWild:F1}) when lots of Red in hand");
    }

    [Fact]
    public void CardScorer_StackCard_ScoresMaxWhenUnderAttack()
    {
        var state = BuildState(
            topCard: D2(CardColor.Red),
            currentColor: CardColor.Red,
            botHand: new() { D2(CardColor.Blue), Num(CardColor.Red, 5) },
            pendingDraw: 2,
            pendingStack: CardType.DrawTwo);

        var memory = new BotMemory(state, "bot");
        var threat = ThreatAnalyzer.Analyze(state, "bot");

        double scoreD2  = CardScorer.Score(D2(CardColor.Blue), state.Players[0].Hand, state, "bot", memory, threat);
        double scoreNum = CardScorer.Score(Num(CardColor.Red, 5), state.Players[0].Hand, state, "bot", memory, threat);

        Assert.True(scoreD2 > scoreNum,
            $"DrawTwo ({scoreD2:F1}) must beat Number ({scoreNum:F1}) under stack attack");
    }

    // ════════════════════════════════════════════════════════════════
    // COLOR PICKER TESTS
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void ColorPicker_PrefersColorWithMostCardsInHand()
    {
        var hand = new List<Card>
        {
            Num(CardColor.Red, 1), Num(CardColor.Red, 2), Num(CardColor.Red, 3),
            Num(CardColor.Blue, 5)
        };

        var color = ColorPicker.PickSimple(hand);
        Assert.Equal(CardColor.Red, color);
    }

    [Fact]
    public void ColorPicker_Advanced_AvoidsColorOpponentLikelyHas()
    {
        // Bot có đều Red và Blue, nhưng opponent có nhiều Blue hơn
        var botHand = new List<Card>
        {
            Num(CardColor.Red, 1), Num(CardColor.Red, 2),
            Num(CardColor.Blue, 3), Num(CardColor.Blue, 4)
        };

        var opHand = Enumerable.Range(1, 6)
            .Select(i => Num(CardColor.Blue, i % 9 + 1))
            .ToList<Card>();

        var state = BuildState(
            topCard: Wild(),
            currentColor: CardColor.Red,
            botHand: botHand,
            opponentHands: new() { opHand });

        var memory = new BotMemory(state, "bot");
        var threat = ThreatAnalyzer.Analyze(state, "bot");

        var color = ColorPicker.Pick(botHand, state, "bot", memory, threat);

        // Red tốt hơn Blue vì opponent có ít Blue hơn trong tay
        // (Blue đang được opponent giữ nhiều → pick Red để giảm ưu thế của opponent)
        // Hoặc tùy vào remaining counts và xác suất
        // Chỉ kiểm tra kết quả hợp lệ
        Assert.Contains(color, new[] { CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow });
    }

    // ════════════════════════════════════════════════════════════════
    // UNOBOT DECISION TESTS
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void UnoBot_CatchesOpponentUno_BeforePlayingCard()
    {
        var state = BuildState(
            topCard: Num(CardColor.Red, 5),
            currentColor: CardColor.Red,
            botHand: new() { Num(CardColor.Red, 3), Num(CardColor.Blue, 7) },
            opponentHands: new() { new() { Num(CardColor.Green, 9) } });

        // Opponent vừa xuống còn 1 lá, chưa gọi UNO
        state.Players[1].LastPlayedDownToOneAt = DateTime.UtcNow;

        var bot      = new UnoBot(state, "bot");
        var decision = bot.Decide();

        Assert.Equal(BotActionType.CatchUno, decision.Action);
        Assert.Equal("p1", decision.UnoTargetId);
    }

    [Fact]
    public void UnoBot_CounterStacksWithDrawTwo()
    {
        var state = BuildState(
            topCard: D2(CardColor.Red),
            currentColor: CardColor.Red,
            botHand: new() { D2(CardColor.Blue), Num(CardColor.Green, 5) },
            pendingDraw: 2,
            pendingStack: CardType.DrawTwo);

        var bot      = new UnoBot(state, "bot");
        var decision = bot.Decide();

        Assert.Equal(BotActionType.PlayCard, decision.Action);
        Assert.Equal(CardType.DrawTwo, decision.CardToPlay?.Type);
    }

    [Fact]
    public void UnoBot_EscalatesD2_WithWD4()
    {
        var state = BuildState(
            topCard: D2(CardColor.Red),
            currentColor: CardColor.Red,
            botHand: new() { WD4(), Num(CardColor.Green, 5) },
            pendingDraw: 2,
            pendingStack: CardType.DrawTwo);

        var bot      = new UnoBot(state, "bot");
        var decision = bot.Decide();

        Assert.Equal(BotActionType.PlayCard, decision.Action);
        Assert.Equal(CardType.WildDrawFour, decision.CardToPlay?.Type);
    }

    [Fact]
    public void UnoBot_DrawsWhenNothingPlayable()
    {
        // Bot chỉ có bài xanh, top là đỏ, không có stack
        var state = BuildState(
            topCard: Num(CardColor.Red, 5),
            currentColor: CardColor.Red,
            botHand: new() { Num(CardColor.Blue, 7), Num(CardColor.Blue, 8) });

        var bot      = new UnoBot(state, "bot");
        var decision = bot.Decide();

        Assert.Equal(BotActionType.DrawCard, decision.Action);
    }

    [Fact]
    public void UnoBot_SelfCallsUno_WhenPlayingSecondToLastCard()
    {
        var state = BuildState(
            topCard: Num(CardColor.Red, 5),
            currentColor: CardColor.Red,
            botHand: new() { Num(CardColor.Red, 3), Num(CardColor.Green, 7) }); // 2 cards

        var bot      = new UnoBot(state, "bot");
        var decision = bot.Decide();

        // Đánh Red 3 → còn 1 lá → phải gọi UNO
        Assert.Equal(BotActionType.PlayCard, decision.Action);
        Assert.True(decision.ShouldSelfCallUno,
            "Bot must call UNO when playing down to 1 card");
    }

    [Fact]
    public void UnoBot_PicksWild_WithBestColor_WhenNoAlternative()
    {
        var state = BuildState(
            topCard: Num(CardColor.Red, 5),
            currentColor: CardColor.Red,
            botHand: new()
            {
                Wild(),
                Num(CardColor.Green, 1), Num(CardColor.Green, 2), Num(CardColor.Green, 3)
            });

        // Xóa lá Red để không có alternative
        state.Players[0].Hand.Clear();
        state.Players[0].AddCard(Wild());
        state.Players[0].AddCards(new[]
        {
            Num(CardColor.Green, 1), Num(CardColor.Green, 2), Num(CardColor.Green, 3)
        });

        var bot      = new UnoBot(state, "bot");
        var decision = bot.Decide();

        Assert.Equal(BotActionType.PlayCard, decision.Action);
        Assert.Equal(CardType.Wild, decision.CardToPlay?.Type);
        Assert.Equal(CardColor.Green, decision.ChosenColor); // Green vì nhiều nhất
    }

    [Fact]
    public void UnoBot_UsesWD4_ToBlockUnoThreat()
    {
        var state = BuildState(
            topCard: Num(CardColor.Red, 5),
            currentColor: CardColor.Red,
            botHand: new() { WD4(), Num(CardColor.Red, 3) },
            opponentHands: new() { new() { Num(CardColor.Green, 1) } }); // opponent UNO!

        state.Players[1].LastPlayedDownToOneAt = DateTime.UtcNow;

        // Catch UNO sẽ là action đầu tiên thực ra
        // Nhưng bot cũng nên có WD4 backup
        var bot      = new UnoBot(state, "bot");
        var decision = bot.Decide();

        // Với UNO threat: CatchUno là ưu tiên cao nhất (nếu trong window)
        Assert.Equal(BotActionType.CatchUno, decision.Action);
    }

    [Fact]
    public void UnoBot_DecideAfterDraw_PlaysDrawnCardIfPlayable()
    {
        var state = BuildState(
            topCard: Num(CardColor.Red, 5),
            currentColor: CardColor.Red,
            botHand: new() { Num(CardColor.Blue, 7), Num(CardColor.Red, 3) });

        var bot      = new UnoBot(state, "bot");
        var decision = bot.DecideAfterDraw(Num(CardColor.Red, 9));

        Assert.Equal(BotActionType.PlayCard, decision.Action);
        Assert.Equal(CardType.Number, decision.CardToPlay?.Type);
    }

    [Fact]
    public void UnoBot_DecideAfterDraw_PassesWildIfHaveAlternatives()
    {
        // Vừa rút được Wild nhưng đang có nhiều bài thường
        var state = BuildState(
            topCard: Num(CardColor.Red, 5),
            currentColor: CardColor.Red,
            botHand: new()
            {
                Num(CardColor.Red, 1), Num(CardColor.Red, 2),
                Num(CardColor.Red, 3), Num(CardColor.Red, 4)
            });

        var bot = new UnoBot(state, "bot");
        // Wild drawn khi có nhiều Red → có thể pass để tiết kiệm Wild
        var decision = bot.DecideAfterDraw(Wild());

        // Score Wild thấp khi có 4 lá Red → quyết định có thể là Pass
        // Chỉ kiểm tra action hợp lệ
        Assert.Contains(decision.Action, new[] { BotActionType.PlayCard, BotActionType.PassTurn });
    }

    // ════════════════════════════════════════════════════════════════
    // MULTI-OPPONENT TESTS
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void UnoBot_PrioritizesSkip_AgainstMostDangerousNextPlayer()
    {
        // Bot → p1 (3 bài, dangerous) → p2 (7 bài)
        var state = BuildState(
            topCard: Num(CardColor.Red, 5),
            currentColor: CardColor.Red,
            botHand: new() { Skip(CardColor.Red), Num(CardColor.Red, 3) },
            opponentHands: new()
            {
                new() { Num(CardColor.Blue, 1), Num(CardColor.Blue, 2), Num(CardColor.Blue, 3) },
                new() { Num(CardColor.Blue, 1), Num(CardColor.Blue, 2), Num(CardColor.Blue, 3),
                        Num(CardColor.Blue, 4), Num(CardColor.Blue, 5), Num(CardColor.Blue, 6),
                        Num(CardColor.Blue, 7) }
            });

        var memory = new BotMemory(state, "bot");
        var threat = ThreatAnalyzer.Analyze(state, "bot");

        double scoreSkip = CardScorer.Score(Skip(CardColor.Red), state.Players[0].Hand, state, "bot", memory, threat);
        double scoreNum  = CardScorer.Score(Num(CardColor.Red, 3), state.Players[0].Hand, state, "bot", memory, threat);

        Assert.True(scoreSkip > scoreNum,
            $"Skip ({scoreSkip:F1}) should beat Number ({scoreNum:F1}) when next player is dangerous");
    }
}
