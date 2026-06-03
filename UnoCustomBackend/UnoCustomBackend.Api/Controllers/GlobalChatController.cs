using Microsoft.AspNetCore.Mvc;

namespace UnoCustomBackend.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GlobalChatController : ControllerBase
    {
        private static readonly List<GlobalChatMessageDto> Messages = new();

        [HttpGet("messages")]
        public IActionResult GetMessages()
        {
            var latestMessages = Messages
                .OrderBy(m => m.createdAt)
                .TakeLast(50)
                .ToList();

            return Ok(new GlobalChatListResponse
            {
                success = true,
                message = "Lấy tin nhắn thành công",
                data = latestMessages
            });
        }

        [HttpPost("send")]
        public IActionResult SendMessage([FromBody] GlobalChatSendRequest request)
        {
            if (request == null)
            {
                return BadRequest(new GlobalChatSendResponse
                {
                    success = false,
                    message = "Dữ liệu không hợp lệ",
                    data = null
                });
            }

            if (string.IsNullOrWhiteSpace(request.message))
            {
                return BadRequest(new GlobalChatSendResponse
                {
                    success = false,
                    message = "Tin nhắn không được để trống",
                    data = null
                });
            }

            string displayName = string.IsNullOrWhiteSpace(request.displayName)
                ? "Player"
                : request.displayName.Trim();

            string message = request.message.Trim();

            if (message.Length > 200)
            {
                message = message.Substring(0, 200);
            }

            var chatMessage = new GlobalChatMessageDto
            {
                id = Guid.NewGuid().ToString(),
                playerId = request.playerId ?? "",
                displayName = displayName,
                message = message,
                roomCode = request.roomCode ?? "",
                createdAt = DateTime.UtcNow.ToString("o")
            };

            Messages.Add(chatMessage);

            if (Messages.Count > 100)
            {
                Messages.RemoveAt(0);
            }

            return Ok(new GlobalChatSendResponse
            {
                success = true,
                message = "Gửi tin nhắn thành công",
                data = chatMessage
            });
        }
    }

    public class GlobalChatSendRequest
    {
        public string playerId { get; set; } = "";
        public string displayName { get; set; } = "";
        public string message { get; set; } = "";
        public string roomCode { get; set; } = "";
    }

    public class GlobalChatMessageDto
    {
        public string id { get; set; } = "";
        public string playerId { get; set; } = "";
        public string displayName { get; set; } = "";
        public string message { get; set; } = "";
        public string roomCode { get; set; } = "";
        public string createdAt { get; set; } = "";
    }

    public class GlobalChatListResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = "";
        public List<GlobalChatMessageDto> data { get; set; } = new();
    }

    public class GlobalChatSendResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = "";
        public GlobalChatMessageDto? data { get; set; }
    }
}