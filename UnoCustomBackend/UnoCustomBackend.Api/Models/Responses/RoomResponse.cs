namespace UnoCustomBackend.Api.Models.Responses
{
    public class RoomResponse
    {
        public string RoomCode { get; set; } = string.Empty;

        public int MaxPlayers { get; set; }

        public string Status { get; set; } = "Waiting";

        public bool IsFull { get; set; }

        public List<RoomPlayerResponse> Players { get; set; } = new();
    }
}
