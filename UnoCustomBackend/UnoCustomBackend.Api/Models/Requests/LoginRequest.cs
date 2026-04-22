using System.ComponentModel.DataAnnotations;

namespace UnoCustomBackend.Api.Models.Requests
{
    public class LoginRequest
    {
        [Required]
        public string Login { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}