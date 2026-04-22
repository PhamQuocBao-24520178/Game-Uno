using MongoDB.Driver;
using UnoGame.Core.DTOs;
using UnoGame.Core.Models;
using UnoGame.Infrastructure.Services;

namespace UnoGame.Infrastructure.Repositories;

// ─── UserRepository ──────────────────────────────────────────────────────────

public interface IUserRepository
{
    Task<UserDocument?> GetByIdAsync(string id);
    Task InsertAsync(UserDocument user);
    Task UpdateAsync(string id, UpdateDefinition<UserDocument> update);
    Task<bool> ExistsAsync(string id);
    Task<List<UserDocument>> GetTopByScoreAsync(int skip, int limit);
    Task<List<UserDocument>> GetTopByWeeklyScoreAsync(int skip, int limit, DateTime since);
    Task<long> CountAsync();
    Task<long> CountRankAboveAsync(int score);
}

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<UserDocument> _col;

    public UserRepository(IMongoDatabase db)
    {
        _col = db.GetCollection<UserDocument>("users");
        // Indexes are created once at startup by DatabaseInitializer
    }

    public Task<UserDocument?> GetByIdAsync(string id) =>
        _col.Find(u => u.Id == id).FirstOrDefaultAsync()!;

    public Task InsertAsync(UserDocument user) =>
        _col.InsertOneAsync(user);

    public Task UpdateAsync(string id, UpdateDefinition<UserDocument> update) =>
        _col.UpdateOneAsync(u => u.Id == id, update);

    public async Task<bool> ExistsAsync(string id) =>
        await _col.CountDocumentsAsync(u => u.Id == id) > 0;

    public Task<List<UserDocument>> GetTopByScoreAsync(int skip, int limit) =>
        _col.Find(_ => true)
            .SortByDescending(u => u.TotalScore)
            .Skip(skip).Limit(limit)
            .ToListAsync();

    public Task<List<UserDocument>> GetTopByWeeklyScoreAsync(int skip, int limit, DateTime since) =>
        _col.Find(u => u.LastPlayedAt >= since)
            .SortByDescending(u => u.WeeklyScore)
            .Skip(skip).Limit(limit)
            .ToListAsync();

    public Task<long> CountAsync() => _col.CountDocumentsAsync(_ => true);

    public Task<long> CountRankAboveAsync(int score) =>
        _col.CountDocumentsAsync(u => u.TotalScore > score);
}

// ─── RoomRepository ──────────────────────────────────────────────────────────

public interface IRoomRepository
{
    Task<RoomDocument?> GetByIdAsync(string id);
    Task<RoomDocument?> GetByCodeAsync(string code);
    Task<List<RoomDocument>> GetPublicWaitingAsync(int skip, int limit, string? search);
    Task<long> CountPublicWaitingAsync(string? search);
    Task InsertAsync(RoomDocument room);
    Task UpdateAsync(string id, UpdateDefinition<RoomDocument> update);
    Task DeleteAsync(string id);
}

public class RoomRepository : IRoomRepository
{
    private readonly IMongoCollection<RoomDocument> _col;

    public RoomRepository(IMongoDatabase db)
    {
        _col = db.GetCollection<RoomDocument>("rooms");
        // Indexes are created once at startup by DatabaseInitializer
    }

    public Task<RoomDocument?> GetByIdAsync(string id) =>
        _col.Find(r => r.Id == id).FirstOrDefaultAsync()!;

    public Task<RoomDocument?> GetByCodeAsync(string code) =>
        _col.Find(r => r.RoomCode == code).FirstOrDefaultAsync()!;

    public Task<List<RoomDocument>> GetPublicWaitingAsync(int skip, int limit, string? search)
    {
        var filter = Builders<RoomDocument>.Filter.And(
            Builders<RoomDocument>.Filter.Eq(r => r.Status, RoomStatus.Waiting),
            Builders<RoomDocument>.Filter.Eq(r => r.IsPrivate, false));

        if (!string.IsNullOrEmpty(search))
        {
            var searchFilter = Builders<RoomDocument>.Filter.Or(
                Builders<RoomDocument>.Filter.Regex(r => r.RoomName,
                    new MongoDB.Bson.BsonRegularExpression(search, "i")),
                Builders<RoomDocument>.Filter.Regex(r => r.RoomCode,
                    new MongoDB.Bson.BsonRegularExpression(search, "i")));
            filter = Builders<RoomDocument>.Filter.And(filter, searchFilter);
        }

        return _col.Find(filter)
                   .SortByDescending(r => r.CreatedAt)
                   .Skip(skip).Limit(limit)
                   .ToListAsync();
    }

    public Task<long> CountPublicWaitingAsync(string? search)
    {
        var filter = Builders<RoomDocument>.Filter.And(
            Builders<RoomDocument>.Filter.Eq(r => r.Status, RoomStatus.Waiting),
            Builders<RoomDocument>.Filter.Eq(r => r.IsPrivate, false));
        return _col.CountDocumentsAsync(filter);
    }

    public Task InsertAsync(RoomDocument room) => _col.InsertOneAsync(room);

    public Task UpdateAsync(string id, UpdateDefinition<RoomDocument> update) =>
        _col.UpdateOneAsync(r => r.Id == id, update);

    public Task DeleteAsync(string id) => _col.DeleteOneAsync(r => r.Id == id);
}

// ─── GameHistoryRepository ───────────────────────────────────────────────────

public interface IGameHistoryRepository
{
    Task<GameHistoryDocument?> GetByIdAsync(string id);
    Task<List<GameHistoryDocument>> GetByRoomAsync(string roomId, int limit);
    Task<List<GameHistoryDocument>> GetByPlayerAsync(string userId, int skip, int limit);
    Task InsertAsync(GameHistoryDocument history);
}

public class GameHistoryRepository : IGameHistoryRepository
{
    private readonly IMongoCollection<GameHistoryDocument> _col;

    public GameHistoryRepository(IMongoDatabase db)
    {
        _col = db.GetCollection<GameHistoryDocument>("game_history");
        // Indexes are created once at startup by DatabaseInitializer
    }

    public Task<GameHistoryDocument?> GetByIdAsync(string id) =>
        _col.Find(g => g.Id == id).FirstOrDefaultAsync()!;

    public Task<List<GameHistoryDocument>> GetByRoomAsync(string roomId, int limit) =>
        _col.Find(g => g.RoomId == roomId)
            .SortByDescending(g => g.PlayedAt)
            .Limit(limit)
            .ToListAsync();

    public Task<List<GameHistoryDocument>> GetByPlayerAsync(string userId, int skip, int limit) =>
        _col.Find(Builders<GameHistoryDocument>.Filter.ElemMatch(
                g => g.Results,
                r => r.PlayerId == userId))
            .SortByDescending(g => g.PlayedAt)
            .Skip(skip).Limit(limit)
            .ToListAsync();

    public Task InsertAsync(GameHistoryDocument history) =>
        _col.InsertOneAsync(history);
}
