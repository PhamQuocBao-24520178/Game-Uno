using MongoDB.Driver;
using UnoGame.Core.DTOs;
using UnoGame.Core.Interfaces;
using UnoGame.Core.Models;
using UnoGame.Infrastructure.Repositories;

namespace UnoGame.Infrastructure.Services;

internal static class UserDtoMapper
{
    internal static UserDto ToDto(UserDocument doc) => new()
    {
        Id          = doc.Id,
        DisplayName = doc.DisplayName,
        AvatarUrl   = doc.AvatarUrl ?? "",
        Stats = new UserStatsDto
        {
            GamesPlayed = doc.GamesPlayed,
            GamesWon    = doc.GamesWon,
            WinRate     = doc.GamesPlayed == 0 ? 0 : Math.Round((double)doc.GamesWon / doc.GamesPlayed * 100, 1),
            TotalScore  = doc.TotalScore
        },
        CreatedAt = doc.CreatedAt
    };
}

// ════════════════════════════════════════════════════════════════════════════
// SERVICE STUBS
// Các service này cần implement đầy đủ business logic.
// Skeleton dưới đây chỉ để project compile được.
// ════════════════════════════════════════════════════════════════════════════

public class UserService : IUserService
{
    private readonly IUserRepository _repo;
    private readonly ILogger<UserService> _log;

    public UserService(IUserRepository repo, ILogger<UserService> log)
    {
        _repo = repo;
        _log  = log;
    }

    public async Task<UserDto?> GetByIdAsync(string userId)
    {
        var doc = await _repo.GetByIdAsync(userId);
        return doc is null ? null : UserDtoMapper.ToDto(doc);
    }

    public async Task<UserDto> RegisterAsync(string firebaseUid, string email, RegisterUserRequest req)
    {
        var doc = new UserDocument
        {
            Id          = firebaseUid,
            Email       = email,
            DisplayName = req.DisplayName,
            AvatarUrl   = req.AvatarUrl ?? "",
            CreatedAt   = DateTime.UtcNow
        };
        await _repo.InsertAsync(doc);
        _log.LogInformation("User registered: {Id}", firebaseUid);
        return UserDtoMapper.ToDto(doc);
    }

    public async Task<UserDto> UpdateProfileAsync(string userId, UpdateProfileRequest req)
    {
        var updates = new List<UpdateDefinition<UserDocument>>();
        if (req.DisplayName is not null)
            updates.Add(Builders<UserDocument>.Update.Set(u => u.DisplayName, req.DisplayName));
        if (req.AvatarUrl is not null)
            updates.Add(Builders<UserDocument>.Update.Set(u => u.AvatarUrl, req.AvatarUrl));

        await _repo.UpdateAsync(userId, Builders<UserDocument>.Update.Combine(updates));
        return (await GetByIdAsync(userId))!;
    }

    public Task<bool> ExistsAsync(string userId) => _repo.ExistsAsync(userId);

    public async Task<UserStatsDto> GetStatsAsync(string userId)
    {
        var doc = await _repo.GetByIdAsync(userId);
        if (doc is null) return new UserStatsDto();
        return new UserStatsDto
        {
            GamesPlayed = doc.GamesPlayed,
            GamesWon    = doc.GamesWon,
            WinRate     = doc.GamesPlayed == 0 ? 0 : Math.Round((double)doc.GamesWon / doc.GamesPlayed * 100, 1),
            TotalScore  = doc.TotalScore
        };
    }

    public Task IncrementStatsAsync(string userId, bool won, int score) =>
        _repo.UpdateAsync(userId,
            Builders<UserDocument>.Update
                .Inc(u => u.GamesPlayed, 1)
                .Inc(u => u.GamesWon,    won ? 1 : 0)
                .Inc(u => u.TotalScore,  won ? score : 0)
                .Set(u => u.LastPlayedAt, DateTime.UtcNow));
}

// ─────────────────────────────────────────────────────────────────────────────

public class RoomService : IRoomService
{
    private readonly IRoomRepository _repo;
    private readonly IUserRepository _userRepo;

    public RoomService(IRoomRepository repo, IUserRepository userRepo)
    {
        _repo    = repo;
        _userRepo = userRepo;
    }

    public async Task<PagedResult<RoomSummaryDto>> ListPublicRoomsAsync(int page, int pageSize, string? search)
    {
        int skip = (page - 1) * pageSize;
        var rooms = await _repo.GetPublicWaitingAsync(skip, pageSize, search);
        long total = await _repo.CountPublicWaitingAsync(search);

        var items = rooms.Select(r => new RoomSummaryDto
        {
            Id          = r.Id,
            RoomCode    = r.RoomCode,
            RoomName    = r.RoomName,
            HostName    = r.HostName ?? "",
            Status      = r.Status,
            PlayerCount = r.PlayerIds.Count,
            MaxPlayers  = r.MaxPlayers,
            HasPassword = !string.IsNullOrEmpty(r.Password),
            CreatedAt   = r.CreatedAt
        });

        return PagedResult<RoomSummaryDto>.Create(items, (int)total, page, pageSize);
    }

