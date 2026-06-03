using System;
using UnityEngine;

public class OnlineRoomManager : MonoBehaviour
{
    public static OnlineRoomManager Instance { get; private set; }

    [Header("Backend")]
    [SerializeField] private string backendBaseUrl = "https://detest-spindle-bonelike.ngrok-free.dev";

    private const string PlayerNameKey = "PlayerName";
    private const string PlayerAvatarIndexKey = "PlayerAvatarIndex";

    private const string OnlinePlayerIdKey = "OnlinePlayerId";
    private const string OnlineRoomCodeKey = "OnlineRoomCode";

    // Key mới để WaitingScene đọc
    private const string CurrentRoomCodeKey = "CurrentRoomCode";
    private const string CurrentRoomCurrentPlayersKey = "CurrentRoomCurrentPlayers";
    private const string CurrentRoomMaxPlayersKey = "CurrentRoomMaxPlayers";

    public string CurrentRoomCode { get; private set; }
    public string CurrentPlayerId { get; private set; }
    public int CurrentPlayers { get; private set; }
    public int MaxPlayers { get; private set; }

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

        CurrentRoomCode = PlayerPrefs.GetString(CurrentRoomCodeKey, "");
        CurrentPlayers = PlayerPrefs.GetInt(CurrentRoomCurrentPlayersKey, 1);
        MaxPlayers = PlayerPrefs.GetInt(CurrentRoomMaxPlayersKey, 2);
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
                SaveRoom(room, fallbackCurrentPlayers: 1, fallbackMaxPlayers: maxPlayers);

                Debug.Log("Tạo phòng thành công. RoomCode = " + CurrentRoomCode);

                onSuccess?.Invoke(room);
            },
            error =>
            {
                Debug.LogError("Tạo phòng lỗi: " + error);
                onError?.Invoke(error);
            }));
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
                SaveRoom(room, fallbackCurrentPlayers: 2, fallbackMaxPlayers: 2);

                Debug.Log("Join phòng thành công. RoomCode = " + CurrentRoomCode);

                onSuccess?.Invoke(room);
            },
            error =>
            {
                Debug.LogError("Join phòng lỗi: " + error);
                onError?.Invoke(error);
            }));
    }

    public void GetCurrentRoom(
        Action<RoomResponse> onSuccess,
        Action<string> onError)
    {
        if (string.IsNullOrEmpty(CurrentRoomCode))
        {
            CurrentRoomCode = PlayerPrefs.GetString(CurrentRoomCodeKey, "");
        }

        if (string.IsNullOrEmpty(CurrentRoomCode))
        {
            onError?.Invoke("Chưa có RoomCode.");
            return;
        }

        StartCoroutine(RoomApiService.GetRoom(
            CurrentRoomCode,
            room =>
            {
                SaveRoom(room, fallbackCurrentPlayers: CurrentPlayers, fallbackMaxPlayers: MaxPlayers);
                onSuccess?.Invoke(room);
            },
            onError));
    }

    public void LeaveCurrentRoom(
        Action<string> onSuccess,
        Action<string> onError)
    {
        if (string.IsNullOrEmpty(CurrentRoomCode))
        {
            CurrentRoomCode = PlayerPrefs.GetString(CurrentRoomCodeKey, "");
        }

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
                ClearRoomData();

                onSuccess?.Invoke(result);
            },
            error =>
            {
                Debug.LogError("Rời phòng lỗi: " + error);
                onError?.Invoke(error);
            }));
    }

    private void SaveRoom(RoomResponse room, int fallbackCurrentPlayers, int fallbackMaxPlayers)
    {
        if (room == null)
        {
            Debug.LogError("SaveRoom lỗi: room null.");
            return;
        }

        CurrentRoomCode = room.roomCode;

        if (string.IsNullOrEmpty(CurrentRoomCode))
        {
            Debug.LogError("SaveRoom lỗi: roomCode rỗng.");
            return;
        }

        CurrentPlayers = room.currentPlayers;
        MaxPlayers = room.maxPlayers;

        if (CurrentPlayers <= 0)
        {
            CurrentPlayers = fallbackCurrentPlayers;
        }

        if (MaxPlayers <= 0)
        {
            MaxPlayers = fallbackMaxPlayers;
        }

        // Key cũ, giữ lại nếu file khác còn dùng
        PlayerPrefs.SetString(OnlineRoomCodeKey, CurrentRoomCode);

        // Key mới cho WaitingScene
        PlayerPrefs.SetString(CurrentRoomCodeKey, CurrentRoomCode);
        PlayerPrefs.SetInt(CurrentRoomCurrentPlayersKey, CurrentPlayers);
        PlayerPrefs.SetInt(CurrentRoomMaxPlayersKey, MaxPlayers);

        PlayerPrefs.Save();

        Debug.Log("Đã lưu CurrentRoomCode = " + CurrentRoomCode);
        Debug.Log("Đã lưu CurrentPlayers = " + CurrentPlayers);
        Debug.Log("Đã lưu MaxPlayers = " + MaxPlayers);
    }

    private void ClearRoomData()
    {
        CurrentRoomCode = "";
        CurrentPlayers = 0;
        MaxPlayers = 0;

        PlayerPrefs.DeleteKey(OnlineRoomCodeKey);

        PlayerPrefs.DeleteKey(CurrentRoomCodeKey);
        PlayerPrefs.DeleteKey(CurrentRoomCurrentPlayersKey);
        PlayerPrefs.DeleteKey(CurrentRoomMaxPlayersKey);

        PlayerPrefs.Save();
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