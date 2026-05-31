using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ResultSceneController : MonoBehaviour
{
    [System.Serializable]
    public class PlayerResultInfo
    {
        public int playerIndex;
        public string playerName;
        public int cardCount;
        public int avatarIndex;
    }

    [Header("Scene")]
    [SerializeField] private string homeSceneName = "Home";

    [Header("Title")]
    [SerializeField] private TMP_Text titleText;

    [Header("First Place")]
    [SerializeField] private Image firstPlaceAvatar;

    [Header("Second Place")]
    [SerializeField] private Image secondPlaceAvatar;
    [SerializeField] private TMP_Text secondPlaceNameText;
    [SerializeField] private TMP_Text secondPlaceText;

    [Header("Third Place")]
    [SerializeField] private Image thirdPlaceAvatar;
    [SerializeField] private TMP_Text thirdPlaceNameText;
    [SerializeField] private TMP_Text thirdPlaceText;

    [Header("Fourth Place")]
    [SerializeField] private Image fourthPlaceAvatar;
    [SerializeField] private TMP_Text fourthPlaceNameText;
    [SerializeField] private TMP_Text fourthPlaceText;

    [Header("Button")]
    [SerializeField] private Button continueButton;

    [Header("Avatar Sprites")]
    [SerializeField] private Sprite[] avatarSprites;

    private void Awake()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(GoHome);
        }
    }

    private void Start()
    {
        ShowResult();
    }

    private void ShowResult()
    {
        int winnerIndex = PlayerPrefs.GetInt("WinnerIndex", 0);

        List<PlayerResultInfo> results = new List<PlayerResultInfo>();

        results.Add(new PlayerResultInfo
        {
            playerIndex = 0,
            playerName = PlayerPrefs.GetString("Game_PlayerName", "Player"),
            avatarIndex = PlayerPrefs.GetInt("Game_PlayerAvatarIndex", 0),
            cardCount = PlayerPrefs.GetInt("PlayerCardCount", 0)
        });

        results.Add(new PlayerResultInfo
        {
            playerIndex = 1,
            playerName = PlayerPrefs.GetString("Game_Bot1Name", "Bot 1"),
            avatarIndex = PlayerPrefs.GetInt("Game_Bot1AvatarIndex", 0),
            cardCount = PlayerPrefs.GetInt("Bot1CardCount", 0)
        });

        results.Add(new PlayerResultInfo
        {
            playerIndex = 2,
            playerName = PlayerPrefs.GetString("Game_Bot2Name", "Bot 2"),
            avatarIndex = PlayerPrefs.GetInt("Game_Bot2AvatarIndex", 0),
            cardCount = PlayerPrefs.GetInt("Bot2CardCount", 0)
        });

        results.Add(new PlayerResultInfo
        {
            playerIndex = 3,
            playerName = PlayerPrefs.GetString("Game_Bot3Name", "Bot 3"),
            avatarIndex = PlayerPrefs.GetInt("Game_Bot3AvatarIndex", 0),
            cardCount = PlayerPrefs.GetInt("Bot3CardCount", 0)
        });

        results.Sort((a, b) =>
        {
            if (a.playerIndex == winnerIndex)
            {
                return -1;
            }

            if (b.playerIndex == winnerIndex)
            {
                return 1;
            }

            return a.cardCount.CompareTo(b.cardCount);
        });

        PlayerResultInfo first = results[0];

        if (titleText != null)
        {
            if (first.playerIndex == 0)
            {
                titleText.text = "YOU WIN GAME.";
            }
            else
            {
                titleText.text = "YOU LOST GAME... TRY AGAIN.";
            }
        }

        SetAvatar(firstPlaceAvatar, first.avatarIndex);

        if (results.Count > 1)
        {
            SetRankSlot(results[1], secondPlaceAvatar, secondPlaceNameText, secondPlaceText, "2ND PLACE");
        }

        if (results.Count > 2)
        {
            SetRankSlot(results[2], thirdPlaceAvatar, thirdPlaceNameText, thirdPlaceText, "3RD PLACE");
        }

        if (results.Count > 3)
        {
            SetRankSlot(results[3], fourthPlaceAvatar, fourthPlaceNameText, fourthPlaceText, "4TH PLACE");
        }
    }

    private void SetRankSlot(
        PlayerResultInfo info,
        Image avatarImage,
        TMP_Text nameText,
        TMP_Text placeText,
        string place)
    {
        SetAvatar(avatarImage, info.avatarIndex);

        if (nameText != null)
        {
            nameText.text = info.playerName;
        }

        if (placeText != null)
        {
            placeText.text = place;
        }
    }

    private void SetAvatar(Image image, int avatarIndex)
    {
        if (image == null)
        {
            return;
        }

        if (avatarSprites == null || avatarSprites.Length == 0)
        {
            return;
        }

        if (avatarIndex < 0 || avatarIndex >= avatarSprites.Length)
        {
            avatarIndex = 0;
        }

        image.sprite = avatarSprites[avatarIndex];
        image.color = Color.white;
        image.preserveAspect = true;
    }

    private void GoHome()
    {
        SceneManager.LoadScene(homeSceneName);
    }
}