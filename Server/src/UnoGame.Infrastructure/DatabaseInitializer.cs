using MongoDB.Driver;
using UnoGame.Infrastructure.Repositories;
using UnoGame.Infrastructure.Services;

namespace UnoGame.Infrastructure;

public static class DatabaseInitializer
{
    /// <summary>
    /// Tạo tất cả indexes khi ứng dụng khởi động.
    /// Idempotent — gọi nhiều lần không ảnh hưởng (MongoDB bỏ qua nếu index đã tồn tại).
    /// </summary>
    public static async Task InitializeAsync(IMongoDatabase db)
    {
        await CreateUserIndexesAsync(db);
        await CreateRoomIndexesAsync(db);
        await CreateSessionIndexesAsync(db);
        await CreateHistoryIndexesAsync(db);
    }

    private static async Task CreateUserIndexesAsync(IMongoDatabase db)
    {
        var col = db.GetCollection<UserDocument>("users");
        var models = new List<CreateIndexModel<UserDocument>>
        {
            new(Builders<UserDocument>.IndexKeys.Ascending(u => u.Email),
                new() { Unique = true, Name = "idx_email_unique" }),

            new(Builders<UserDocument>.IndexKeys.Descending(u => u.TotalScore),
                new() { Name = "idx_total_score_desc" }),

            new(Builders<UserDocument>.IndexKeys
                    .Descending(u => u.LastPlayedAt)
                    .Descending(u => u.WeeklyScore),
                new() { Name = "idx_weekly_leaderboard" }),
        };
        await col.Indexes.CreateManyAsync(models);
    }

    private static async Task CreateRoomIndexesAsync(IMongoDatabase db)
    {
        var col = db.GetCollection<RoomDocument>("rooms");
        var models = new List<CreateIndexModel<RoomDocument>>
        {
            new(Builders<RoomDocument>.IndexKeys.Ascending(r => r.RoomCode),
                new() { Unique = true, Name = "idx_room_code_unique" }),

            new(Builders<RoomDocument>.IndexKeys
                    .Ascending(r => r.Status)
                    .Descending(r => r.CreatedAt),
                new() { Name = "idx_status_created" }),

            new(Builders<RoomDocument>.IndexKeys
                    .Ascending(r => r.Status)
                    .Ascending(r => r.IsPrivate)
                    .Descending(r => r.CreatedAt),
                new() { Name = "idx_public_waiting_rooms" }),

            // TTL 24h
            new(Builders<RoomDocument>.IndexKeys.Ascending(r => r.CreatedAt),
                new() { ExpireAfter = TimeSpan.FromHours(24), Name = "idx_ttl_rooms_24h" }),
        };
        await col.Indexes.CreateManyAsync(models);
    }

    private static async Task CreateSessionIndexesAsync(IMongoDatabase db)
    {
        var col = db.GetCollection<GameSessionDocument>("game_sessions");
        var models = new List<CreateIndexModel<GameSessionDocument>>
        {
            new(Builders<GameSessionDocument>.IndexKeys.Ascending(s => s.RoomId),
                new() { Unique = true, Name = "idx_session_room_unique" }),

            // TTL 48h — dọn session cũ
            new(Builders<GameSessionDocument>.IndexKeys.Ascending(s => s.UpdatedAt),
                new() { ExpireAfter = TimeSpan.FromHours(48), Name = "idx_ttl_sessions_48h" }),
        };
        await col.Indexes.CreateManyAsync(models);
    }

    private static async Task CreateHistoryIndexesAsync(IMongoDatabase db)
    {
        var col = db.GetCollection<GameHistoryDocument>("game_history");
        var models = new List<CreateIndexModel<GameHistoryDocument>>
        {
            new(Builders<GameHistoryDocument>.IndexKeys
                    .Ascending(g => g.RoomId)
                    .Descending(g => g.PlayedAt),
                new() { Name = "idx_history_room_time" }),

            // ElemMatch index cho query "ván nào có player này"
            new(Builders<GameHistoryDocument>.IndexKeys
                    .Ascending("results.playerId")
                    .Descending(g => g.PlayedAt),
                new() { Name = "idx_history_player_time" }),

            new(Builders<GameHistoryDocument>.IndexKeys
                    .Ascending(g => g.WinnerId)
                    .Descending(g => g.PlayedAt),
                new() { Name = "idx_history_winner" }),
        };
        await col.Indexes.CreateManyAsync(models);
    }
}