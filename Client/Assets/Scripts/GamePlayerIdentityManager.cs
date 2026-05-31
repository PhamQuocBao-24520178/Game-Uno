using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GamePlayerIdentityManager : MonoBehaviour
{
    [Header("Avatar Sprites")]
    [SerializeField] private Sprite[] avatarSprites;

    [Header("Player UI")]
    [SerializeField] private Image playerAvatarImage;
    [SerializeField] private TMP_Text playerNameText;

    [Header("Bot 1 UI")]
    [SerializeField] private Image bot1AvatarImage;
    [SerializeField] private TMP_Text bot1NameText;

    [Header("Bot 2 UI")]
    [SerializeField] private Image bot2AvatarImage;
    [SerializeField] private TMP_Text bot2NameText;

    [Header("Bot 3 UI")]
    [SerializeField] private Image bot3AvatarImage;
    [SerializeField] private TMP_Text bot3NameText;

    private readonly string[] botNamePool =
    {
        "Oliver", "Jack", "Harry", "George", "Noah",
        "Leo", "Oscar", "Charlie", "Thomas", "Henry",
        "James", "William", "Lucas", "Benjamin", "Mason",
        "Emma", "Sophia", "Emily", "Olivia", "Ava",
        "Mia", "Grace", "Chloe", "Lily", "Ella"
    };

    private void Start()
    {
        SetupPlayersForVsComputer();
    }

    public void SetupPlayersForVsComputer()
    {
        string playerName = PlayerPrefs.GetString("PlayerName", "Player");
        int playerAvatarIndex = PlayerPrefs.GetInt("PlayerAvatarIndex", 0);

        playerAvatarIndex = ClampAvatarIndex(playerAvatarIndex);

        string bot1Name = GetRandomBotName(new List<string>());
        string bot2Name = GetRandomBotName(new List<string> { bot1Name });
        string bot3Name = GetRandomBotName(new List<string> { bot1Name, bot2Name });

        int bot1AvatarIndex = GetRandomAvatarIndexExcept(playerAvatarIndex, new List<int>());
        int bot2AvatarIndex = GetRandomAvatarIndexExcept(playerAvatarIndex, new List<int> { bot1AvatarIndex });
        int bot3AvatarIndex = GetRandomAvatarIndexExcept(playerAvatarIndex, new List<int> { bot1AvatarIndex, bot2AvatarIndex });

        ApplyToUI(playerAvatarImage, playerNameText, playerAvatarIndex, playerName);
        ApplyToUI(bot1AvatarImage, bot1NameText, bot1AvatarIndex, bot1Name);
        ApplyToUI(bot2AvatarImage, bot2NameText, bot2AvatarIndex, bot2Name);
        ApplyToUI(bot3AvatarImage, bot3NameText, bot3AvatarIndex, bot3Name);

        SaveGameIdentity(playerName, playerAvatarIndex, bot1Name, bot1AvatarIndex, bot2Name, bot2AvatarIndex, bot3Name, bot3AvatarIndex);
    }

    private void SaveGameIdentity(
        string playerName,
        int playerAvatarIndex,
        string bot1Name,
        int bot1AvatarIndex,
        string bot2Name,
        int bot2AvatarIndex,
        string bot3Name,
        int bot3AvatarIndex)
    {
        PlayerPrefs.SetString("Game_PlayerName", playerName);
        PlayerPrefs.SetInt("Game_PlayerAvatarIndex", playerAvatarIndex);

        PlayerPrefs.SetString("Game_Bot1Name", bot1Name);
        PlayerPrefs.SetInt("Game_Bot1AvatarIndex", bot1AvatarIndex);

        PlayerPrefs.SetString("Game_Bot2Name", bot2Name);
        PlayerPrefs.SetInt("Game_Bot2AvatarIndex", bot2AvatarIndex);

        PlayerPrefs.SetString("Game_Bot3Name", bot3Name);
        PlayerPrefs.SetInt("Game_Bot3AvatarIndex", bot3AvatarIndex);

        PlayerPrefs.Save();
    }

    private void ApplyToUI(Image avatarImage, TMP_Text nameText, int avatarIndex, string playerName)
    {
        if (avatarImage != null && avatarSprites != null && avatarSprites.Length > 0)
        {
            avatarImage.sprite = avatarSprites[avatarIndex];
            avatarImage.color = Color.white;
            avatarImage.preserveAspect = true;
        }

        if (nameText != null)
        {
            nameText.text = playerName;
        }
    }

    private string GetRandomBotName(List<string> usedNames)
    {
        if (botNamePool.Length == 0)
        {
            return "Bot";
        }

        for (int i = 0; i < 100; i++)
        {
            string name = botNamePool[Random.Range(0, botNamePool.Length)];

            if (!usedNames.Contains(name))
            {
                return name;
            }
        }

        return botNamePool[Random.Range(0, botNamePool.Length)];
    }

    private int GetRandomAvatarIndexExcept(int playerAvatarIndex, List<int> usedBotAvatarIndexes)
    {
        if (avatarSprites == null || avatarSprites.Length == 0)
        {
            return 0;
        }

        for (int i = 0; i < 100; i++)
        {
            int randomIndex = Random.Range(0, avatarSprites.Length);

            if (randomIndex != playerAvatarIndex && !usedBotAvatarIndexes.Contains(randomIndex))
            {
                return randomIndex;
            }
        }

        return Random.Range(0, avatarSprites.Length);
    }

    private int ClampAvatarIndex(int avatarIndex)
    {
        if (avatarSprites == null || avatarSprites.Length == 0)
        {
            return 0;
        }

        if (avatarIndex < 0)
        {
            return 0;
        }

        if (avatarIndex >= avatarSprites.Length)
        {
            return 0;
        }

        return avatarIndex;
    }
}
