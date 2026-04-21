namespace UnoGame.Core.Engine;

using UnoGame.Core.Models;

/// <summary>
/// Tất cả luật UNO — pure static functions, không có side-effect.
/// GameEngine dùng class này để validate trước khi thực thi action.
/// </summary>
public static class RuleValidator
{
    // ════════════════════════════════════════════════════════════════
    // TURN VALIDATION
    // ════════════════════════════════════════════════════════════════

    /// <summary>Kiểm tra có phải lượt của player không.</summary>
    public static ValidationResult ValidateTurn(GameState state, string playerId)
    {
        if (state.Phase != GamePhase.Playing)
            return ValidationResult.Fail("Game is not active", "GAME_NOT_ACTIVE");

        if (state.CurrentPlayer.PlayerId != playerId)
            return ValidationResult.Fail("It is not your turn", "NOT_YOUR_TURN");

        return ValidationResult.Ok;
    }

    // ════════════════════════════════════════════════════════════════
    // PLAY CARD VALIDATION
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Kiểm tra player có thể đánh lá card không.
    ///
    /// Rules:
    ///   1. Phải là lượt của player
    ///   2. Lá phải có trong tay
    ///   3. Nếu đang bị stack (+2/+4): chỉ được counter-stack
    ///   4. Lá phải hợp lệ với top card theo luật UNO
    ///   5. Wild/WildDrawFour: cần chosenColor
    ///   6. WildDrawFour: theo luật chính thức chỉ đánh khi không có bài cùng màu
    ///      (enforcement tùy config — mặc định bỏ qua vì khó verify server-side)
    /// </summary>
    public static ValidationResult ValidatePlayCard(
        GameState  state,
        string     playerId,
        Card       card,
        CardColor? chosenColor)
    {
        var turnCheck = ValidateTurn(state, playerId);
        if (!turnCheck.IsValid) return turnCheck;

        var player = state.CurrentPlayer;

        // Lá phải có trong tay
        if (!player.Hand.Contains(card))
            return ValidationResult.Fail("Card is not in your hand", "CARD_NOT_IN_HAND");

        var topCard = state.TopCard;
        if (topCard is null)
            return ValidationResult.Fail("No top card on discard pile", "GAME_ERROR");

        // Wild cần chosenColor
        if (card.IsWild && chosenColor is null)
            return ValidationResult.Fail(
                "Must choose a color when playing Wild or Wild Draw Four", "MISSING_COLOR");

        if (card.IsWild && chosenColor == CardColor.Wild)
            return ValidationResult.Fail(
                "Chosen color cannot be Wild", "INVALID_COLOR");

        // Check CanPlayOn (bao gồm stack logic)
        if (!card.CanPlayOn(topCard, state.CurrentColor, state.PendingStackType))
        {
            if (state.PendingDrawCount > 0)
                return ValidationResult.Fail(
                    $"You must draw {state.PendingDrawCount} cards or stack a matching card",
                    "MUST_STACK_OR_DRAW");

            return ValidationResult.Fail(
                $"Cannot play {card} on {topCard} with current color {state.CurrentColor}",
                "INVALID_CARD");
        }

        return ValidationResult.Ok;
    }

    // ════════════════════════════════════════════════════════════════
    // DRAW CARD VALIDATION
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Kiểm tra player có thể rút bài không.
    ///
    /// Rules:
    ///   - Phải là lượt của player
    ///   - Nếu player đã rút trong lượt này và lá rút không thể đánh → pass turn
    ///   - Không được rút thêm nếu đã có lá đánh được (enforce khi CanPlay = true)
    /// </summary>
    public static ValidationResult ValidateDrawCard(GameState state, string playerId)
    {
        var turnCheck = ValidateTurn(state, playerId);
        if (!turnCheck.IsValid) return turnCheck;

        return ValidationResult.Ok;
    }

