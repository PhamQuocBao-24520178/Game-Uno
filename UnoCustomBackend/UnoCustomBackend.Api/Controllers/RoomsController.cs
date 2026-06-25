using Microsoft.AspNetCore.Mvc;
using UnoCustomBackend.Api.Models.Requests;
using UnoCustomBackend.Api.Models.Responses;

namespace UnoCustomBackend.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RoomsController : ControllerBase
    {
        private static readonly object RoomLock = new object();
        private static readonly Dictionary<string, RoomState> Rooms = new Dictionary<string, RoomState>();

        [HttpPost("create")]
        public IActionResult CreateRoom([FromBody] CreateRoomRequest request)
        {
            if (request.MaxPlayers < 2 || request.MaxPlayers > 4)
            {
                return BadRequest(new
                {
                    message = "MaxPlayers phải từ 2 đến 4."
                });
            }

            if (string.IsNullOrWhiteSpace(request.PlayerId))
            {
                return BadRequest(new
                {
                    message = "PlayerId không được rỗng."
                });
            }

            if (string.IsNullOrWhiteSpace(request.DisplayName))
            {
                return BadRequest(new
                {
                    message = "DisplayName không được rỗng."
                });
            }

            lock (RoomLock)
            {
                string roomCode = GenerateUniqueRoomCode();

                RoomState room = new RoomState
                {
                    RoomCode = roomCode,
                    MaxPlayers = request.MaxPlayers,
                    Status = "Waiting"
                };

                room.Players.Add(new RoomPlayerState
                {
                    PlayerId = request.PlayerId,
                    DisplayName = request.DisplayName,
                    AvatarIndex = request.AvatarIndex,
                    IsHost = true
                });

                Rooms[roomCode] = room;

                return Ok(ToRoomResponse(room));
            }
        }

        [HttpPost("join")]
        public IActionResult JoinRoom([FromBody] JoinRoomRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RoomCode))
            {
                return BadRequest(new
                {
                    message = "RoomCode không được rỗng."
                });
            }

            if (string.IsNullOrWhiteSpace(request.PlayerId))
            {
                return BadRequest(new
                {
                    message = "PlayerId không được rỗng."
                });
            }

            if (string.IsNullOrWhiteSpace(request.DisplayName))
            {
                return BadRequest(new
                {
                    message = "DisplayName không được rỗng."
                });
            }

            string roomCode = request.RoomCode.Trim().ToUpper();

            lock (RoomLock)
            {
                if (!Rooms.TryGetValue(roomCode, out RoomState? room))
                {
                    return NotFound(new
                    {
                        message = "Không tìm thấy phòng."
                    });
                }

                if (room.Status != "Waiting")
                {
                    return BadRequest(new
                    {
                        message = "Phòng này đã bắt đầu hoặc đã đóng."
                    });
                }

                bool playerAlreadyInRoom = room.Players.Any(p => p.PlayerId == request.PlayerId);

                if (playerAlreadyInRoom)
                {
                    return Ok(ToRoomResponse(room));
                }

                if (room.Players.Count >= room.MaxPlayers)
                {
                    return BadRequest(new
                    {
                        message = "Phòng đã đủ người."
                    });
                }

                room.Players.Add(new RoomPlayerState
                {
                    PlayerId = request.PlayerId,
                    DisplayName = request.DisplayName,
                    AvatarIndex = request.AvatarIndex,
                    IsHost = false
                });

                if (room.Players.Count >= room.MaxPlayers)
                {
                    room.Status = "Ready";
                }

                return Ok(ToRoomResponse(room));
            }
        }

        [HttpGet("{roomCode}")]
        public IActionResult GetRoom(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                return BadRequest(new
                {
                    message = "RoomCode không được rỗng."
                });
            }

            roomCode = roomCode.Trim().ToUpper();

            lock (RoomLock)
            {
                if (!Rooms.TryGetValue(roomCode, out RoomState? room))
                {
                    return NotFound(new
                    {
                        message = "Không tìm thấy phòng."
                    });
                }

                return Ok(ToRoomResponse(room));
            }
        }

        [HttpPost("leave")]
        public IActionResult LeaveRoom([FromBody] LeaveRoomRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RoomCode))
            {
                return BadRequest(new
                {
                    message = "RoomCode không được rỗng."
                });
            }

            if (string.IsNullOrWhiteSpace(request.PlayerId))
            {
                return BadRequest(new
                {
                    message = "PlayerId không được rỗng."
                });
            }

            string roomCode = request.RoomCode.Trim().ToUpper();

            lock (RoomLock)
            {
                if (!Rooms.TryGetValue(roomCode, out RoomState? room))
                {
                    return NotFound(new
                    {
                        message = "Không tìm thấy phòng."
                    });
                }

                RoomPlayerState? leavingPlayer = room.Players.FirstOrDefault(p => p.PlayerId == request.PlayerId);

                if (leavingPlayer == null)
                {
                    return Ok(ToRoomResponse(room));
                }

                bool leavingPlayerWasHost = leavingPlayer.IsHost;

                room.Players.Remove(leavingPlayer);

                if (room.Players.Count == 0)
                {
                    Rooms.Remove(roomCode);

                    return Ok(new
                    {
                        message = "Phòng đã bị xóa vì không còn người chơi."
                    });
                }

                if (leavingPlayerWasHost)
                {
                    room.Players[0].IsHost = true;
                }

                room.Status = "Waiting";

                return Ok(ToRoomResponse(room));
            }
        }

        [HttpPost("{roomCode}/start")]
        public IActionResult StartRoom(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                return BadRequest(new
                {
                    message = "RoomCode không được rỗng."
                });
            }

            roomCode = roomCode.Trim().ToUpper();

            lock (RoomLock)
            {
                if (!Rooms.TryGetValue(roomCode, out RoomState? room))
                {
                    return NotFound(new
                    {
                        message = "Không tìm thấy phòng."
                    });
                }

                if (room.Players.Count < room.MaxPlayers)
                {
                    return BadRequest(new
                    {
                        message = "Chưa đủ người chơi."
                    });
                }

                room.Status = "Playing";

                return Ok(ToRoomResponse(room));
            }
        }

        private static RoomResponse ToRoomResponse(RoomState room)
        {
            return new RoomResponse
            {
                RoomCode = room.RoomCode,
                MaxPlayers = room.MaxPlayers,
                Status = room.Status,
                IsFull = room.Players.Count >= room.MaxPlayers,
                Players = room.Players.Select(p => new RoomPlayerResponse
                {
                    PlayerId = p.PlayerId,
                    DisplayName = p.DisplayName,
                    AvatarIndex = p.AvatarIndex,
                    IsHost = p.IsHost
                }).ToList()
            };
        }

        public static bool TryGetRoomSnapshot(string roomCode, out RoomSnapshot snapshot)
        {
            roomCode = roomCode.Trim().ToUpper();

            lock (RoomLock)
            {
                if (!Rooms.TryGetValue(roomCode, out RoomState? room))
                {
                    snapshot = new RoomSnapshot();
                    return false;
                }

                snapshot = new RoomSnapshot
                {
                    RoomCode = room.RoomCode,
                    MaxPlayers = room.MaxPlayers,
                    Status = room.Status,
                    Players = room.Players.Select(p => new RoomSnapshotPlayer
                    {
                        PlayerId = p.PlayerId,
                        DisplayName = p.DisplayName,
                        AvatarIndex = p.AvatarIndex,
                        IsHost = p.IsHost
                    }).ToList()
                };

                return true;
            }
        }

        public class RoomSnapshot
        {
            public string RoomCode { get; set; } = "";
            public int MaxPlayers { get; set; }
            public string Status { get; set; } = "";
            public List<RoomSnapshotPlayer> Players { get; set; } = new();
        }

        public class RoomSnapshotPlayer
        {
            public string PlayerId { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public int AvatarIndex { get; set; }
            public bool IsHost { get; set; }
        }

        private static string GenerateUniqueRoomCode()
        {
            string code;

            do
            {
                code = GenerateRoomCode();
            }
            while (Rooms.ContainsKey(code));

            return code;
        }

        private static string GenerateRoomCode()
        {
            Random random = new Random();
            int number = random.Next(0, 10000);

            return number.ToString("D4");
        }

        private class RoomState
        {
            public string RoomCode { get; set; } = string.Empty;

            public int MaxPlayers { get; set; }

            public string Status { get; set; } = "Waiting";

            public List<RoomPlayerState> Players { get; set; } = new();
        }

        private class RoomPlayerState
        {
            public string PlayerId { get; set; } = string.Empty;

            public string DisplayName { get; set; } = string.Empty;

            public int AvatarIndex { get; set; }

            public bool IsHost { get; set; }
        }
    }
}