using UnoGame.Core.DTOs;
using UnoGame.Core.Models;

namespace UnoGame.Core.Interfaces;

// ════════════════════════════════════════════════════════════════
// Auth support types
// Đặt ở Core để cả Infrastructure và API đều dùng được
// ════════════════════════════════════════════════════════════════

public record FirebaseTokenInfo
{
    public string Uid { get; init; } = null!;
    public string Email { get; init; } = null!;
    public bool EmailVerified { get; init; }
    public string Jti { get; init; } = null!;
    public DateTime ExpiresAt { get; init; }
    public string? DisplayName { get; init; }
    public string? PhotoUrl { get; init; }
}

public interface ITokenBlacklistService
{
    Task BlacklistAsync(string jti, DateTime expiresAt);
    Task<bool> IsBlacklistedAsync(string jti);
}

public class AuthException : Exception
{
    public AuthErrorCode Code { get; }
    public AuthException(AuthErrorCode code, string message) : base(message) => Code = code;
}

public enum AuthErrorCode
{
    InvalidToken, TokenExpired, TokenRevoked, TokenBlacklisted,
    UserNotFound, EmailNotVerified, WeakPassword, FirebaseError
}

// ════════════════════════════════════════════════════════════════
// Service interfaces
// ════════════════════════════════════════════════════════════════

public interface IUserService
{
    Task<UserDto?> GetByIdAsync(string userId);
    Task<UserDto> RegisterAsync(string firebaseUid, string email, RegisterUserRequest req);
    Task<UserDto> UpdateProfileAsync(string userId, UpdateProfileRequest req);
    Task<bool> ExistsAsync(string userId);
    Task<UserStatsDto> GetStatsAsync(string userId);
    Task IncrementStatsAsync(string userId, bool won, int score);
}

public interface IRoomService
{
    Task<PagedResult<RoomSummaryDto>> ListPublicRoomsAsync(int page, int pageSize, string? search);
    Task<RoomDto?> GetByIdAsync(string roomId);
    Task<RoomDto?> GetByCodeAsync(string code);
    Task<RoomDto> CreateAsync(string hostId, CreateRoomRequest req);
    Task<RoomDto> JoinAsync(string roomId, string userId, string? password);
    Task LeaveAsync(string roomId, string userId);
    Task KickPlayerAsync(string roomId, string hostId, string targetUserId);
    Task<RoomDto> StartGameAsync(string roomId, string hostId);
    Task CloseRoomAsync(string roomId, string hostId);
    Task<RoomDto> UpdateSettingsAsync(string roomId, string hostId, UpdateRoomSettingsRequest req);
    Task<List<RoomPlayerDto>> GetPlayersAsync(string roomId);
    Task MarkReadyAsync(string roomId, string userId);
    Task<bool> IsPlayerInRoomAsync(string roomId, string userId);
}

public interface IGameService
{
    Task<GameStateDto?> GetPublicStateAsync(string roomId, string requesterId);
    Task<MyHandDto?> GetMyHandAsync(string roomId, string userId);
    Task<GameActionResult> PlayCardAsync(string roomId, string userId, PlayCardRequest req);
    Task<GameActionResult> DrawCardAsync(string roomId, string userId);
    Task<GameActionResult> CallUnoAsync(string roomId, string callerId, string targetId);
    Task<List<GameHistoryDto>> GetRoomHistoryAsync(string roomId, int limit);
    Task<List<GameHistoryDto>> GetUserHistoryAsync(string userId, int page, int pageSize);
    Task<GameHistoryDto?> GetGameByIdAsync(string gameId);
    Task<GameState?> GetInternalStateAsync(string roomId);
    Task InitializeGameAsync(string roomId, List<RoomPlayerDto> players);
}

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(string idToken);
    Task LogoutAsync(string userId, string idToken, bool logoutAll);
    Task SendPasswordResetEmailAsync(string email);
    Task<ChangePasswordResponse> ChangePasswordAsync(string userId, string newPassword);
    Task<FirebaseTokenInfo?> VerifyTokenAsync(string idToken);
}

public interface ILeaderboardService
{
    Task<PagedResult<LeaderboardEntryDto>> GetGlobalAsync(int page, int pageSize);
    Task<PagedResult<LeaderboardEntryDto>> GetWeeklyAsync(int page, int pageSize);
    Task<MyRankDto> GetMyRankAsync(string userId);
}