    public async Task<RoomDto?> GetByIdAsync(string roomId)
    {
        var doc = await _repo.GetByIdAsync(roomId);
        return doc is null ? null : await MapToDtoAsync(doc);
    }

    public async Task<RoomDto?> GetByCodeAsync(string code)
    {
        var doc = await _repo.GetByCodeAsync(code);
        return doc is null ? null : await MapToDtoAsync(doc);
    }

    public async Task<RoomDto> CreateAsync(string hostId, CreateRoomRequest req)
    {
        var host = await _userRepo.GetByIdAsync(hostId);
        var code = GenerateRoomCode();

        var doc = new RoomDocument
        {
            HostId        = hostId,
            HostName      = host?.DisplayName ?? "Host",
            RoomCode      = code,
            RoomName      = req.RoomName,
            MaxPlayers    = req.MaxPlayers,
            BotCount      = req.BotCount,
            BotDifficulty = req.BotDifficulty,
            IsPrivate     = req.IsPrivate,
            Password      = req.Password,
            MaxRounds     = req.MaxRounds,
            PlayerIds     = new List<string> { hostId },
            Status        = RoomStatus.Waiting,
            CreatedAt     = DateTime.UtcNow
        };

        await _repo.InsertAsync(doc);
        return (await GetByIdAsync(doc.Id))!;
    }

    public async Task<RoomDto> JoinAsync(string roomId, string userId, string? password)
    {
        var room = await _repo.GetByIdAsync(roomId)
            ?? throw new KeyNotFoundException("Room not found");

        if (!string.IsNullOrEmpty(room.Password) && room.Password != password)
            throw new InvalidOperationException("Incorrect password");

        if (!room.PlayerIds.Contains(userId))
        {
            await _repo.UpdateAsync(roomId,
                Builders<RoomDocument>.Update.AddToSet(r => r.PlayerIds, userId));
        }

        return (await GetByIdAsync(roomId))!;
    }

    public async Task LeaveAsync(string roomId, string userId)
    {
        var room = await _repo.GetByIdAsync(roomId);
        if (room is null) return;

        await _repo.UpdateAsync(roomId,
            Builders<RoomDocument>.Update.Pull(r => r.PlayerIds, userId));

        // Transfer host if needed
        var updated = await _repo.GetByIdAsync(roomId);
        if (updated is not null && updated.HostId == userId && updated.PlayerIds.Count > 0)
        {
            await _repo.UpdateAsync(roomId,
                Builders<RoomDocument>.Update.Set(r => r.HostId, updated.PlayerIds[0]));
        }

        // Auto-close if empty
        if (updated?.PlayerIds.Count == 0)
            await _repo.DeleteAsync(roomId);
    }

    public async Task KickPlayerAsync(string roomId, string hostId, string targetUserId) =>
        await _repo.UpdateAsync(roomId,
            Builders<RoomDocument>.Update.Pull(r => r.PlayerIds, targetUserId));

    public async Task<RoomDto> StartGameAsync(string roomId, string hostId)
    {
        await _repo.UpdateAsync(roomId,
            Builders<RoomDocument>.Update.Set(r => r.Status, RoomStatus.Playing));
        return (await GetByIdAsync(roomId))!;
    }

    public async Task CloseRoomAsync(string roomId, string hostId)
    {
        await _repo.UpdateAsync(roomId,
            Builders<RoomDocument>.Update.Set(r => r.Status, RoomStatus.Closed));
    }

    public async Task<RoomDto> UpdateSettingsAsync(string roomId, string hostId, UpdateRoomSettingsRequest req)
    {
        var updates = new List<UpdateDefinition<RoomDocument>>();
        if (req.MaxPlayers.HasValue) updates.Add(Builders<RoomDocument>.Update.Set(r => r.MaxPlayers, req.MaxPlayers.Value));
        if (req.BotCount.HasValue)   updates.Add(Builders<RoomDocument>.Update.Set(r => r.BotCount, req.BotCount.Value));
        if (req.BotDifficulty != null) updates.Add(Builders<RoomDocument>.Update.Set(r => r.BotDifficulty, req.BotDifficulty));
        if (req.IsPrivate.HasValue)  updates.Add(Builders<RoomDocument>.Update.Set(r => r.IsPrivate, req.IsPrivate.Value));

        if (updates.Count > 0)
            await _repo.UpdateAsync(roomId, Builders<RoomDocument>.Update.Combine(updates));

        return (await GetByIdAsync(roomId))!;
    }

    public async Task<List<RoomPlayerDto>> GetPlayersAsync(string roomId)
    {
        var room = await _repo.GetByIdAsync(roomId);
        if (room is null) return new();
        return await BuildPlayerDtos(room);
    }

