using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using static UnoCustomBackend.Api.Controllers.RoomsController;

namespace UnoCustomBackend.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameController : ControllerBase
    {
        private static readonly object GameLock = new object();

        private static readonly Dictionary<string, OnlineGameState> Games
            = new Dictionary<string, OnlineGameState>();

        // 30 giây cho mỗi lượt.
        private const int TurnDurationSeconds = 30;

        // =====================================================
        // START GAME
        // =====================================================

        [HttpPost("start")]
        public IActionResult StartGame([FromBody] StartGameRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.RoomCode) ||
                string.IsNullOrWhiteSpace(request.PlayerId))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "RoomCode và PlayerId không được rỗng."
                });
            }

            string roomCode = request.RoomCode.Trim().ToUpper();

            if (!RoomsController.TryGetRoomSnapshot(
                roomCode,
                out RoomSnapshot room))
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy phòng."
                });
            }

            if (room.Players == null ||
                room.Players.Count < room.MaxPlayers)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Phòng chưa đủ người để bắt đầu game."
                });
            }

            bool isPlayerInRoom = room.Players.Any(
                player => player.PlayerId == request.PlayerId
            );

            if (!isPlayerInRoom)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Người chơi không thuộc phòng này."
                });
            }

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                    roomCode,
                    out OnlineGameState game))
                {
                    game = CreateNewGame(room);
                    Games[roomCode] = game;
                }

                ProcessTurnTimeoutIfNeeded(game);

                return Ok(ToGameResponse(game, request.PlayerId));
            }
        }

        // =====================================================
        // GET GAME STATE
        // =====================================================

        [HttpGet("{roomCode}")]
        public IActionResult GetGameState(
            string roomCode,
            [FromQuery] string playerId)
        {
            if (string.IsNullOrWhiteSpace(roomCode) ||
                string.IsNullOrWhiteSpace(playerId))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "RoomCode và playerId không được rỗng."
                });
            }

            roomCode = roomCode.Trim().ToUpper();

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                    roomCode,
                    out OnlineGameState game))
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Game chưa được khởi tạo."
                    });
                }

                if (!game.Hands.ContainsKey(playerId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Người chơi không thuộc ván game này."
                    });
                }

                // Unity polling GameState sẽ kích hoạt kiểm tra hết giờ.
                ProcessTurnTimeoutIfNeeded(game);

                return Ok(ToGameResponse(game, playerId));
            }
        }

        // =====================================================
        // UNO
        // =====================================================

        [HttpPost("declare-uno")]
        public IActionResult DeclareUno(
            [FromBody] DeclareUnoRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.RoomCode) ||
                string.IsNullOrWhiteSpace(request.PlayerId))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Thiếu RoomCode hoặc PlayerId."
                });
            }

            string roomCode = request.RoomCode.Trim().ToUpper();

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                    roomCode,
                    out OnlineGameState game))
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Game chưa được khởi tạo."
                    });
                }

                ProcessTurnTimeoutIfNeeded(game);

                if (!game.Hands.ContainsKey(request.PlayerId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Người chơi không thuộc ván game này."
                    });
                }

                if (game.CurrentTurnPlayerId != request.PlayerId)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Chưa tới lượt để bấm UNO."
                    });
                }

                if (game.Hands[request.PlayerId].Count != 2)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Chỉ được bấm UNO khi còn đúng 2 lá."
                    });
                }

                game.DeclaredUnoPlayers.Add(request.PlayerId);

                Console.WriteLine(
                    $"UNO DECLARED | room={roomCode} | " +
                    $"player={request.PlayerId}"
                );

                return Ok(ToGameResponse(game, request.PlayerId));
            }
        }

        // =====================================================
        // DRAW CARD / DRAW PENALTY
        // =====================================================

        [HttpPost("draw-card")]
        public IActionResult DrawCardOnline(
            [FromBody] DrawCardRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.RoomCode) ||
                string.IsNullOrWhiteSpace(request.PlayerId))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Thiếu RoomCode hoặc PlayerId."
                });
            }

            string roomCode = request.RoomCode.Trim().ToUpper();

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                    roomCode,
                    out OnlineGameState game))
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Game chưa được khởi tạo."
                    });
                }

                ProcessTurnTimeoutIfNeeded(game);

                if (!game.Hands.ContainsKey(request.PlayerId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Người chơi không thuộc ván game này."
                    });
                }

                if (game.CurrentTurnPlayerId != request.PlayerId)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Chưa tới lượt của bạn."
                    });
                }

                int drawCount = game.PendingDrawPenalty > 0
                    ? game.PendingDrawPenalty
                    : 1;

                bool wasPenalty =
                    game.PendingDrawPenalty > 0;

                int drawnCount = DrawCardsToPlayer(
                    game,
                    request.PlayerId,
                    drawCount
                );

                if (drawnCount == 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Không còn bài để rút."
                    });
                }

                game.DeclaredUnoPlayers.Remove(request.PlayerId);

                game.PendingDrawPenalty = 0;
                game.PendingPenaltyType = "";

                MoveToNextTurn(game);

                Console.WriteLine(
                    $"DRAW | room={roomCode} | " +
                    $"player={request.PlayerId} | " +
                    $"count={drawnCount} | " +
                    $"penalty={wasPenalty}"
                );

                return Ok(ToGameResponse(game, request.PlayerId));
            }
        }

        // =====================================================
        // PLAY CARD
        // =====================================================

        [HttpPost("play-card")]
        public IActionResult PlayCard(
            [FromBody] PlayCardRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.RoomCode) ||
                string.IsNullOrWhiteSpace(request.PlayerId) ||
                string.IsNullOrWhiteSpace(request.CardId))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Thiếu RoomCode, PlayerId hoặc CardId."
                });
            }

            string roomCode = request.RoomCode.Trim().ToUpper();

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                    roomCode,
                    out OnlineGameState game))
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Game chưa được khởi tạo."
                    });
                }

                // Kiểm tra timeout trước khi xét lượt.
                ProcessTurnTimeoutIfNeeded(game);

                if (!game.Hands.ContainsKey(request.PlayerId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Người chơi không thuộc ván game này."
                    });
                }

                if (game.CurrentTurnPlayerId != request.PlayerId)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Chưa tới lượt của bạn."
                    });
                }

                List<UnoCardData> myHand =
                    game.Hands[request.PlayerId];

                UnoCardData selectedCard =
                    myHand.FirstOrDefault(
                        card => card.CardId == request.CardId
                    );

                if (selectedCard == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Không tìm thấy lá bài trong tay."
                    });
                }

                // =================================================
                // PENALTY STACK
                //
                // Đang +2: được chồng +2 hoặc +4.
                // Đang +4: chỉ được chồng +4.
                // =================================================

                if (game.PendingDrawPenalty > 0)
                {
                    bool canStack = false;

                    if (game.PendingPenaltyType == "DrawTwo")
                    {
                        canStack =
                            selectedCard.Value == "DrawTwo" ||
                            selectedCard.Value == "WildDrawFour";
                    }
                    else if (game.PendingPenaltyType == "WildDrawFour")
                    {
                        canStack =
                            selectedCard.Value == "WildDrawFour";
                    }

                    if (!canStack)
                    {
                        string allowedText =
                            game.PendingPenaltyType == "DrawTwo"
                                ? "Chỉ được chồng +2, +4 hoặc bấm Rút."
                                : "Chỉ được chồng +4 hoặc bấm Rút.";

                        return BadRequest(new
                        {
                            success = false,
                            message =
                                $"Bạn đang bị phạt " +
                                $"{game.PendingDrawPenalty} lá. " +
                                allowedText
                        });
                    }
                }
                else
                {
                    UnoCardData topCard =
                        game.DiscardPile.Count > 0
                            ? game.DiscardPile[^1]
                            : null;

                    bool isWild =
                        selectedCard.Color == "Wild";

                    bool sameColor =
                        selectedCard.Color == game.CurrentColor;

                    bool sameValue =
                        topCard != null &&
                        selectedCard.Value == topCard.Value;

                    if (!isWild && !sameColor && !sameValue)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "Lá bài không hợp lệ."
                        });
                    }
                }

                bool isWildCard =
                    selectedCard.Color == "Wild";

                // Wild / Wild +4 bắt buộc chọn màu.
                if (isWildCard)
                {
                    if (!IsValidUnoColor(request.ChosenColor))
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message =
                                "Bài Wild cần chọn Red, Yellow, Green hoặc Blue."
                        });
                    }

                    game.CurrentColor =
                        NormalizeUnoColor(request.ChosenColor);
                }
                else
                {
                    game.CurrentColor = selectedCard.Color;
                }

                int handCountBeforePlay = myHand.Count;

                myHand.Remove(selectedCard);
                game.DiscardPile.Add(selectedCard);

                // =================================================
                // UNO PENALTY
                // =================================================

                bool playedFromTwoToOne =
                    handCountBeforePlay == 2 &&
                    myHand.Count == 1;

                bool hasDeclaredUno =
                    game.DeclaredUnoPlayers.Contains(
                        request.PlayerId
                    );

                if (playedFromTwoToOne && !hasDeclaredUno)
                {
                    DrawCardsToPlayer(
                        game,
                        request.PlayerId,
                        2
                    );

                    Console.WriteLine(
                        $"UNO PENALTY | room={roomCode} | " +
                        $"player={request.PlayerId}"
                    );
                }

                game.DeclaredUnoPlayers.Remove(request.PlayerId);

                // =================================================
                // WIN
                // =================================================

                if (myHand.Count == 0)
                {
                    game.Status = "Finished";

                    Console.WriteLine(
                        $"GAME FINISHED | room={roomCode} | " +
                        $"winner={request.PlayerId}"
                    );

                    return Ok(ToGameResponse(
                        game,
                        request.PlayerId
                    ));
                }

                // =================================================
                // +2
                // =================================================

                if (selectedCard.Value == "DrawTwo")
                {
                    game.PendingDrawPenalty += 2;

                    // Người sau đang bị +2 vẫn có thể chồng +2 hoặc +4.
                    game.PendingPenaltyType = "DrawTwo";

                    MoveToNextTurn(game);

                    return Ok(ToGameResponse(
                        game,
                        request.PlayerId
                    ));
                }

                // =================================================
                // WILD +4
                // =================================================

                if (selectedCard.Value == "WildDrawFour")
                {
                    game.PendingDrawPenalty += 4;

                    // Sau +4 chỉ được chồng +4 tiếp.
                    game.PendingPenaltyType = "WildDrawFour";

                    MoveToNextTurn(game);

                    return Ok(ToGameResponse(
                        game,
                        request.PlayerId
                    ));
                }

                // =================================================
                // SKIP / REVERSE / NORMAL
                // =================================================

                int turnSteps = 1;

                if (selectedCard.Value == "Skip")
                {
                    turnSteps = 2;
                }

                if (selectedCard.Value == "Reverse")
                {
                    game.Direction *= -1;

                    if (game.Players.Count == 2)
                    {
                        turnSteps = 2;
                    }
                }

                MoveToNextTurn(game, turnSteps);

                return Ok(ToGameResponse(game, request.PlayerId));
            }
        }

        // =====================================================
        // RESET GAME
        // =====================================================

        [HttpPost("reset/{roomCode}")]
        public IActionResult ResetGame(string roomCode)
        {
            roomCode = roomCode.Trim().ToUpper();

            lock (GameLock)
            {
                Games.Remove(roomCode);
            }

            return Ok(new
            {
                success = true,
                message = "Đã xóa GameState của phòng " + roomCode
            });
        }

        // =====================================================
        // CREATE GAME
        // =====================================================

        private static OnlineGameState CreateNewGame(
            RoomSnapshot room)
        {
            OnlineGameState game = new OnlineGameState
            {
                RoomCode = room.RoomCode,
                MaxPlayers = room.MaxPlayers,
                Status = "Playing",
                CurrentColor = "",
                Direction = 1,
                PendingDrawPenalty = 0,
                PendingPenaltyType = "",
                TurnDurationSeconds = TurnDurationSeconds,

                Players = room.Players.Select(player =>
                    new GamePlayerInfo
                    {
                        PlayerId = player.PlayerId,
                        DisplayName = player.DisplayName,
                        AvatarIndex = player.AvatarIndex,
                        IsHost = player.IsHost
                    }
                ).ToList()
            };

            game.Deck = CreateStandardUnoDeck();
            Shuffle(game.Deck);

            foreach (GamePlayerInfo player in game.Players)
            {
                game.Hands[player.PlayerId] =
                    new List<UnoCardData>();

                for (int i = 0; i < 7; i++)
                {
                    UnoCardData card = DrawCard(game);

                    if (card != null)
                    {
                        game.Hands[player.PlayerId].Add(card);
                    }
                }
            }

            UnoCardData firstCard =
                DrawFirstDiscardCard(game);

            if (firstCard != null)
            {
                game.DiscardPile.Add(firstCard);
                game.CurrentColor = firstCard.Color;
            }

            game.CurrentTurnPlayerId =
                game.Players[0].PlayerId;

            ResetTurnTimer(game);

            return game;
        }

        // =====================================================
        // DECK
        // =====================================================

        private static List<UnoCardData> CreateStandardUnoDeck()
        {
            List<UnoCardData> deck =
                new List<UnoCardData>();

            AddColorCards(deck, "Red");
            AddColorCards(deck, "Yellow");
            AddColorCards(deck, "Green");
            AddColorCards(deck, "Blue");

            for (int i = 0; i < 4; i++)
            {
                deck.Add(NewCard("Wild", "Wild"));
                deck.Add(NewCard("Wild", "WildDrawFour"));
            }

            return deck;
        }

        private static void AddColorCards(
            List<UnoCardData> deck,
            string color)
        {
            deck.Add(NewCard(color, "0"));

            string[] numbers =
            {
            "1", "2", "3", "4", "5",
            "6", "7", "8", "9"
        };

            foreach (string value in numbers)
            {
                deck.Add(NewCard(color, value));
                deck.Add(NewCard(color, value));
            }

            string[] actions =
            {
            "Skip",
            "Reverse",
            "DrawTwo"
        };

            foreach (string value in actions)
            {
                deck.Add(NewCard(color, value));
                deck.Add(NewCard(color, value));
            }
        }

        private static UnoCardData NewCard(
            string color,
            string value)
        {
            return new UnoCardData
            {
                CardId = Guid.NewGuid().ToString(),
                Color = color,
                Value = value
            };
        }

        private static void Shuffle(List<UnoCardData> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Shared.Next(i + 1);

                UnoCardData temp = cards[i];
                cards[i] = cards[randomIndex];
                cards[randomIndex] = temp;
            }
        }

        private static UnoCardData DrawCard(
            OnlineGameState game)
        {
            if (game.Deck.Count == 0)
            {
                RefillDeckFromDiscard(game);
            }

            if (game.Deck.Count == 0)
            {
                return null;
            }

            UnoCardData card = game.Deck[0];
            game.Deck.RemoveAt(0);

            return card;
        }

        private static int DrawCardsToPlayer(
            OnlineGameState game,
            string playerId,
            int amount)
        {
            if (!game.Hands.ContainsKey(playerId))
            {
                return 0;
            }

            int drawn = 0;

            for (int i = 0; i < amount; i++)
            {
                UnoCardData card = DrawCard(game);

                if (card == null)
                {
                    break;
                }

                game.Hands[playerId].Add(card);
                drawn++;
            }

            return drawn;
        }

        private static UnoCardData DrawFirstDiscardCard(
            OnlineGameState game)
        {
            int safeCount = 0;

            while (safeCount < 30)
            {
                UnoCardData card = DrawCard(game);

                if (card == null)
                {
                    return null;
                }

                // Lá đầu không là Wild để không phải chọn màu.
                if (card.Color != "Wild")
                {
                    return card;
                }

                game.Deck.Add(card);
                Shuffle(game.Deck);

                safeCount++;
            }

            return DrawCard(game);
        }

        private static void RefillDeckFromDiscard(
            OnlineGameState game)
        {
            if (game.DiscardPile.Count <= 1)
            {
                return;
            }

            UnoCardData topCard =
                game.DiscardPile[^1];

            List<UnoCardData> refillCards =
                game.DiscardPile
                    .Take(game.DiscardPile.Count - 1)
                    .ToList();

            game.DiscardPile.Clear();
            game.DiscardPile.Add(topCard);

            game.Deck.AddRange(refillCards);
            Shuffle(game.Deck);
        }

        // =====================================================
        // TURN / TIMER
        // =====================================================

        private static void MoveToNextTurn(
            OnlineGameState game,
            int steps = 1)
        {
            if (game.Players == null ||
                game.Players.Count == 0)
            {
                return;
            }

            int currentIndex =
                game.Players.FindIndex(
                    player =>
                        player.PlayerId ==
                        game.CurrentTurnPlayerId
                );

            if (currentIndex < 0)
            {
                game.CurrentTurnPlayerId =
                    game.Players[0].PlayerId;

                ResetTurnTimer(game);
                return;
            }

            int playerCount = game.Players.Count;

            int nextIndex =
                (currentIndex +
                game.Direction * steps) % playerCount;

            if (nextIndex < 0)
            {
                nextIndex += playerCount;
            }

            game.CurrentTurnPlayerId =
                game.Players[nextIndex].PlayerId;

            ResetTurnTimer(game);
        }

        private static void ResetTurnTimer(
            OnlineGameState game)
        {
            game.TurnDurationSeconds =
                TurnDurationSeconds;

            game.TurnEndsAtUnixMs =
                GetUtcNowUnixMs() +
                TurnDurationSeconds * 1000L;
        }

        private static void ProcessTurnTimeoutIfNeeded(
            OnlineGameState game)
        {
            if (game == null ||
                game.Status != "Playing" ||
                string.IsNullOrEmpty(game.CurrentTurnPlayerId))
            {
                return;
            }

            if (game.TurnEndsAtUnixMs <= 0)
            {
                ResetTurnTimer(game);
                return;
            }

            long now = GetUtcNowUnixMs();

            if (now < game.TurnEndsAtUnixMs)
            {
                return;
            }

            string timeoutPlayerId =
                game.CurrentTurnPlayerId;

            int drawCount = game.PendingDrawPenalty > 0
                ? game.PendingDrawPenalty
                : 1;

            DrawCardsToPlayer(
                game,
                timeoutPlayerId,
                drawCount
            );

            game.DeclaredUnoPlayers.Remove(timeoutPlayerId);

            game.PendingDrawPenalty = 0;
            game.PendingPenaltyType = "";

            MoveToNextTurn(game);

            Console.WriteLine(
                $"TURN TIMEOUT | room={game.RoomCode} | " +
                $"player={timeoutPlayerId} | " +
                $"draw={drawCount} | " +
                $"next={game.CurrentTurnPlayerId}"
            );
        }

        private static long GetUtcNowUnixMs()
        {
            return DateTimeOffset.UtcNow
                .ToUnixTimeMilliseconds();
        }

        // =====================================================
        // COLOR
        // =====================================================

        private static bool IsValidUnoColor(string color)
        {
            return color == "Red" ||
                   color == "Yellow" ||
                   color == "Green" ||
                   color == "Blue";
        }

        private static string NormalizeUnoColor(string color)
        {
            switch (color)
            {
                case "Red": return "Red";
                case "Yellow": return "Yellow";
                case "Green": return "Green";
                case "Blue": return "Blue";
                default: return "";
            }
        }

        // =====================================================
        // RESPONSE
        // =====================================================

        private static GameStateResponse ToGameResponse(
            OnlineGameState game,
            string currentPlayerId)
        {
            List<UnoCardData> myHand =
                new List<UnoCardData>();

            if (game.Hands.TryGetValue(
                currentPlayerId,
                out List<UnoCardData> hand))
            {
                myHand = hand;
            }

            UnoCardData topCard =
                game.DiscardPile.Count > 0
                    ? game.DiscardPile[^1]
                    : null;

            return new GameStateResponse
            {
                Success = true,
                Message = "Lấy GameState thành công.",

                RoomCode = game.RoomCode,
                Status = game.Status,

                CurrentTurnPlayerId =
                    game.CurrentTurnPlayerId,

                CurrentColor = game.CurrentColor,
                Direction = game.Direction,

                PendingDrawPenalty =
                    game.PendingDrawPenalty,

                PendingPenaltyType =
                    game.PendingPenaltyType,

                HasDeclaredUno =
                    game.DeclaredUnoPlayers.Contains(
                        currentPlayerId
                    ),

                DeclaredUnoPlayerIds =
                    game.DeclaredUnoPlayers.ToList(),

                // Timer gửi cho Unity.
                ServerTimeUnixMs =
                    GetUtcNowUnixMs(),

                TurnEndsAtUnixMs =
                    game.TurnEndsAtUnixMs,

                TurnDurationSeconds =
                    game.TurnDurationSeconds,

                DeckCount = game.Deck.Count,
                TopCard = topCard,
                MyHand = myHand,

                Players = game.Players.Select(player =>
                    new GamePlayerStateResponse
                    {
                        PlayerId = player.PlayerId,
                        DisplayName = player.DisplayName,
                        AvatarIndex = player.AvatarIndex,
                        IsHost = player.IsHost,

                        CardCount =
                            game.Hands.ContainsKey(player.PlayerId)
                                ? game.Hands[player.PlayerId].Count
                                : 0
                    }
                ).ToList()
            };
        }
    }

    // =====================================================
    // REQUEST MODELS
    // =====================================================

    public class StartGameRequest
    {
        public string RoomCode { get; set; } = "";
        public string PlayerId { get; set; } = "";
    }

    public class DrawCardRequest
    {
        public string RoomCode { get; set; } = "";
        public string PlayerId { get; set; } = "";
    }

    public class PlayCardRequest
    {
        public string RoomCode { get; set; } = "";
        public string PlayerId { get; set; } = "";
        public string CardId { get; set; } = "";
        public string ChosenColor { get; set; } = "";
    }

    public class DeclareUnoRequest
    {
        public string RoomCode { get; set; } = "";
        public string PlayerId { get; set; } = "";
    }

    // =====================================================
    // INTERNAL GAME MODELS
    // =====================================================

    public class OnlineGameState
    {
        public string RoomCode { get; set; } = "";
        public int MaxPlayers { get; set; }
        public string Status { get; set; } = "Playing";

        public string CurrentTurnPlayerId { get; set; } = "";
        public string CurrentColor { get; set; } = "";

        public int Direction { get; set; } = 1;

        public int PendingDrawPenalty { get; set; }
        public string PendingPenaltyType { get; set; } = "";

        public HashSet<string> DeclaredUnoPlayers { get; set; }
            = new HashSet<string>();

        // Timer.
        public long TurnEndsAtUnixMs { get; set; }

        // Phải là số 30, không được gán lại chính tên property.
        public int TurnDurationSeconds { get; set; } = 30;

        public List<GamePlayerInfo> Players { get; set; }
            = new List<GamePlayerInfo>();

        public Dictionary<string, List<UnoCardData>> Hands { get; set; }
            = new Dictionary<string, List<UnoCardData>>();

        public List<UnoCardData> Deck { get; set; }
            = new List<UnoCardData>();

        public List<UnoCardData> DiscardPile { get; set; }
            = new List<UnoCardData>();
    }

    public class GamePlayerInfo
    {
        public string PlayerId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int AvatarIndex { get; set; }
        public bool IsHost { get; set; }
    }

    public class UnoCardData
    {
        public string CardId { get; set; } = "";
        public string Color { get; set; } = "";
        public string Value { get; set; } = "";
    }

    // =====================================================
    // RESPONSE MODELS
    // =====================================================

    public class GameStateResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";

        public string RoomCode { get; set; } = "";
        public string Status { get; set; } = "";

        public string CurrentTurnPlayerId { get; set; } = "";
        public string CurrentColor { get; set; } = "";

        public int Direction { get; set; }

        public int PendingDrawPenalty { get; set; }
        public string PendingPenaltyType { get; set; } = "";

        public bool HasDeclaredUno { get; set; }

        public List<string> DeclaredUnoPlayerIds { get; set; }
            = new List<string>();

        public long ServerTimeUnixMs { get; set; }

        public long TurnEndsAtUnixMs { get; set; }

        public int TurnDurationSeconds { get; set; } = 30;

        public int DeckCount { get; set; }

        public UnoCardData TopCard { get; set; }

        public List<UnoCardData> MyHand { get; set; }
            = new List<UnoCardData>();

        public List<GamePlayerStateResponse> Players { get; set; }
            = new List<GamePlayerStateResponse>();
    }

    public class GamePlayerStateResponse
    {
        public string PlayerId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int AvatarIndex { get; set; }
        public bool IsHost { get; set; }
        public int CardCount { get; set; }
    }

}