using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WaitingSceneController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text[] playerSlotTexts;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private RectTransform circleLoading;
    [SerializeField] private Button cancelButton;

[Header("Scenes")]
    [SerializeField] private string homeSceneName = "Home";
    [SerializeField] private string onlineGameSceneName = "OnlineGame";

    [Header("Loading Animation")]
    [SerializeField] private float rotateSpeed = 180f;

    [Header("Room Polling")]
    [SerializeField] private float pollInterval = 1f;
    [SerializeField] private float delayBeforeGoToGame = 0.8f;

    private float pollTimer;
    private bool isGoingToGame;
    private bool isStartingGame;

    private string currentRoomCode = "";
    private string currentPlayerId = "";

    private int currentPlayers = 1;
    private int maxPlayers = 2;

    private RoomPlayerResponse[] currentRoomPlayers;

    private void Start()
    {
        currentRoomCode = PlayerPrefs.GetString("CurrentRoomCode", "----");
        currentPlayerId = PlayerPrefs.GetString("OnlinePlayerId", "");

        currentPlayers = PlayerPrefs.GetInt("CurrentRoomCurrentPlayers", 1);
        maxPlayers = PlayerPrefs.GetInt("CurrentRoomMaxPlayers", 2);

        if (OnlineRoomManager.Instance != null &&
            !string.IsNullOrEmpty(OnlineRoomManager.Instance.CurrentPlayerId))
        {
            currentPlayerId = OnlineRoomManager.Instance.CurrentPlayerId;
        }

        if (titleText != null)
        {
            titleText.text = "PHÒNG CHỜ";
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(CancelWaiting);
        }

        UpdateLobbyUI();
        GetRoomNow();
    }

    private void Update()
    {
        RotateLoadingCircle();

        if (isGoingToGame || isStartingGame)
        {
            return;
        }

        pollTimer += Time.deltaTime;

        if (pollTimer >= pollInterval)
        {
            pollTimer = 0f;
            GetRoomNow();
        }
    }

    private void RotateLoadingCircle()
    {
        if (circleLoading == null)
        {
            return;
        }

        circleLoading.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
    }

    private void UpdateLobbyUI()
    {
        if (roomCodeText != null)
        {
            roomCodeText.text = "MÃ PHÒNG: " + currentRoomCode;
        }

        UpdatePlayerSlots();

        if (currentPlayers >= maxPlayers)
        {
            SetStatus("Đã đủ người chơi. Đang tạo ván bài...");

            if (!isGoingToGame && !isStartingGame)
            {
                StartCoroutine(StartGameThenEnterOnlineGame());
            }
        }
        else
        {
            SetStatus("Đang chờ " + currentPlayers + "/" + maxPlayers + " người chơi");
        }
    }

    private void UpdatePlayerSlots()
    {
        if (playerSlotTexts == null || playerSlotTexts.Length == 0)
        {
            return;
        }

        for (int i = 0; i < playerSlotTexts.Length; i++)
        {
            if (playerSlotTexts[i] == null)
            {
                continue;
            }

            if (i >= maxPlayers)
            {
                playerSlotTexts[i].gameObject.SetActive(false);
                continue;
            }

            playerSlotTexts[i].gameObject.SetActive(true);

            if (currentRoomPlayers != null &&
                i < currentRoomPlayers.Length &&
                currentRoomPlayers[i] != null)
            {
                string playerName = currentRoomPlayers[i].displayName;

                if (string.IsNullOrEmpty(playerName))
                {
                    playerName = "Người chơi " + (i + 1);
                }

                playerSlotTexts[i].text = currentRoomPlayers[i].isHost
                    ? playerName + " (HOST)"
                    : playerName;
            }
            else
            {
                playerSlotTexts[i].text = "Đang chờ người chơi...";
            }
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void GetRoomNow()
    {
        if (string.IsNullOrEmpty(currentRoomCode) || currentRoomCode == "----")
        {
            SetStatus("Chưa có mã phòng.");
            return;
        }

        StartCoroutine(RoomApiService.GetRoom(
            currentRoomCode,
            onSuccess: roomResponse =>
            {
                if (roomResponse == null)
                {
                    SetStatus("Không lấy được thông tin phòng.");
                    return;
                }

                currentRoomCode = roomResponse.roomCode;
                maxPlayers = roomResponse.maxPlayers;
                currentRoomPlayers = roomResponse.players;

                if (currentRoomPlayers != null &&
                    currentRoomPlayers.Length > 0)
                {
                    currentPlayers = currentRoomPlayers.Length;
                }
                else
                {
                    currentPlayers = roomResponse.currentPlayers;
                }

                if (string.IsNullOrEmpty(currentRoomCode))
                {
                    currentRoomCode = PlayerPrefs.GetString(
                        "CurrentRoomCode",
                        "----"
                    );
                }

                if (currentPlayers <= 0)
                {
                    currentPlayers = 1;
                }

                if (maxPlayers <= 0)
                {
                    maxPlayers = 2;
                }

                PlayerPrefs.SetString("CurrentRoomCode", currentRoomCode);
                PlayerPrefs.SetInt(
                    "CurrentRoomCurrentPlayers",
                    currentPlayers
                );
                PlayerPrefs.SetInt(
                    "CurrentRoomMaxPlayers",
                    maxPlayers
                );
                PlayerPrefs.Save();

                Debug.Log(
                    "WaitingScene room = " + currentRoomCode +
                    " | players = " + currentPlayers + "/" + maxPlayers
                );

                UpdateLobbyUI();
            },
            onError: error =>
            {
                Debug.LogError("Get room lỗi: " + error);
                SetStatus("Không lấy được thông tin phòng.");
            }
        ));
    }

    private IEnumerator StartGameThenEnterOnlineGame()
    {
        isStartingGame = true;

        if (string.IsNullOrEmpty(currentPlayerId))
        {
            currentPlayerId = PlayerPrefs.GetString("OnlinePlayerId", "");
        }

        if (string.IsNullOrEmpty(currentPlayerId))
        {
            SetStatus("Không tìm thấy Player ID.");
            isStartingGame = false;
            yield break;
        }

        SetStatus("Đang chia bài...");

        yield return StartCoroutine(GameApiService.StartGame(
            currentRoomCode,
            currentPlayerId,
            onSuccess: gameState =>
            {
                Debug.Log(
                    "Start Game thành công | Room = " +
                    gameState.roomCode +
                    " | My Hand = " +
                    (gameState.myHand != null
                        ? gameState.myHand.Length
                        : 0)
                );

                isGoingToGame = true;
            },
            onError: error =>
            {
                Debug.LogError("Start Game lỗi: " + error);
                SetStatus("Không tạo được ván bài. Đang thử lại...");
                isStartingGame = false;
            }
        ));

        if (!isGoingToGame)
        {
            yield break;
        }

        yield return new WaitForSeconds(delayBeforeGoToGame);

        SceneManager.LoadScene(onlineGameSceneName);
    }

    private void CancelWaiting()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(homeSceneName);
    }

}
