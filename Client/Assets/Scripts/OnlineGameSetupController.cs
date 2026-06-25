using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OnlineGameSetupController : MonoBehaviour
{
    [System.Serializable]
    public class OnlinePlayerSlot
    {
        public GameObject rootObject;
        public Image avatarImage;
        public TMP_Text nameText;
        public GameObject waitingObject;
    }

[Header("Player Slots")]
    [SerializeField] private OnlinePlayerSlot playerSlot;
    [SerializeField] private OnlinePlayerSlot leftSlot;
    [SerializeField] private OnlinePlayerSlot topSlot;
    [SerializeField] private OnlinePlayerSlot rightSlot;

    [Header("Avatar Sprites")]
    [SerializeField] private Sprite[] avatarSprites;

    [Header("Default")]
    [SerializeField] private string defaultPlayerName = "Player";
    [SerializeField] private string waitingText = "Waiting...";

    [Header("Game State Polling")]
    [SerializeField] private float gamePollInterval = 0.7f;

    [Header("Optional Debug UI")]
    [SerializeField] private TMP_Text topCardText;
    [SerializeField] private TMP_Text deckCountText;
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text myCardCountText;
    [SerializeField] private TMP_Text opponentCardCountText;

    public OnlineGameStateResponse CurrentGameState { get; private set; }

    public bool IsMyTurn
    {
        get
        {
            return CurrentGameState != null &&
                   CurrentGameState.currentTurnPlayerId == currentPlayerId;
        }
    }

    private string currentRoomCode;
    private string currentPlayerId;

    private int maxPlayers = 2;
    private RoomPlayerResponse[] roomPlayers;

    private bool isGettingGameState;

    private void Start()
    {
        currentRoomCode = PlayerPrefs.GetString("CurrentRoomCode", "");
        currentPlayerId = PlayerPrefs.GetString("OnlinePlayerId", "");

        if (OnlineRoomManager.Instance != null &&
            !string.IsNullOrEmpty(OnlineRoomManager.Instance.CurrentPlayerId))
        {
            currentPlayerId = OnlineRoomManager.Instance.CurrentPlayerId;
        }

        LoadFallbackData();
        SetupFallbackSlots();

        GetRoomAndSetupPlayers();

        StartCoroutine(GameStatePollingRoutine());
    }

    private void LoadFallbackData()
    {
        maxPlayers = PlayerPrefs.GetInt("CurrentRoomMaxPlayers", 2);

        if (maxPlayers < 2)
        {
            maxPlayers = 2;
        }

        if (maxPlayers > 4)
        {
            maxPlayers = 4;
        }
    }

    private IEnumerator GameStatePollingRoutine()
    {
        yield return new WaitForSeconds(0.15f);

        while (true)
        {
            if (!isGettingGameState)
            {
                yield return StartCoroutine(GetGameStateNow());
            }

            yield return new WaitForSeconds(gamePollInterval);
        }
    }

    private IEnumerator GetGameStateNow()
    {
        if (string.IsNullOrEmpty(currentRoomCode))
        {
            yield break;
        }

        if (string.IsNullOrEmpty(currentPlayerId))
        {
            Debug.LogWarning("OnlineGameSetup: chưa có OnlinePlayerId.");
            yield break;
        }

        isGettingGameState = true;

        yield return StartCoroutine(GameApiService.GetGameState(
            currentRoomCode,
            currentPlayerId,
            onSuccess: gameState =>
            {
                CurrentGameState = gameState;

                Debug.Log(
                    "Online GameState | room = " +
                    gameState.roomCode +
                    " | myHand = " +
                    (gameState.myHand != null
                        ? gameState.myHand.Length
                        : 0) +
                    " | topCard = " +
                    GetCardDisplayText(gameState.topCard) +
                    " | myTurn = " + IsMyTurn
                );

                UpdateGameInfoUI();
            },
            onError: error =>
            {
                Debug.LogWarning("Get GameState lỗi: " + error);
            }
        ));

        isGettingGameState = false;
    }

    private void UpdateGameInfoUI()
    {
        if (CurrentGameState == null)
        {
            return;
        }

        if (topCardText != null)
        {
            topCardText.text = GetCardDisplayText(CurrentGameState.topCard);
        }

        if (deckCountText != null)
        {
            deckCountText.text = "Bộ bài: " + CurrentGameState.deckCount;
        }

        if (turnText != null)
        {
            turnText.text = IsMyTurn
                ? "Lượt của bạn"
                : "Đang chờ đối thủ";
        }

        int myCardCount = CurrentGameState.myHand != null
            ? CurrentGameState.myHand.Length
            : 0;

        if (myCardCountText != null)
        {
            myCardCountText.text = "Bài của bạn: " + myCardCount;
        }

        int opponentCardCount = GetOpponentCardCount();

        if (opponentCardCountText != null)
        {
            opponentCardCountText.text = "Bài đối thủ: " + opponentCardCount;
        }
    }

    private int GetOpponentCardCount()
    {
        if (CurrentGameState == null ||
            CurrentGameState.players == null)
        {
            return 0;
        }

        for (int i = 0; i < CurrentGameState.players.Length; i++)
        {
            OnlineGamePlayerStateResponse player =
                CurrentGameState.players[i];

            if (player == null ||
                player.playerId == currentPlayerId)
            {
                continue;
            }

            return player.cardCount;
        }

        return 0;
    }

    private string GetCardDisplayText(OnlineApiCardData card)
    {
        if (card == null)
        {
            return "Chưa có lá bài";
        }

        return card.color + " " + card.value;
    }

    private void GetRoomAndSetupPlayers()
    {
        if (string.IsNullOrEmpty(currentRoomCode))
        {
            Debug.LogWarning(
                "OnlineGameSetup: Chưa có CurrentRoomCode, dùng fallback."
            );

            return;
        }

        StartCoroutine(RoomApiService.GetRoom(
            currentRoomCode,
            onSuccess: room =>
            {
                if (room == null)
                {
                    Debug.LogError("OnlineGameSetup: room null.");
                    return;
                }

                maxPlayers = room.maxPlayers;

                if (maxPlayers < 2)
                {
                    maxPlayers = 2;
                }

                if (maxPlayers > 4)
                {
                    maxPlayers = 4;
                }

                roomPlayers = room.players;

                SetupOnlinePlayersFromRoom();
            },
            onError: error =>
            {
                Debug.LogError("OnlineGameSetup GetRoom lỗi: " + error);
            }
        ));
    }

    private void SetupFallbackSlots()
    {
        string myName = GetLocalPlayerName();
        int myAvatarIndex = PlayerPrefs.GetInt("PlayerAvatarIndex", 0);

        SetupSlot(
            playerSlot,
            true,
            true,
            myName,
            myAvatarIndex
        );

        SetupSlot(leftSlot, maxPlayers >= 3, false, waitingText, 0);
        SetupSlot(topSlot, maxPlayers >= 2, false, waitingText, 0);
        SetupSlot(rightSlot, maxPlayers >= 4, false, waitingText, 0);
    }

    private void SetupOnlinePlayersFromRoom()
    {
        if (roomPlayers == null || roomPlayers.Length == 0)
        {
            SetupFallbackSlots();
            return;
        }

        RoomPlayerResponse me = FindCurrentPlayer();

        if (me == null)
        {
            me = roomPlayers[0];
        }

        RoomPlayerResponse[] others = GetOtherPlayers(me);

        SetupSlot(
            playerSlot,
            true,
            true,
            GetPlayerDisplayName(me, GetLocalPlayerName()),
            me.avatarIndex
        );

        if (maxPlayers == 2)
        {
            SetupSlot(leftSlot, false, false, waitingText, 0);
            SetupSlot(rightSlot, false, false, waitingText, 0);

            SetupOtherSlot(topSlot, others, 0, true);
        }
        else if (maxPlayers == 3)
        {
            SetupSlot(topSlot, false, false, waitingText, 0);

            SetupOtherSlot(leftSlot, others, 0, true);
            SetupOtherSlot(rightSlot, others, 1, true);
        }
        else
        {
            SetupOtherSlot(leftSlot, others, 0, true);
            SetupOtherSlot(topSlot, others, 1, true);
            SetupOtherSlot(rightSlot, others, 2, true);
        }
    }

    private RoomPlayerResponse FindCurrentPlayer()
    {
        if (roomPlayers == null)
        {
            return null;
        }

        for (int i = 0; i < roomPlayers.Length; i++)
        {
            if (roomPlayers[i] == null)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(currentPlayerId) &&
                roomPlayers[i].playerId == currentPlayerId)
            {
                return roomPlayers[i];
            }
        }

        return null;
    }

    private RoomPlayerResponse[] GetOtherPlayers(RoomPlayerResponse me)
    {
        if (roomPlayers == null)
        {
            return new RoomPlayerResponse[0];
        }

        int count = 0;

        for (int i = 0; i < roomPlayers.Length; i++)
        {
            if (roomPlayers[i] == null)
            {
                continue;
            }

            if (me != null &&
                roomPlayers[i].playerId == me.playerId)
            {
                continue;
            }

            count++;
        }

        RoomPlayerResponse[] others =
            new RoomPlayerResponse[count];

        int index = 0;

        for (int i = 0; i < roomPlayers.Length; i++)
        {
            if (roomPlayers[i] == null)
            {
                continue;
            }

            if (me != null &&
                roomPlayers[i].playerId == me.playerId)
            {
                continue;
            }

            others[index] = roomPlayers[i];
            index++;
        }

        return others;
    }

    private void SetupOtherSlot(
        OnlinePlayerSlot slot,
        RoomPlayerResponse[] others,
        int otherIndex,
        bool slotActive
    )
    {
        if (!slotActive)
        {
            SetupSlot(slot, false, false, waitingText, 0);
            return;
        }

        if (others != null &&
            otherIndex >= 0 &&
            otherIndex < others.Length &&
            others[otherIndex] != null)
        {
            SetupSlot(
                slot,
                true,
                true,
                GetPlayerDisplayName(
                    others[otherIndex],
                    "Người chơi " + (otherIndex + 2)
                ),
                others[otherIndex].avatarIndex
            );
        }
        else
        {
            SetupSlot(slot, true, false, waitingText, 0);
        }
    }

    private void SetupSlot(
        OnlinePlayerSlot slot,
        bool isActive,
        bool isJoined,
        string playerName,
        int avatarIndex
    )
    {
        if (slot == null)
        {
            return;
        }

        if (slot.rootObject != null)
        {
            slot.rootObject.SetActive(isActive);
        }

        if (!isActive)
        {
            return;
        }

        if (slot.waitingObject != null)
        {
            slot.waitingObject.SetActive(!isJoined);
        }

        if (slot.nameText != null)
        {
            slot.nameText.text = isJoined
                ? playerName
                : waitingText;
        }

        if (slot.avatarImage != null)
        {
            if (isJoined)
            {
                Sprite avatarSprite = GetAvatarSprite(avatarIndex);

                if (avatarSprite != null)
                {
                    slot.avatarImage.sprite = avatarSprite;
                }

                slot.avatarImage.color = Color.white;
                slot.avatarImage.preserveAspect = true;
            }
            else
            {
                slot.avatarImage.color =
                    new Color(1f, 1f, 1f, 0.35f);
            }
        }
    }

    private Sprite GetAvatarSprite(int avatarIndex)
    {
        if (avatarSprites == null || avatarSprites.Length == 0)
        {
            return null;
        }

        if (avatarIndex < 0 || avatarIndex >= avatarSprites.Length)
        {
            avatarIndex = 0;
        }

        return avatarSprites[avatarIndex];
    }

    private string GetPlayerDisplayName(
        RoomPlayerResponse player,
        string fallback
    )
    {
        if (player == null)
        {
            return fallback;
        }

        string playerName = player.displayName;

        if (string.IsNullOrEmpty(playerName))
        {
            playerName = fallback;
        }

        return player.isHost
            ? playerName + " (HOST)"
            : playerName;
    }

    private string GetLocalPlayerName()
    {
        string playerName = PlayerPrefs.GetString("PlayerName", "");

        if (string.IsNullOrEmpty(playerName))
        {
            playerName = PlayerPrefs.GetString("displayName", "");
        }

        if (string.IsNullOrEmpty(playerName))
        {
            playerName = PlayerPrefs.GetString(
                "username",
                defaultPlayerName
            );
        }

        return playerName;
    }

}
