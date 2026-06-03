using System.ComponentModel.DataAnnotations;

namespace UnoCustomBackend.Api.Models.Requests
{
    public class SendChangePasswordCodeRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}