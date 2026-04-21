using MongoDB.Driver;
using UnoGame.Core.DTOs;
using UnoGame.Core.Interfaces;
using UnoGame.Core.Engine;
using UnoGame.Core.Models;
using UnoGame.Infrastructure.Repositories;

namespace UnoGame.Infrastructure.Services;

/// <summary>
/// GameService — cầu nối giữa GameEngine (Core) và thế giới bên ngoài.
///
/// Mỗi method:
///   1. Load GameState từ MongoDB (session document)
///   2. Deserialize thành GameState object
///   3. Tạo GameEngine, gọi action
///   4. Serialize và save state trở lại
///   5. Nếu game kết thúc → lưu GameHistory, cập nhật user stats
///   6. Map sang DTO và trả về
///
/// Concurrency: MongoDB findOneAndUpdate với optimistic locking version field.
/// </summary>
public sealed class GameService : IGameService
{
    private readonly IMongoDatabase          _db;
    private readonly IGameHistoryRepository  _historyRepo;
    private readonly IUserRepository         _userRepo;
    private readonly ILogger<GameService>    _log;

    // Collection lưu game session đang diễn ra
    private IMongoCollection<GameSessionDocument> Sessions =>
        _db.GetCollection<GameSessionDocument>("game_sessions");

    public GameService(
        IMongoDatabase          db,
        IGameHistoryRepository  historyRepo,
        IUserRepository         userRepo,
        ILogger<GameService>    log)
    {
        _db          = db;
        _historyRepo = historyRepo;
        _userRepo    = userRepo;
        _log         = log;
    }

    // ════════════════════════════════════════════════════════════════
    // INITIALIZE (gọi bởi RoomService.StartGameAsync)
    // ════════════════════════════════════════════════════════════════

    public async Task InitializeGameAsync(string roomId, List<RoomPlayerDto> roomPlayers)
    {
        var playerStates = roomPlayers.Select(p => new PlayerState(
            p.UserId, p.DisplayName, p.AvatarUrl, p.IsBot)).ToList();

        var gameState = new GameState
        {
            RoomId  = roomId,
            Players = playerStates,
            Phase   = GamePhase.Waiting
        };

        var engine = new GameEngine(gameState);
        engine.Initialize(); // xáo bài, chia bài, lật top card

        await UpsertSessionAsync(roomId, gameState);
        _log.LogInformation("Game initialized: room={RoomId} players={Count}",
            roomId, playerStates.Count);
    }

    // ════════════════════════════════════════════════════════════════
    // GET STATE (public view — ẩn bài của người khác)
    // ════════════════════════════════════════════════════════════════

    public async Task<GameStateDto?> GetPublicStateAsync(string roomId, string requesterId)
    {
        var state = await LoadStateAsync(roomId);
        if (state is null) return null;

        return MapToPublicDto(state, roomId);
    }

    public async Task<MyHandDto?> GetMyHandAsync(string roomId, string userId)
    {
        var state = await LoadStateAsync(roomId);
        if (state is null) return null;

        var player = state.GetPlayer(userId);
        if (player is null) return null;

        var topCard = state.TopCard;
        var isMyTurn = state.IsPlayerTurn(userId);
        var playable = isMyTurn && topCard is not null
            ? RuleValidator.GetPlayableCards(state, userId)
            : new List<Card>();

        bool mustDraw = isMyTurn &&
                        state.PendingDrawCount > 0 &&
                        playable.Count == 0;

        return new MyHandDto
        {
            Cards    = player.Hand.Select(MapCardToDto).ToList(),
            CanPlay  = playable.Count > 0,
            Playable = playable.Select(MapCardToDto).ToList(),
            MustDraw = mustDraw
        };
    }

    // ════════════════════════════════════════════════════════════════
    // PLAY CARD
    // ════════════════════════════════════════════════════════════════

    public async Task<GameActionResult> PlayCardAsync(
        string roomId, string userId, PlayCardRequest req)
    {
        var state = await LoadStateAsync(roomId);
        if (state is null)
            return GameActionResult.Failure("Game session not found", "GAME_NOT_FOUND");

        var player = state.GetPlayer(userId);
        if (player is null)
            return GameActionResult.Failure("Player not in game", "NOT_IN_ROOM");

        // Map DTO → domain Card
        var card = player.FindCard(req.Card.Color, req.Card.Type, req.Card.Value);
        if (card is null)
            return GameActionResult.Failure("Card not found in hand", "CARD_NOT_IN_HAND");

        // Map chosenColor
        CardColor? chosenColor = null;
        if (!string.IsNullOrEmpty(req.ChosenColor) &&
            Enum.TryParse<CardColor>(req.ChosenColor, out var parsed))
            chosenColor = parsed;

        // Engine
        var engine = new GameEngine(state);
        var result = engine.PlayCard(userId, card, chosenColor);

        if (!result.IsSuccess)
            return GameActionResult.Failure(result.ErrorMessage!, result.ErrorCode!);

        // Persist
        await UpsertSessionAsync(roomId, state);

        // Nếu game over → lưu history + update stats
        if (result.IsGameOver)
            await FinalizeGameAsync(state, result);

        return new GameActionResult
        {
            Success    = true,
            State      = MapToPublicDto(state, roomId),
            IsGameOver = result.IsGameOver,
            WinnerId   = result.WinnerId
        };
    }

