using Microsoft.Extensions.Logging;
using Moq;
using UnoGame.API.Services;
using UnoGame.Core.DTOs;
using UnoGame.Core.Interfaces;
using UnoGame.Core.Models;
using UnoGame.Core.Room;
using Xunit;

namespace UnoGame.API.Tests;

/// <summary>
/// Unit tests cho RoomManager.
/// Kiểm tra tất cả scenarios: create, join, spectate, matchmaking, disconnect.
/// </summary>
public class RoomManagerTests
{
    // ─── Setup ────────────────────────────────────────────────────────────────

    private static (RoomManager mgr, Mock<IRoomService> roomSvc, Mock<IGameService> gameSvc)
        Build(RoomManagerOptions? opts = null)
    {
        var roomSvc = new Mock<IRoomService>();
        var userSvc = new Mock<IUserService>();
        var gameSvc = new Mock<IGameService>();
        var log     = new Mock<ILogger<RoomManager>>();

        // Default stubs
        roomSvc.Setup(r => r.CreateAsync(It.IsAny<string>(), It.IsAny<CreateRoomRequest>()))
            .ReturnsAsync((string hostId, CreateRoomRequest req) => new RoomDto
            {
                Id = $"room-{Guid.NewGuid():N}"[..12],
                RoomCode = "ABC123", HostId = hostId, HostName = "Host",
                MaxPlayers = req.MaxPlayers, BotCount = req.BotCount,
                BotDifficulty = req.BotDifficulty, Status = RoomStatus.Waiting,
                Players = new(), CreatedAt = DateTime.UtcNow
            });

        roomSvc.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new RoomDto
            {
                Id = id, RoomCode = "TST001", HostId = "host1", HostName = "Host",
                MaxPlayers = 4, BotCount = 0, BotDifficulty = "hard",
                Status = RoomStatus.Waiting, Players = new(), CreatedAt = DateTime.UtcNow
            });

        roomSvc.Setup(r => r.JoinAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new RoomDto { Id = "r1", Players = new(), RoomCode = "X" });

        roomSvc.Setup(r => r.LeaveAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        roomSvc.Setup(r => r.MarkReadyAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        roomSvc.Setup(r => r.CloseRoomAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        roomSvc.Setup(r => r.StartGameAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new RoomDto { Id = "r1", Players = new(), RoomCode = "X", Status = RoomStatus.Playing });
        roomSvc.Setup(r => r.KickPlayerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        roomSvc.Setup(r => r.UpdateSettingsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UpdateRoomSettingsRequest>()))
            .ReturnsAsync(new RoomDto { Id = "r1", Players = new(), RoomCode = "X" });
        roomSvc.Setup(r => r.IsPlayerInRoomAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        userSvc.Setup(u => u.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new UserDto { Id = id, DisplayName = $"User-{id}" });

        gameSvc.Setup(g => g.InitializeGameAsync(It.IsAny<string>(), It.IsAny<List<RoomPlayerDto>>()))
            .Returns(Task.CompletedTask);

        var mgr = new RoomManager(roomSvc.Object, userSvc.Object, gameSvc.Object, log.Object,
            opts ?? new RoomManagerOptions { ReconnectWindowSeconds = 5, MatchmakingTimeoutSeconds = 30 });

        return (mgr, roomSvc, gameSvc);
    }

    private static CreateRoomRequest DefaultReq(int max = 4, int bots = 0) =>
        new() { MaxPlayers = max, BotCount = bots, BotDifficulty = "hard" };

    // ════════════════════════════════════════════════════════════════════
    // CREATE ROOM
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateRoom_HostAdded_AsFirstSlot()
    {
        var (mgr, _, _) = Build();
        var state = await mgr.CreateRoomAsync("host1", "Host One", "", DefaultReq());

        Assert.Single(state.Slots, s => !s.IsBot);
        Assert.Equal("host1", state.Slots[0].SlotId);
        Assert.True(state.Slots[0].IsHost);
    }

    [Fact]
    public async Task CreateRoom_WithBots_SlotsCreated()
    {
        var (mgr, _, _) = Build();
        var state = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq(max: 4, bots: 2));

        Assert.Equal(2, state.Slots.Count(s => s.IsBot));
        Assert.All(state.Slots.Where(s => s.IsBot), b => Assert.True(b.IsReady));
    }

    [Fact]
    public async Task CreateRoom_UserRoom_Mapping_Updated()
    {
        var (mgr, _, _) = Build();
        var state = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq());

        Assert.True(mgr.IsInRoom("host1"));
        Assert.Equal(state.RoomId, mgr.GetRoomByUser("host1")?.RoomId);
    }

    [Fact]
    public async Task CreateRoom_LeavesOldRoom_First()
    {
        var (mgr, _, _) = Build();
        var room1 = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq());
        var room2 = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq());

