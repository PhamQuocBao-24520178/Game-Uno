using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OnlineResultSceneController : MonoBehaviour
{
    [Header("Only Online Result")]
    [SerializeField] private string homeSceneName = "Home";

    [Header("Title")]
    [SerializeField] private TMP_Text onlineTitleText;

    [Header("First Place")]
    [SerializeField] private Image onlineFirstPlaceAvatar;

    [Header("Second Place")]
    [SerializeField] private GameObject onlineSecondPlaceRoot;
    [SerializeField] private Image onlineSecondPlaceAvatar;
    [SerializeField] private TMP_Text onlineSecondPlaceNameText;
    [SerializeField] private TMP_Text onlineSecondPlaceText;

    [Header("Third Place")]
    [SerializeField] private GameObject onlineThirdPlaceRoot;
    [SerializeField] private Image onlineThirdPlaceAvatar;
    [SerializeField] private TMP_Text onlineThirdPlaceNameText;
    [SerializeField] private TMP_Text onlineThirdPlaceText;

    [Header("Fourth Place")]
    [SerializeField] private GameObject onlineFourthPlaceRoot;
    [SerializeField] private Image onlineFourthPlaceAvatar;
    [SerializeField] private TMP_Text onlineFourthPlaceNameText;
    [SerializeField] private TMP_Text onlineFourthPlaceText;

    [Header("Button")]
    [SerializeField] private Button onlineContinueButton;

    [Header("Avatar Sprites")]
    [SerializeField] private Sprite[] onlineAvatarSprites;

    private void Awake()
    {
        // Chỉ chạy khi đi từ OnlineGame sang ResultGame.
        if (PlayerPrefs.GetInt("IsOnlineResult", 0) != 1)
        {
            enabled = false;
            return;
        }

        if (onlineContinueButton != null)
        {
            onlineContinueButton.onClick.RemoveAllListeners();
            onlineContinueButton.onClick.AddListener(GoHome);
        }
    }

    private void Start()
    {
        if (!enabled)
        {
            return;
        }

        ShowOnlineResult();
    }

    private void ShowOnlineResult()
    {
        List<OnlineResultPlayerData> ranking =
            OnlineResultDataStore.GetRanking();

        if (ranking == null || ranking.Count == 0)
        {
            return;
        }

        ShowTitle(ranking);
        ShowFirstPlace(ranking.Count >= 1 ? ranking[0] : null);
        ShowSecondPlace(ranking.Count >= 2 ? ranking[1] : null);
        ShowThirdPlace(ranking.Count >= 3 ? ranking[2] : null);
        ShowFourthPlace(ranking.Count >= 4 ? ranking[3] : null);
    }

    private void ShowTitle(List<OnlineResultPlayerData> ranking)
    {
        if (onlineTitleText == null || ranking.Count == 0)
        {
            return;
        }

        string myPlayerId =
            PlayerPrefs.GetString("OnlinePlayerId", "");

        bool iAmWinner =
            ranking[0].playerId == myPlayerId;

        onlineTitleText.text = iAmWinner
            ? "YOU WIN GAME."
            : ranking[0].displayName + " WIN GAME.";
    }

    private void ShowFirstPlace(OnlineResultPlayerData player)
    {
        if (onlineFirstPlaceAvatar == null)
        {
            return;
        }

        onlineFirstPlaceAvatar.gameObject.SetActive(player != null);

        if (player != null)
        {
            SetAvatar(onlineFirstPlaceAvatar, player.avatarIndex);
        }
    }

    private void ShowSecondPlace(OnlineResultPlayerData player)
    {
        SetPlace(
            onlineSecondPlaceRoot,
            onlineSecondPlaceAvatar,
            onlineSecondPlaceNameText,
            onlineSecondPlaceText,
            player,
            "Hạng 2"
        );
    }

    private void ShowThirdPlace(OnlineResultPlayerData player)
    {
        SetPlace(
            onlineThirdPlaceRoot,
            onlineThirdPlaceAvatar,
            onlineThirdPlaceNameText,
            onlineThirdPlaceText,
            player,
            "Hạng 3"
        );
    }

    private void ShowFourthPlace(OnlineResultPlayerData player)
    {
        SetPlace(
            onlineFourthPlaceRoot,
            onlineFourthPlaceAvatar,
            onlineFourthPlaceNameText,
            onlineFourthPlaceText,
            player,
            "Hạng 4"
        );
    }

    private void SetPlace(
        GameObject placeRoot,
        Image avatar,
        TMP_Text nameText,
        TMP_Text placeText,
        OnlineResultPlayerData player,
        string placeLabel)
    {
        bool hasPlayer = player != null;

        if (placeRoot != null)
        {
            placeRoot.SetActive(hasPlayer);
        }

        if (!hasPlayer)
        {
            return;
        }

        SetAvatar(avatar, player.avatarIndex);

        if (nameText != null)
        {
            nameText.text = player.displayName;
        }

        if (placeText != null)
        {
            placeText.text = placeLabel;
        }
    }

    private void SetAvatar(Image avatarImage, int avatarIndex)
    {
        if (avatarImage == null ||
            onlineAvatarSprites == null ||
            onlineAvatarSprites.Length == 0)
        {
            return;
        }

        int safeIndex = Mathf.Clamp(
            avatarIndex,
            0,
            onlineAvatarSprites.Length - 1
        );

        avatarImage.sprite = onlineAvatarSprites[safeIndex];
        avatarImage.preserveAspect = true;
    }

    private void GoHome()
    {
        PlayerPrefs.SetInt("IsOnlineResult", 0);
        PlayerPrefs.Save();

        OnlineResultDataStore.Clear();

        SceneManager.LoadScene(homeSceneName);
    }

}

[System.Serializable]
public class OnlineResultPlayerData
{
    public string playerId;
    public string displayName;
    public int avatarIndex;
    public int cardCount;
    public int place;
}

public static class OnlineResultDataStore
{
    private static List<OnlineResultPlayerData> ranking =
    new List<OnlineResultPlayerData>();

    public static void SetRanking(
        List<OnlineResultPlayerData> newRanking)
    {
        ranking = newRanking != null
            ? new List<OnlineResultPlayerData>(newRanking)
            : new List<OnlineResultPlayerData>();
    }

    public static List<OnlineResultPlayerData> GetRanking()
    {
        return new List<OnlineResultPlayerData>(ranking);
    }

    public static void Clear()
    {
        ranking.Clear();
    }

}