    public async Task MarkReadyAsync(string roomId, string userId)
    {
        var field = $"readyStatus.{userId}";
        await _repo.UpdateAsync(roomId,
            Builders<RoomDocument>.Update.Set(field, true));
    }

    public async Task<bool> IsPlayerInRoomAsync(string roomId, string userId)
    {
        var room = await _repo.GetByIdAsync(roomId);
        return room?.PlayerIds.Contains(userId) ?? false;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<RoomDto> MapToDtoAsync(RoomDocument doc)
    {
        var host = await _userRepo.GetByIdAsync(doc.HostId);
        return new RoomDto
        {
            Id            = doc.Id,
            RoomCode      = doc.RoomCode,
            RoomName      = doc.RoomName,
            HostId        = doc.HostId,
            HostName      = host?.DisplayName ?? doc.HostName ?? "",
            Status        = doc.Status,
            MaxPlayers    = doc.MaxPlayers,
            BotCount      = doc.BotCount,
            BotDifficulty = doc.BotDifficulty,
            IsPrivate     = doc.IsPrivate,
            MaxRounds     = doc.MaxRounds,
            CreatedAt     = doc.CreatedAt,
            Players       = await BuildPlayerDtos(doc)
        };
    }

    private async Task<List<RoomPlayerDto>> BuildPlayerDtos(RoomDocument doc)
    {
        var result = new List<RoomPlayerDto>();
        foreach (var uid in doc.PlayerIds)
        {
            var user = await _userRepo.GetByIdAsync(uid);
            result.Add(new RoomPlayerDto
            {
                UserId      = uid,
                DisplayName = user?.DisplayName ?? "Unknown",
                AvatarUrl   = user?.AvatarUrl ?? "",
                IsHost      = uid == doc.HostId,
                IsBot       = false,
                IsReady     = doc.ReadyStatus.GetValueOrDefault(uid),
                IsConnected = true
            });
        }
        // Add bots
        for (int i = 0; i < doc.BotCount; i++)
            result.Add(new RoomPlayerDto
            {
                UserId      = $"bot-{i + 1}",
                DisplayName = $"Bot {i + 1}",
                IsBot       = true, IsReady = true, IsConnected = true
            });
        return result;
    }

    private static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 6)
            .Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
    }
}

// ─────────────────────────────────────────────────────────────────────────────

public class LeaderboardService : ILeaderboardService
{
    private readonly IUserRepository _repo;

    public LeaderboardService(IUserRepository repo) => _repo = repo;

    public async Task<PagedResult<LeaderboardEntryDto>> GetGlobalAsync(int page, int pageSize)
    {
        int skip = (page - 1) * pageSize;
        var users = await _repo.GetTopByScoreAsync(skip, pageSize);
        long total = await _repo.CountAsync();

        var items = users.Select((u, i) => new LeaderboardEntryDto
        {
            Rank        = skip + i + 1,
            UserId      = u.Id,
            DisplayName = u.DisplayName,
            AvatarUrl   = u.AvatarUrl,
            GamesWon    = u.GamesWon,
            GamesPlayed = u.GamesPlayed,
            WinRate     = u.GamesPlayed == 0 ? 0 : Math.Round((double)u.GamesWon / u.GamesPlayed * 100, 1),
            TotalScore  = u.TotalScore
        });

        return PagedResult<LeaderboardEntryDto>.Create(items, (int)total, page, pageSize);
    }

    public async Task<PagedResult<LeaderboardEntryDto>> GetWeeklyAsync(int page, int pageSize)
    {
        var since = DateTime.UtcNow.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
        int skip  = (page - 1) * pageSize;
        var users = await _repo.GetTopByWeeklyScoreAsync(skip, pageSize, since);
        long total = await _repo.CountAsync();

        var items = users.Select((u, i) => new LeaderboardEntryDto
        {
            Rank        = skip + i + 1,
            UserId      = u.Id,
            DisplayName = u.DisplayName,
            AvatarUrl   = u.AvatarUrl,
            GamesWon    = u.GamesWon,
            GamesPlayed = u.GamesPlayed,
            WinRate     = u.GamesPlayed == 0 ? 0 : Math.Round((double)u.GamesWon / u.GamesPlayed * 100, 1),
            TotalScore  = u.TotalScore
        });

        return PagedResult<LeaderboardEntryDto>.Create(items, (int)total, page, pageSize);
    }

    public async Task<MyRankDto> GetMyRankAsync(string userId)
    {
        var me = await _repo.GetByIdAsync(userId);
        if (me is null) return new MyRankDto();

        long above = await _repo.CountRankAboveAsync(me.TotalScore);
        long total = await _repo.CountAsync();

        return new MyRankDto
        {
            GlobalRank   = (int)above + 1,
            WeeklyRank   = (int)above + 1,
            TotalPlayers = (int)total,
            Percentile   = total == 0 ? 100 : (int)Math.Ceiling((double)above / total * 100)
        };
    }
}
