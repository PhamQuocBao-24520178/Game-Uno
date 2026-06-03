namespace UnoCustomBackend.Api.Models.Requests
{
    public class LeaveRoomRequest
    {
        public string RoomCode { get; set; } = string.Empty;

        public string PlayerId { get; set; } = string.Empty;
    }
}