        // host1 should be in room2 only
        Assert.Equal(room2.RoomId, mgr.GetRoomByUser("host1")?.RoomId);
        Assert.NotEqual(room1.RoomId, mgr.GetRoomByUser("host1")?.RoomId);
    }

    // ════════════════════════════════════════════════════════════════════
    // JOIN ROOM
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task JoinRoom_AddsPlayerSlot()
    {
        var (mgr, _, _) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq());

        var result = await mgr.JoinRoomAsync(room.RoomId, "p2", "Player2", "");

        Assert.True(result.Success);
        Assert.Equal(2, result.Room!.HumanPlayerCount);
        Assert.Contains(result.Room.Slots, s => s.SlotId == "p2");
    }

    [Fact]
    public async Task JoinRoom_AlreadyInRoom_Fails()
    {
        var (mgr, _, _) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq());
        await mgr.JoinRoomAsync(room.RoomId, "p2", "P2", "");

        var result = await mgr.JoinRoomAsync(room.RoomId, "p2", "P2", "");

        Assert.False(result.Success);
        Assert.Equal(JoinRoomOutcome.AlreadyInRoom, result.Outcome);
    }

    [Fact]
    public async Task JoinRoom_Full_Fails()
    {
        var (mgr, _, _) = Build();
        // max 2 players
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq(max: 2));
        await mgr.JoinRoomAsync(room.RoomId, "p2", "P2", "");

        // 3rd player should fail
        var result = await mgr.JoinRoomAsync(room.RoomId, "p3", "P3", "");

        Assert.False(result.Success);
        Assert.Equal(JoinRoomOutcome.RoomFull, result.Outcome);
    }

    [Fact]
    public async Task JoinRoom_GameInProgress_Fails()
    {
        var (mgr, _, _) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq(max: 4, bots: 1));
        var state = mgr.GetRoomState(room.RoomId)!;
        state.Slots[0].IsReady = true;
        await mgr.TryStartGameAsync(room.RoomId, "host1");

        var result = await mgr.JoinRoomAsync(room.RoomId, "p3", "P3", "");

        Assert.False(result.Success);
        Assert.Equal(JoinRoomOutcome.GameInProgress, result.Outcome);
    }

    // ════════════════════════════════════════════════════════════════════
    // SPECTATOR
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task JoinAsSpectator_AddsToSpectatorList()
    {
        var (mgr, _, _) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq());

        var result = await mgr.JoinAsSpectatorAsync(room.RoomId, "spec1", "Spectator", "");

        Assert.True(result.Success);
        Assert.Contains(result.Room!.Spectators, s => s.UserId == "spec1");
        Assert.DoesNotContain(result.Room.Slots, s => s.SlotId == "spec1");
    }

    [Fact]
    public async Task JoinAsSpectator_AlreadyPlayer_Fails()
    {
        var (mgr, _, _) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq());

        var result = await mgr.JoinAsSpectatorAsync(room.RoomId, "host1", "Host", "");

        Assert.False(result.Success);
        Assert.Equal(JoinRoomOutcome.AlreadyInRoom, result.Outcome);
    }

    [Fact]
    public async Task PromoteSpectator_MovesToPlayerSlot()
    {
        var (mgr, _, _) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq());
        await mgr.JoinAsSpectatorAsync(room.RoomId, "spec1", "Spectator", "");

        await mgr.PromoteSpectatorAsync(room.RoomId, "spec1");
        var state = mgr.GetRoomState(room.RoomId)!;

        Assert.Contains(state.Slots, s => s.SlotId == "spec1");
        Assert.DoesNotContain(state.Spectators, s => s.UserId == "spec1");
    }

    [Fact]
    public async Task DemoteToSpectator_MovesFromPlayerToSpectator()
    {
        var (mgr, _, _) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq());
        await mgr.JoinRoomAsync(room.RoomId, "p2", "P2", "");

        await mgr.DemoteToSpectatorAsync(room.RoomId, "p2");
        var state = mgr.GetRoomState(room.RoomId)!;

        Assert.Contains(state.Spectators, s => s.UserId == "p2");
        Assert.DoesNotContain(state.Slots, s => s.SlotId == "p2");
    }

    // ════════════════════════════════════════════════════════════════════
    // LEAVE / HOST TRANSFER
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LeaveRoom_HostLeaves_HostTransferred()
    {
        var (mgr, _, _) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq());
        await mgr.JoinRoomAsync(room.RoomId, "p2", "P2", "");

        await mgr.LeaveRoomAsync(room.RoomId, "host1");
        var state = mgr.GetRoomState(room.RoomId)!;

        Assert.Equal("p2", state.HostId);
        Assert.True(state.GetSlot("p2")!.IsHost);
    }

    [Fact]
    public async Task LeaveRoom_LastPlayer_RoomClosed()
    {
        var (mgr, _, _) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq());

        await mgr.LeaveRoomAsync(room.RoomId, "host1");

        Assert.Null(mgr.GetRoomState(room.RoomId));
        Assert.False(mgr.IsInRoom("host1"));
    }

    // ════════════════════════════════════════════════════════════════════
    // DISCONNECT / RECONNECT
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task HandleDisconnect_InGame_CreatesRecord()
    {
        var (mgr, _, _) = Build();
        var room  = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq(bots: 1));
        var state = mgr.GetRoomState(room.RoomId)!;
        state.Phase = RoomPhase.Playing;   // simulate game running

        var result = await mgr.HandleDisconnectAsync("host1");

        Assert.Equal(DisconnectOutcome.Noted, result.Outcome);
        Assert.NotNull(result.Record);
        Assert.Equal("host1", result.Record!.UserId);
        Assert.False(result.Record.IsExpired);
        Assert.Equal(SlotStatus.Disconnected, state.GetSlot("host1")!.Status);
    }

    [Fact]
    public async Task HandleReconnect_WithinWindow_RestoresSlot()
    {
        var (mgr, _, _) = Build();
        var room  = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq(bots: 1));
        var state = mgr.GetRoomState(room.RoomId)!;
        state.Phase = RoomPhase.Playing;

        await mgr.HandleDisconnectAsync("host1");
        var reconnect = await mgr.HandleReconnectAsync("host1", room.RoomId);

        Assert.Equal(ReconnectOutcome.Success, reconnect.Outcome);
        Assert.Equal(SlotStatus.Connected, state.GetSlot("host1")!.Status);
    }

    [Fact]
    public async Task HandleReconnect_NotDisconnected_ReturnsNotDisconnected()
    {
        var (mgr, _, _) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq());

        var result = await mgr.HandleReconnectAsync("host1", room.RoomId);

        Assert.Equal(ReconnectOutcome.NotDisconnected, result.Outcome);
    }

    [Fact]
    public async Task HandleDisconnect_InLobby_RemovesPlayer()
    {
        var (mgr, _, _) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq());
        await mgr.JoinRoomAsync(room.RoomId, "p2", "P2", "");

        // p2 disconnects in lobby
        await mgr.HandleDisconnectAsync("p2");

        // Give async leave time to execute
        await Task.Delay(50);
        var state = mgr.GetRoomState(room.RoomId);
        Assert.True(state is null || !state.IsPlayer("p2"));
    }

    // ════════════════════════════════════════════════════════════════════
    // READY / START GAME
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ToggleReady_ChangesReadyStatus()
    {
        var (mgr, _, _) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq());
        await mgr.JoinRoomAsync(room.RoomId, "p2", "P2", "");

        await mgr.ToggleReadyAsync(room.RoomId, "p2");
        var slot = mgr.GetRoomState(room.RoomId)!.GetSlot("p2")!;
        Assert.True(slot.IsReady);

        await mgr.ToggleReadyAsync(room.RoomId, "p2");
        Assert.False(slot.IsReady);
    }

    [Fact]
    public async Task TryStartGame_AllReady_Succeeds()
    {
        var (mgr, _, gameSvc) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq(max: 2));
        await mgr.JoinRoomAsync(room.RoomId, "p2", "P2", "");
        await mgr.ToggleReadyAsync(room.RoomId, "p2");

        bool started = await mgr.TryStartGameAsync(room.RoomId, "host1");

        Assert.True(started);
        Assert.Equal(RoomPhase.Playing, mgr.GetRoomState(room.RoomId)!.Phase);
        gameSvc.Verify(g => g.InitializeGameAsync(room.RoomId, It.IsAny<List<RoomPlayerDto>>()), Times.Once);
    }

    [Fact]
    public async Task TryStartGame_NotReady_Fails()
    {
        var (mgr, _, _) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq(max: 2));
        await mgr.JoinRoomAsync(room.RoomId, "p2", "P2", "");
        // p2 not ready

        bool started = await mgr.TryStartGameAsync(room.RoomId, "host1");
        Assert.False(started);
    }

    [Fact]
    public async Task TryStartGame_NonHost_Fails()
    {
        var (mgr, _, _) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq(max: 2));
        await mgr.JoinRoomAsync(room.RoomId, "p2", "P2", "");

        bool started = await mgr.TryStartGameAsync(room.RoomId, "p2");
        Assert.False(started);
    }

    // ════════════════════════════════════════════════════════════════════
    // MATCHMAKING
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EnqueueMatchmaking_CreatesTicket()
    {
        var (mgr, _, _) = Build();
        var ticket = await mgr.EnqueueMatchmakingAsync("u1", "User1", "", 4, true);

        Assert.NotNull(ticket);
        Assert.Equal("u1", ticket.UserId);
        Assert.True(mgr.IsInMatchmaking("u1"));
    }

    [Fact]
    public async Task CancelMatchmaking_RemovesTicket()
    {
        var (mgr, _, _) = Build();
        await mgr.EnqueueMatchmakingAsync("u1", "User1", "");
        await mgr.CancelMatchmakingAsync("u1");

        Assert.False(mgr.IsInMatchmaking("u1"));
    }

    [Fact]
    public async Task GetMatchmakingStatus_Queued_ReturnsPosition()
    {
        var (mgr, _, _) = Build();
        await mgr.EnqueueMatchmakingAsync("u1", "User1", "");
        await mgr.EnqueueMatchmakingAsync("u2", "User2", "");

        var status = await mgr.GetMatchmakingStatusAsync("u2");

        Assert.Equal(MatchmakingStatusEnum.Queued, status.Status);
        Assert.Equal(2, status.QueuePosition);
    }

    [Fact]
    public async Task GetMatchmakingStatus_NotQueued_ReturnsNotQueued()
    {
        var (mgr, _, _) = Build();
        var status = await mgr.GetMatchmakingStatusAsync("nobody");
        Assert.Equal(MatchmakingStatusEnum.NotQueued, status.Status);
    }

    // ════════════════════════════════════════════════════════════════════
    // KICK
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task KickPlayer_ByHost_RemovesSlot()
    {
        var (mgr, _, _) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq());
        await mgr.JoinRoomAsync(room.RoomId, "p2", "P2", "");

        await mgr.KickPlayerAsync(room.RoomId, "host1", "p2");
        var state = mgr.GetRoomState(room.RoomId)!;

        Assert.DoesNotContain(state.Slots, s => s.SlotId == "p2");
    }

    [Fact]
    public async Task KickPlayer_ByNonHost_Throws()
    {
        var (mgr, _, _) = Build();
        var room = await mgr.CreateRoomAsync("host1", "Host", "", DefaultReq());
        await mgr.JoinRoomAsync(room.RoomId, "p2", "P2", "");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mgr.KickPlayerAsync(room.RoomId, "p2", "host1"));
    }

    // ════════════════════════════════════════════════════════════════════
    // QUERIES
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetPublicWaitingRooms_OnlyWaiting()
    {
        var (mgr, _, _) = Build();
        var r1 = await mgr.CreateRoomAsync("h1", "Host1", "", new CreateRoomRequest { MaxPlayers = 4, BotCount = 1 });
        var r2 = await mgr.CreateRoomAsync("h2", "Host2", "", new CreateRoomRequest { MaxPlayers = 4, BotCount = 1 });
        mgr.GetRoomState(r1.RoomId)!.Phase = RoomPhase.Playing;

        var waiting = mgr.GetPublicWaitingRooms();

        Assert.DoesNotContain(waiting, r => r.RoomId == r1.RoomId);
        Assert.Contains(waiting, r => r.RoomId == r2.RoomId);
    }
}
