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

    private float pollTimer = 0f;
    private bool isGoingToGame = false;

    private string currentRoomCode = "";
    private int currentPlayers = 1;
    private int maxPlayers = 2;

    private RoomPlayerResponse[] currentRoomPlayers;

    private void Start()
    {
        currentRoomCode = PlayerPrefs.GetString("CurrentRoomCode", "----");
        currentPlayers = PlayerPrefs.GetInt("CurrentRoomCurrentPlayers", 1);
        maxPlayers = PlayerPrefs.GetInt("CurrentRoomMaxPlayers", 2);

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

        if (isGoingToGame)
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
            SetStatus("Đã đủ người chơi. Đang vào trận...");

            if (!isGoingToGame)
            {
                StartCoroutine(GoToOnlineGameRoutine());
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
                string name = currentRoomPlayers[i].displayName;

                if (string.IsNullOrEmpty(name))
                {
                    name = "Người chơi " + (i + 1);
                }

                if (currentRoomPlayers[i].isHost)
                {
                    playerSlotTexts[i].text = name + " (HOST)";
                }
                else
                {
                    playerSlotTexts[i].text = name;
                }
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
            onSuccess: (roomResponse) =>
            {
                if (roomResponse == null)
                {
                    SetStatus("Không lấy được thông tin phòng.");
                    return;
                }

                currentRoomCode = roomResponse.roomCode;
                maxPlayers = roomResponse.maxPlayers;
                currentRoomPlayers = roomResponse.players;

                if (currentRoomPlayers != null && currentRoomPlayers.Length > 0)
                {
                    currentPlayers = currentRoomPlayers.Length;
                }
                else
                {
                    currentPlayers = roomResponse.currentPlayers;
                }

                if (string.IsNullOrEmpty(currentRoomCode))
                {
                    currentRoomCode = PlayerPrefs.GetString("CurrentRoomCode", "----");
                }

                if (currentPlayers <= 0)
                {
                    currentPlayers = PlayerPrefs.GetInt("CurrentRoomCurrentPlayers", 1);
                }

                if (currentPlayers <= 0)
                {
                    currentPlayers = 1;
                }

                if (maxPlayers <= 0)
                {
                    maxPlayers = PlayerPrefs.GetInt("CurrentRoomMaxPlayers", 2);
                }

                if (maxPlayers <= 0)
                {
                    maxPlayers = 2;
                }

                PlayerPrefs.SetString("CurrentRoomCode", currentRoomCode);
                PlayerPrefs.SetInt("CurrentRoomCurrentPlayers", currentPlayers);
                PlayerPrefs.SetInt("CurrentRoomMaxPlayers", maxPlayers);
                PlayerPrefs.Save();

                Debug.Log("WaitingScene roomCode = " + currentRoomCode);
                Debug.Log("WaitingScene players = " + currentPlayers + "/" + maxPlayers);

                UpdateLobbyUI();
            },
            onError: (error) =>
            {
                Debug.LogError("Get room lỗi: " + error);
                SetStatus("Không lấy được thông tin phòng.");
            }
        ));
    }

    private IEnumerator GoToOnlineGameRoutine()
    {
        isGoingToGame = true;

        yield return new WaitForSeconds(delayBeforeGoToGame);

        SceneManager.LoadScene(onlineGameSceneName);
    }

    private void CancelWaiting()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(homeSceneName);
    }
}