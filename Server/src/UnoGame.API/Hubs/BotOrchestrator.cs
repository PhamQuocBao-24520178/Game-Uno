using Microsoft.AspNetCore.SignalR;
using UnoGame.API.Services;
using UnoGame.Core.Bot;
using UnoGame.Core.Engine;
using UnoGame.Core.Models;

namespace UnoGame.API.Hubs;

public interface IBotOrchestrator
{
    void TriggerIfBotTurn(string roomId);
}

/// <summary>
/// BotOrchestrator — thực thi quyết định của UnoBot.
///
/// Flow mỗi lượt bot:
///   1. Load real GameState từ DB (qua IGameService.GetInternalStateAsync)
///   2. Tạo UnoBot → bot.Decide()
///   3. Nếu CatchUno → CallUnoAsync trước
///   4. BotThinking delay (600–1600ms)
///   5. Thực thi MainAction (PlayCard / DrawCard)
///   6. Nếu DrawCard trả về canPlayDrawn → bot.DecideAfterDraw()
///   7. Broadcast events qua SignalR
///   8. Loop nếu turn tiếp theo vẫn là bot (max 10 vòng)
/// </summary>
public sealed class BotOrchestrator : IBotOrchestrator
{
    private readonly IServiceProvider         _services;
    private readonly IHubContext<GameHub>     _hub;
    private readonly IConnectionManager       _connections;
    private readonly ILogger<BotOrchestrator> _log;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _running = new();

    public BotOrchestrator(
        IServiceProvider         services,
        IHubContext<GameHub>     hub,
        IConnectionManager       connections,
        ILogger<BotOrchestrator> log)
    {
        _services    = services;
        _hub         = hub;
        _connections = connections;
        _log         = log;
    }

    public void TriggerIfBotTurn(string roomId) =>
        _ = Task.Run(() => RunBotLoopAsync(roomId));

    // ════════════════════════════════════════════════════════════════
    // MAIN LOOP
    // ════════════════════════════════════════════════════════════════

