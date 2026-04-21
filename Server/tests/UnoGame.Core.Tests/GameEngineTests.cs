using UnoGame.Core.Engine;
using UnoGame.Core.Models;
using Xunit;

namespace UnoGame.Core.Tests;

/// <summary>
/// Unit tests toàn diện cho UNO Game Engine.
/// Chạy: dotnet test --filter "FullyQualifiedName~GameEngineTests"
///
/// Nhóm tests:
///   Deck       — cấu trúc bộ bài, shuffle, replenish
///   Dealing    — chia bài, top card đầu tiên
///   PlayCard   — validation, action effects, color change
///   DrawCard   — normal draw, penalty draw, can-play-drawn
///   Stack      — +2/+4 stacking rules
///   UnoCall    — self-call, catch, timing window
///   TurnOrder  — direction, skip, reverse, 2-player special
///   WinCondition — win detection, scoring
///   EdgeCases  — replenish mid-game, empty draw pile
/// </summary>
public class GameEngineTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static GameState BuildState(int playerCount = 2, bool initialize = true)
    {
        var state = new GameState { RoomId = "test-room" };

        for (int i = 0; i < playerCount; i++)
            state.Players.Add(new PlayerState($"p{i + 1}", $"Player {i + 1}"));

        if (initialize)
        {
            var engine = new GameEngine(state, new Random(42)); // seed for determinism
            engine.Initialize();
        }

        return state;
    }

    private static Card Num(CardColor color, int value) =>
        new(color, CardType.Number, value);

    private static Card Skip(CardColor color) =>
        new(color, CardType.Skip);

    private static Card Rev(CardColor color) =>
        new(color, CardType.Reverse);

    private static Card D2(CardColor color) =>
        new(color, CardType.DrawTwo);

    private static Card Wild() =>
        new(CardColor.Wild, CardType.Wild);

    private static Card WD4() =>
        new(CardColor.Wild, CardType.WildDrawFour);

    // Tạo state với setup thủ công (không qua Initialize)
    private static GameEngine BuildManualEngine(
        Card topCard,
        CardColor currentColor,
        List<Card> p1Hand,
        List<Card>? p2Hand = null,
        int playerCount = 2,
        int pendingDraw = 0,
        CardType? pendingStack = null)
    {
        var state = new GameState
        {
            RoomId     = "test",
            Phase      = GamePhase.Playing,
            Direction  = 1,
            CurrentPlayerIndex = 0,
            CurrentColor       = currentColor,
            PendingDrawCount   = pendingDraw,
            PendingStackType   = pendingStack
        };

        for (int i = 0; i < playerCount; i++)
            state.Players.Add(new PlayerState($"p{i + 1}", $"P{i + 1}"));

        state.Players[0].AddCards(p1Hand);
        state.Players[1].AddCards(p2Hand ?? new List<Card> { Num(CardColor.Blue, 5) });

        state.DiscardPile.Add(topCard);

        // Deck bình thường
        state.DrawPile.AddRange(Deck.BuildShuffled(new Random(99)));

        return new GameEngine(state);
    }

    // ════════════════════════════════════════════════════════════════
    // DECK TESTS
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void Deck_Build_Has108Cards()
    {
        var deck = Deck.Build();
        Assert.Equal(108, deck.Count);
    }

    [Fact]
    public void Deck_Build_HasCorrectComposition()
    {
        var deck = Deck.Build();

        // Wild cards
        Assert.Equal(4, deck.Count(c => c.Type == CardType.Wild));
        Assert.Equal(4, deck.Count(c => c.Type == CardType.WildDrawFour));

        // Each color
        foreach (var color in new[] { CardColor.Red, CardColor.Green, CardColor.Blue, CardColor.Yellow })
        {
            Assert.Equal(1, deck.Count(c => c.Color == color && c.Type == CardType.Number && c.Value == 0));
            for (int n = 1; n <= 9; n++)
                Assert.Equal(2, deck.Count(c => c.Color == color && c.Type == CardType.Number && c.Value == n));
            Assert.Equal(2, deck.Count(c => c.Color == color && c.Type == CardType.Skip));
            Assert.Equal(2, deck.Count(c => c.Color == color && c.Type == CardType.Reverse));
            Assert.Equal(2, deck.Count(c => c.Color == color && c.Type == CardType.DrawTwo));
        }
    }

    [Fact]
    public void Deck_Shuffle_ChangeOrder()
    {
        var d1 = Deck.Build();
        var d2 = Deck.Build();
        Deck.Shuffle(d2, new Random(1));
        Assert.NotEqual(d1.Select(c => c.ToString()), d2.Select(c => c.ToString()));
    }

    [Fact]
    public void Deck_Replenish_MovesDiscardToDrawPile()
    {
        var draw    = new List<Card> { Num(CardColor.Red, 1) };
        var discard = new List<Card>
        {
            Num(CardColor.Red, 2), Num(CardColor.Blue, 3),
            Num(CardColor.Green, 4) // top card — kept
        };

        int added = Deck.Replenish(draw, discard);

        Assert.Equal(2, added);
        Assert.Equal(3, draw.Count);   // 1 original + 2 recycled
        Assert.Single(discard);        // only top card remains
    }

    // ════════════════════════════════════════════════════════════════
    // DEALING TESTS
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void Initialize_DealsSevenCardsEach()
    {
        var state = BuildState(playerCount: 4);
        Assert.All(state.Players, p => Assert.Equal(7, p.Hand.Count));
    }

    [Fact]
    public void Initialize_TopCardNotWildDrawFour()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var state = new GameState { RoomId = "t" };
            state.Players.Add(new PlayerState("p1", "P1"));
            state.Players.Add(new PlayerState("p2", "P2"));
            var engine = new GameEngine(state, new Random(seed));
            engine.Initialize();
            Assert.NotEqual(CardType.WildDrawFour, state.TopCard!.Type);
        }
    }

    [Fact]
    public void Initialize_SetsGamePhaseToPlaying()
    {
        var state = BuildState();
        Assert.Equal(GamePhase.Playing, state.Phase);
    }

    // ════════════════════════════════════════════════════════════════
    // PLAY CARD — BASIC
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void PlayCard_SameColor_Succeeds()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 5),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { Num(CardColor.Red, 7) });

        var result = engine.PlayCard("p1", Num(CardColor.Red, 7), null);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void PlayCard_SameNumber_Succeeds()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 5),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { Num(CardColor.Blue, 5) });

        var result = engine.PlayCard("p1", Num(CardColor.Blue, 5), null);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void PlayCard_WrongColorAndNumber_Fails()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 5),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { Num(CardColor.Blue, 7) });

        var result = engine.PlayCard("p1", Num(CardColor.Blue, 7), null);
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_CARD", result.ErrorCode);
    }

    [Fact]
    public void PlayCard_NotYourTurn_Fails()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 5),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { Num(CardColor.Red, 3) });

        var result = engine.PlayCard("p2", Num(CardColor.Red, 3), null);
        Assert.False(result.IsSuccess);
        Assert.Equal("NOT_YOUR_TURN", result.ErrorCode);
    }

    [Fact]
    public void PlayCard_CardNotInHand_Fails()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 5),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { Num(CardColor.Blue, 5) });

        // Cố đánh lá không có trong tay
        var result = engine.PlayCard("p1", Num(CardColor.Red, 9), null);
        Assert.False(result.IsSuccess);
        Assert.Equal("CARD_NOT_IN_HAND", result.ErrorCode);
    }

    // ════════════════════════════════════════════════════════════════
    // ACTION CARDS
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void PlaySkip_SkipsNextPlayer_In3PlayerGame()
    {
        var state = new GameState { RoomId = "t", Phase = GamePhase.Playing, Direction = 1 };
        state.Players.Add(new PlayerState("p1", "P1"));
        state.Players.Add(new PlayerState("p2", "P2"));
        state.Players.Add(new PlayerState("p3", "P3"));
        state.Players[0].AddCard(Skip(CardColor.Red));
        state.Players[1].AddCard(Num(CardColor.Blue, 1));
        state.Players[2].AddCard(Num(CardColor.Blue, 2));
        state.DiscardPile.Add(Num(CardColor.Red, 5));
        state.DrawPile.AddRange(Deck.BuildShuffled());
        state.CurrentColor = CardColor.Red;

        var engine = new GameEngine(state);
        var result = engine.PlayCard("p1", Skip(CardColor.Red), null);

        Assert.True(result.IsSuccess);
        // Skip: p1 đánh → p2 bị bỏ → p3 đi
        Assert.Equal("p3", state.CurrentPlayer.PlayerId);
        Assert.Equal("p2", result.Effects!.SkippedPlayerId);
    }

    [Fact]
    public void PlayReverse_2Players_ActsLikeSkip()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 1),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { Rev(CardColor.Red) });

        var p1Id = engine.State.Players[0].PlayerId;
        var result = engine.PlayCard("p1", Rev(CardColor.Red), null);

        Assert.True(result.IsSuccess);
        Assert.True(result.Effects!.Reversed);
        // 2 người: Reverse = Skip → p1 đi tiếp (p2 bị skip)
        Assert.Equal("p1", engine.State.CurrentPlayer.PlayerId);
    }

    [Fact]
    public void PlayReverse_3Players_ReversesDirection()
    {
        var state = new GameState { RoomId = "t", Phase = GamePhase.Playing, Direction = 1 };
        state.Players.Add(new PlayerState("p1", "P1"));
        state.Players.Add(new PlayerState("p2", "P2"));
        state.Players.Add(new PlayerState("p3", "P3"));
        state.Players[0].AddCard(Rev(CardColor.Red));
        state.Players[1].AddCard(Num(CardColor.Blue, 1));
        state.Players[2].AddCard(Num(CardColor.Blue, 2));
        state.DiscardPile.Add(Num(CardColor.Red, 5));
        state.DrawPile.AddRange(Deck.BuildShuffled());
        state.CurrentColor = CardColor.Red;

        var engine = new GameEngine(state);
        engine.PlayCard("p1", Rev(CardColor.Red), null);

        Assert.Equal(-1, state.Direction);
        Assert.Equal("p3", state.CurrentPlayer.PlayerId); // đi ngược: p1 → p3
    }

    [Fact]
    public void PlayDrawTwo_SetsPendingDraw_And_SkipsTarget()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 1),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { D2(CardColor.Red) },
            p2Hand       : new List<Card> { Num(CardColor.Blue, 5) });

        engine.PlayCard("p1", D2(CardColor.Red), null);

        Assert.Equal(2, engine.State.PendingDrawCount);
        Assert.Equal(CardType.DrawTwo, engine.State.PendingStackType);
        Assert.Equal("p2", engine.State.CurrentPlayer.PlayerId);
    }

    [Fact]
    public void PlayWild_ChangesColor()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 5),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { Wild() });

        engine.PlayCard("p1", Wild(), CardColor.Blue);

        Assert.Equal(CardColor.Blue, engine.State.CurrentColor);
    }

    [Fact]
    public void PlayWild_WithoutColor_Fails()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 5),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { Wild() });

        var result = engine.PlayCard("p1", Wild(), null);
        Assert.False(result.IsSuccess);
        Assert.Equal("MISSING_COLOR", result.ErrorCode);
    }

    // ════════════════════════════════════════════════════════════════
    // STACK +2 / +4
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void DrawTwo_CanBeStackedWithAnotherDrawTwo()
    {
        var engine = BuildManualEngine(
            topCard      : D2(CardColor.Red),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { D2(CardColor.Blue) },
            pendingDraw  : 2,
            pendingStack : CardType.DrawTwo);

        var result = engine.PlayCard("p1", D2(CardColor.Blue), null);
        Assert.True(result.IsSuccess);
        Assert.Equal(4, engine.State.PendingDrawCount);
    }

    [Fact]
    public void DrawTwo_CannotBeStackedWithNormalCard_WhenPending()
    {
        var engine = BuildManualEngine(
            topCard      : D2(CardColor.Red),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { Num(CardColor.Red, 5) },
            pendingDraw  : 2,
            pendingStack : CardType.DrawTwo);

        var result = engine.PlayCard("p1", Num(CardColor.Red, 5), null);
        Assert.False(result.IsSuccess);
        Assert.Equal("MUST_STACK_OR_DRAW", result.ErrorCode);
    }

    [Fact]
    public void WildDrawFour_CanEscalateFromDrawTwo()
    {
        var engine = BuildManualEngine(
            topCard      : D2(CardColor.Red),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { WD4() },
            pendingDraw  : 2,
            pendingStack : CardType.DrawTwo);

        var result = engine.PlayCard("p1", WD4(), CardColor.Green);
        Assert.True(result.IsSuccess);
        Assert.Equal(6, engine.State.PendingDrawCount); // 2 + 4
    }

    [Fact]
    public void DrawCard_WithPendingDraw_DrawsPenaltyCards()
    {
        var engine = BuildManualEngine(
            topCard      : D2(CardColor.Red),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card>(),
            p2Hand       : new List<Card> { Num(CardColor.Blue, 3) },
            pendingDraw  : 4,
            pendingStack : CardType.WildDrawFour);

        // p1 phải rút 4 lá
        int before = engine.State.Players[0].Hand.Count;
        var result = engine.DrawCard("p1");

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.DrawnCards.Count);
        Assert.Equal(DrawSource.Penalty, result.Source);
        Assert.Equal(before + 4, engine.State.Players[0].Hand.Count);
        Assert.Equal(0, engine.State.PendingDrawCount);
    }

    // ════════════════════════════════════════════════════════════════
    // DRAW CARD — NORMAL
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void DrawCard_Normal_DrawsOneCard()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 5),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card>());

        int before = engine.State.Players[0].Hand.Count;
        var result = engine.DrawCard("p1");

        Assert.True(result.IsSuccess);
        Assert.Single(result.DrawnCards);
        Assert.Equal(before + 1, engine.State.Players[0].Hand.Count);
    }

    [Fact]
    public void DrawCard_DrawnCardPlayable_ReturnsCanPlayTrue_DoesNotAdvanceTurn()
    {
        // Đảm bảo lá rút = Red 5 (cùng màu với top)
        var drawPile = new List<Card> { Num(CardColor.Red, 5) };

        var state = new GameState
        {
            RoomId = "t", Phase = GamePhase.Playing,
            Direction = 1, CurrentColor = CardColor.Red
        };
        state.Players.Add(new PlayerState("p1", "P1"));
        state.Players.Add(new PlayerState("p2", "P2"));
        state.Players[0].AddCard(Num(CardColor.Blue, 9));  // không đánh được
        state.Players[1].AddCard(Num(CardColor.Blue, 3));
        state.DiscardPile.Add(Num(CardColor.Red, 7));
        state.DrawPile.AddRange(drawPile);

        var engine = new GameEngine(state);
        var result = engine.DrawCard("p1");

        Assert.True(result.IsSuccess);
        Assert.True(result.CanPlayDrawn);
        // Turn không chuyển — vẫn là p1
        Assert.Equal("p1", state.CurrentPlayer.PlayerId);
    }

    // ════════════════════════════════════════════════════════════════
    // UNO CALL
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void CallUno_SelfCall_WhenOneCard_Succeeds()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 5),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { Num(CardColor.Blue, 3) });

        // Đặt timestamp như vừa đánh
        engine.State.Players[0].LastPlayedDownToOneAt = DateTime.UtcNow;

        var result = engine.CallUno("p1", "p1");
        Assert.True(result.IsSuccess);
        Assert.Equal(UnoCallResult.SelfCalled, result.Type);
        Assert.True(engine.State.Players[0].HasCalledUno);
    }

    [Fact]
    public void CallUno_CatchOpponent_WhoForgot_DrawsTwoCards()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 5),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { Num(CardColor.Blue, 7), Num(CardColor.Blue, 8) },
            p2Hand       : new List<Card> { Num(CardColor.Green, 3) });

        // p2 vừa đánh xuống còn 1 lá nhưng chưa gọi UNO
        engine.State.Players[1].LastPlayedDownToOneAt = DateTime.UtcNow;

        int p2Before = engine.State.Players[1].Hand.Count;
        var result = engine.CallUno("p1", "p2");

        Assert.True(result.IsSuccess);
        Assert.Equal(UnoCallResult.Caught, result.Type);
        Assert.Equal(2, result.PenaltyCards!.Count);
        Assert.Equal(p2Before + 2, engine.State.Players[1].Hand.Count);
    }

    [Fact]
    public void CallUno_CatchOpponent_WhoAlreadyCalled_Fails()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 5),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { Num(CardColor.Blue, 7) },
            p2Hand       : new List<Card> { Num(CardColor.Green, 3) });

        engine.State.Players[1].HasCalledUno           = true;
        engine.State.Players[1].LastPlayedDownToOneAt  = DateTime.UtcNow;

        var result = engine.CallUno("p1", "p2");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void CallUno_CatchWindow_ExpiredAfter5Seconds_Fails()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 5),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { Num(CardColor.Blue, 7) },
            p2Hand       : new List<Card> { Num(CardColor.Green, 3) });

        // Timestamp cũ hơn 5 giây
        engine.State.Players[1].LastPlayedDownToOneAt = DateTime.UtcNow.AddSeconds(-6);

        var result = engine.CallUno("p1", "p2");
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_UNO", result.ErrorCode);
    }

    [Fact]
    public void CallUno_OnPlayerWithMoreThanOneCard_Fails()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 5),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { Num(CardColor.Blue, 7) },
            p2Hand       : new List<Card> { Num(CardColor.Green, 3), Num(CardColor.Red, 2) });

        var result = engine.CallUno("p1", "p2");
        Assert.False(result.IsSuccess);
    }

    // ════════════════════════════════════════════════════════════════
    // WIN CONDITION
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void PlayLastCard_GameEnds_WithCorrectWinner()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 5),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { Num(CardColor.Red, 3) },  // chỉ 1 lá
            p2Hand       : new List<Card> { Num(CardColor.Blue, 7), Num(CardColor.Blue, 8) });

        var result = engine.PlayCard("p1", Num(CardColor.Red, 3), null);

        Assert.True(result.IsSuccess);
        Assert.True(result.IsGameOver);
        Assert.Equal("p1", result.WinnerId);
        Assert.Equal(GamePhase.Ended, engine.State.Phase);
    }

    [Fact]
    public void WinnerScore_IsSum_OfOtherPlayersHands()
    {
        var p2Hand = new List<Card>
        {
            Num(CardColor.Blue, 7),   // 7 pts
            Skip(CardColor.Red),      // 20 pts
            Wild()                    // 50 pts
        };

        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 5),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card> { Num(CardColor.Red, 3) },
            p2Hand       : p2Hand);

        engine.PlayCard("p1", Num(CardColor.Red, 3), null);

        int expectedScore = 7 + 20 + 50; // = 77
        Assert.Equal(expectedScore, engine.GetWinnerScore("p1"));
    }

    // ════════════════════════════════════════════════════════════════
    // TURN ORDER
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void TurnAdvances_Clockwise_By_Default()
    {
        var state = BuildState(playerCount: 3);
        string first = state.CurrentPlayer.PlayerId;

        // p1 đánh số
        var engine = new GameEngine(state);
        var p1Card = state.CurrentPlayer.Hand.First(c => c.Type == CardType.Number);
        engine.PlayCard(first, p1Card, null);

        string second = state.CurrentPlayer.PlayerId;
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void TurnManager_NextIndex_WrapsAround()
    {
        var state = new GameState { RoomId = "t", Direction = 1 };
        state.Players.AddRange(Enumerable.Range(0, 4).Select(i =>
            new PlayerState($"p{i}", $"P{i}")));
        state.CurrentPlayerIndex = 3; // cuối list

        int next = TurnManager.NextIndex(state, 1);
        Assert.Equal(0, next); // wrap về đầu
    }

    // ════════════════════════════════════════════════════════════════
    // EDGE CASES
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void Replenish_TriggeredAutomatically_WhenDrawPileEmpty()
    {
        var engine = BuildManualEngine(
            topCard      : Num(CardColor.Red, 5),
            currentColor : CardColor.Red,
            p1Hand       : new List<Card>());

        // Drain draw pile
        engine.State.DrawPile.Clear();

        // Thêm nhiều lá vào discard (để replenish)
        for (int i = 0; i < 10; i++)
            engine.State.DiscardPile.Insert(0, Num(CardColor.Blue, i % 10));

        // DrawCard phải tự replenish
        var result = engine.DrawCard("p1");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Card_CanPlayOn_SameActionType_DifferentColor()
    {
        var topCard = Skip(CardColor.Red);
        var card    = Skip(CardColor.Blue);

        // Skip đánh lên Skip bất kể màu
        Assert.True(card.CanPlayOn(topCard, CardColor.Red));
    }

    [Fact]
    public void Card_CanPlayOn_WildAlwaysTrue()
    {
        var topCard = Num(CardColor.Red, 5);
        Assert.True(Wild().CanPlayOn(topCard, CardColor.Red));
        Assert.True(WD4().CanPlayOn(topCard, CardColor.Red));
    }

    [Fact]
    public void Card_ScoreValue_Correct()
    {
        Assert.Equal(7,  Num(CardColor.Red, 7).ScoreValue);
        Assert.Equal(20, Skip(CardColor.Red).ScoreValue);
        Assert.Equal(20, Rev(CardColor.Red).ScoreValue);
        Assert.Equal(20, D2(CardColor.Red).ScoreValue);
        Assert.Equal(50, Wild().ScoreValue);
        Assert.Equal(50, WD4().ScoreValue);
    }

    [Fact]
    public void Serializer_RoundTrip_PreservesState()
    {
        var state = BuildState(playerCount: 3);
        var json  = GameStateSerializer.Serialize(state);
        var back  = GameStateSerializer.Deserialize(json);

        Assert.Equal(state.RoomId, back.RoomId);
        Assert.Equal(state.Phase, back.Phase);
        Assert.Equal(state.CurrentPlayerIndex, back.CurrentPlayerIndex);
        Assert.Equal(state.CurrentColor, back.CurrentColor);
        Assert.Equal(state.Players.Count, back.Players.Count);
        Assert.Equal(state.Players[0].Hand.Count, back.Players[0].Hand.Count);
    }
}