    // ════════════════════════════════════════════════════════════════
    // UNO CALL VALIDATION
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Kiểm tra lần gọi UNO có hợp lệ không.
    ///
    /// Có 2 trường hợp:
    ///   A. Tự gọi (callerId == targetId):
    ///      - Target phải có đúng 1 lá trên tay
    ///      - Target chưa gọi UNO
    ///      - Game đang diễn ra
    ///
    ///   B. Bắt người khác quên gọi (callerId != targetId):
    ///      - Target phải có đúng 1 lá
    ///      - Target chưa gọi UNO
    ///      - Phải trong cửa sổ thời gian hợp lệ (trước khi người tiếp theo đánh)
    ///      - Target vừa mới đánh lá áp cuối
    /// </summary>
    public static UnoCallValidation ValidateUnoCall(
        GameState state,
        string    callerId,
        string    targetId)
    {
        if (state.Phase != GamePhase.Playing)
            return new UnoCallValidation(UnoCallResult.Invalid, "Game is not active");

        var target = state.GetPlayer(targetId);
        if (target is null)
            return new UnoCallValidation(UnoCallResult.Invalid, "Target player not found");

        bool isSelfCall = callerId == targetId;

        // Target phải có đúng 1 lá
        if (target.Hand.Count != 1)
            return new UnoCallValidation(UnoCallResult.Invalid,
                target.Hand.Count == 0
                    ? "Target has already won"
                    : $"Target has {target.Hand.Count} cards, not 1");

        // Target đã gọi UNO rồi → không bắt được, không cần gọi lại
        if (target.HasCalledUno)
            return new UnoCallValidation(UnoCallResult.Invalid,
                isSelfCall ? "You already called UNO" : "Target already called UNO");

        // Tự gọi UNO
        if (isSelfCall)
            return new UnoCallValidation(UnoCallResult.SelfCalled, null);

        // Bắt người khác — kiểm tra thời gian cửa sổ
        // Target phải vừa mới đánh xuống còn 1 lá (có LastPlayedDownToOneAt)
        if (target.LastPlayedDownToOneAt is null)
            return new UnoCallValidation(UnoCallResult.Invalid,
                "Target did not recently play down to one card");

        var elapsed = DateTime.UtcNow - target.LastPlayedDownToOneAt.Value;
        if (elapsed > TimeSpan.FromSeconds(5))
            return new UnoCallValidation(UnoCallResult.Invalid,
                "UNO catch window has expired (5 seconds)");

        return new UnoCallValidation(UnoCallResult.Caught, null);
    }

    // ════════════════════════════════════════════════════════════════
    // PLAYABLE CARD COMPUTATION
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lấy danh sách tất cả bài có thể đánh trong tay của player.
    /// Dùng để pre-compute cho MyHandDto và highlight UI.
    /// </summary>
    public static List<Card> GetPlayableCards(GameState state, string playerId)
    {
        var player = state.GetPlayer(playerId);
        if (player is null || !state.IsPlayerTurn(playerId)) return new();

        var topCard = state.TopCard;
        if (topCard is null) return new();

        return player.Hand
            .Where(c => c.CanPlayOn(topCard, state.CurrentColor, state.PendingStackType))
            .ToList();
    }

    // ════════════════════════════════════════════════════════════════
    // WIN CONDITION
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Kiểm tra player có thắng không (tay rỗng sau khi đánh bài).
    /// </summary>
    public static bool IsWinner(PlayerState player) => player.Hand.Count == 0;

    // ════════════════════════════════════════════════════════════════
    // SCORING
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tính điểm cho winner = tổng điểm của tất cả tay còn lại.
    /// Number = face value, Skip/Reverse/DrawTwo = 20, Wild/W+4 = 50.
    /// </summary>
    public static int CalculateWinnerScore(GameState state, string winnerId)
    {
        return state.Players
            .Where(p => p.PlayerId != winnerId)
            .Sum(p => p.HandScore);
    }

    /// <summary>
    /// Xếp hạng players theo số lá còn lại (ít nhất = rank cao nhất).
    /// Người thắng luôn là rank 1.
    /// </summary>
    public static List<(PlayerState Player, int Rank, int Score)> CalculateResults(
        GameState state, string winnerId)
    {
        var results = new List<(PlayerState, int, int)>();
        int rank = 1;

        // Winner luôn rank 1
        var winner = state.GetPlayer(winnerId)!;
        results.Add((winner, 1, CalculateWinnerScore(state, winnerId)));

        // Others ranked by hand size (ascending)
        var losers = state.Players
            .Where(p => p.PlayerId != winnerId)
            .OrderBy(p => p.Hand.Count)
            .ThenBy(p => p.HandScore)
            .ToList();

        foreach (var loser in losers)
            results.Add((loser, ++rank, loser.HandScore));

        return results;
    }
}

// ════════════════════════════════════════════════════════════════
// RESULT TYPES
// ════════════════════════════════════════════════════════════════

public readonly struct ValidationResult
{
    public bool   IsValid  { get; }
    public string? Error   { get; }
    public string? Code    { get; }

    private ValidationResult(bool valid, string? error, string? code)
    {
        IsValid = valid;
        Error   = error;
        Code    = code;
    }

    public static ValidationResult Ok => new(true, null, null);

    public static ValidationResult Fail(string error, string code) =>
        new(false, error, code);
}

public readonly struct UnoCallValidation
{
    public UnoCallResult Result  { get; }
    public string?       Message { get; }

    public UnoCallValidation(UnoCallResult result, string? message)
    {
        Result  = result;
        Message = message;
    }

    public bool IsValid => Result != UnoCallResult.Invalid;
}
