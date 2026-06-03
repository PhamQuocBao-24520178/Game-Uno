using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ProfileSettingController : MonoBehaviour
{
    [Header("Avatar")]
    [SerializeField] private Image bigAvatarImage;
    [SerializeField] private Sprite[] avatarSprites;
    [SerializeField] private Button[] avatarButtons;

    [Header("Name")]
    [SerializeField] private TMP_InputField nameInput;

    [Header("Buttons")]
    [SerializeField] private Button continueButton;

    [Header("Warning Popup")]
    [SerializeField] private GameObject warningPopup;
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private float popupShowTime = 2f;

    [Header("Scene")]
    [SerializeField] private string homeSceneName = "Home";

    private const string PlayerNameKey = "PlayerName";
    private const string PlayerAvatarIndexKey = "PlayerAvatarIndex";

    private int selectedAvatarIndex = 0;
    private Coroutine popupCoroutine;

    private void Awake()
    {
        SetupAvatarButtons();

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        if (warningPopup != null)
        {
            warningPopup.SetActive(false);
        }
    }

    private void Start()
    {
        LoadSavedProfile();
    }

    private void SetupAvatarButtons()
    {
        if (avatarButtons == null)
        {
            return;
        }

        for (int i = 0; i < avatarButtons.Length; i++)
        {
            int index = i;

            if (avatarButtons[i] != null)
            {
                avatarButtons[i].onClick.RemoveAllListeners();
                avatarButtons[i].onClick.AddListener(() => SelectAvatar(index));
            }
        }
    }

    private void LoadSavedProfile()
    {
        string savedName = PlayerPrefs.GetString(PlayerNameKey, "");
        int savedAvatarIndex = PlayerPrefs.GetInt(PlayerAvatarIndexKey, 0);

        if (nameInput != null)
        {
            nameInput.text = savedName;
        }

        if (avatarSprites != null && avatarSprites.Length > 0)
        {
            if (savedAvatarIndex < 0 || savedAvatarIndex >= avatarSprites.Length)
            {
                savedAvatarIndex = 0;
            }

            selectedAvatarIndex = savedAvatarIndex;

            if (bigAvatarImage != null)
            {
                bigAvatarImage.sprite = avatarSprites[selectedAvatarIndex];
                bigAvatarImage.color = Color.white;
                bigAvatarImage.preserveAspect = true;
            }
        }
    }

    private void SelectAvatar(int index)
    {
        if (avatarSprites == null || avatarSprites.Length == 0)
        {
            return;
        }

        if (index < 0 || index >= avatarSprites.Length)
        {
            return;
        }

        selectedAvatarIndex = index;

        if (bigAvatarImage != null)
        {
            bigAvatarImage.sprite = avatarSprites[selectedAvatarIndex];
            bigAvatarImage.color = Color.white;
            bigAvatarImage.preserveAspect = true;
        }
    }

    private void OnContinueClicked()
    {
        if (nameInput == null)
        {
            return;
        }

        string playerName = nameInput.text.Trim();

        if (string.IsNullOrEmpty(playerName))
        {
            ShowWarningPopup("Please enter your name!");
            return;
        }

        PlayerPrefs.SetString(PlayerNameKey, playerName);
        PlayerPrefs.SetInt(PlayerAvatarIndexKey, selectedAvatarIndex);
        PlayerPrefs.Save();

        SceneManager.LoadScene(homeSceneName);

        string accountKey = PlayerPrefs.GetString("CurrentAccountKey", "Guest");

        PlayerPrefs.SetString("PlayerName_" + accountKey, playerName);
        PlayerPrefs.SetInt("PlayerAvatarIndex_" + accountKey, selectedAvatarIndex);
        PlayerPrefs.SetInt("ProfileCompleted_" + accountKey, 1);

        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.SetInt("PlayerAvatarIndex", selectedAvatarIndex);

        PlayerPrefs.Save();

        SceneManager.LoadScene("Home");
    }

    private void ShowWarningPopup(string message)
    {
        if (warningText != null)
        {
            warningText.text = message;
        }

        if (warningPopup == null)
        {
            return;
        }

        if (popupCoroutine != null)
        {
            StopCoroutine(popupCoroutine);
        }

        popupCoroutine = StartCoroutine(ShowWarningRoutine());
    }

    private System.Collections.IEnumerator ShowWarningRoutine()
    {
        warningPopup.SetActive(true);

        yield return new WaitForSeconds(popupShowTime);

        warningPopup.SetActive(false);
    }
}