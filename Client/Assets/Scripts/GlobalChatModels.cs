using System;

[Serializable]
public class GlobalChatSendRequest
{
    public string playerId;
    public string displayName;
    public string message;
    public string roomCode;
}

[Serializable]
public class GlobalChatMessage
{
    public string id;
    public string playerId;
    public string displayName;
    public string message;
    public string roomCode;
    public string createdAt;
}

[Serializable]
public class GlobalChatListResponse
{
    public bool success;
    public string message;
    public GlobalChatMessage[] data;
}

[Serializable]
public class GlobalChatSendResponse
{
    public bool success;
    public string message;
    public GlobalChatMessage data;
}