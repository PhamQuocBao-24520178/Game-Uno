using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OnlineCardRenderer : MonoBehaviour
{
    [Header("Source")]
    [SerializeField]
    private OnlineGameSetupController onlineGameSetupController;

    [Header("Card Prefabs")]
    [SerializeField] private GameObject faceCardPrefab;
    [SerializeField] private GameObject backCardPrefab;

    [Header("My Hand")]
    [SerializeField] private RectTransform myHandContainer;
    [SerializeField] private float playerSpacing = 75f;

    [SerializeField]
    private Vector2 playerCardSize = new Vector2(160f, 210f);

    [Header("Opponent Hands")]
    [SerializeField] private RectTransform leftOpponentHandContainer;
    [SerializeField] private RectTransform topOpponentHandContainer;
    [SerializeField] private RectTransform rightOpponentHandContainer;

    [SerializeField] private float topOpponentSpacing = 28f;
    [SerializeField] private float sideOpponentSpacing = 28f;

    [SerializeField]
    private Vector2 topOpponentCardSize =
        new Vector2(110f, 145f);

    [SerializeField]
    private Vector2 sideOpponentCardSize =
        new Vector2(95f, 125f);

    [Header("Center")]
    [SerializeField] private RectTransform drawPileContainer;
    [SerializeField] private RectTransform discardPileContainer;

    [SerializeField]
    private Vector2 pileCardSize = new Vector2(160f, 210f);

    [Header("Sprites")]
    [SerializeField] private Sprite[] unoFaceSprites;
    [SerializeField] private Sprite unoBackSprite;

    [Header("Buttons / Text")]
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private Button drawButton;
    [SerializeField] private Button unoButton;

    [Header("Wild")]
    [SerializeField] private Image currentColorRing;
    [SerializeField] private OnlineWildColorPicker wildColorPicker;

    [Header("Invalid Card")]
    [SerializeField]
    private Color invalidCardTint =
        new Color(0.48f, 0.48f, 0.48f, 1f);

    private readonly Dictionary<string, Sprite> spriteLookup =
        new Dictionary<string, Sprite>();

    private string lastVisualSignature = "";
    private bool isSendingAction;

    private void Awake()
    {
        BuildSpriteLookup();

        if (drawButton != null)
        {
            drawButton.onClick.RemoveAllListeners();
            drawButton.onClick.AddListener(TryDrawCard);
        }

        if (unoButton != null)
        {
            unoButton.onClick.RemoveAllListeners();
            unoButton.onClick.AddListener(TryDeclareUno);
        }
    }

    private void Update()
    {
        if (onlineGameSetupController == null)
        {
            return;
        }

        OnlineGameStateResponse state =
            onlineGameSetupController.CurrentGameState;

        if (state == null)
        {
            return;
        }

        string signature = BuildVisualSignature(state);

        if (signature == lastVisualSignature)
        {
            return;
        }

        lastVisualSignature = signature;

        RenderGameState(state);
    }

    private void RenderGameState(OnlineGameStateResponse state)
    {
        RenderMyHand(state.myHand);
        RenderOpponentHands(state);
        RenderDrawPile(state.deckCount);
        RenderDiscardPile(state.topCard);

        UpdateCurrentColorRing(state.currentColor);
        UpdateTurnText();
        UpdateDrawButton();
        UpdateUnoButton();
    }

    // =====================================================
    // MY HAND
    // =====================================================

    private void RenderMyHand(OnlineApiCardData[] myHand)
    {
        ClearChildren(myHandContainer);

        if (myHandContainer == null ||
            faceCardPrefab == null ||
            myHand == null)
        {
            return;
        }

        float startX =
            -((myHand.Length - 1) * playerSpacing) * 0.5f;

        for (int i = 0; i < myHand.Length; i++)
        {
            OnlineApiCardData card = myHand[i];

            GameObject cardObject =
                Instantiate(faceCardPrefab, myHandContainer);

            cardObject.name =
                "OnlineMyCard_" + i + "_" +
                card.color + "_" + card.value;

            SetupRect(
                cardObject,
                playerCardSize,
                new Vector2(startX + i * playerSpacing, 0f),
                0f
            );

            SetCardSprite(cardObject, FindSpriteForCard(card));
            HideLegacyTexts(cardObject);

            bool canPlay = CanPlayCard(card);

            SetCardVisualState(cardObject, canPlay);

            Button cardButton =
                cardObject.GetComponent<Button>();

            if (cardButton == null)
            {
                cardButton =
                    cardObject.GetComponentInChildren<Button>(true);
            }

            if (cardButton != null)
            {
                OnlineApiCardData clickedCard = card;

                cardButton.onClick.RemoveAllListeners();
                cardButton.onClick.AddListener(
                    () => TryPlayCard(clickedCard)
                );

                cardButton.interactable = canPlay;
            }
        }
    }

    // =====================================================
    // OPPONENTS
    // =====================================================

    private void RenderOpponentHands(OnlineGameStateResponse state)
    {
        ClearChildren(leftOpponentHandContainer);
        ClearChildren(topOpponentHandContainer);
        ClearChildren(rightOpponentHandContainer);

        if (state == null || state.players == null)
        {
            return;
        }

        List<OnlineGamePlayerStateResponse> opponents =
            GetOpponents(state);

        int totalPlayers = state.players.Length;

        if (totalPlayers == 2)
        {
            if (opponents.Count > 0)
            {
                RenderTopOpponent(opponents[0].cardCount);
            }

            return;
        }

        if (totalPlayers == 3)
        {
            if (opponents.Count > 0)
            {
                RenderLeftOpponent(opponents[0].cardCount);
            }

            if (opponents.Count > 1)
            {
                RenderRightOpponent(opponents[1].cardCount);
            }

            return;
        }

        if (totalPlayers >= 4)
        {
            if (opponents.Count > 0)
            {
                RenderLeftOpponent(opponents[0].cardCount);
            }

            if (opponents.Count > 1)
            {
                RenderTopOpponent(opponents[1].cardCount);
            }

            if (opponents.Count > 2)
            {
                RenderRightOpponent(opponents[2].cardCount);
            }
        }
    }

    private List<OnlineGamePlayerStateResponse> GetOpponents(
        OnlineGameStateResponse state)
    {
        List<OnlineGamePlayerStateResponse> result =
            new List<OnlineGamePlayerStateResponse>();

        string myPlayerId =
            PlayerPrefs.GetString("OnlinePlayerId", "");

        for (int i = 0; i < state.players.Length; i++)
        {
            OnlineGamePlayerStateResponse player =
                state.players[i];

            if (player == null ||
                player.playerId == myPlayerId)
            {
                continue;
            }

            result.Add(player);
        }

        return result;
    }

    private void RenderTopOpponent(int cardCount)
    {
        RenderBackCards(
            topOpponentHandContainer,
            cardCount,
            topOpponentCardSize,
            topOpponentSpacing,
            false,
            0f,
            "Top"
        );
    }

    private void RenderLeftOpponent(int cardCount)
    {
        RenderBackCards(
            leftOpponentHandContainer,
            cardCount,
            sideOpponentCardSize,
            sideOpponentSpacing,
            true,
            90f,
            "Left"
        );
    }

    private void RenderRightOpponent(int cardCount)
    {
        RenderBackCards(
            rightOpponentHandContainer,
            cardCount,
            sideOpponentCardSize,
            sideOpponentSpacing,
            true,
            -90f,
            "Right"
        );
    }

    private void RenderBackCards(
        RectTransform container,
        int count,
        Vector2 cardSize,
        float spacing,
        bool vertical,
        float rotationZ,
        string positionName)
    {
        if (container == null ||
            backCardPrefab == null ||
            count <= 0)
        {
            return;
        }

        float start =
            -((count - 1) * spacing) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            GameObject cardObject =
                Instantiate(backCardPrefab, container);

            cardObject.name =
                "Online" + positionName + "Back_" + i;

            Vector2 position = vertical
                ? new Vector2(0f, start + i * spacing)
                : new Vector2(start + i * spacing, 0f);

            SetupRect(
                cardObject,
                cardSize,
                position,
                rotationZ
            );

            SetCardSprite(cardObject, unoBackSprite);
            HideLegacyTexts(cardObject);
        }
    }

    // =====================================================
    // CENTER PILES
    // =====================================================

    private void RenderDrawPile(int deckCount)
    {
        ClearChildren(drawPileContainer);

        if (drawPileContainer == null ||
            backCardPrefab == null ||
            deckCount <= 0)
        {
            return;
        }

        GameObject cardObject =
            Instantiate(backCardPrefab, drawPileContainer);

        SetupRect(
            cardObject,
            pileCardSize,
            Vector2.zero,
            0f
        );

        SetCardSprite(cardObject, unoBackSprite);
        HideLegacyTexts(cardObject);
    }

    private void RenderDiscardPile(OnlineApiCardData topCard)
    {
        ClearChildren(discardPileContainer);

        if (discardPileContainer == null ||
            faceCardPrefab == null ||
            topCard == null)
        {
            return;
        }

        GameObject cardObject =
            Instantiate(faceCardPrefab, discardPileContainer);

        SetupRect(
            cardObject,
            pileCardSize,
            Vector2.zero,
            0f
        );

        SetCardSprite(cardObject, FindSpriteForCard(topCard));
        HideLegacyTexts(cardObject);
    }

    // =====================================================
    // DRAW / UNO
    // =====================================================

    private void TryDrawCard()
    {
        if (isSendingAction ||
            onlineGameSetupController == null ||
            !onlineGameSetupController.IsMyTurn)
        {
            return;
        }

        string roomCode =
            PlayerPrefs.GetString("CurrentRoomCode", "");

        string playerId =
            PlayerPrefs.GetString("OnlinePlayerId", "");

        if (string.IsNullOrEmpty(roomCode) ||
            string.IsNullOrEmpty(playerId))
        {
            return;
        }

        isSendingAction = true;

        StartCoroutine(GameApiService.DrawCard(
            roomCode,
            playerId,
            onSuccess: response =>
            {
                lastVisualSignature = "";
                isSendingAction = false;
            },
            onError: error =>
            {
                Debug.LogError("Rút bài lỗi: " + error);
                isSendingAction = false;
            }
        ));
    }

    private void TryDeclareUno()
    {
        if (isSendingAction ||
            onlineGameSetupController == null ||
            onlineGameSetupController.CurrentGameState == null ||
            !onlineGameSetupController.IsMyTurn)
        {
            return;
        }

        OnlineGameStateResponse state =
            onlineGameSetupController.CurrentGameState;

        int cardCount =
            state.myHand != null
                ? state.myHand.Length
                : 0;

        if (cardCount != 2 || state.hasDeclaredUno)
        {
            return;
        }

        string roomCode =
            PlayerPrefs.GetString("CurrentRoomCode", "");

        string playerId =
            PlayerPrefs.GetString("OnlinePlayerId", "");

        if (string.IsNullOrEmpty(roomCode) ||
            string.IsNullOrEmpty(playerId))
        {
            return;
        }

        isSendingAction = true;

        StartCoroutine(GameApiService.DeclareUno(
            roomCode,
            playerId,
            onSuccess: response =>
            {
                lastVisualSignature = "";
                isSendingAction = false;
            },
            onError: error =>
            {
                Debug.LogError("UNO lỗi: " + error);
                isSendingAction = false;
            }
        ));
    }

    // =====================================================
    // PLAY CARD
    // =====================================================

    private bool CanPlayCard(OnlineApiCardData card)
    {
        if (card == null ||
            isSendingAction ||
            onlineGameSetupController == null ||
            onlineGameSetupController.CurrentGameState == null ||
            !onlineGameSetupController.IsMyTurn)
        {
            return false;
        }

        OnlineGameStateResponse state =
            onlineGameSetupController.CurrentGameState;

        // =================================================
        // UNITY RULE GIỐNG BACKEND
        //
        // Đang +2: sáng +2 và +4.
        // Đang +4: chỉ sáng +4.
        // =================================================

        if (state.pendingDrawPenalty > 0)
        {
            if (state.pendingPenaltyType == "DrawTwo")
            {
                return card.value == "DrawTwo" ||
                       card.value == "WildDrawFour";
            }

            if (state.pendingPenaltyType == "WildDrawFour")
            {
                return card.value == "WildDrawFour";
            }

            return false;
        }

        if (card.color == "Wild")
        {
            return true;
        }

        if (state.topCard == null)
        {
            return false;
        }

        bool sameColor =
            card.color == state.currentColor;

        bool sameValue =
            card.value == state.topCard.value;

        return sameColor || sameValue;
    }

    private void TryPlayCard(OnlineApiCardData card)
    {
        if (!CanPlayCard(card))
        {
            return;
        }

        if (card.color == "Wild")
        {
            if (wildColorPicker == null)
            {
                Debug.LogError(
                    "Chưa kéo Wild Color Picker vào Inspector."
                );

                return;
            }

            wildColorPicker.Open(
                color => SendPlayCard(card, color)
            );

            return;
        }

        SendPlayCard(card, "");
    }

    private void SendPlayCard(
        OnlineApiCardData card,
        string chosenColor)
    {
        string roomCode =
            PlayerPrefs.GetString("CurrentRoomCode", "");

        string playerId =
            PlayerPrefs.GetString("OnlinePlayerId", "");

        if (string.IsNullOrEmpty(roomCode) ||
            string.IsNullOrEmpty(playerId))
        {
            return;
        }

        isSendingAction = true;

        StartCoroutine(GameApiService.PlayCard(
            roomCode,
            playerId,
            card.cardId,
            chosenColor,
            onSuccess: response =>
            {
                lastVisualSignature = "";
                isSendingAction = false;
            },
            onError: error =>
            {
                Debug.LogError("Đánh bài lỗi: " + error);
                isSendingAction = false;
            }
        ));
    }

    // =====================================================
    // UI STATE
    // =====================================================

    private void UpdateDrawButton()
    {
        if (drawButton == null ||
            onlineGameSetupController == null)
        {
            return;
        }

        drawButton.interactable =
            !isSendingAction &&
            onlineGameSetupController.IsMyTurn;
    }

    private void UpdateUnoButton()
    {
        if (unoButton == null ||
            onlineGameSetupController == null ||
            onlineGameSetupController.CurrentGameState == null)
        {
            return;
        }

        OnlineGameStateResponse state =
            onlineGameSetupController.CurrentGameState;

        int cardCount =
            state.myHand != null
                ? state.myHand.Length
                : 0;

        unoButton.interactable =
            !isSendingAction &&
            onlineGameSetupController.IsMyTurn &&
            cardCount == 2 &&
            !state.hasDeclaredUno;
    }

    private void UpdateTurnText()
    {
        if (turnText == null ||
            onlineGameSetupController == null ||
            onlineGameSetupController.CurrentGameState == null)
        {
            return;
        }

        OnlineGameStateResponse state =
            onlineGameSetupController.CurrentGameState;

        if (!onlineGameSetupController.IsMyTurn)
        {
            turnText.text = "ĐANG CHỜ ĐỐI THỦ";
            return;
        }

        if (state.pendingDrawPenalty > 0)
        {
            string allowed =
                state.pendingPenaltyType == "DrawTwo"
                    ? "+2 HOẶC +4"
                    : "+4";

            turnText.text =
                "RÚT " + state.pendingDrawPenalty +
                " LÁ HOẶC CHỒNG " + allowed;

            return;
        }

        int cardCount =
            state.myHand != null
                ? state.myHand.Length
                : 0;

        if (cardCount == 2 && !state.hasDeclaredUno)
        {
            turnText.text = "BẤM UNO TRƯỚC KHI ĐÁNH";
            return;
        }

        if (cardCount == 2 && state.hasDeclaredUno)
        {
            turnText.text = "UNO! HÃY ĐÁNH BÀI";
            return;
        }

        turnText.text = "LƯỢT CỦA BẠN";
    }

    private void UpdateCurrentColorRing(string currentColor)
    {
        if (currentColorRing == null)
        {
            return;
        }

        Color color = Color.white;

        switch (currentColor)
        {
            case "Red":
                color = new Color(0.95f, 0.12f, 0.12f, 1f);
                break;

            case "Yellow":
                color = new Color(1f, 0.78f, 0.05f, 1f);
                break;

            case "Green":
                color = new Color(0.15f, 0.72f, 0.22f, 1f);
                break;

            case "Blue":
                color = new Color(0.10f, 0.43f, 0.95f, 1f);
                break;
        }

        currentColorRing.color = color;
    }

    private void SetCardVisualState(
        GameObject cardObject,
        bool isPlayable)
    {
        if (cardObject == null)
        {
            return;
        }

        CanvasGroup group =
            cardObject.GetComponent<CanvasGroup>();

        if (group == null)
        {
            group = cardObject.AddComponent<CanvasGroup>();
        }

        group.alpha = 1f;
        group.interactable = isPlayable;
        group.blocksRaycasts = isPlayable;

        Image image =
            cardObject.GetComponent<Image>();

        if (image == null)
        {
            image =
                cardObject.GetComponentInChildren<Image>(true);
        }

        if (image != null)
        {
            image.color = isPlayable
                ? Color.white
                : invalidCardTint;
        }
    }

    // =====================================================
    // SIGNATURE
    // =====================================================

    private string BuildVisualSignature(
        OnlineGameStateResponse state)
    {
        string result =
            state.deckCount + "|" +
            GetCardKey(state.topCard) + "|" +
            state.currentColor + "|" +
            state.currentTurnPlayerId + "|" +
            state.pendingDrawPenalty + "|" +
            state.pendingPenaltyType + "|" +
            state.hasDeclaredUno + "|";

        if (state.myHand != null)
        {
            for (int i = 0; i < state.myHand.Length; i++)
            {
                result += GetCardKey(state.myHand[i]) + "|";
            }
        }

        if (state.players != null)
        {
            for (int i = 0; i < state.players.Length; i++)
            {
                OnlineGamePlayerStateResponse player =
                    state.players[i];

                if (player != null)
                {
                    result +=
                        player.playerId + "_" +
                        player.cardCount + "|";
                }
            }
        }

        return result;
    }

    private string GetCardKey(OnlineApiCardData card)
    {
        if (card == null)
        {
            return "null";
        }

        return card.cardId + "_" +
               card.color + "_" +
               card.value;
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private void SetupRect(
        GameObject cardObject,
        Vector2 size,
        Vector2 position,
        float rotationZ)
    {
        RectTransform rect =
            cardObject.GetComponent<RectTransform>();

        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localRotation =
            Quaternion.Euler(0f, 0f, rotationZ);

        rect.localScale = Vector3.one;
    }

    private void ClearChildren(RectTransform container)
    {
        if (container == null)
        {
            return;
        }

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Destroy(container.GetChild(i).gameObject);
        }
    }

    private void SetCardSprite(
        GameObject cardObject,
        Sprite sprite)
    {
        if (cardObject == null || sprite == null)
        {
            return;
        }

        Image image =
            cardObject.GetComponent<Image>();

        if (image == null)
        {
            image =
                cardObject.GetComponentInChildren<Image>(true);
        }

        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = true;
    }

    private void HideLegacyTexts(GameObject cardObject)
    {
        if (cardObject == null)
        {
            return;
        }

        TMP_Text[] tmpTexts =
            cardObject.GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < tmpTexts.Length; i++)
        {
            tmpTexts[i].gameObject.SetActive(false);
        }

        Text[] legacyTexts =
            cardObject.GetComponentsInChildren<Text>(true);

        for (int i = 0; i < legacyTexts.Length; i++)
        {
            legacyTexts[i].gameObject.SetActive(false);
        }
    }

    // =====================================================
    // SPRITES
    // =====================================================

    private void BuildSpriteLookup()
    {
        spriteLookup.Clear();

        if (unoFaceSprites == null)
        {
            return;
        }

        for (int i = 0; i < unoFaceSprites.Length; i++)
        {
            Sprite sprite = unoFaceSprites[i];

            if (sprite == null)
            {
                continue;
            }

            string key = NormalizeName(sprite.name);

            if (!spriteLookup.ContainsKey(key))
            {
                spriteLookup.Add(key, sprite);
            }
        }
    }

    private Sprite FindSpriteForCard(OnlineApiCardData card)
    {
        if (card == null)
        {
            return null;
        }

        string[] keys = GetPossibleKeys(card);

        for (int i = 0; i < keys.Length; i++)
        {
            string key = NormalizeName(keys[i]);

            if (spriteLookup.TryGetValue(
                key,
                out Sprite sprite))
            {
                return sprite;
            }
        }

        foreach (KeyValuePair<string, Sprite> item in spriteLookup)
        {
            if (IsSpriteMatch(item.Key, card))
            {
                return item.Value;
            }
        }

        Debug.LogError(
            "Không tìm thấy sprite: " +
            card.color + " - " + card.value
        );

        return null;
    }

    private string[] GetPossibleKeys(OnlineApiCardData card)
    {
        string color = card.color.ToLower();
        string value = card.value.ToLower();

        if (color == "wild")
        {
            if (value == "wild")
            {
                return new[]
                {
                "wild",
                "joker",
                "black_wild"
            };
            }

            return new[]
            {
            "wild_draw4",
            "wilddraw4",
            "draw4",
            "plus4",
            "wild_plus4"
        };
        }

        if (value == "drawtwo")
        {
            value = "draw2";
        }

        return new[]
        {
        color + "_" + value,
        color + value,
        color + "-" + value
    };
    }

    private bool IsSpriteMatch(
        string spriteName,
        OnlineApiCardData card)
    {
        string name = NormalizeName(spriteName);

        string color = card.color.ToLower();
        string value = card.value.ToLower();

        if (color == "wild")
        {
            if (value == "wild")
            {
                return name.Contains("wild") &&
                       !name.Contains("draw4") &&
                       !name.Contains("plus4");
            }

            return name.Contains("draw4") ||
                   name.Contains("wilddraw4") ||
                   name.Contains("plus4");
        }

        if (value == "drawtwo")
        {
            value = "draw2";
        }

        return name.Contains(color) &&
               name.Contains(value);
    }

    private string NormalizeName(string value)
    {
        string result = value.ToLower().Trim();

        result = result.Replace(" ", "_");
        result = result.Replace("-", "_");

        result = result.Replace("+2", "draw2");
        result = result.Replace("plus2", "draw2");
        result = result.Replace("draw_2", "draw2");

        result = result.Replace("+4", "draw4");
        result = result.Replace("plus4", "draw4");
        result = result.Replace("draw_4", "draw4");

        while (result.Contains("__"))
        {
            result = result.Replace("__", "_");
        }

        return result;
    }

}