namespace UnoCustomBackend.Api.Models.Entities
{
    public class PasswordResetCodeEntity
    {
        public string Code { get; set; } = string.Empty;
        public string ExpiredAt { get; set; } = string.Empty;
        public bool Used { get; set; }
    }
}
