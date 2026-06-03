using UnoCustomBackend.Api.Helpers;
using UnoCustomBackend.Api.Models.Entities;
using UnoCustomBackend.Api.Models.Requests;
using UnoCustomBackend.Api.Models.Responses;

namespace UnoCustomBackend.Api.Services
{
    public class AuthService
    {
        private readonly FirebaseDatabaseService _firebaseDatabaseService;
        private readonly PasswordService _passwordService;
        private readonly JwtService _jwtService;
        private readonly EmailService _emailService;

        public AuthService(
            FirebaseDatabaseService firebaseDatabaseService,
            PasswordService passwordService,
            JwtService jwtService,
            EmailService emailService)
        {
            _firebaseDatabaseService = firebaseDatabaseService;
            _passwordService = passwordService;
            _jwtService = jwtService;
            _emailService = emailService;
        }

        public async Task<(bool Success, string Message)> RegisterAsync(RegisterRequest request)
        {
            if (request.Password != request.ConfirmPassword)
                return (false, "Confirm password không khớp.");

            if (await _firebaseDatabaseService.EmailExistsAsync(request.Email))
                return (false, "Email đã tồn tại.");

            if (await _firebaseDatabaseService.UsernameExistsAsync(request.Username))
                return (false, "Username đã tồn tại.");

            var passwordResult = _passwordService.HashPassword(request.Password);

            var user = new UserEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                Email = request.Email.Trim().ToLower(),
                Username = request.Username.Trim(),
                DisplayName = "",
                PasswordHash = passwordResult.Hash,
                PasswordSalt = passwordResult.Salt,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                LastLoginAt = DateTime.UtcNow.ToString("o"),
                IsActive = true
            };

            await _firebaseDatabaseService.SaveUserAsync(user);

            return (true, "Đăng ký thành công.");
        }

        public async Task<(bool Success, string Message, LoginResponse? Data)> LoginAsync(LoginRequest request)
        {
            string loginInput = request.Login.Trim();
            string? userId;

            if (loginInput.Contains("@"))
                userId = await _firebaseDatabaseService.GetUserIdByEmailAsync(loginInput);
            else
                userId = await _firebaseDatabaseService.GetUserIdByUsernameAsync(loginInput);

            if (string.IsNullOrWhiteSpace(userId))
                return (false, "Tên đăng nhập/email hoặc mật khẩu không đúng.", null);

            var user = await _firebaseDatabaseService.GetUserByIdAsync(userId);
            if (user == null)
                return (false, "Tài khoản không tồn tại.", null);

            bool validPassword = _passwordService.VerifyPassword(
                request.Password,
                user.PasswordHash,
                user.PasswordSalt);

            if (!validPassword)
                return (false, "Tên đăng nhập/email hoặc mật khẩu không đúng.", null);

            await _firebaseDatabaseService.UpdateLastLoginAsync(user.Id);

            var tokenResult = _jwtService.GenerateToken(user);

            var response = new LoginResponse
            {
                Token = tokenResult.Token,
                UserId = user.Id,
                Email = user.Email,
                Username = user.Username,
                DisplayName = user.DisplayName,
                NeedCreateDisplayName = string.IsNullOrWhiteSpace(user.DisplayName),
                ExpiredAt = tokenResult.ExpiredAt
            };

            return (true, "Đăng nhập thành công.", response);
        }

        public async Task<(bool Success, string Message)> ForgotPasswordAsync(string email)
        {
            if (!await _firebaseDatabaseService.EmailExistsAsync(email))
                return (false, "Email không tồn tại.");

            string code = RandomCodeGenerator.Generate6Digits();

            var resetData = new PasswordResetCodeEntity
            {
                Code = code,
                ExpiredAt = DateTime.UtcNow.AddMinutes(10).ToString("o"),
                Used = false
            };

            await _firebaseDatabaseService.SaveResetCodeAsync(email, resetData);
            await _emailService.SendResetCodeAsync(email, code);

            return (true, "Đã gửi mã OTP về email.");
        }

        public async Task<(bool Success, string Message)> ResetPasswordAsync(string email, string code, string newPassword)
        {
            var resetData = await _firebaseDatabaseService.GetResetCodeAsync(email);

            if (resetData == null)
                return (false, "Không tìm thấy mã OTP.");

            if (resetData.Used)
                return (false, "Mã OTP đã được sử dụng.");

            if (DateTime.Parse(resetData.ExpiredAt) < DateTime.UtcNow)
                return (false, "Mã OTP đã hết hạn.");

            if (resetData.Code != code)
                return (false, "Mã OTP không đúng.");

            var userId = await _firebaseDatabaseService.GetUserIdByEmailAsync(email);
            if (string.IsNullOrWhiteSpace(userId))
                return (false, "Người dùng không tồn tại.");

            var passwordResult = _passwordService.HashPassword(newPassword);

            await _firebaseDatabaseService.UpdatePasswordAsync(userId, passwordResult.Hash, passwordResult.Salt);
            await _firebaseDatabaseService.MarkResetCodeUsedAsync(email);

            return (true, "Đặt lại mật khẩu thành công.");
        }

        public async Task<(bool Success, string Message)> SendChangePasswordCodeAsync(string email)
        {
            if (!await _firebaseDatabaseService.EmailExistsAsync(email))
                return (false, "Email không tồn tại.");

            string code = RandomCodeGenerator.Generate6Digits();

            var resetData = new PasswordResetCodeEntity
            {
                Code = code,
                ExpiredAt = DateTime.UtcNow.AddMinutes(10).ToString("o"),
                Used = false
            };

            await _firebaseDatabaseService.SaveResetCodeAsync(email, resetData);
            await _emailService.SendResetCodeAsync(email, code);

            return (true, "Đã gửi mã đổi mật khẩu về email.");
        }

        public async Task<(bool Success, string Message)> ChangePasswordAsync(
            string email,
            string oldPassword,
            string code,
            string newPassword)
        {
            var userId = await _firebaseDatabaseService.GetUserIdByEmailAsync(email);
            if (string.IsNullOrWhiteSpace(userId))
                return (false, "Người dùng không tồn tại.");

            var user = await _firebaseDatabaseService.GetUserByIdAsync(userId);
            if (user == null)
                return (false, "Người dùng không tồn tại.");

            bool validOldPassword = _passwordService.VerifyPassword(
                oldPassword,
                user.PasswordHash,
                user.PasswordSalt);

            if (!validOldPassword)
                return (false, "Mật khẩu cũ không đúng.");

            var resetData = await _firebaseDatabaseService.GetResetCodeAsync(email);

            if (resetData == null)
                return (false, "Không tìm thấy mã OTP.");

            if (resetData.Used)
                return (false, "Mã OTP đã được sử dụng.");

            if (DateTime.Parse(resetData.ExpiredAt) < DateTime.UtcNow)
                return (false, "Mã OTP đã hết hạn.");

            if (resetData.Code != code)
                return (false, "Mã OTP không đúng.");

            var passwordResult = _passwordService.HashPassword(newPassword);

            await _firebaseDatabaseService.UpdatePasswordAsync(userId, passwordResult.Hash, passwordResult.Salt);
            await _firebaseDatabaseService.MarkResetCodeUsedAsync(email);

            return (true, "Đổi mật khẩu thành công.");
        }
    }
}