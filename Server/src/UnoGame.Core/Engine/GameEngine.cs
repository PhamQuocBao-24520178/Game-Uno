namespace UnoGame.Core.Engine;

using UnoGame.Core.Models;

/// <summary>
/// UNO Game Engine — orchestrator trung tâm.
///
/// Không có dependency ngoài (không dùng DI, không dùng DB).
/// Hoạt động trên GameState object được truyền vào.
/// GameService trong Infrastructure layer:
///   1. Load GameState từ MongoDB
///   2. Tạo GameEngine với state đó
///   3. Gọi method trên engine
///   4. Lưu state trở lại
///   5. Map sang DTO và trả về
///
/// Tất cả method trả về EngineResult — không throw exception business logic.
/// Chỉ throw nếu state bị corrupt (defensive programming).
/// </summary>
public sealed class GameEngine
{
    private readonly GameState _state;
    private readonly Random    _rng;

    public GameState State => _state;

    public GameEngine(GameState state, Random? rng = null)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _rng   = rng ?? Random.Shared;
    }

    // ════════════════════════════════════════════════════════════════
    // INITIALIZE GAME
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Khởi tạo game mới: xáo bài, chia bài, lật top card.
    /// Gọi một lần duy nhất khi bắt đầu ván chơi.
    ///
    /// Nếu topCard là action card:
    ///   Skip → người đầu tiên mất lượt
    ///   Reverse → đảo chiều ngay
    ///   DrawTwo → người đầu tiên phải rút 2
    ///   Wild → hỏi người đầu để chọn màu (client xử lý)
    /// </summary>
    public void Initialize()
    {
        if (_state.Phase != GamePhase.Waiting)
            throw new InvalidOperationException("Game is already initialized");

        // Xáo và chia bài
        _state.DrawPile = Deck.BuildShuffled(_rng);
        var topCard = Deck.DealInitialHands(_state.DrawPile, _state.Players);

        // Đặt top card lên discard pile
        _state.DiscardPile.Add(topCard);
        _state.CurrentColor = topCard.IsWild ? CardColor.Red : topCard.Color;
        _state.Phase        = GamePhase.Playing;
        _state.StartedAt    = DateTime.UtcNow;
        _state.LastActionAt = DateTime.UtcNow;

        // Xáo thứ tự player ngẫu nhiên
        Deck.Shuffle(_state.Players, _rng);
        _state.CurrentPlayerIndex = 0;

        // Áp dụng effect của top card đầu tiên
        ApplyFirstCardEffect(topCard);
    }

    private void ApplyFirstCardEffect(Card topCard)
    {
        switch (topCard.Type)
        {
            case CardType.Skip:
                // Người đầu tiên bị skip → người thứ hai đi
                TurnManager.AdvanceAfterDraw(_state);
                break;

            case CardType.Reverse:
                if (_state.Players.Count > 2)
                {
                    _state.Direction = -1;
                    // Người cuối cùng đi trước
                    TurnManager.AdvanceAfterDraw(_state);
                }
                // 2 người: Reverse = Skip → người đầu vẫn đi
                break;

            case CardType.DrawTwo:
                // Người đầu tiên phải rút 2 hoặc stack
                _state.PendingDrawCount = 2;
                _state.PendingStackType = CardType.DrawTwo;
                break;

            case CardType.Wild:
                // Người đầu tiên chọn màu (GameService sẽ hỏi client)
                // Mặc định Red cho đến khi client respond
                _state.CurrentColor = CardColor.Red;
                break;
        }
    }

    // ════════════════════════════════════════════════════════════════
    // PLAY CARD
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Player đánh một lá bài.
    ///
    /// Validation → Execute → Apply effect → Advance turn → Check win.
    /// </summary>
    public PlayCardResult PlayCard(string playerId, Card card, CardColor? chosenColor)
    {
        // ── Validate ───────────────────────────────────────────────────
        var check = RuleValidator.ValidatePlayCard(_state, playerId, card, chosenColor);
        if (!check.IsValid)
            return PlayCardResult.Failure(check.Error!, check.Code!);

        var player = _state.CurrentPlayer;

        // ── Execute: remove card from hand ─────────────────────────────
        player.RemoveCard(card);

        // ── Place on discard pile ──────────────────────────────────────
        _state.DiscardPile.Add(card);

        // ── Set current color ──────────────────────────────────────────
        _state.CurrentColor = card.IsWild
            ? chosenColor!.Value
            : card.Color;

        // ── Reset pending stack nếu card không phải draw card ──────────
        if (!card.IsDrawCard)
        {
            _state.PendingDrawCount = 0;
            _state.PendingStackType = null;
        }

        // ── UNO auto-detection ─────────────────────────────────────────
        if (player.Hand.Count == 1)
        {
            // Player vừa đánh xuống còn 1 lá → mở cửa sổ bắt UNO
            player.LastPlayedDownToOneAt = DateTime.UtcNow;
        }
        else
        {
            player.LastPlayedDownToOneAt = null;
        }

        // ── Kiểm tra thắng ────────────────────────────────────────────
        if (RuleValidator.IsWinner(player))
        {
            _state.Phase    = GamePhase.Ended;
            _state.WinnerId = playerId;
            _state.EndedAt  = DateTime.UtcNow;

            var results = RuleValidator.CalculateResults(_state, playerId);
            var score   = RuleValidator.CalculateWinnerScore(_state, playerId);

            // Cộng điểm vào cumulative
            _state.CumulativeScores.TryGetValue(playerId, out int prev);
            _state.CumulativeScores[playerId] = prev + score;

            return PlayCardResult.Win(playerId, card, score, results);
        }

        // ── Apply card effect + advance turn ──────────────────────────
        var effects = TurnManager.ApplyCardAndAdvance(_state, card);

        return PlayCardResult.Success(card, effects, _state.CurrentPlayer.PlayerId);
    }

    // ════════════════════════════════════════════════════════════════
    // DRAW CARD
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Player rút bài từ draw pile.
    ///
    /// Hai trường hợp:
    ///   A. Đang bị stack penalty (+2/+4): rút đúng số lá phạt → mất lượt
    ///   B. Không bị phạt: rút 1 lá
    ///      - Nếu lá rút có thể đánh → player có thể chọn đánh hoặc pass
    ///      - Nếu không đánh được → tự động pass
    /// </summary>
    public DrawCardResult DrawCard(string playerId)
    {
        // ── Validate ───────────────────────────────────────────────────
        var check = RuleValidator.ValidateDrawCard(_state, playerId);
        if (!check.IsValid)
            return DrawCardResult.Failure(check.Error!, check.Code!);

        var player = _state.CurrentPlayer;

        // ── Case A: Draw penalty từ +2/+4 stack ───────────────────────
        if (_state.PendingDrawCount > 0)
        {
            int count = _state.PendingDrawCount;
            Deck.EnsureAvailable(_state.DrawPile, _state.DiscardPile, count, _rng);

            var penaltyCards = Deck.DrawMany(_state.DrawPile, count);
            player.AddCards(penaltyCards);
            player.PenaltyDraws += count;

            // Clear stack
            _state.PendingDrawCount = 0;
            _state.PendingStackType = null;

            // Mất lượt sau khi nhận phạt
            TurnManager.AdvanceAfterDraw(_state);

            return DrawCardResult.Success(
                penaltyCards, canPlayDrawn: false, DrawSource.Penalty, nextPlayerId: _state.CurrentPlayer.PlayerId);
        }

        // ── Case B: Rút 1 lá thường ───────────────────────────────────
        Deck.EnsureAvailable(_state.DrawPile, _state.DiscardPile, 1, _rng);

        var drawn    = Deck.DrawOne(_state.DrawPile);
        player.AddCard(drawn);

        var topCard      = _state.TopCard!;
        bool canPlayDrawn = drawn.CanPlayOn(topCard, _state.CurrentColor);

        if (canPlayDrawn)
        {
            // Player có thể đánh lá vừa rút ngay — KHÔNG advance turn
            // GameService sẽ đợi player quyết định (PlayCard hoặc PassTurn)
            _state.LastActionAt = DateTime.UtcNow;
            return DrawCardResult.Success(
                new List<Card> { drawn }, canPlayDrawn: true,
                DrawSource.Normal, nextPlayerId: playerId);
        }

        // Lá rút không đánh được → tự động pass
        TurnManager.AdvanceAfterDraw(_state);

        return DrawCardResult.Success(
            new List<Card> { drawn }, canPlayDrawn: false,
            DrawSource.Normal, nextPlayerId: _state.CurrentPlayer.PlayerId);
    }

    // ════════════════════════════════════════════════════════════════
    // PASS TURN (sau khi rút lá đánh được nhưng không muốn đánh)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Player chọn không đánh lá vừa rút → pass turn.
    /// Chỉ hợp lệ khi DrawCard vừa trả về canPlayDrawn = true.
    /// </summary>
    public EngineResult PassTurn(string playerId)
    {
        var check = RuleValidator.ValidateTurn(_state, playerId);
        if (!check.IsValid) return EngineResult.Failure(check.Error!, check.Code!);

        TurnManager.AdvanceAfterDraw(_state);
        return EngineResult.Ok(nextPlayerId: _state.CurrentPlayer.PlayerId);
    }

    // ════════════════════════════════════════════════════════════════
    // CALL UNO
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gọi UNO (tự gọi) hoặc bắt người quên gọi.
    ///
    /// Self-call: đánh dấu HasCalledUno = true → tránh bị bắt.
    /// Catch: target chưa gọi UNO, còn 1 lá → bị phạt rút 2 lá.
    /// </summary>
    public CallUnoResult CallUno(string callerId, string targetId)
    {
        var validation = RuleValidator.ValidateUnoCall(_state, callerId, targetId);

        if (!validation.IsValid)
            return CallUnoResult.Invalid(validation.Message ?? "Invalid UNO call");

        var target = _state.GetPlayer(targetId)!;

        switch (validation.Result)
        {
            case UnoCallResult.SelfCalled:
                target.HasCalledUno           = true;
                target.LastPlayedDownToOneAt   = null; // đóng cửa sổ bắt
                return CallUnoResult.SelfCalled();

            case UnoCallResult.Caught:
                // Phạt 2 lá
                Deck.EnsureAvailable(_state.DrawPile, _state.DiscardPile, 2, _rng);
                var penaltyCards = Deck.DrawMany(_state.DrawPile, 2);
                target.AddCards(penaltyCards);
                target.PenaltyDraws          += 2;
                target.LastPlayedDownToOneAt   = null;
                return CallUnoResult.Caught(targetId, penaltyCards);

            default:
                return CallUnoResult.Invalid("Unknown error");
        }
    }

    // ════════════════════════════════════════════════════════════════
    // QUERY HELPERS (không thay đổi state)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lấy danh sách bài có thể đánh của player hiện tại.
    /// </summary>
    public List<Card> GetPlayableCards(string playerId) =>
        RuleValidator.GetPlayableCards(_state, playerId);

    /// <summary>
    /// Player có thể đánh bất kỳ bài nào không.
    /// </summary>
    public bool CanPlay(string playerId) =>
        GetPlayableCards(playerId).Count > 0;

    /// <summary>Tổng điểm winners nhận được từ tay người thua.</summary>
    public int GetWinnerScore(string winnerId) =>
        RuleValidator.CalculateWinnerScore(_state, winnerId);
}

