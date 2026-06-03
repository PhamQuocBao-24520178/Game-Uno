namespace UnoCustomBackend.Api.Models.Requests
{
    public class JoinRoomRequest
    {
        public string RoomCode { get; set; } = string.Empty;

        public string PlayerId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public int AvatarIndex { get; set; }
    }
}
