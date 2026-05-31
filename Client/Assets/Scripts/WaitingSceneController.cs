using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WaitingSceneController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text waitingText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private RectTransform circleLoading;
    [SerializeField] private Button cancelButton;

    [Header("Scenes")]
    [SerializeField] private string homeSceneName = "Home";
    [SerializeField] private string onlineGameSceneName = "OnlineGame";

    [Header("Loading Animation")]
    [SerializeField] private string waitingBaseText = "Vui lòng chờ";
    [SerializeField] private float dotChangeInterval = 0.4f;
    [SerializeField] private float rotateSpeed = 180f;

    [Header("Room Polling")]
    [SerializeField] private float pollInterval = 1f;
    [SerializeField] private float delayBeforeGoToGame = 0.8f;

    private OnlineRoomManager onlineRoomManager;

    private float dotTimer;
    private int dotCount = 1;

    private float pollTimer;
    private float goGameTimer;

    private bool isRoomReady;

    private void Awake()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(CancelWaiting);
        }
    }

    private void Start()
    {
        onlineRoomManager = OnlineRoomManager.Instance;

        if (onlineRoomManager == null)
        {
            onlineRoomManager = FindFirstObjectByType<OnlineRoomManager>();
        }

        dotTimer = 0f;
        dotCount = 1;
        pollTimer = 0f;
        goGameTimer = 0f;
        isRoomReady = false;

        UpdateWaitingText();
        UpdateRoomCodeText();

        GetRoomNow();
    }

    private void Update()
    {
        RotateLoadingCircle();
        AnimateWaitingDots();

        if (isRoomReady)
        {
            HandleGoToGame();
        }
        else
        {
            HandleRoomPolling();
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

    private void AnimateWaitingDots()
    {
        if (waitingText == null)
        {
            return;
        }

        dotTimer += Time.deltaTime;

        if (dotTimer >= dotChangeInterval)
        {
            dotTimer = 0f;
            dotCount++;

            if (dotCount > 3)
            {
                dotCount = 1;
            }

            UpdateWaitingText();
        }
    }

    private void UpdateWaitingText()
    {
        if (waitingText == null)
        {
            return;
        }

        string dots = "";

        for (int i = 0; i < dotCount; i++)
        {
            dots += ".";
        }

        if (isRoomReady)
        {
            waitingText.text = "Đã đủ người" + dots;
        }
        else
        {
            waitingText.text = waitingBaseText + dots;
        }
    }

    private void UpdateRoomCodeText()
    {
        if (roomCodeText == null)
        {
            return;
        }

        if (onlineRoomManager == null || string.IsNullOrEmpty(onlineRoomManager.CurrentRoomCode))
        {
            roomCodeText.text = "ROOM CODE: ----";
            return;
        }

        roomCodeText.text = "ROOM CODE: " + onlineRoomManager.CurrentRoomCode;
    }

    private void HandleRoomPolling()
    {
        pollTimer += Time.deltaTime;

        if (pollTimer >= pollInterval)
        {
            pollTimer = 0f;
            GetRoomNow();
        }
    }

    private void GetRoomNow()
    {
        if (onlineRoomManager == null)
        {
            ShowRoomError("Không tìm thấy OnlineRoomManager.");
            return;
        }

        onlineRoomManager.GetCurrentRoom(
            room =>
            {
                UpdateRoomUI(room);

                if (room.isFull || room.status == "Ready" || room.status == "Playing")
                {
                    isRoomReady = true;
                    goGameTimer = 0f;
                    UpdateWaitingText();
                }
            },
            error =>
            {
                Debug.LogError("Get room lỗi: " + error);
                ShowRoomError("Không lấy được thông tin phòng.");
            });
    }

    private void UpdateRoomUI(RoomResponse room)
    {
        if (room == null)
        {
            return;
        }

        if (roomCodeText != null)
        {
            roomCodeText.text = "ROOM CODE: " + room.roomCode;
        }

        int currentPlayers = 0;

        if (room.players != null)
        {
            currentPlayers = room.players.Length;
        }

        if (playerCountText != null)
        {
            playerCountText.text = "Đang chờ " + currentPlayers + "/" + room.maxPlayers + " người chơi";
        }
    }

    private void ShowRoomError(string message)
    {
        if (playerCountText != null)
        {
            playerCountText.text = message;
        }
    }

    private void HandleGoToGame()
    {
        goGameTimer += Time.deltaTime;

        if (goGameTimer >= delayBeforeGoToGame)
        {
            SceneManager.LoadScene(onlineGameSceneName);
        }
    }

    private void CancelWaiting()
    {
        if (onlineRoomManager == null)
        {
            SceneManager.LoadScene(homeSceneName);
            return;
        }

        onlineRoomManager.LeaveCurrentRoom(
            result =>
            {
                SceneManager.LoadScene(homeSceneName);
            },
            error =>
            {
                Debug.LogError("Leave room lỗi: " + error);
                SceneManager.LoadScene(homeSceneName);
            });
    }
}