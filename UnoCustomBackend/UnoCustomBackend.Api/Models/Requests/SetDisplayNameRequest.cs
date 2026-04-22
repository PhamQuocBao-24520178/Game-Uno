using System.ComponentModel.DataAnnotations;

namespace UnoCustomBackend.Api.Models.Requests
{
    public class SetDisplayNameRequest
    {
        [Required]
        [MinLength(2)]
        public string DisplayName { get; set; } = string.Empty;
    }
}