// ════════════════════════════════════════════════════════════════
// ENGINE RESULT TYPES
// ════════════════════════════════════════════════════════════════

public abstract class EngineResult
{
    public bool    IsSuccess    { get; protected set; }
    public string? ErrorMessage { get; protected set; }
    public string? ErrorCode    { get; protected set; }
    public string? NextPlayerId { get; protected set; }

    public static EngineResult Ok(string? nextPlayerId = null) =>
        new SimpleResult { IsSuccess = true, NextPlayerId = nextPlayerId };

    public static EngineResult Failure(string msg, string code) =>
        new SimpleResult { IsSuccess = false, ErrorMessage = msg, ErrorCode = code };

    private sealed class SimpleResult : EngineResult { }
}

public sealed class PlayCardResult : EngineResult
{
    public Card?          PlayedCard  { get; private set; }
    public TurnEffects?   Effects     { get; private set; }
    public bool           IsGameOver  { get; private set; }
    public string?        WinnerId    { get; private set; }
    public int            WinnerScore { get; private set; }

    /// <summary>Xếp hạng cuối game — chỉ có khi IsGameOver = true.</summary>
    public List<(PlayerState Player, int Rank, int Score)>? Results { get; private set; }

    public static PlayCardResult Success(Card card, TurnEffects effects, string nextPlayerId) =>
        new()
        {
            IsSuccess    = true,
            PlayedCard   = card,
            Effects      = effects,
            NextPlayerId = nextPlayerId
        };