    private async Task RunBotLoopAsync(string roomId)
    {
        if (!_running.TryAdd(roomId, 0)) return;

        try
        {
            const int maxBotTurns = 10;
            for (int turn = 0; turn < maxBotTurns; turn++)
            {
                using var scope       = _services.CreateScope();
                var gameService       = scope.ServiceProvider.GetRequiredService<IGameService>();

                // ── 1. Load real GameState ────────────────────────────────
                var state = await gameService.GetInternalStateAsync(roomId);
                if (state is null || state.Phase != GamePhase.Playing) break;

                var current = state.CurrentPlayer;
                if (!current.IsBot) break;

                var botId = current.PlayerId;

                // ── 2. Tạo bot và phân tích ───────────────────────────────
                var bot      = new UnoBot(state, botId);
                var decision = bot.Decide();

                // ── 3. Catch UNO ngay (không cần delay) ──────────────────
                if (decision.Action == BotActionType.CatchUno && decision.UnoTargetId is not null)
                {
                    var unoResult = await gameService.CallUnoAsync(
                        roomId, botId, decision.UnoTargetId);

                    if (unoResult.Success && (unoResult.DrawnCards?.Count ?? 0) > 0)
                    {
                        var victim  = state.GetPlayer(decision.UnoTargetId);
                        await _hub.Clients.Group(roomId).SendAsync(
                            HubEvents.UnoCaught,
                            new UnoCaughtPayload(
                                decision.UnoTargetId,
                                victim?.DisplayName ?? "Player",
                                unoResult.DrawnCards!.Count,
                                botId,
                                DateTime.UtcNow));
                    }

                    // Reload state sau catch, tiếp tục vòng lặp để đánh bài
                    state    = await gameService.GetInternalStateAsync(roomId);
                    if (state is null || state.Phase != GamePhase.Playing) break;
                    bot      = new UnoBot(state, botId);
                    decision = bot.Decide();
                }

                // ── 4. BotThinking delay ──────────────────────────────────
                int thinkMs = ComputeThinkTime(state, decision);
                await _hub.Clients.Group(roomId).SendAsync(
                    HubEvents.BotThinking,
                    new BotThinkingPayload(botId, current.DisplayName, thinkMs));
                await Task.Delay(thinkMs);

                // ── 5. Self UNO call broadcast (trước khi đánh lá cuối) ──
                if (decision.ShouldSelfCallUno)
                {
                    await gameService.CallUnoAsync(roomId, botId, botId);
                    await _hub.Clients.Group(roomId).SendAsync(
                        HubEvents.UnoCalled,
                        new UnoCalledPayload(
                            botId, current.DisplayName,
                            botId, current.DisplayName,
                            IsSelfCall: true, Timestamp: DateTime.UtcNow));
                }

                // ── 6. Execute main action ────────────────────────────────
                GameActionResult result;
                bool drewCard = false;

                switch (decision.Action)
                {
                    case BotActionType.PlayCard when decision.CardToPlay is not null:
                        result = await gameService.PlayCardAsync(roomId, botId,
                            new PlayCardRequest
                            {
                                Card = MapCardToDto(decision.CardToPlay),
                                ChosenColor = decision.ChosenColor?.ToString()
                            });
                        break;

                    case BotActionType.DrawCard:
                    default:
                        result    = await gameService.DrawCardAsync(roomId, botId);
                        drewCard  = true;
                        break;
                }

                if (!result.Success)
                {
                    _log.LogWarning(
                        "Bot {Id} action failed in room {Room}: {Err} (decision={Dec})",
                        botId, roomId, result.Error, decision.Action);
                    break;
                }

                // ── 7. After draw: decide whether to play drawn card ──────
                if (drewCard && result.DrawnCards?.Count == 1 &&
                    result.State?.CurrentPlayerId == botId) // still my turn
                {
                    // Reload state để có drawn card trong hand
                    var freshState = await gameService.GetInternalStateAsync(roomId);
                    if (freshState is not null && freshState.Phase == GamePhase.Playing)
                    {
                        var drawnDomainCard = freshState.GetPlayer(botId)?.Hand.LastOrDefault();
                        if (drawnDomainCard is not null)
                        {
                            var freshBot     = new UnoBot(freshState, botId);
                            var afterDecision = freshBot.DecideAfterDraw(drawnDomainCard);

                            if (afterDecision.Action == BotActionType.PlayCard &&
                                afterDecision.CardToPlay is not null)
                            {
                                await Task.Delay(400); // brief pause
                                result = await gameService.PlayCardAsync(roomId, botId,
                                    new PlayCardRequest
                                    {
                                        Card = MapCardToDto(afterDecision.CardToPlay),
                                        ChosenColor = afterDecision.ChosenColor?.ToString()
                                    });
                                drewCard = false;
                            }
                        }
                    }
                }

                // ── 8. Broadcast ──────────────────────────────────────────
                var newState = result.State;
                if (newState is null) break;

                await BroadcastActionAsync(roomId, botId, current.DisplayName,
                    decision, result, drewCard);

                // ── 9. Game over? ─────────────────────────────────────────
                if (result.IsGameOver)
                {
                    await BroadcastGameOverAsync(roomId, result, gameService);
                    break;
                }

                // ── 10. TurnChanged ───────────────────────────────────────
                var nextPlayer = newState.Players
                    .FirstOrDefault(p => p.PlayerId == newState.CurrentPlayerId);

                await _hub.Clients.Group(roomId).SendAsync(
                    HubEvents.TurnChanged,
                    new TurnChangedPayload(
                        CurrentPlayerId   : newState.CurrentPlayerId,
                        CurrentPlayerName : nextPlayer?.DisplayName ?? "",
                        IsBot             : nextPlayer?.IsBot ?? false,
                        TurnNumber        : newState.TurnNumber,
                        PendingDrawCount  : newState.PendingDrawCount,
                        TimeoutSeconds    : 30,
                        TurnStartedAt     : DateTime.UtcNow));

                // ── 11. Loop nếu turn tiếp vẫn là bot ────────────────────
                if (nextPlayer is null || !nextPlayer.IsBot) break;
                await Task.Delay(200);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "BotOrchestrator crashed for room {RoomId}", roomId);
        }
        finally
        {
            _running.TryRemove(roomId, out _);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // BROADCAST HELPERS
    // ════════════════════════════════════════════════════════════════

    private async Task BroadcastActionAsync(
        string          roomId,
        string          botId,
        string          botName,
        BotDecision     decision,
        GameActionResult result,
        bool            drewCard)
    {
        var state      = result.State!;
        var myStats    = state.Players.FirstOrDefault(p => p.PlayerId == botId);
        var nextPlayer = state.Players.FirstOrDefault(p => p.PlayerId == state.CurrentPlayerId);

        if (!drewCard)
        {
            await _hub.Clients.Group(roomId).SendAsync(
                HubEvents.CardPlayed,
                new CardPlayedPayload(
                    PlayerId        : botId,
                    PlayerName      : botName,
                    Card            : state.TopCard != null
                        ? MapCardToApiDto(state.TopCard) : new CardDto { Color = "Red", Type = "Number", Value = 0 },
                    CurrentColor    : state.CurrentColor,
                    RemainingCards  : myStats?.CardCount ?? 0,
                    HasCalledUno    : myStats?.HasCalledUno ?? false,
                    NextPlayerId    : state.CurrentPlayerId,
                    NextPlayerName  : nextPlayer?.DisplayName ?? "",
                    PendingDrawCount: state.PendingDrawCount,
                    IsGameOver      : result.IsGameOver,
                    Timestamp       : DateTime.UtcNow));
        }
        else
        {
            await _hub.Clients.Group(roomId).SendAsync(
                HubEvents.PlayerDrewCards,
                new PlayerDrewCardsPayload(
                    PlayerId    : botId,
                    PlayerName  : botName,
                    CardCount   : result.DrawnCards?.Count ?? 1,
                    WasPenalty  : (result.DrawnCards?.Count ?? 0) > 1,
                    NextPlayerId: state.CurrentPlayerId,
                    Timestamp   : DateTime.UtcNow));
        }

        // UNO self-call broadcast
        if (myStats?.HasCalledUno == true && myStats.CardCount == 1)
        {
            await _hub.Clients.Group(roomId).SendAsync(
                HubEvents.UnoCalled,
                new UnoCalledPayload(
                    botId, botName, botId, botName,
                    IsSelfCall: true, Timestamp: DateTime.UtcNow));
        }
    }

    private async Task BroadcastGameOverAsync(
        string roomId, GameActionResult result, IGameService gameService)
    {
        var history = await gameService.GetRoomHistoryAsync(roomId, 1);
        var latest  = history.FirstOrDefault();
        if (latest is null) return;

        await _hub.Clients.Group(roomId).SendAsync(
            HubEvents.GameOver,
            new GameOverPayload(
                WinnerId  : latest.WinnerId,
                WinnerName: latest.WinnerName,
                Results   : latest.Results,
                TotalTurns: latest.TotalTurns,
                Duration  : latest.Duration,
                EndedAt   : DateTime.UtcNow));

        await _hub.Clients.Group(roomId).SendAsync(
            HubEvents.ChatMessage,
            new ChatMessagePayload(
                SenderId  : "__system__",
                SenderName: "UNO",
                Message   : $"🏆 {latest.WinnerName} won the game!",
                IsSystem  : true,
                Timestamp : DateTime.UtcNow));
    }

    // ════════════════════════════════════════════════════════════════
    // THINK TIME
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tính thời gian "suy nghĩ" thực tế của bot.
    /// Action phức tạp hơn → delay lâu hơn → trải nghiệm tự nhiên hơn.
    /// </summary>
    private static int ComputeThinkTime(GameState state, BotDecision decision)
    {
        int baseMs = decision.Action switch
        {
            BotActionType.CatchUno => 200,  // phản xạ nhanh
            BotActionType.DrawCard => 800,  // "cân nhắc" trước khi rút
            BotActionType.PlayCard => decision.CardToPlay?.IsWild == true
                ? 1200  // chọn màu cần nghĩ lâu hơn
                : 700,
            _ => 600
        };

        // Thêm jitter để tự nhiên hơn (±200ms)
        int jitter = Random.Shared.Next(-200, 201);

        // Nếu đang gần thắng → "suy nghĩ" ít hơn (hứng khởi)
        int myCards = state.CurrentPlayer.Hand.Count;
        if (myCards <= 2) baseMs = Math.Max(400, baseMs - 300);

        return Math.Max(300, baseMs + jitter);
    }

    // ════════════════════════════════════════════════════════════════
    // MAPPING
    // ════════════════════════════════════════════════════════════════

    private static CardDto MapCardToDto(Card card) => new()
    {
        Color = card.Color.ToString(),
        Type  = card.Type.ToString(),
        Value = card.Value
    };

    // Mapping cho GameStateDto (TopCard là CardDto từ DTO layer)
    private static CardDto MapCardToApiDto(object card)
    {
        // GameStateDto.TopCard là CardDto — return as-is
        if (card is CardDto dto) return dto;
        if (card is Card c) return MapCardToDto(c);
        return new CardDto { Color = "Red", Type = "Number", Value = 0 };
    }
}
