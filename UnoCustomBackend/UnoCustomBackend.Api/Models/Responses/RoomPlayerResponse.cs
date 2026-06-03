namespace UnoCustomBackend.Api.Models.Responses
{
    public class RoomPlayerResponse
    {
        public string PlayerId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public int AvatarIndex { get; set; }

        public bool IsHost { get; set; }
    }
}