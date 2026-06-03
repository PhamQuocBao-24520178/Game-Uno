namespace UnoCustomBackend.Api.Models.Requests
{
    public class CreateRoomRequest
    {
        public int MaxPlayers { get; set; }

        public string PlayerId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public int AvatarIndex { get; set; }
    }
}
