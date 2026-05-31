using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OnlineGameSetupController : MonoBehaviour
{
    [System.Serializable]
    public class OpponentSlot
    {
        public string slotName;
        public GameObject root;
        public Image avatarImage;
        public TMP_Text nameText;
    }

    [Header("Avatar Sprites")]
    [SerializeField] private Sprite[] avatarSprites;

    [Header("Player UI")]
    [SerializeField] private Image playerAvatarImage;
    [SerializeField] private TMP_Text playerNameText;

    [Header("Opponent Slots")]
    [SerializeField] private OpponentSlot opponentLeft;
    [SerializeField] private OpponentSlot opponentTop;
    [SerializeField] private OpponentSlot opponentRight;

    [Header("Fake Join")]
    [SerializeField] private bool fakePlayersJoin = true;
    [SerializeField] private float fakeJoinInterval = 1f;

    private const string PlayerNameKey = "PlayerName";
    private const string PlayerAvatarIndexKey = "PlayerAvatarIndex";
    private const string OnlinePlayerCountKey = "OnlinePlayerCount";

    private readonly string[] fakeNames =
    {
        "Emma", "Lucas", "Olivia", "Harry", "Grace",
        "Noah", "Lily", "Mason", "Ella", "Oscar",
        "Jack", "Emily", "George", "Charlie"
    };

    private int targetPlayerCount = 4;

    private void Start()
    {
        SetupOnlineGameUI();
    }

    private void SetupOnlineGameUI()
    {
        targetPlayerCount = PlayerPrefs.GetInt(OnlinePlayerCountKey, 4);

        if (targetPlayerCount < 2)
        {
            targetPlayerCount = 2;
        }

        if (targetPlayerCount > 4)
        {
            targetPlayerCount = 4;
        }

        SetupMainPlayer();
        SetupWaitingSlots();

        if (fakePlayersJoin)
        {
            StartCoroutine(FakeJoinPlayersRoutine());
        }
    }

    private void SetupMainPlayer()
    {
        string playerName = PlayerPrefs.GetString(PlayerNameKey, "Player");
        int avatarIndex = PlayerPrefs.GetInt(PlayerAvatarIndexKey, 0);

        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }

        SetAvatar(playerAvatarImage, avatarIndex, true);
    }

    private void SetupWaitingSlots()
    {
        SetSlotRoot(opponentLeft, false);
        SetSlotRoot(opponentTop, false);
        SetSlotRoot(opponentRight, false);

        if (targetPlayerCount == 2)
        {
            SetSlotRoot(opponentTop, true);
            SetWaitingSlot(opponentTop);
        }
        else if (targetPlayerCount == 3)
        {
            SetSlotRoot(opponentLeft, true);
            SetSlotRoot(opponentRight, true);

            SetWaitingSlot(opponentLeft);
            SetWaitingSlot(opponentRight);
        }
        else
        {
            SetSlotRoot(opponentLeft, true);
            SetSlotRoot(opponentTop, true);
            SetSlotRoot(opponentRight, true);

            SetWaitingSlot(opponentLeft);
            SetWaitingSlot(opponentTop);
            SetWaitingSlot(opponentRight);
        }
    }

    private IEnumerator FakeJoinPlayersRoutine()
    {
        List<OpponentSlot> activeSlots = GetActiveOpponentSlots();

        List<int> usedAvatarIndexes = new List<int>();
        int playerAvatarIndex = PlayerPrefs.GetInt(PlayerAvatarIndexKey, 0);
        usedAvatarIndexes.Add(playerAvatarIndex);

        List<string> usedNames = new List<string>();
        usedNames.Add(PlayerPrefs.GetString(PlayerNameKey, "Player"));

        for (int i = 0; i < activeSlots.Count; i++)
        {
            yield return new WaitForSeconds(fakeJoinInterval);

            string randomName = GetRandomName(usedNames);
            int randomAvatarIndex = GetRandomAvatarIndex(usedAvatarIndexes);

            usedNames.Add(randomName);
            usedAvatarIndexes.Add(randomAvatarIndex);

            SetJoinedSlot(activeSlots[i], randomName, randomAvatarIndex);
        }
    }

    private List<OpponentSlot> GetActiveOpponentSlots()
    {
        List<OpponentSlot> slots = new List<OpponentSlot>();

        if (targetPlayerCount == 2)
        {
            slots.Add(opponentTop);
        }
        else if (targetPlayerCount == 3)
        {
            slots.Add(opponentLeft);
            slots.Add(opponentRight);
        }
        else
        {
            slots.Add(opponentLeft);
            slots.Add(opponentTop);
            slots.Add(opponentRight);
        }

        return slots;
    }

    private void SetWaitingSlot(OpponentSlot slot)
    {
        if (slot == null)
        {
            return;
        }

        if (slot.nameText != null)
        {
            slot.nameText.text = "Waiting...";
        }

        if (slot.avatarImage != null)
        {
            slot.avatarImage.color = new Color(1f, 1f, 1f, 0.45f);
            slot.avatarImage.preserveAspect = true;
        }
    }

    private void SetJoinedSlot(OpponentSlot slot, string playerName, int avatarIndex)
    {
        if (slot == null)
        {
            return;
        }

        if (slot.nameText != null)
        {
            slot.nameText.text = playerName;
        }

        SetAvatar(slot.avatarImage, avatarIndex, true);
    }

    private void SetAvatar(Image image, int avatarIndex, bool fullBright)
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

        if (fullBright)
        {
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(1f, 1f, 1f, 0.45f);
        }

        image.preserveAspect = true;
    }

    private void SetSlotRoot(OpponentSlot slot, bool active)
    {
        if (slot != null && slot.root != null)
        {
            slot.root.SetActive(active);
        }
    }

    private string GetRandomName(List<string> usedNames)
    {
        if (fakeNames == null || fakeNames.Length == 0)
        {
            return "Player";
        }

        for (int i = 0; i < 100; i++)
        {
            string name = fakeNames[Random.Range(0, fakeNames.Length)];

            if (!usedNames.Contains(name))
            {
                return name;
            }
        }

        return fakeNames[Random.Range(0, fakeNames.Length)];
    }

    private int GetRandomAvatarIndex(List<int> usedAvatarIndexes)
    {
        if (avatarSprites == null || avatarSprites.Length == 0)
        {
            return 0;
        }

        for (int i = 0; i < 100; i++)
        {
            int index = Random.Range(0, avatarSprites.Length);

            if (!usedAvatarIndexes.Contains(index))
            {
                return index;
            }
        }

        return Random.Range(0, avatarSprites.Length);
    }
}