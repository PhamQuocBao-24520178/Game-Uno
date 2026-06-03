using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OnlineGameSettingController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button settingButton;
    [SerializeField] private GameObject settingPanel;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button quitButton;

    [Header("Music Setting")]
    [SerializeField] private Button musicButton;
    [SerializeField] private Image musicButtonImage;
    [SerializeField] private Image musicLogoImage;
    [SerializeField] private Sprite musicButtonOnSprite;
    [SerializeField] private Sprite musicButtonOffSprite;
    [SerializeField] private Sprite musicLogoOnSprite;
    [SerializeField] private Sprite musicLogoOffSprite;
    [SerializeField] private AudioSource musicAudioSource;

    [Header("Sound Setting")]
    [SerializeField] private Button soundButton;
    [SerializeField] private Image soundButtonImage;
    [SerializeField] private Image soundLogoImage;
    [SerializeField] private Sprite soundButtonOnSprite;
    [SerializeField] private Sprite soundButtonOffSprite;
    [SerializeField] private Sprite soundLogoOnSprite;
    [SerializeField] private Sprite soundLogoOffSprite;

    [Header("Scene")]
    [SerializeField] private string homeSceneName = "Home";

    private const string MusicOnKey = "MusicOn";
    private const string SoundOnKey = "SoundOn";

    private bool isMusicOn = true;
    private bool isSoundOn = true;

    private void Awake()
    {
        isMusicOn = PlayerPrefs.GetInt(MusicOnKey, 1) == 1;
        isSoundOn = PlayerPrefs.GetInt(SoundOnKey, 1) == 1;

        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }

        SetupButtons();
        ApplyMusicState();
        ApplySoundState();
    }

    private void SetupButtons()
    {
        if (settingButton != null)
        {
            settingButton.onClick.RemoveAllListeners();
            settingButton.onClick.AddListener(OpenSettingPanel);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseSettingPanel);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitToHome);
        }

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

    private void OpenSettingPanel()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(true);
            settingPanel.transform.SetAsLastSibling();
        }

        if (settingButton != null)
        {
            settingButton.gameObject.SetActive(false);
        }

        // OnlineGame KHÔNG pause game.
        Time.timeScale = 1f;
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

        Time.timeScale = 1f;
    }

    private void QuitToHome()
    {
        Time.timeScale = 1f;

        // Nếu sau này có API LeaveRoom thì gọi trước khi về Home.
        SceneManager.LoadScene(homeSceneName);
    }

    private void ToggleMusic()
    {
        isMusicOn = !isMusicOn;

        PlayerPrefs.SetInt(MusicOnKey, isMusicOn ? 1 : 0);
        PlayerPrefs.Save();

        ApplyMusicState();
    }

    private void ToggleSound()
    {
        isSoundOn = !isSoundOn;

        PlayerPrefs.SetInt(SoundOnKey, isSoundOn ? 1 : 0);
        PlayerPrefs.Save();

        ApplySoundState();
    }

    private void ApplyMusicState()
    {
        if (musicButtonImage != null)
        {
            musicButtonImage.sprite = isMusicOn ? musicButtonOnSprite : musicButtonOffSprite;
        }

        if (musicLogoImage != null)
        {
            musicLogoImage.sprite = isMusicOn ? musicLogoOnSprite : musicLogoOffSprite;
        }

        if (musicAudioSource != null)
        {
            musicAudioSource.mute = !isMusicOn;

            if (isMusicOn && !musicAudioSource.isPlaying)
            {
                musicAudioSource.Play();
            }
        }
    }

    private void ApplySoundState()
    {
        if (soundButtonImage != null)
        {
            soundButtonImage.sprite = isSoundOn ? soundButtonOnSprite : soundButtonOffSprite;
        }

        if (soundLogoImage != null)
        {
            soundLogoImage.sprite = isSoundOn ? soundLogoOnSprite : soundLogoOffSprite;
        }

        AudioListener.volume = isSoundOn ? 1f : 0f;

        if (musicAudioSource != null && isMusicOn)
        {
            musicAudioSource.mute = false;
        }
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}