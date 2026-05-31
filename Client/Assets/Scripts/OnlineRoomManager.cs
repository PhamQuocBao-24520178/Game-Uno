using System;
using UnityEngine;

public class OnlineRoomManager : MonoBehaviour
{
    public static OnlineRoomManager Instance { get; private set; }

    [Header("Backend")]
    [SerializeField] private string backendBaseUrl = "https://detest-spindle-bonelike.ngrok-free.dev";

    private const string PlayerNameKey = "PlayerName";
    private const string PlayerAvatarIndexKey = "PlayerAvatarIndex";
    private const string OnlinePlayerCountKey = "OnlinePlayerCount";

    private const string OnlinePlayerIdKey = "OnlinePlayerId";
    private const string OnlineRoomCodeKey = "OnlineRoomCode";

    public string CurrentRoomCode { get; private set; }
    public string CurrentPlayerId { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        RoomApiService.BaseUrl = backendBaseUrl;

        CurrentPlayerId = GetOrCreatePlayerId();
        CurrentRoomCode = PlayerPrefs.GetString(OnlineRoomCodeKey, "");
    }

    public void CreateRoom(
        int maxPlayers,
        Action<RoomResponse> onSuccess,
        Action<string> onError)
    {
        CreateRoomRequest request = new CreateRoomRequest
        {
            maxPlayers = maxPlayers,
            playerId = CurrentPlayerId,
            displayName = PlayerPrefs.GetString(PlayerNameKey, "Player"),
            avatarIndex = PlayerPrefs.GetInt(PlayerAvatarIndexKey, 0)
        };

        StartCoroutine(RoomApiService.CreateRoom(
            request,
            room =>
            {
                SaveRoom(room);
                onSuccess?.Invoke(room);
            },
            onError));
    }

    public void JoinRoom(
        string roomCode,
        Action<RoomResponse> onSuccess,
        Action<string> onError)
    {
        JoinRoomRequest request = new JoinRoomRequest
        {
            roomCode = roomCode,
            playerId = CurrentPlayerId,
            displayName = PlayerPrefs.GetString(PlayerNameKey, "Player"),
            avatarIndex = PlayerPrefs.GetInt(PlayerAvatarIndexKey, 0)
        };

        StartCoroutine(RoomApiService.JoinRoom(
            request,
            room =>
            {
                SaveRoom(room);
                onSuccess?.Invoke(room);
            },
            onError));
    }

    public void GetCurrentRoom(
        Action<RoomResponse> onSuccess,
        Action<string> onError)
    {
        if (string.IsNullOrEmpty(CurrentRoomCode))
        {
            onError?.Invoke("Chưa có RoomCode.");
            return;
        }

        StartCoroutine(RoomApiService.GetRoom(CurrentRoomCode, onSuccess, onError));
    }

    public void LeaveCurrentRoom(
        Action<string> onSuccess,
        Action<string> onError)
    {
        if (string.IsNullOrEmpty(CurrentRoomCode))
        {
            onSuccess?.Invoke("Không có phòng để rời.");
            return;
        }

        LeaveRoomRequest request = new LeaveRoomRequest
        {
            roomCode = CurrentRoomCode,
            playerId = CurrentPlayerId
        };

        StartCoroutine(RoomApiService.LeaveRoom(
            request,
            result =>
            {
                CurrentRoomCode = "";
                PlayerPrefs.DeleteKey(OnlineRoomCodeKey);
                PlayerPrefs.Save();

                onSuccess?.Invoke(result);
            },
            onError));
    }

    private void SaveRoom(RoomResponse room)
    {
        if (room == null)
        {
            return;
        }

        CurrentRoomCode = room.roomCode;

        PlayerPrefs.SetString(OnlineRoomCodeKey, CurrentRoomCode);
        PlayerPrefs.SetInt(OnlinePlayerCountKey, room.maxPlayers);
        PlayerPrefs.Save();

        Debug.Log("Room Code: " + CurrentRoomCode);
    }

    private string GetOrCreatePlayerId()
    {
        string savedId = PlayerPrefs.GetString(OnlinePlayerIdKey, "");

        if (!string.IsNullOrEmpty(savedId))
        {
            return savedId;
        }

        string newId = Guid.NewGuid().ToString();

        PlayerPrefs.SetString(OnlinePlayerIdKey, newId);
        PlayerPrefs.Save();

        return newId;
    }
}