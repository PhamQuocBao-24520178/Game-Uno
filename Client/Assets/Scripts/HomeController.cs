using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HomeController : MonoBehaviour
{
    [Header("Home UI")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMP_Text playerNameText;

    [Header("Avatar Sprites")]
    [SerializeField] private Sprite[] avatarSprites;

    [Header("Main Buttons")]
    [SerializeField] private Button vsComputerButton;
    [SerializeField] private Button avatarButton;
    [SerializeField] private Button onlineMultipleButton;

    [Header("Online Room")]
[SerializeField] private OnlineRoomManager onlineRoomManager;

    [Header("Setting Panel")]
    [SerializeField] private Button settingButton;
    [SerializeField] private GameObject settingPanel;
    [SerializeField] private Button closeSettingButton;

    [Header("Music Setting")]
    [SerializeField] private Button musicButton;
    [SerializeField] private Image musicButtonImage;
    [SerializeField] private Image musicLogoImage;
    [SerializeField] private Sprite musicButtonOnSprite;
    [SerializeField] private Sprite musicButtonOffSprite;
    [SerializeField] private Sprite musicLogoOnSprite;
    [SerializeField] private Sprite musicLogoOffSprite;

    [Header("Sound Setting")]
    [SerializeField] private Button soundButton;
    [SerializeField] private Image soundButtonImage;
    [SerializeField] private Image soundLogoImage;
    [SerializeField] private Sprite soundButtonOnSprite;
    [SerializeField] private Sprite soundButtonOffSprite;
    [SerializeField] private Sprite soundLogoOnSprite;
    [SerializeField] private Sprite soundLogoOffSprite;

    [Header("Select Player Panel")]
    [SerializeField] private GameObject selectPlayerPanel;
    [SerializeField] private Button closeSelectPlayerButton;

    [Header("Select Player Tick Buttons")]
    [SerializeField] private Button twoPlayersTickButton;
    [SerializeField] private Button threePlayersTickButton;
    [SerializeField] private Button fourPlayersTickButton;

    [Header("Select Player Tick Images")]
    [SerializeField] private Image twoPlayersTickImage;
    [SerializeField] private Image threePlayersTickImage;
    [SerializeField] private Image fourPlayersTickImage;

    [Header("Select Player Tick Sprite")]
    [SerializeField] private Sprite checkedTickSprite;

    [Header("Online Play Button")]
    [SerializeField] private Button playOnlineButton;

    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string profileSettingSceneName = "ProfileSetting";
    [SerializeField] private string onlineSceneName = "OnlineRoom";

    [Header("Join Room Panel")]
    [SerializeField] private Button codeButton;
    [SerializeField] private GameObject enterCodePanel;
    [SerializeField] private TMP_InputField enterCodeInput;
    [SerializeField] private Button enterRoomButton;
    [SerializeField] private Button closeEnterCodeButton;
    [SerializeField] private TMP_Text enterCodeMessageText;

    private const string PlayerNameKey = "PlayerName";
    private const string PlayerAvatarIndexKey = "PlayerAvatarIndex";

    private const string MusicOnKey = "MusicOn";
    private const string SoundOnKey = "SoundOn";
    private const string OnlinePlayerCountKey = "OnlinePlayerCount";

    private bool isMusicOn = true;
    private bool isSoundOn = true;
    private int selectedOnlinePlayerCount = 0;

    private void Awake()
    {
        SetupMainButtons();
        SetupSettingButtons();
        SetupAudioButtons();
        SetupSelectPlayerButtons();
        SetupEnterCodePanel();

        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }

        if (settingButton != null)
        {
            settingButton.gameObject.SetActive(true);
        }

        if (selectPlayerPanel != null)
        {
            selectPlayerPanel.SetActive(false);
        }

        if (enterCodePanel != null)
        {
            enterCodePanel.SetActive(false);
        }

        selectedOnlinePlayerCount = 0;
        UpdateSelectPlayerTickUI();
    }

    private void Start()
    {
        LoadPlayerProfile();
        LoadAudioSetting();
        UpdateMusicUI();
        UpdateSoundUI();
        UpdateSelectPlayerTickUI();
    }

    private void OnEnable()
    {
        LoadPlayerProfile();
    }

    private void SetupMainButtons()
    {
        if (vsComputerButton != null)
        {
            vsComputerButton.onClick.RemoveAllListeners();
            vsComputerButton.onClick.AddListener(OpenVsComputerGame);
        }

        if (avatarButton != null)
        {
            avatarButton.onClick.RemoveAllListeners();
            avatarButton.onClick.AddListener(OpenProfileSetting);
        }

        if (onlineMultipleButton != null)
        {
            onlineMultipleButton.onClick.RemoveAllListeners();
            onlineMultipleButton.onClick.AddListener(OpenSelectPlayerPanel);
        }
    }

    private void SetupSettingButtons()
    {
        if (settingButton != null)
        {
            settingButton.onClick.RemoveAllListeners();
            settingButton.onClick.AddListener(OpenSettingPanel);
        }

        if (closeSettingButton != null)
        {
            closeSettingButton.onClick.RemoveAllListeners();
            closeSettingButton.onClick.AddListener(CloseSettingPanel);
        }
    }

    private void SetupAudioButtons()
    {
        if (musicButton != null)
        {
            musicButton.onClick.RemoveAllListeners();
            musicButton.onClick.AddListener(ToggleMusic);
        }

        if (soundButton != null)
        {
            soundButton.onClick.RemoveAllListeners();
            soundButton.onClick.AddListener(ToggleSound);
        }
    }

    private void SetupSelectPlayerButtons()
    {
        if (closeSelectPlayerButton != null)
        {
            closeSelectPlayerButton.onClick.RemoveAllListeners();
            closeSelectPlayerButton.onClick.AddListener(CloseSelectPlayerPanel);
        }

        if (twoPlayersTickButton != null)
        {
            twoPlayersTickButton.onClick.RemoveAllListeners();
            twoPlayersTickButton.onClick.AddListener(() => SelectOnlinePlayerCount(2));
        }

        if (threePlayersTickButton != null)
        {
            threePlayersTickButton.onClick.RemoveAllListeners();
            threePlayersTickButton.onClick.AddListener(() => SelectOnlinePlayerCount(3));
        }

        if (fourPlayersTickButton != null)
        {
            fourPlayersTickButton.onClick.RemoveAllListeners();
            fourPlayersTickButton.onClick.AddListener(() => SelectOnlinePlayerCount(4));
        }

        if (playOnlineButton != null)
        {
            playOnlineButton.onClick.RemoveAllListeners();
            playOnlineButton.onClick.AddListener(PlayOnlineMultiple);
        }
    }

    private void LoadPlayerProfile()
    {
        string playerName = PlayerPrefs.GetString(PlayerNameKey, "Player");
        int avatarIndex = PlayerPrefs.GetInt(PlayerAvatarIndexKey, 0);

        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        if (avatarImage != null && avatarSprites != null && avatarSprites.Length > 0)
        {
            if (avatarIndex < 0 || avatarIndex >= avatarSprites.Length)
            {
                avatarIndex = 0;
            }

            avatarImage.sprite = avatarSprites[avatarIndex];
            avatarImage.color = Color.white;
            avatarImage.preserveAspect = true;
        }
    }

    private void LoadAudioSetting()
    {
        isMusicOn = PlayerPrefs.GetInt(MusicOnKey, 1) == 1;
        isSoundOn = PlayerPrefs.GetInt(SoundOnKey, 1) == 1;
    }

    private void ToggleMusic()
    {
        isMusicOn = !isMusicOn;

        PlayerPrefs.SetInt(MusicOnKey, isMusicOn ? 1 : 0);
        PlayerPrefs.Save();

        UpdateMusicUI();

        Debug.Log("Music: " + (isMusicOn ? "ON" : "OFF"));
    }

    private void ToggleSound()
    {
        isSoundOn = !isSoundOn;

        PlayerPrefs.SetInt(SoundOnKey, isSoundOn ? 1 : 0);
        PlayerPrefs.Save();

        UpdateSoundUI();

        Debug.Log("Sound: " + (isSoundOn ? "ON" : "OFF"));
    }

    private void UpdateMusicUI()
    {
        if (musicButtonImage != null)
        {
            musicButtonImage.sprite = isMusicOn ? musicButtonOnSprite : musicButtonOffSprite;
            musicButtonImage.color = Color.white;
            musicButtonImage.preserveAspect = true;
        }

        if (musicLogoImage != null)
        {
            musicLogoImage.sprite = isMusicOn ? musicLogoOnSprite : musicLogoOffSprite;
            musicLogoImage.color = Color.white;
            musicLogoImage.preserveAspect = true;
        }
    }

    private void UpdateSoundUI()
    {
        if (soundButtonImage != null)
        {
            soundButtonImage.sprite = isSoundOn ? soundButtonOnSprite : soundButtonOffSprite;
            soundButtonImage.color = Color.white;
            soundButtonImage.preserveAspect = true;
        }

        if (soundLogoImage != null)
        {
            soundLogoImage.sprite = isSoundOn ? soundLogoOnSprite : soundLogoOffSprite;
            soundLogoImage.color = Color.white;
            soundLogoImage.preserveAspect = true;
        }
    }

    private void OpenSettingPanel()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(true);
        }

        if (settingButton != null)
        {
            settingButton.gameObject.SetActive(false);
        }
    }

    private void CloseSettingPanel()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }

        if (settingButton != null)
        {
            settingButton.gameObject.SetActive(true);
        }
    }

    private void OpenSelectPlayerPanel()
    {
        if (selectPlayerPanel != null)
        {
            selectPlayerPanel.SetActive(true);
        }

        SetHomeButtonsInteractable(false);

        selectedOnlinePlayerCount = 0;
        UpdateSelectPlayerTickUI();
    }

    private void CloseSelectPlayerPanel()
    {
        if (selectPlayerPanel != null)
        {
            selectPlayerPanel.SetActive(false);
        }

        SetHomeButtonsInteractable(true);

        selectedOnlinePlayerCount = 0;
        UpdateSelectPlayerTickUI();
    }

    private void SelectOnlinePlayerCount(int playerCount)
    {
        selectedOnlinePlayerCount = playerCount;
        UpdateSelectPlayerTickUI();

        Debug.Log("Đã chọn: " + selectedOnlinePlayerCount + " players");
    }

    private void UpdateSelectPlayerTickUI()
    {
        SetTickSprite(twoPlayersTickImage, selectedOnlinePlayerCount == 2);
        SetTickSprite(threePlayersTickImage, selectedOnlinePlayerCount == 3);
        SetTickSprite(fourPlayersTickImage, selectedOnlinePlayerCount == 4);
    }

    private void SetTickSprite(Image tickImage, bool isSelected)
    {
        if (tickImage == null)
        {
            return;
        }

        if (isSelected)
        {
            tickImage.sprite = checkedTickSprite;
            tickImage.color = Color.white;
        }
        else
        {
            tickImage.sprite = null;
            tickImage.color = new Color(1f, 1f, 1f, 0f);
        }

        tickImage.preserveAspect = true;
        tickImage.raycastTarget = true;
    }

    private void SetHomeButtonsInteractable(bool canClick)
    {
        if (vsComputerButton != null)
        {
            vsComputerButton.interactable = canClick;
        }

        if (avatarButton != null)
        {
            avatarButton.interactable = canClick;
        }

        if (onlineMultipleButton != null)
        {
            onlineMultipleButton.interactable = canClick;
        }

        if (settingButton != null)
        {
            settingButton.interactable = canClick;
        }

        if (codeButton != null)
        {
            codeButton.interactable = canClick;
        }
    }

    private void PlayOnlineMultiple()
    {
        if (selectedOnlinePlayerCount == 0)
        {
            Debug.Log("Bạn chưa chọn số người chơi.");
            return;
        }

        PlayerPrefs.SetInt(OnlinePlayerCountKey, selectedOnlinePlayerCount);
        PlayerPrefs.Save();

        SetHomeButtonsInteractable(true);

        SceneManager.LoadScene(onlineSceneName);
    }

    private void OpenVsComputerGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    private void OpenProfileSetting()
    {
        SceneManager.LoadScene(profileSettingSceneName);
    }

    private void SetupEnterCodePanel()
    {
        if (codeButton != null)
        {
            codeButton.onClick.RemoveAllListeners();
            codeButton.onClick.AddListener(OpenEnterCodePanel);
        }

        if (closeEnterCodeButton != null)
        {
            closeEnterCodeButton.onClick.RemoveAllListeners();
            closeEnterCodeButton.onClick.AddListener(CloseEnterCodePanel);
        }

        if (enterRoomButton != null)
        {
            enterRoomButton.onClick.RemoveAllListeners();
            enterRoomButton.onClick.AddListener(SubmitEnterRoomCode);
        }
    }

    private void OpenEnterCodePanel()
    {
        if (enterCodePanel != null)
        {
            enterCodePanel.SetActive(true);
        }

        if (enterCodeInput != null)
        {
            enterCodeInput.text = "";
        }

        ShowEnterCodeMessage("");

        SetHomeButtonsInteractable(false);
    }

    private void CloseEnterCodePanel()
    {
        if (enterCodePanel != null)
        {
            enterCodePanel.SetActive(false);
        }

        SetHomeButtonsInteractable(true);
    }

    private void SubmitEnterRoomCode()
    {
        if (enterCodeInput == null)
        {
            Debug.LogError("Chưa kéo EnterCodeInput vào HomeController.");
            return;
        }

        string roomCode = enterCodeInput.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(roomCode))
        {
            ShowEnterCodeMessage("Vui lòng nhập mã phòng.");
            return;
        }

        if (onlineRoomManager == null)
        {
            onlineRoomManager = FindAnyObjectByType<OnlineRoomManager>();
        }

        if (onlineRoomManager == null)
        {
            ShowEnterCodeMessage("Chưa có OnlineRoomManager.");
            Debug.LogError("Chưa có OnlineRoomManager trong scene.");
            return;
        }

        ShowEnterCodeMessage("Đang vào phòng...");

        onlineRoomManager.JoinRoom(
            roomCode,
            room =>
            {
                Debug.Log("Join phòng thành công: " + room.roomCode);

                PlayerPrefs.SetInt(OnlinePlayerCountKey, room.maxPlayers);
                PlayerPrefs.Save();

                SetHomeButtonsInteractable(true);

                SceneManager.LoadScene(onlineSceneName);
            },
            error =>
            {
                Debug.LogError("Join phòng lỗi: " + error);
                ShowEnterCodeMessage("Không vào được phòng.");
            });
    }

    private void ShowEnterCodeMessage(string message)
    {
        if (enterCodeMessageText != null)
        {
            enterCodeMessageText.text = message;
        }
    }
}