    public static PlayCardResult Win(
        string winnerId, Card card, int score,
        List<(PlayerState, int, int)> results) =>
        new()
        {
            IsSuccess    = true,
            PlayedCard   = card,
            IsGameOver   = true,
            WinnerId     = winnerId,
            WinnerScore  = score,
            Results      = results,
            NextPlayerId = winnerId
        };

    public static new PlayCardResult Failure(string msg, string code) =>
        new() { IsSuccess = false, ErrorMessage = msg, ErrorCode = code };
}

public sealed class DrawCardResult : EngineResult
{
    public List<Card>  DrawnCards   { get; private set; } = new();
    public bool        CanPlayDrawn { get; private set; }
    public DrawSource  Source       { get; private set; }

    public static DrawCardResult Success(
        List<Card> cards, bool canPlayDrawn, DrawSource source, string nextPlayerId) =>
        new()
        {
            IsSuccess    = true,
            DrawnCards   = cards,
            CanPlayDrawn = canPlayDrawn,
            Source       = source,
            NextPlayerId = nextPlayerId
        };

    public static new DrawCardResult Failure(string msg, string code) =>
        new() { IsSuccess = false, ErrorMessage = msg, ErrorCode = code };
}

public sealed class CallUnoResult : EngineResult
{
    public UnoCallResult Type         { get; private set; }
    public string?       VictimId    { get; private set; }
    public List<Card>?   PenaltyCards { get; private set; }

    public static CallUnoResult SelfCalled() =>
        new() { IsSuccess = true, Type = UnoCallResult.SelfCalled };

    public static CallUnoResult Caught(string victimId, List<Card> cards) =>
        new() { IsSuccess = true, Type = UnoCallResult.Caught, VictimId = victimId, PenaltyCards = cards };

    public static CallUnoResult Invalid(string msg) =>
        new() { IsSuccess = false, Type = UnoCallResult.Invalid, ErrorMessage = msg, ErrorCode = "INVALID_UNO" };
}
