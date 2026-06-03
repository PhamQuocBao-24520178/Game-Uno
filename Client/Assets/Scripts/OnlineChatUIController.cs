using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OnlineChatUIController : MonoBehaviour
{
    [Header("Main Panel")]
    [SerializeField] private GameObject chatPanel;
    [SerializeField] private Button openChatButton;
    [SerializeField] private Button closeChatButton;

    [Header("Chat")]
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private Transform messageContent;

    [Header("Emoji")]
    [SerializeField] private Button emojiButton;
    [SerializeField] private GameObject emojiPanel;
    [SerializeField] private Button[] emojiButtons;
    [SerializeField] private Sprite[] emojiSprites;

    [Header("Message Style")]
    [SerializeField] private int textFontSize = 20;
    [SerializeField] private int nameFontSize = 18;
    [SerializeField] private Vector2 emojiSize = new Vector2(38f, 38f);
    [SerializeField] private float minMessageHeight = 38f;

    private const string PlayerNameKey = "PlayerName";

    private void Awake()
    {
        if (chatPanel != null)
        {
            chatPanel.SetActive(false);
        }

        if (emojiPanel != null)
        {
            emojiPanel.SetActive(false);
        }

        SetupButtons();
    }

    private void SetupButtons()
    {
        if (openChatButton != null)
        {
            openChatButton.onClick.RemoveAllListeners();
            openChatButton.onClick.AddListener(OpenChatPanel);
        }

        if (closeChatButton != null)
        {
            closeChatButton.onClick.RemoveAllListeners();
            closeChatButton.onClick.AddListener(CloseChatPanel);
        }

        if (sendButton != null)
        {
            sendButton.onClick.RemoveAllListeners();
            sendButton.onClick.AddListener(SendTextMessage);
        }

        if (emojiButton != null)
        {
            emojiButton.onClick.RemoveAllListeners();
            emojiButton.onClick.AddListener(ToggleEmojiPanel);
        }

        if (chatInputField != null)
        {
            chatInputField.onSubmit.RemoveAllListeners();
            chatInputField.onSubmit.AddListener(OnInputSubmit);
            chatInputField.lineType = TMP_InputField.LineType.SingleLine;

            if (chatInputField.textComponent != null)
            {
                chatInputField.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
                chatInputField.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
                chatInputField.textComponent.overflowMode = TextOverflowModes.Overflow;
            }
        }

        if (emojiButtons != null)
        {
            for (int i = 0; i < emojiButtons.Length; i++)
            {
                int index = i;

                if (emojiButtons[i] != null)
                {
                    emojiButtons[i].onClick.RemoveAllListeners();
                    emojiButtons[i].onClick.AddListener(() => SendEmojiMessage(index));
                }
            }
        }
    }

    private void OnInputSubmit(string value)
    {
        SendTextMessage();
    }

    private void OpenChatPanel()
    {
        if (chatPanel != null)
        {
            chatPanel.SetActive(true);
        }

        if (chatInputField != null)
        {
            chatInputField.ActivateInputField();
        }
    }

    private void CloseChatPanel()
    {
        if (chatPanel != null)
        {
            chatPanel.SetActive(false);
        }

        if (emojiPanel != null)
        {
            emojiPanel.SetActive(false);
        }
    }

    private void ToggleEmojiPanel()
    {
        if (emojiPanel != null)
        {
            emojiPanel.SetActive(!emojiPanel.activeSelf);
        }

        if (chatInputField != null)
        {
            chatInputField.ActivateInputField();
        }
    }

    private void SendTextMessage()
    {
        if (chatInputField == null)
        {
            return;
        }

        string message = chatInputField.text.Trim();

        if (string.IsNullOrEmpty(message))
        {
            chatInputField.text = "";
            chatInputField.ActivateInputField();
            return;
        }

        string playerName = PlayerPrefs.GetString(PlayerNameKey, "Player");

        AddTextMessage(playerName, message);

        chatInputField.text = "";
        chatInputField.DeactivateInputField();
        chatInputField.ActivateInputField();
    }

    private void SendEmojiMessage(int emojiIndex)
    {
        if (emojiSprites == null || emojiSprites.Length == 0)
        {
            Debug.LogError("Chưa kéo Emoji Sprites.");
            return;
        }

        if (emojiIndex < 0 || emojiIndex >= emojiSprites.Length)
        {
            Debug.LogError("Emoji index không hợp lệ: " + emojiIndex);
            return;
        }

        string playerName = PlayerPrefs.GetString(PlayerNameKey, "Player");

        AddEmojiMessage(playerName, emojiSprites[emojiIndex]);

        if (emojiPanel != null)
        {
            emojiPanel.SetActive(false);
        }

        if (chatInputField != null)
        {
            chatInputField.ActivateInputField();
        }
    }

    private void AddTextMessage(string playerName, string message)
    {
        if (messageContent == null)
        {
            Debug.LogError("Chưa kéo MessageContent.");
            return;
        }

        GameObject messageObject = new GameObject(
            "TextMessage",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement)
        );

        messageObject.transform.SetParent(messageContent, false);

        RectTransform rect = messageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(0f, minMessageHeight);

        TMP_Text text = messageObject.GetComponent<TMP_Text>();
        text.richText = true;
        text.text = playerName + ": " + message;
        text.color = Color.black;
        text.fontSize = textFontSize;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.margin = new Vector4(6f, 2f, 6f, 2f);

        LayoutElement layout = messageObject.GetComponent<LayoutElement>();
        layout.flexibleWidth = 1f;

        RefreshTextMessageHeight(text, layout);
        ForceRefreshLayout();
    }

    private void AddEmojiMessage(string playerName, Sprite emojiSprite)
    {
        if (messageContent == null)
        {
            Debug.LogError("Chưa kéo MessageContent.");
            return;
        }

        GameObject row = new GameObject(
            "EmojiMessage",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement)
        );

        row.transform.SetParent(messageContent, false);

        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0f, 1f);
        rowRect.sizeDelta = new Vector2(0f, minMessageHeight);

        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.spacing = 6f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.padding = new RectOffset(6, 6, 2, 2);

        LayoutElement rowElement = row.GetComponent<LayoutElement>();
        rowElement.preferredHeight = Mathf.Max(minMessageHeight, emojiSize.y + 6f);
        rowElement.flexibleWidth = 1f;

        GameObject nameObject = new GameObject(
            "NameText",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement)
        );

        nameObject.transform.SetParent(row.transform, false);

        TMP_Text nameText = nameObject.GetComponent<TMP_Text>();
        nameText.text = playerName + ":";
        nameText.color = Color.black;
        nameText.fontSize = nameFontSize;
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Overflow;

        LayoutElement nameLayout = nameObject.GetComponent<LayoutElement>();
        nameLayout.preferredWidth = 45f;
        nameLayout.preferredHeight = minMessageHeight;
        nameLayout.flexibleWidth = 0f;

        GameObject emojiObject = new GameObject(
            "EmojiImage",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement)
        );

        emojiObject.transform.SetParent(row.transform, false);

        Image emojiImage = emojiObject.GetComponent<Image>();
        emojiImage.sprite = emojiSprite;
        emojiImage.preserveAspect = true;
        emojiImage.raycastTarget = false;

        LayoutElement emojiLayout = emojiObject.GetComponent<LayoutElement>();
        emojiLayout.preferredWidth = emojiSize.x;
        emojiLayout.preferredHeight = emojiSize.y;
        emojiLayout.flexibleWidth = 0f;
        emojiLayout.flexibleHeight = 0f;

        ForceRefreshLayout();
    }

    private void RefreshTextMessageHeight(TMP_Text text, LayoutElement layout)
    {
        if (text == null || layout == null)
        {
            return;
        }

        RectTransform contentRect = messageContent as RectTransform;

        float availableWidth = 360f;

        if (contentRect != null && contentRect.rect.width > 0f)
        {
            availableWidth = contentRect.rect.width - 12f;
        }

        Vector2 preferred = text.GetPreferredValues(text.text, availableWidth, 0f);
        layout.preferredHeight = Mathf.Max(minMessageHeight, preferred.y + 6f);
    }

    private void ForceRefreshLayout()
    {
        if (messageContent == null)
        {
            return;
        }

        RectTransform contentRect = messageContent as RectTransform;

        if (contentRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }
    }
}