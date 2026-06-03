using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GlobalChatUIController : MonoBehaviour
{
    [Header("Main UI")]
    [SerializeField] private GameObject chatPanel;
    [SerializeField] private Button openChatButton;
    [SerializeField] private Button closeChatButton;

    [Header("Chat")]
    [SerializeField] private RectTransform messageContent;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button sendButton;

    [Header("Message Style")]
    [SerializeField] private int textFontSize = 22;
    [SerializeField] private Color textColor = Color.black;
    [SerializeField] private Vector2 emojiSize = new Vector2(42f, 42f);
    [SerializeField] private float messageSpacing = 8f;

    [Header("Emoji")]
    [SerializeField] private Button emojiButton;
    [SerializeField] private GameObject emojiPanel;
    [SerializeField] private Button[] emojiButtons;
    [SerializeField] private Sprite[] emojiSprites;

    [Header("Polling")]
    [SerializeField] private float refreshInterval = 1f;

    [Header("Options")]
    [SerializeField] private bool autoSuggestRoomCode = false;

    private float refreshTimer = 0f;
    private string lastRenderedSignature = "";

    private static readonly Regex EmojiRegex = new Regex(@"^\[emoji:(\d+)\]$");

    private void Start()
    {
        SetupPanel();
        SetupButtons();
        SetupContentLayout();
        RefreshMessagesNow();
    }

    private void Update()
    {
        refreshTimer += Time.deltaTime;

        if (refreshTimer >= refreshInterval)
        {
            refreshTimer = 0f;
            RefreshMessagesNow();
        }
    }

    private void SetupPanel()
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

    private void SetupButtons()
    {
        if (openChatButton != null)
        {
            openChatButton.onClick.RemoveAllListeners();
            openChatButton.onClick.AddListener(OpenChat);
        }

        if (closeChatButton != null)
        {
            closeChatButton.onClick.RemoveAllListeners();
            closeChatButton.onClick.AddListener(CloseChat);
        }

        if (sendButton != null)
        {
            sendButton.onClick.RemoveAllListeners();
            sendButton.onClick.AddListener(SendCurrentTextMessage);
        }

        if (emojiButton != null)
        {
            emojiButton.onClick.RemoveAllListeners();
            emojiButton.onClick.AddListener(ToggleEmojiPanel);
        }

        if (inputField != null)
        {
            inputField.onSubmit.RemoveAllListeners();
            inputField.onSubmit.AddListener(_ => SendCurrentTextMessage());
        }

        SetupEmojiButtons();
    }

    private void SetupContentLayout()
    {
        if (messageContent == null)
        {
            return;
        }

        VerticalLayoutGroup layout = messageContent.GetComponent<VerticalLayoutGroup>();

        if (layout == null)
        {
            layout = messageContent.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = messageSpacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = messageContent.GetComponent<ContentSizeFitter>();

        if (fitter == null)
        {
            fitter = messageContent.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void SetupEmojiButtons()
    {
        if (emojiButtons == null)
        {
            return;
        }

        for (int i = 0; i < emojiButtons.Length; i++)
        {
            int emojiIndex = i;

            if (emojiButtons[i] == null)
            {
                continue;
            }

            emojiButtons[i].onClick.RemoveAllListeners();
            emojiButtons[i].onClick.AddListener(() => SendEmojiMessage(emojiIndex));
        }
    }

    private void OpenChat()
    {
        if (chatPanel != null)
        {
            chatPanel.SetActive(true);
            chatPanel.transform.SetAsLastSibling();
        }

        if (autoSuggestRoomCode && inputField != null && string.IsNullOrWhiteSpace(inputField.text))
        {
            string roomCode = PlayerPrefs.GetString("CurrentRoomCode", "");

            if (!string.IsNullOrEmpty(roomCode))
            {
                inputField.text = "Phòng " + roomCode + " vào chơi nha";
                inputField.caretPosition = inputField.text.Length;
            }
        }

        RefreshMessagesNow();
    }

    private void CloseChat()
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
        if (emojiPanel == null)
        {
            return;
        }

        emojiPanel.SetActive(!emojiPanel.activeSelf);
    }

    private void SendCurrentTextMessage()
    {
        if (inputField == null)
        {
            return;
        }

        string message = inputField.text.Trim();

        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        inputField.text = "";
        inputField.ActivateInputField();

        SendMessageToServer(message);
    }

    private void SendEmojiMessage(int emojiIndex)
    {
        string emojiMessage = "[emoji:" + emojiIndex + "]";
        SendMessageToServer(emojiMessage);

        if (emojiPanel != null)
        {
            emojiPanel.SetActive(false);
        }
    }

    private void SendMessageToServer(string message)
    {
        string playerId = PlayerPrefs.GetString("OnlinePlayerId", "");
        string displayName = GetDisplayName();
        string roomCode = PlayerPrefs.GetString("CurrentRoomCode", "");

        GlobalChatSendRequest request = new GlobalChatSendRequest
        {
            playerId = playerId,
            displayName = displayName,
            message = message,
            roomCode = roomCode
        };

        StartCoroutine(GlobalChatApiService.SendMessage(
            request,
            onSuccess: response =>
            {
                RefreshMessagesNow();

                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlayButtonClick();
                }
            },
            onError: error =>
            {
                Debug.LogError("Gửi global chat lỗi: " + error);
            }
        ));
    }

    private void RefreshMessagesNow()
    {
        StartCoroutine(GlobalChatApiService.GetMessages(
            onSuccess: response =>
            {
                RenderMessages(response.data);
            },
            onError: error =>
            {
                Debug.LogError("Lấy global chat lỗi: " + error);
            }
        ));
    }

    private void RenderMessages(GlobalChatMessage[] messages)
    {
        if (messageContent == null)
        {
            return;
        }

        string signature = BuildSignature(messages);

        if (signature == lastRenderedSignature)
        {
            return;
        }

        lastRenderedSignature = signature;

        ClearMessageContent();

        if (messages == null || messages.Length == 0)
        {
            CreateTextRow("Chưa có tin nhắn.");
            return;
        }

        for (int i = 0; i < messages.Length; i++)
        {
            GlobalChatMessage msg = messages[i];

            if (msg == null)
            {
                continue;
            }

            string name = string.IsNullOrEmpty(msg.displayName) ? "Player" : msg.displayName;
            string roomText = string.IsNullOrEmpty(msg.roomCode) ? "" : " [Phòng " + msg.roomCode + "]";

            int emojiIndex;

            if (TryParseEmojiMessage(msg.message, out emojiIndex))
            {
                CreateEmojiRow(name + roomText + ": ", emojiIndex);
            }
            else
            {
                CreateTextRow(name + roomText + ": " + msg.message);
            }
        }
    }

    private string BuildSignature(GlobalChatMessage[] messages)
    {
        if (messages == null || messages.Length == 0)
        {
            return "";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        for (int i = 0; i < messages.Length; i++)
        {
            if (messages[i] == null)
            {
                continue;
            }

            builder.Append(messages[i].id);
            builder.Append(messages[i].message);
            builder.Append("|");
        }

        return builder.ToString();
    }

    private void ClearMessageContent()
    {
        for (int i = messageContent.childCount - 1; i >= 0; i--)
        {
            Destroy(messageContent.GetChild(i).gameObject);
        }
    }

    private void CreateTextRow(string text)
    {
        GameObject row = new GameObject("TextMessage", typeof(RectTransform));
        row.transform.SetParent(messageContent, false);

        TMP_Text tmp = row.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = textFontSize;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = true;
        tmp.richText = true;

        LayoutElement layout = row.AddComponent<LayoutElement>();
        layout.preferredHeight = Mathf.Max(32f, tmp.preferredHeight + 8f);
        layout.flexibleWidth = 1f;
    }

    private void CreateEmojiRow(string prefixText, int emojiIndex)
    {
        GameObject row = new GameObject("EmojiMessage", typeof(RectTransform));
        row.transform.SetParent(messageContent, false);

        HorizontalLayoutGroup layoutGroup = row.AddComponent<HorizontalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.MiddleLeft;
        layoutGroup.spacing = 6f;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;

        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = emojiSize.y + 8f;
        rowLayout.flexibleWidth = 1f;

        GameObject textObj = new GameObject("NameText", typeof(RectTransform));
        textObj.transform.SetParent(row.transform, false);

        TMP_Text nameText = textObj.AddComponent<TextMeshProUGUI>();
        nameText.text = prefixText;
        nameText.fontSize = textFontSize;
        nameText.color = textColor;
        nameText.alignment = TextAlignmentOptions.MidlineLeft;

        LayoutElement textLayout = textObj.AddComponent<LayoutElement>();
        textLayout.preferredWidth = nameText.preferredWidth + 8f;
        textLayout.preferredHeight = emojiSize.y;

        GameObject emojiObj = new GameObject("EmojiImage", typeof(RectTransform));
        emojiObj.transform.SetParent(row.transform, false);

        Image emojiImage = emojiObj.AddComponent<Image>();
        emojiImage.preserveAspect = true;

        Sprite sprite = GetEmojiSprite(emojiIndex);

        if (sprite != null)
        {
            emojiImage.sprite = sprite;
        }

        LayoutElement emojiLayout = emojiObj.AddComponent<LayoutElement>();
        emojiLayout.preferredWidth = emojiSize.x;
        emojiLayout.preferredHeight = emojiSize.y;
    }

    private bool TryParseEmojiMessage(string message, out int emojiIndex)
    {
        emojiIndex = -1;

        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        Match match = EmojiRegex.Match(message);

        if (!match.Success)
        {
            return false;
        }

        return int.TryParse(match.Groups[1].Value, out emojiIndex);
    }

    private Sprite GetEmojiSprite(int index)
    {
        if (emojiSprites == null || emojiSprites.Length == 0)
        {
            return null;
        }

        if (index < 0 || index >= emojiSprites.Length)
        {
            return null;
        }

        return emojiSprites[index];
    }

    private string GetDisplayName()
    {
        string name = PlayerPrefs.GetString("PlayerName", "");

        if (string.IsNullOrEmpty(name))
        {
            name = PlayerPrefs.GetString("displayName", "");
        }

        if (string.IsNullOrEmpty(name))
        {
            name = PlayerPrefs.GetString("username", "Player");
        }

        return name;
    }
}