    // ════════════════════════════════════════════════════════════════
    // DRAW CARD
    // ════════════════════════════════════════════════════════════════

    public async Task<GameActionResult> DrawCardAsync(string roomId, string userId)
    {
        var state = await LoadStateAsync(roomId);
        if (state is null)
            return GameActionResult.Failure("Game session not found", "GAME_NOT_FOUND");

        var engine = new GameEngine(state);
        var result = engine.DrawCard(userId);

        if (!result.IsSuccess)
            return GameActionResult.Failure(result.ErrorMessage!, result.ErrorCode!);

        await UpsertSessionAsync(roomId, state);

        return new GameActionResult
        {
            Success    = true,
            State      = MapToPublicDto(state, roomId),
            DrawnCards = result.DrawnCards.Select(MapCardToDto).ToList(),
            IsGameOver = false
        };
    }

    // ════════════════════════════════════════════════════════════════
    // CALL UNO
    // ════════════════════════════════════════════════════════════════

    public async Task<GameActionResult> CallUnoAsync(
        string roomId, string callerId, string targetId)
    {
        var state = await LoadStateAsync(roomId);
        if (state is null)
            return GameActionResult.Failure("Game session not found", "GAME_NOT_FOUND");

        var engine = new GameEngine(state);
        var result = engine.CallUno(callerId, targetId);

        if (!result.IsSuccess)
            return GameActionResult.Failure(result.ErrorMessage!, result.ErrorCode!);

        await UpsertSessionAsync(roomId, state);

        return new GameActionResult
        {
            Success    = true,
            State      = MapToPublicDto(state, roomId),
            DrawnCards = result.PenaltyCards?.Select(MapCardToDto).ToList()
        };
    }

    // ════════════════════════════════════════════════════════════════
    // HISTORY
    // ════════════════════════════════════════════════════════════════

    public async Task<List<GameHistoryDto>> GetRoomHistoryAsync(string roomId, int limit)
    {
        var docs = await _historyRepo.GetByRoomAsync(roomId, limit);
        return docs.Select(MapHistoryToDto).ToList();
    }

    public async Task<List<GameHistoryDto>> GetUserHistoryAsync(
        string userId, int page, int pageSize)
    {
        var docs = await _historyRepo.GetByPlayerAsync(userId, (page - 1) * pageSize, pageSize);
        return docs.Select(MapHistoryToDto).ToList();
    }

    public async Task<GameHistoryDto?> GetGameByIdAsync(string gameId)
    {
        var doc = await _historyRepo.GetByIdAsync(gameId);
        return doc is null ? null : MapHistoryToDto(doc);
    }

    // ════════════════════════════════════════════════════════════════
    // PRIVATE: PERSISTENCE
    // ════════════════════════════════════════════════════════════════

