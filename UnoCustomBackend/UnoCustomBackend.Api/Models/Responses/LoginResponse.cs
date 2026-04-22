namespace UnoCustomBackend.Api.Models.Responses
{
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool NeedCreateDisplayName { get; set; }
        public DateTime ExpiredAt { get; set; }
    }
}