using System;

[Serializable]
public class CreateRoomRequest
{
    public int maxPlayers;
    public string playerId;
    public string displayName;
    public int avatarIndex;
}

[Serializable]
public class JoinRoomRequest
{
    public string roomCode;
    public string playerId;
    public string displayName;
    public int avatarIndex;
}

[Serializable]
public class LeaveRoomRequest
{
    public string roomCode;
    public string playerId;
}

[Serializable]
public class RoomPlayerResponse
{
    public string playerId;
    public string displayName;
    public int avatarIndex;
    public bool isHost;
}

[Serializable]
public class RoomResponse
{
    public bool success;
    public string message;

    public string roomCode;
    public int currentPlayers;
    public int maxPlayers;

    public string status;
    public bool isFull;
    public RoomPlayerResponse[] players;
}