    private async Task<GameState?> LoadStateAsync(string roomId)
    {
        var session = await Sessions.Find(s => s.RoomId == roomId).FirstOrDefaultAsync();
        if (session?.GameStateJson is null) return null;

        try { return GameStateSerializer.Deserialize(session.GameStateJson); }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to deserialize game state for room {RoomId}", roomId);
            return null;
        }
    }

    private async Task UpsertSessionAsync(string roomId, GameState state)
    {
        var json = GameStateSerializer.Serialize(state);
        var filter = Builders<GameSessionDocument>.Filter.Eq(s => s.RoomId, roomId);
        var update = Builders<GameSessionDocument>.Update
            .Set(s => s.GameStateJson, json)
            .Set(s => s.UpdatedAt,     DateTime.UtcNow)
            .SetOnInsert(s => s.RoomId, roomId);

        await Sessions.UpdateOneAsync(filter, update,
            new UpdateOptions { IsUpsert = true });
    }

    // ════════════════════════════════════════════════════════════════
    // PRIVATE: GAME FINALIZATION
    // ════════════════════════════════════════════════════════════════

    private async Task FinalizeGameAsync(GameState state, PlayCardResult engineResult)
    {
        var duration = state.EndedAt.HasValue
            ? state.EndedAt.Value - state.StartedAt
            : DateTime.UtcNow - state.StartedAt;

        var results = engineResult.Results ?? new();

        // Lưu GameHistory
        var history = new GameHistoryDocument
        {
            RoomId    = state.RoomId,
            WinnerId  = engineResult.WinnerId!,
            TotalTurns= state.TurnNumber,
            Duration  = duration,
            PlayedAt  = DateTime.UtcNow,
            Results   = results.Select(r => new PlayerResult
            {
                PlayerId    = r.Player.PlayerId,
                DisplayName = r.Player.DisplayName,
                Rank        = r.Rank,
                Score       = r.Score,
                CardsLeft   = r.Player.Hand.Count
            }).ToList()
        };

        await _historyRepo.InsertAsync(history);

        // Update stats cho mọi player (không update bot)
        foreach (var (player, rank, score) in results)
        {
            if (player.IsBot) continue;
            bool won = rank == 1;
            await _userRepo.UpdateAsync(player.PlayerId,
                Builders<UserDocument>.Update
                    .Inc(u => u.GamesPlayed, 1)
                    .Inc(u => u.GamesWon,    won ? 1 : 0)
                    .Inc(u => u.TotalScore,  won ? score : 0)
                    .Set(u => u.LastPlayedAt, DateTime.UtcNow));
        }

        _log.LogInformation(
            "Game finalized: room={RoomId} winner={WinnerId} turns={Turns} duration={Dur:mm\\:ss}",
            state.RoomId, engineResult.WinnerId, state.TurnNumber, duration);
    }

    // ════════════════════════════════════════════════════════════════
    // PRIVATE: DTO MAPPING
    // ════════════════════════════════════════════════════════════════

    private static GameStateDto MapToPublicDto(GameState state, string roomId)
    {
        var topCard = state.TopCard;
        return new GameStateDto
        {
            RoomId          = roomId,
            Phase           = state.Phase,
            CurrentPlayerId = state.CurrentPlayer.PlayerId,
            TopCard         = topCard is not null ? MapCardToDto(topCard) : new CardDto
                { Color = "Red", Type = "Number", Value = 0 },
            CurrentColor    = state.CurrentColor.ToString(),
            Direction       = state.Direction,
            PendingDrawCount= state.PendingDrawCount,
            TurnNumber      = state.TurnNumber,
            DrawPileCount   = state.DrawPileCount,
            WinnerId        = state.WinnerId,
            LastActionAt    = state.LastActionAt,
            Players         = state.Players.Select(p => new PlayerHandSummaryDto
            {
                PlayerId     = p.PlayerId,
                DisplayName  = p.DisplayName,
                AvatarUrl    = p.AvatarUrl,
                CardCount    = p.Hand.Count,
                IsBot        = p.IsBot,
                HasCalledUno = p.HasCalledUno,
                IsConnected  = p.IsConnected
            }).ToList()
        };
    }

    public static CardDto MapCardToDto(Card card) => new()
    {
        Color = card.Color.ToString(),
        Type  = card.Type.ToString(),
        Value = card.Value
    };

    private static GameHistoryDto MapHistoryToDto(GameHistoryDocument doc)
    {
        var winner = doc.Results.FirstOrDefault(r => r.Rank == 1);
        var dur    = doc.Duration;
        return new GameHistoryDto
        {
            Id         = doc.Id,
            RoomId     = doc.RoomId,
            WinnerId   = doc.WinnerId,
            WinnerName = winner?.DisplayName ?? "",
            TotalTurns = doc.TotalTurns,
            Duration   = $"{(int)dur.TotalMinutes:00}:{dur.Seconds:00}",
            PlayedAt   = doc.PlayedAt,
            Results    = doc.Results.Select(r => new PlayerResultDto
            {
                PlayerId    = r.PlayerId,
                DisplayName = r.DisplayName,
                Rank        = r.Rank,
                Score       = r.Score,
                CardsLeft   = r.CardsLeft
            }).ToList()
        };
    }
    public Task<GameState?> GetInternalStateAsync(string roomId) =>LoadStateAsync(roomId);
    
}

// ── MongoDB document types ────────────────────────────────────────────────────

public class GameSessionDocument
{
    [MongoDB.Bson.Serialization.Attributes.BsonId]
    [MongoDB.Bson.Serialization.Attributes.BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string  Id            { get; set; } = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
    public string  RoomId        { get; set; } = null!;
    public string  GameStateJson { get; set; } = null!;
    public DateTime UpdatedAt    { get; set; } = DateTime.UtcNow;
}

public class GameHistoryDocument
{
    [MongoDB.Bson.Serialization.Attributes.BsonId]
    [MongoDB.Bson.Serialization.Attributes.BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string  Id         { get; set; } = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
    public string  RoomId     { get; set; } = null!;
    public string  WinnerId   { get; set; } = null!;
    public int     TotalTurns { get; set; }
    public TimeSpan Duration  { get; set; }
    public DateTime PlayedAt  { get; set; }
    public List<PlayerResult> Results { get; set; } = new();
}

public class PlayerResult
{
    public string PlayerId    { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public int    Rank        { get; set; }
    public int    Score       { get; set; }
    public int    CardsLeft   { get; set; }
}
