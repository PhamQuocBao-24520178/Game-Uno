// Các interface và types đã chuyển sang UnoGame.Core.Interfaces
// File này chỉ re-export để code API không cần sửa using statement
global using IUserService = UnoGame.Core.Interfaces.IUserService;
global using IRoomService = UnoGame.Core.Interfaces.IRoomService;
global using IGameService = UnoGame.Core.Interfaces.IGameService;
global using IAuthService = UnoGame.Core.Interfaces.IAuthService;
global using ILeaderboardService = UnoGame.Core.Interfaces.ILeaderboardService;
global using ITokenBlacklistService = UnoGame.Core.Interfaces.ITokenBlacklistService;
global using FirebaseTokenInfo = UnoGame.Core.Interfaces.FirebaseTokenInfo;
global using AuthException = UnoGame.Core.Interfaces.AuthException;
global using AuthErrorCode = UnoGame.Core.Interfaces.AuthErrorCode;