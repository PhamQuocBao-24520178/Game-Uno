using System.ComponentModel.DataAnnotations;
using UnoGame.Core.Models;

namespace UnoGame.Core.DTOs;

// ════════════════════════════════════════════════════════════════
// Chuyển từ UnoGame.API.DTOs → UnoGame.Core.DTOs
// để UnoGame.Infrastructure có thể dùng mà không tạo circular ref
// ════════════════════════════════════════════════════════════════

// ─── User ─────────────────────────────────────────────────────────────────────

public record RegisterUserRequest
{
    [Required, StringLength(32, MinimumLength = 2)]
    public string DisplayName { get; init; } = null!;
    public string? AvatarUrl { get; init; }
}

public record UpdateProfileRequest
{
    [StringLength(32, MinimumLength = 2)]
    public string? DisplayName { get; init; }

    [Url]
    public string? AvatarUrl { get; init; }
}

public record UserDto
{
    public string Id { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public string AvatarUrl { get; init; } = "";
    public UserStatsDto Stats { get; init; } = new();
    public DateTime CreatedAt { get; init; }
}

public record UserStatsDto
{
    public int GamesPlayed { get; init; }
    public int GamesWon { get; init; }
    public double WinRate { get; init; }
    public int TotalScore { get; init; }
}

// ─── Room ─────────────────────────────────────────────────────────────────────

public record CreateRoomRequest
{
    [StringLength(32, MinimumLength = 2)]
    public string? RoomName { get; init; }

    [Range(2, 4)]
    public int MaxPlayers { get; init; } = 4;

    [Range(0, 3)]
    public int BotCount { get; init; } = 0;

    public string BotDifficulty { get; init; } = "hard";
    public bool IsPrivate { get; init; } = false;
    public string? Password { get; init; }

    [Range(1, 20)]
    public int MaxRounds { get; init; } = 1;
}

public record UpdateRoomSettingsRequest
{
    [Range(2, 4)] public int? MaxPlayers { get; init; }
    [Range(0, 3)] public int? BotCount { get; init; }
    public string? BotDifficulty { get; init; }
    public bool? IsPrivate { get; init; }
}

public record JoinRoomRequest
{
    public string? Password { get; init; }
}

public record RoomDto
{
    public string Id { get; init; } = null!;
    public string RoomCode { get; init; } = null!;
    public string? RoomName { get; init; }
    public string HostId { get; init; } = null!;
    public string HostName { get; init; } = null!;
    public RoomStatus Status { get; init; }
    public int MaxPlayers { get; init; }
    public int BotCount { get; init; }
    public string BotDifficulty { get; init; } = null!;
    public bool IsPrivate { get; init; }
    public int MaxRounds { get; init; }
    public List<RoomPlayerDto> Players { get; init; } = new();
    public DateTime CreatedAt { get; init; }
}

public record RoomPlayerDto
{
    public string UserId { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public string AvatarUrl { get; init; } = "";
    public bool IsHost { get; init; }
    public bool IsBot { get; init; }
    public bool IsReady { get; init; }
    public bool IsConnected { get; init; }
}

public record RoomSummaryDto
{
    public string Id { get; init; } = null!;
    public string RoomCode { get; init; } = null!;
    public string? RoomName { get; init; }
    public string HostName { get; init; } = null!;
    public RoomStatus Status { get; init; }
    public int PlayerCount { get; init; }
    public int MaxPlayers { get; init; }
    public bool HasPassword { get; init; }
    public DateTime CreatedAt { get; init; }
}

// ─── Game ─────────────────────────────────────────────────────────────────────

public record PlayCardRequest
{
    [Required]
    public CardDto Card { get; init; } = null!;
    public string? ChosenColor { get; init; }
}

public record CardDto
{
    public string Color { get; init; } = null!;
    public string Type { get; init; } = null!;
    public int? Value { get; init; }
}

public record GameStateDto
{
    public string RoomId { get; init; } = null!;
    public GamePhase Phase { get; init; }
    public string CurrentPlayerId { get; init; } = null!;
    public CardDto TopCard { get; init; } = null!;
    public string CurrentColor { get; init; } = null!;
    public int Direction { get; init; }
    public int PendingDrawCount { get; init; }
    public List<PlayerHandSummaryDto> Players { get; init; } = new();
    public string? WinnerId { get; init; }
    public int TurnNumber { get; init; }
    public int DrawPileCount { get; init; }
    public DateTime LastActionAt { get; init; }
}

public record PlayerHandSummaryDto
{
    public string PlayerId { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public string AvatarUrl { get; init; } = "";
    public int CardCount { get; init; }
    public bool IsBot { get; init; }
    public bool HasCalledUno { get; init; }
    public bool IsConnected { get; init; }
}

public record MyHandDto
{
    public List<CardDto> Cards { get; init; } = new();
    public bool CanPlay { get; init; }
    public List<CardDto> Playable { get; init; } = new();
    public bool MustDraw { get; init; }
}

public record GameActionResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }
    public GameStateDto? State { get; init; }
    public List<CardDto>? DrawnCards { get; init; }
    public bool IsGameOver { get; init; }
    public string? WinnerId { get; init; }

    public static GameActionResult Failure(string error, string? code = null) =>
        new() { Success = false, Error = error, ErrorCode = code };
}

public record GameHistoryDto
{
    public string Id { get; init; } = null!;
    public string RoomId { get; init; } = null!;
    public string WinnerId { get; init; } = null!;
    public string WinnerName { get; init; } = null!;
    public List<PlayerResultDto> Results { get; init; } = new();
    public int TotalTurns { get; init; }
    public string Duration { get; init; } = null!;
    public DateTime PlayedAt { get; init; }
}

public record PlayerResultDto
{
    public string PlayerId { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public int Rank { get; init; }
    public int Score { get; init; }
    public int CardsLeft { get; init; }
}

// ─── Leaderboard ─────────────────────────────────────────────────────────────

public record LeaderboardEntryDto
{
    public int Rank { get; init; }
    public string UserId { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public string AvatarUrl { get; init; } = "";
    public int GamesWon { get; init; }
    public int GamesPlayed { get; init; }
    public double WinRate { get; init; }
    public int TotalScore { get; init; }
}

public record MyRankDto
{
    public int GlobalRank { get; init; }
    public int WeeklyRank { get; init; }
    public int TotalPlayers { get; init; }
    public int Percentile { get; init; }
}

// ─── Paging ───────────────────────────────────────────────────────────────────

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; init; } = Enumerable.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;

    public static PagedResult<T> Create(IEnumerable<T> items, int total, int page, int size) =>
        new() { Items = items, TotalCount = total, Page = page, PageSize = size };
}