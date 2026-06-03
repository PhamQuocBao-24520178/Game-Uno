using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UnoGameDealController : MonoBehaviour
{
    private enum CardColor
    {
        Red,
        Yellow,
        Green,
        Blue,
        Wild
    }

    private enum CardValue
    {
        Zero,
        One,
        Two,
        Three,
        Four,
        Five,
        Six,
        Seven,
        Eight,
        Nine,
        Skip,
        Reverse,
        DrawTwo,
        Wild,
        WildDrawFour
    }

    private enum BotSide
    {
        Left,
        Top,
        Right
    }

    private class UnoCard
    {
        public CardColor color;
        public CardValue value;
        public Sprite sprite;
    }

    [Header("AUTO START")]
    [SerializeField] private bool autoDealOnStart = true;
    [SerializeField] private float startDelay = 0.3f;
    [SerializeField] private int cardsPerPlayer = 7;

    [Header("CARD PREFABS")]
    [SerializeField] private GameObject faceCardPrefab;
    [SerializeField] private GameObject backCardPrefab;

    [Header("CARD CONTAINERS")]
    [SerializeField] private RectTransform myHandContainer;
    [SerializeField] private RectTransform leftBotCardContainer;
    [SerializeField] private RectTransform topBotCardContainer;
    [SerializeField] private RectTransform rightBotCardContainer;
    [SerializeField] private RectTransform drawPileContainer;
    [SerializeField] private RectTransform discardPileContainer;

    [Header("DEAL TARGET POINTS")]
    [SerializeField] private RectTransform playerDealPoint;
    [SerializeField] private RectTransform bot1DealPoint;
    [SerializeField] private RectTransform bot2DealPoint;
    [SerializeField] private RectTransform bot3DealPoint;

    [Header("UNO POPUP")]
    [SerializeField] private UnoPopupManager unoPopupManager;

    [Header("DRAW PENALTY POPUP")]
    [SerializeField] private DrawPenaltyPopupManager drawPenaltyPopupManager;

    [Header("SPRITES")]
    [SerializeField] private Sprite[] unoFaceSprites;
    [SerializeField] private Sprite unoBackSprite;

    [Header("BUTTONS")]
    [SerializeField] private Button drawButton;
    [SerializeField] private Button drawPileButton;
    [SerializeField] private Button unoButton;

    [Header("COLOR PICKER")]
    [SerializeField] private GameObject colorChoosePanel;
    [SerializeField] private Button yellowButton;
    [SerializeField] private Button greenButton;
    [SerializeField] private Button blueButton;
    [SerializeField] private Button redButton;

    [Header("RINGS")]
    [SerializeField] private Image currentColorRing;
    [SerializeField] private TurnTimerRingManager turnTimerRingManager;
    [SerializeField] private float turnTimeLimit = 10f;

    [Header("ANIMATION")]
    [SerializeField] private CardAnimationManager cardAnimationManager;

    [Header("LAYOUT")]
    [SerializeField] private float playerSpacing = 75f;
    [SerializeField] private float botSpacing = 50f;
    [SerializeField] private Vector2 playerCardSize = new Vector2(160f, 210f);
    [SerializeField] private Vector2 botCardSize = new Vector2(120f, 160f);
    [SerializeField] private Vector2 drawPileCardSize = new Vector2(160f, 210f);
    [SerializeField] private Vector2 discardCardSize = new Vector2(160f, 210f);
    [SerializeField] private Vector2 discardTopOffset = new Vector2(0f, 0f);
    [SerializeField] private Vector2 discardUnderOffset = new Vector2(-10f, 10f);

    [Header("BOT SETTINGS")]
    [SerializeField] private float botDelaySeconds = 1f;

    [Header("END GAME")]
    [SerializeField] private string resultSceneName = "GameResult";

    private readonly Dictionary<string, Sprite> spriteLookup = new Dictionary<string, Sprite>();

    private readonly List<UnoCard> drawPile = new List<UnoCard>();
    private readonly List<UnoCard> discardPile = new List<UnoCard>();

    private int pendingDrawAmount = 0;
    private CardValue pendingDrawSource = CardValue.Zero;

    private readonly List<UnoCard> playerHand = new List<UnoCard>();
    private readonly List<UnoCard> leftBotHand = new List<UnoCard>();
    private readonly List<UnoCard> topBotHand = new List<UnoCard>();
    private readonly List<UnoCard> rightBotHand = new List<UnoCard>();

    private CardColor currentColor;
    private int currentTurnIndex = 0;
    private int direction = 1;

    private bool waitingForColorChoice = false;
    private bool playerPressedUno = false;
    private bool actionInProgress = false;
    private bool gameStarted = false;

    private UnoCard selectedWildCard = null;
    private RectTransform selectedWildCardRect = null;

    private void Awake()
    {
        SetupButtons();
    }

    private void Start()
    {
        if (colorChoosePanel != null)
        {
            colorChoosePanel.SetActive(false);
        }

        if (autoDealOnStart)
        {
            StartCoroutine(StartNewRoundRoutine());
        }
    }

    private void SetupButtons()
    {
        if (drawButton != null)
        {
            drawButton.onClick.RemoveAllListeners();
            drawButton.onClick.AddListener(PlayerDrawOneCardAndEndTurn);
        }

        if (drawPileButton != null)
        {
            drawPileButton.onClick.RemoveAllListeners();
            drawPileButton.onClick.AddListener(PlayerDrawOneCardAndEndTurn);
        }

        if (unoButton != null)
        {
            unoButton.onClick.RemoveAllListeners();
            unoButton.onClick.AddListener(PlayerPressUno);
        }

        if (yellowButton != null)
        {
            yellowButton.onClick.RemoveAllListeners();
            yellowButton.onClick.AddListener(() => ChooseWildColor(CardColor.Yellow));
        }

        if (greenButton != null)
        {
            greenButton.onClick.RemoveAllListeners();
            greenButton.onClick.AddListener(() => ChooseWildColor(CardColor.Green));
        }

        if (blueButton != null)
        {
            blueButton.onClick.RemoveAllListeners();
            blueButton.onClick.AddListener(() => ChooseWildColor(CardColor.Blue));
        }

        if (redButton != null)
        {
            redButton.onClick.RemoveAllListeners();
            redButton.onClick.AddListener(() => ChooseWildColor(CardColor.Red));
        }
    }

    [ContextMenu("Start New Round")]
    public void StartNewRound()
    {
        StopAllCoroutines();
        StartCoroutine(StartNewRoundRoutine());
    }

    private IEnumerator StartNewRoundRoutine()
    {
        actionInProgress = true;
        gameStarted = false;

        BuildSpriteLookup();

        if (turnTimerRingManager != null)
        {
            turnTimerRingManager.StopTimer();
            turnTimerRingManager.HideAllRings();
        }

        ClearAllData();
        ClearAllVisuals();

        currentTurnIndex = 0;
        direction = 1;
        waitingForColorChoice = false;
        playerPressedUno = false;
        selectedWildCard = null;
        selectedWildCardRect = null;

        pendingDrawAmount = 0;
        pendingDrawSource = CardValue.Zero;

        if (drawPenaltyPopupManager != null)
        {
            drawPenaltyPopupManager.HideAllPopups();
        }

        CreateStandard108Deck();
        Shuffle(drawPile);

        yield return new WaitForSeconds(startDelay);

        yield return StartCoroutine(DealOpeningHandsRoutine());

        OpenFirstDiscardCard();
        RefreshAllVisuals();
        UpdateCurrentColorRing();

        actionInProgress = false;
        gameStarted = true;

        StartTurn(0);
    }

    private IEnumerator DealOpeningHandsRoutine()
    {
        if (cardAnimationManager == null)
        {
            DealOpeningHandsWithoutAnimation();
            yield break;
        }

        List<CardAnimationRequest> requests = new List<CardAnimationRequest>();

        for (int i = 0; i < cardsPerPlayer; i++)
        {
            AddDealRequest(0, true, playerDealPoint, 0f, requests);
            AddDealRequest(1, false, bot1DealPoint, 90f, requests);
            AddDealRequest(2, false, bot2DealPoint, 180f, requests);
            AddDealRequest(3, false, bot3DealPoint, -90f, requests);
        }

        bool completed = false;

        cardAnimationManager.PlayDealSequence(requests, () =>
        {
            completed = true;
        });

        while (!completed)
        {
            yield return null;
        }
    }

    private void AddDealRequest(int playerIndex, bool faceUp, RectTransform targetPoint, float endRotation, List<CardAnimationRequest> requests)
    {
        UnoCard card = DrawOneFromDeck();

        if (card == null)
        {
            return;
        }

        CardAnimationRequest request = new CardAnimationRequest(
            target: targetPoint,
            faceSprite: card.sprite,
            faceUp: faceUp,
            endRotationZ: endRotation,
            duration: 0f,
            onArrive: () =>
            {
                AddCardToHandByIndex(playerIndex, card);
                RefreshAllVisuals();
            }
        );

        requests.Add(request);
    }

    private void DealOpeningHandsWithoutAnimation()
    {
        for (int i = 0; i < cardsPerPlayer; i++)
        {
            AddCardToHandByIndex(0, DrawOneFromDeck());
            AddCardToHandByIndex(1, DrawOneFromDeck());
            AddCardToHandByIndex(2, DrawOneFromDeck());
            AddCardToHandByIndex(3, DrawOneFromDeck());
        }
    }

    private void AddCardToHandByIndex(int index, UnoCard card)
    {
        if (card == null)
        {
            return;
        }

        List<UnoCard> hand = GetHandByTurnIndex(index);

        if (hand != null)
        {
            hand.Add(card);
        }
    }

    private void ClearAllData()
    {
        drawPile.Clear();
        discardPile.Clear();

        playerHand.Clear();
        leftBotHand.Clear();
        topBotHand.Clear();
        rightBotHand.Clear();
    }

    private void ClearAllVisuals()
    {
        ClearChildren(myHandContainer);
        ClearChildren(leftBotCardContainer);
        ClearChildren(topBotCardContainer);
        ClearChildren(rightBotCardContainer);
        ClearChildren(drawPileContainer);
        ClearChildren(discardPileContainer);
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

    private void CreateStandard108Deck()
    {
        AddColorSet(CardColor.Red);
        AddColorSet(CardColor.Yellow);
        AddColorSet(CardColor.Green);
        AddColorSet(CardColor.Blue);

        AddCard(CardColor.Wild, CardValue.Wild, 4);
        AddCard(CardColor.Wild, CardValue.WildDrawFour, 4);
    }

    private void AddColorSet(CardColor color)
    {
        AddCard(color, CardValue.Zero, 1);

        AddCard(color, CardValue.One, 2);
        AddCard(color, CardValue.Two, 2);
        AddCard(color, CardValue.Three, 2);
        AddCard(color, CardValue.Four, 2);
        AddCard(color, CardValue.Five, 2);
        AddCard(color, CardValue.Six, 2);
        AddCard(color, CardValue.Seven, 2);
        AddCard(color, CardValue.Eight, 2);
        AddCard(color, CardValue.Nine, 2);

        AddCard(color, CardValue.Skip, 2);
        AddCard(color, CardValue.Reverse, 2);
        AddCard(color, CardValue.DrawTwo, 2);
    }

    private void AddCard(CardColor color, CardValue value, int amount)
    {
        Sprite sprite = FindSpriteForCard(color, value);

        for (int i = 0; i < amount; i++)
        {
            UnoCard card = new UnoCard();
            card.color = color;
            card.value = value;
            card.sprite = sprite;
            drawPile.Add(card);
        }
    }

    private void Shuffle(List<UnoCard> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);

            UnoCard temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    private UnoCard DrawOneFromDeck()
    {
        if (drawPile.Count == 0)
        {
            RefillDrawPileFromDiscard();
        }

        if (drawPile.Count == 0)
        {
            return null;
        }

        UnoCard card = drawPile[0];
        drawPile.RemoveAt(0);
        return card;
    }

    private void RefillDrawPileFromDiscard()
    {
        if (discardPile.Count <= 1)
        {
            return;
        }

        UnoCard topCard = discardPile[discardPile.Count - 1];
        List<UnoCard> refillCards = new List<UnoCard>();

        for (int i = 0; i < discardPile.Count - 1; i++)
        {
            refillCards.Add(discardPile[i]);
        }

        discardPile.Clear();
        discardPile.Add(topCard);

        drawPile.AddRange(refillCards);
        Shuffle(drawPile);
    }

    private void OpenFirstDiscardCard()
    {
        UnoCard firstCard = DrawOneFromDeck();

        int safe = 0;

        while (firstCard != null && firstCard.color == CardColor.Wild && drawPile.Count > 0 && safe < 30)
        {
            drawPile.Add(firstCard);
            Shuffle(drawPile);
            firstCard = DrawOneFromDeck();
            safe++;
        }

        if (firstCard == null)
        {
            return;
        }

        discardPile.Add(firstCard);
        currentColor = firstCard.color;
    }

    private void StartTurn(int turnIndex)
    {
        if (!gameStarted)
        {
            return;
        }

        currentTurnIndex = turnIndex;
        actionInProgress = false;

        if (currentTurnIndex == 0)
        {
            playerPressedUno = false;
        }

        Debug.Log("Current turn: " + GetPlayerName(currentTurnIndex));

        if (turnTimerRingManager != null)
        {
            turnTimerRingManager.StartTurnTimer(currentTurnIndex, turnTimeLimit, OnTurnTimerFinished);
        }

        if (currentTurnIndex != 0)
        {
            StartCoroutine(BotTurnCoroutine(currentTurnIndex));
        }
    }

    private IEnumerator BotTurnCoroutine(int botIndex)
    {
        yield return new WaitForSeconds(botDelaySeconds);

        if (currentTurnIndex == botIndex && !actionInProgress)
        {
            BotTakeTurn(botIndex);
        }
    }

    private void BotTakeTurn(int botIndex)
    {
        if (actionInProgress)
        {
            return;
        }

        List<UnoCard> botHand = GetHandByTurnIndex(botIndex);

        if (botHand == null)
        {
            AdvanceTurn(1);
            return;
        }

        // Nếu bot đang bị cộng dồn +2/+4
        if (pendingDrawAmount > 0)
        {
            UnoCard stackCard = FindStackableDrawCard(botHand);

            if (stackCard == null)
            {
                StartCoroutine(DrawPendingPenaltyAndEndTurnRoutine(botIndex));
                return;
            }

            CardColor chosenColor = stackCard.color;

            if (IsWildCard(stackCard))
            {
                chosenColor = ChooseBestColorForBot(botHand);
            }

            PlayCard(botHand, stackCard, chosenColor, null);
            return;
        }

        UnoCard playableCard = FindPlayableCard(botHand);

        if (playableCard == null)
        {
            StartCoroutine(DrawCardAndEndTurnRoutine(botIndex));
            return;
        }

        CardColor normalChosenColor = playableCard.color;

        if (IsWildCard(playableCard))
        {
            normalChosenColor = ChooseBestColorForBot(botHand);
        }

        PlayCard(botHand, playableCard, normalChosenColor, null);
    }

    private void PlayerPressUno()
    {
        if (currentTurnIndex != 0)
        {
            Debug.Log("Chưa tới lượt Player.");
            return;
        }

        if (actionInProgress || waitingForColorChoice)
        {
            return;
        }

        if (playerHand.Count != 2)
        {
            Debug.Log("Chỉ bấm UNO khi còn đúng 2 lá.");
            return;
        }

        if (!HasPlayableCard(playerHand))
        {
            Debug.Log("Còn 2 lá nhưng không có bài đánh được, không cần UNO. Hãy rút 1 lá.");
            return;
        }

        playerPressedUno = true;

        if (unoPopupManager != null)
        {
            unoPopupManager.ShowPlayerUno();
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayUno();
        }

        Debug.Log("Player đã bấm UNO!");
    }

    private void PlayerDrawOneCardAndEndTurn()
    {
        if (currentTurnIndex != 0)
        {
            Debug.Log("Chưa tới lượt Player.");
            return;
        }

        if (actionInProgress || waitingForColorChoice)
        {
            return;
        }

        // Nếu đang bị cộng dồn +2/+4
        // bấm DRAW nghĩa là nhận rút tổng số lá đang bị phạt.
        if (pendingDrawAmount > 0)
        {
            StartCoroutine(DrawPendingPenaltyAndEndTurnRoutine(0));
            return;
        }

        if (HasPlayableCard(playerHand))
        {
            if (playerHand.Count == 2)
            {
                Debug.Log("Bạn còn 2 lá và có bài đánh được. Hãy bấm UNO rồi đánh bài.");
            }
            else
            {
                Debug.Log("Bạn còn bài đánh được, không được rút.");
            }

            return;
        }

        StartCoroutine(DrawCardAndEndTurnRoutine(0));
    }

    private IEnumerator DrawPendingPenaltyAndEndTurnRoutine(int playerIndex)
    {
        if (pendingDrawAmount <= 0)
        {
            AdvanceTurn(1);
            yield break;
        }

        actionInProgress = true;

        int amountToDraw = pendingDrawAmount;

        if (turnTimerRingManager != null)
        {
            turnTimerRingManager.StopTimer();
        }

        if (drawPenaltyPopupManager != null)
        {
            drawPenaltyPopupManager.ShowPenaltyByTurnIndex(playerIndex, amountToDraw);
        }

        RectTransform target = GetDealPointByIndex(playerIndex);
        bool faceUp = playerIndex == 0;

        for (int i = 0; i < amountToDraw; i++)
        {
            UnoCard drawnCard = DrawOneFromDeck();

            if (drawnCard == null)
            {
                continue;
            }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayDrawCard();
            }
            else
            {
                Debug.LogError("Không tìm thấy SoundManager.Instance khi rút phạt.");
            }

            if (cardAnimationManager != null && target != null)
            {
                bool completed = false;

                cardAnimationManager.PlayDrawToTarget(target, drawnCard.sprite, faceUp, () =>
                {
                    completed = true;
                });

                while (!completed)
                {
                    yield return null;
                }
            }

            AddCardToHandByIndex(playerIndex, drawnCard);
        }

        pendingDrawAmount = 0;
        pendingDrawSource = CardValue.Zero;
        playerPressedUno = false;

        RefreshAllVisuals();

        actionInProgress = false;

        Debug.Log(GetPlayerName(playerIndex) + " rút phạt +" + amountToDraw + " lá và hết lượt.");

        AdvanceTurn(1);
    }

    private IEnumerator DrawCardAndEndTurnRoutine(int playerIndex)
    {
        actionInProgress = true;

        UnoCard drawnCard = DrawOneFromDeck();

        if (drawnCard != null)
        {
            RectTransform target = GetDealPointByIndex(playerIndex);
            
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayDrawCard();
            }
            else
            {
                Debug.LogError("Không tìm thấy SoundManager.Instance khi rút bài.");
            }

            if (cardAnimationManager != null && target != null)
            {
                bool faceUp = playerIndex == 0;
                bool completed = false;

                cardAnimationManager.PlayDrawToTarget(target, drawnCard.sprite, faceUp, () =>
                {
                    completed = true;
                });

                while (!completed)
                {
                    yield return null;
                }
            }

            AddCardToHandByIndex(playerIndex, drawnCard);
        }

        playerPressedUno = false;

        RefreshAllVisuals();

        actionInProgress = false;

        Debug.Log(GetPlayerName(playerIndex) + " rút 1 lá và hết lượt.");

        AdvanceTurn(1);
    }

    private void OnPlayerCardClicked(UnoCard card, RectTransform cardRect)
    {
        if (currentTurnIndex != 0)
        {
            Debug.Log("Chưa tới lượt Player.");
            return;
        }

        if (actionInProgress || waitingForColorChoice)
        {
            return;
        }

        if (card == null)
        {
            return;
        }

        if (!CanPlayCard(card))
        {
            Debug.Log("Lá này không đánh được.");
            return;
        }

        if (playerHand.Count == 2 && !playerPressedUno)
        {
            StartCoroutine(UnoPenaltyAndEndTurnRoutine());
            return;
        }

        if (IsWildCard(card))
        {
            selectedWildCard = card;
            selectedWildCardRect = cardRect;
            waitingForColorChoice = true;

            if (colorChoosePanel != null)
            {
                colorChoosePanel.SetActive(true);
            }
            else
            {
                ChooseWildColor(CardColor.Red);
            }

            return;
        }

        PlayCard(playerHand, card, card.color, cardRect);
    }

    private IEnumerator UnoPenaltyAndEndTurnRoutine()
    {
        actionInProgress = true;

        UnoCard penaltyCard = DrawOneFromDeck();

        if (penaltyCard != null)
        {
            if (cardAnimationManager != null && playerDealPoint != null)
            {
                bool completed = false;

                cardAnimationManager.PlayDrawToTarget(playerDealPoint, penaltyCard.sprite, true, () =>
                {
                    completed = true;
                });

                while (!completed)
                {
                    yield return null;
                }
            }

            playerHand.Add(penaltyCard);
        }

        playerPressedUno = false;

        RefreshAllVisuals();

        actionInProgress = false;

        Debug.Log("Quên bấm UNO → rút phạt 1 lá và hết lượt.");

        AdvanceTurn(1);
    }

    private void ChooseWildColor(CardColor chosenColor)
    {
        if (!waitingForColorChoice || selectedWildCard == null)
        {
            return;
        }

        if (colorChoosePanel != null)
        {
            colorChoosePanel.SetActive(false);
        }

        UnoCard cardToPlay = selectedWildCard;
        RectTransform cardRect = selectedWildCardRect;

        selectedWildCard = null;
        selectedWildCardRect = null;
        waitingForColorChoice = false;

        PlayCard(playerHand, cardToPlay, chosenColor, cardRect);
    }

    private void PlayCard(List<UnoCard> hand, UnoCard card, CardColor chosenColor, RectTransform sourceRect)
    {
        if (actionInProgress)
        {
            return;
        }

        if (hand == null || card == null || !hand.Contains(card))
        {
            return;
        }

        StartCoroutine(PlayCardRoutine(hand, card, chosenColor, sourceRect));
    }

    private IEnumerator PlayCardRoutine(List<UnoCard> hand, UnoCard card, CardColor chosenColor, RectTransform sourceRect)
    {
        actionInProgress = true;

        int playerIndexBeforePlay = currentTurnIndex;

        if (cardAnimationManager != null && sourceRect != null)
        {
            bool completed = false;

            cardAnimationManager.PlayCardToDiscard(sourceRect, card.sprite, () =>
            {
                completed = true;
            });

            while (!completed)
            {
                yield return null;
            }
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayPlayCard();
        }
        hand.Remove(card);
        discardPile.Add(card);

        if (IsWildCard(card))
        {
            currentColor = chosenColor;
        }
        else
        {
            currentColor = card.color;
        }

        RefreshAllVisuals();
        UpdateCurrentColorRing();

        // Hiện UNO popup sau khi refresh UI xong
        TryShowUnoPopupAfterPlay(hand, playerIndexBeforePlay);

        // Nếu người vừa đánh hết bài thì kết thúc game ngay
        if (CheckGameOverAfterPlay(hand))
        {
            yield break;
        }

        actionInProgress = false;

        ApplyCardEffect(card);
    }

    private void ApplyCardEffect(UnoCard card)
    {
        switch (card.value)
        {
            case CardValue.Skip:
                AdvanceTurn(2);
                break;

            case CardValue.Reverse:
                direction *= -1;
                AdvanceTurn(1);
                break;

            case CardValue.DrawTwo:
                pendingDrawAmount += 2;
                pendingDrawSource = CardValue.DrawTwo;

                Debug.Log("Đánh +2. Tổng phạt hiện tại: +" + pendingDrawAmount);

                AdvanceTurn(1);
                break;

            case CardValue.WildDrawFour:
                pendingDrawAmount += 4;
                pendingDrawSource = CardValue.WildDrawFour;

                Debug.Log("Đánh +4. Tổng phạt hiện tại: +" + pendingDrawAmount);

                AdvanceTurn(1);
                break;

            default:
                AdvanceTurn(1);
                break;
        }
    }

    private void DrawCardsForNextPlayer(int amount)
    {
        int nextIndex = GetNextTurnIndex(currentTurnIndex, 1);
        List<UnoCard> nextHand = GetHandByTurnIndex(nextIndex);

        if (nextHand == null)
        {
            return;
        }

        for (int i = 0; i < amount; i++)
        {
            UnoCard card = DrawOneFromDeck();

            if (card != null)
            {
                nextHand.Add(card);
            }
        }

        RefreshAllVisuals();
    }

    private void AdvanceTurn(int step)
    {
        currentTurnIndex = GetNextTurnIndex(currentTurnIndex, step);
        StartTurn(currentTurnIndex);
    }

    private int GetNextTurnIndex(int fromIndex, int step)
    {
        int result = fromIndex;

        for (int i = 0; i < step; i++)
        {
            result += direction;

            if (result > 3)
            {
                result = 0;
            }
            else if (result < 0)
            {
                result = 3;
            }
        }

        return result;
    }

    private void TryShowUnoPopupAfterPlay(List<UnoCard> hand, int playerIndex)
    {
        if (unoPopupManager == null)
        {
            return;
        }

        if (hand == null)
        {
            return;
        }

        if (hand.Count != 1)
        {
            return;
        }

        // Player chỉ hiện và phát tiếng nếu đã bấm UNO trước đó
        if (playerIndex == 0)
        {
            if (playerPressedUno)
            {
                unoPopupManager.ShowPlayerUno();

                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlayUno();
                }
            }

            return;
        }

        // Bot tự hiện UNO khi đánh còn 1 lá
        unoPopupManager.ShowUnoByTurnIndex(playerIndex);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayUno();
        }
    }

    private void OnTurnTimerFinished()
    {
        if (!gameStarted || actionInProgress)
        {
            return;
        }

        if (waitingForColorChoice)
        {
            waitingForColorChoice = false;
            selectedWildCard = null;
            selectedWildCardRect = null;

            if (colorChoosePanel != null)
            {
                colorChoosePanel.SetActive(false);
            }
        }

        Debug.Log(GetPlayerName(currentTurnIndex) + " hết giờ → rút 1 lá và hết lượt.");

        if (pendingDrawAmount > 0)
        {
            StartCoroutine(DrawPendingPenaltyAndEndTurnRoutine(currentTurnIndex));
        }
        else
        {
            StartCoroutine(DrawCardAndEndTurnRoutine(currentTurnIndex));
        }
    }

    private bool CanPlayCard(UnoCard card)
    {
        if (card == null)
        {
            return false;
        }

        if (discardPile.Count == 0)
        {
            return true;
        }

        // Đang bị phạt +2/+4 thì chỉ được đánh đè lá phạt hợp lệ
        if (pendingDrawAmount > 0)
        {
            return CanStackDrawCard(card);
        }

        UnoCard topCard = discardPile[discardPile.Count - 1];

        if (card.value == CardValue.Wild || card.value == CardValue.WildDrawFour)
        {
            return true;
        }

        if (card.color == currentColor)
        {
            return true;
        }

        if (card.value == topCard.value)
        {
            return true;
        }

        return false;
    }

    private void SortPlayerHand()
    {
        playerHand.Sort((a, b) =>
        {
            int colorCompare = GetColorSortOrder(a.color).CompareTo(GetColorSortOrder(b.color));

            if (colorCompare != 0)
            {
                return colorCompare;
            }

            return GetValueSortOrder(a.value).CompareTo(GetValueSortOrder(b.value));
        });
    }

    private int GetColorSortOrder(CardColor color)
    {
        switch (color)
        {
            case CardColor.Red:
                return 0;

            case CardColor.Blue:
                return 1;

            case CardColor.Yellow:
                return 2;

            case CardColor.Green:
                return 3;

            case CardColor.Wild:
                return 4;

            default:
                return 99;
        }
    }

    private int GetValueSortOrder(CardValue value)
    {
        switch (value)
        {
            case CardValue.Zero:
                return 0;

            case CardValue.One:
                return 1;

            case CardValue.Two:
                return 2;

            case CardValue.Three:
                return 3;

            case CardValue.Four:
                return 4;

            case CardValue.Five:
                return 5;

            case CardValue.Six:
                return 6;

            case CardValue.Seven:
                return 7;

            case CardValue.Eight:
                return 8;

            case CardValue.Nine:
                return 9;

            case CardValue.Skip:
                return 10;

            case CardValue.Reverse:
                return 11;

            case CardValue.DrawTwo:
                return 12;

            case CardValue.Wild:
                return 13;

            case CardValue.WildDrawFour:
                return 14;

            default:
                return 99;
        }
    }

    private Color GetPlayerCardTint(UnoCard card)
    {
        if (CanHighlightPlayerCard(card))
        {
            return Color.white;
        }

        return new Color(0.42f, 0.42f, 0.42f, 1f);
    }

    private bool CanHighlightPlayerCard(UnoCard card)
    {
        if (card == null)
        {
            return false;
        }

        // Đang bị phạt +2/+4 thì không xét màu nữa
        // Chỉ xét lá đánh đè phạt
        if (pendingDrawAmount > 0)
        {
            return CanStackDrawCard(card);
        }

        if (discardPile.Count == 0)
        {
            return true;
        }

        UnoCard topCard = discardPile[discardPile.Count - 1];

        if (card.value == CardValue.Wild || card.value == CardValue.WildDrawFour)
        {
            return true;
        }

        if (card.color == currentColor)
        {
            return true;
        }

        if (card.value == topCard.value)
        {
            return true;
        }

        return false;
    }

    private bool CanStackDrawCard(UnoCard card)
    {
        if (card == null)
        {
            return false;
        }

        if (pendingDrawSource == CardValue.DrawTwo)
        {
            return card.value == CardValue.DrawTwo ||
                   card.value == CardValue.WildDrawFour;
        }

        if (pendingDrawSource == CardValue.WildDrawFour)
        {
            return card.value == CardValue.WildDrawFour;
        }

        return false;
    }

    private bool HasStackableDrawCard(List<UnoCard> hand)
    {
        if (hand == null)
        {
            return false;
        }

        for (int i = 0; i < hand.Count; i++)
        {
            if (CanStackDrawCard(hand[i]))
            {
                return true;
            }
        }

        return false;
    }

    private UnoCard FindStackableDrawCard(List<UnoCard> hand)
    {
        if (hand == null)
        {
            return null;
        }

        for (int i = 0; i < hand.Count; i++)
        {
            if (CanStackDrawCard(hand[i]))
            {
                return hand[i];
            }
        }

        return null;
    }

    private bool HasPlayableCard(List<UnoCard> hand)
    {
        if (hand == null)
        {
            return false;
        }

        for (int i = 0; i < hand.Count; i++)
        {
            if (CanPlayCard(hand[i]))
            {
                return true;
            }
        }

        return false;
    }

    private UnoCard FindPlayableCard(List<UnoCard> hand)
    {
        if (hand == null)
        {
            return null;
        }

        for (int i = 0; i < hand.Count; i++)
        {
            if (CanPlayCard(hand[i]))
            {
                return hand[i];
            }
        }

        return null;
    }

    private bool IsWildCard(UnoCard card)
    {
        return card.value == CardValue.Wild || card.value == CardValue.WildDrawFour;
    }

    private CardColor ChooseBestColorForBot(List<UnoCard> hand)
    {
        int red = 0;
        int yellow = 0;
        int green = 0;
        int blue = 0;

        for (int i = 0; i < hand.Count; i++)
        {
            switch (hand[i].color)
            {
                case CardColor.Red:
                    red++;
                    break;

                case CardColor.Yellow:
                    yellow++;
                    break;

                case CardColor.Green:
                    green++;
                    break;

                case CardColor.Blue:
                    blue++;
                    break;
            }
        }

        int max = red;
        CardColor best = CardColor.Red;

        if (yellow > max)
        {
            max = yellow;
            best = CardColor.Yellow;
        }

        if (green > max)
        {
            max = green;
            best = CardColor.Green;
        }

        if (blue > max)
        {
            best = CardColor.Blue;
        }

        return best;
    }

    private List<UnoCard> GetHandByTurnIndex(int index)
    {
        switch (index)
        {
            case 0:
                return playerHand;

            case 1:
                return leftBotHand;

            case 2:
                return topBotHand;

            case 3:
                return rightBotHand;

            default:
                return null;
        }
    }

    private RectTransform GetDealPointByIndex(int index)
    {
        switch (index)
        {
            case 0:
                return playerDealPoint;

            case 1:
                return bot1DealPoint;

            case 2:
                return bot2DealPoint;

            case 3:
                return bot3DealPoint;

            default:
                return playerDealPoint;
        }
    }

    private string GetPlayerName(int index)
    {
        switch (index)
        {
            case 0:
                return "Player";

            case 1:
                return "Bot 1";

            case 2:
                return "Bot 2";

            case 3:
                return "Bot 3";

            default:
                return "Unknown";
        }
    }

    private void RefreshAllVisuals()
    {
        RefreshPlayerHand();
        RefreshBotHand(leftBotCardContainer, leftBotHand, BotSide.Left);
        RefreshBotHand(topBotCardContainer, topBotHand, BotSide.Top);
        RefreshBotHand(rightBotCardContainer, rightBotHand, BotSide.Right);
        RefreshDrawPileVisual();
        RefreshDiscardPileVisual();
    }

    private void RefreshPlayerHand()
    {
        ClearChildren(myHandContainer);

        if (myHandContainer == null || faceCardPrefab == null)
        {
            return;
        }

        SortPlayerHand();

        int count = playerHand.Count;

        if (count <= 0)
        {
            return;
        }

        float startX = -((count - 1) * playerSpacing) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            UnoCard card = playerHand[i];

            GameObject cardObject = Instantiate(faceCardPrefab, myHandContainer);
            cardObject.name = "PlayerCard_" + i + "_" + card.color + "_" + card.value;

            RectTransform rect = cardObject.GetComponent<RectTransform>();

            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = playerCardSize;
                rect.anchoredPosition = new Vector2(startX + i * playerSpacing, 0f);
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
            }

            SetCardSprite(cardObject, card.sprite);
            SetPlayerCardTint(cardObject, GetPlayerCardTint(card));
            HideLegacyTexts(cardObject);

            Button button = cardObject.GetComponent<Button>();

            if (button != null)
            {
                button.onClick.RemoveAllListeners();

                UnoCard capturedCard = card;
                RectTransform capturedRect = rect;

                button.interactable = true;
                button.onClick.AddListener(() => OnPlayerCardClicked(capturedCard, capturedRect));
            }
        }
    }

    private void SetPlayerCardTint(GameObject cardObject, Color tintColor)
    {
        if (cardObject == null)
        {
            return;
        }

        Image[] images = cardObject.GetComponentsInChildren<Image>(true);

        for (int i = 0; i < images.Length; i++)
        {
            images[i].color = tintColor;
        }
    }

    private void RefreshBotHand(RectTransform container, List<UnoCard> hand, BotSide side)
    {
        ClearChildren(container);

        if (container == null || backCardPrefab == null || hand == null)
        {
            return;
        }

        int count = hand.Count;

        if (side == BotSide.Top)
        {
            float startX = -((count - 1) * botSpacing) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                GameObject cardObject = Instantiate(backCardPrefab, container);
                cardObject.name = "TopBotCard_" + i;

                RectTransform rect = cardObject.GetComponent<RectTransform>();

                if (rect != null)
                {
                    rect.sizeDelta = botCardSize;
                    rect.anchoredPosition = new Vector2(startX + i * botSpacing, 0f);
                    rect.localRotation = Quaternion.Euler(0f, 0f, 180f);
                    rect.localScale = Vector3.one;
                }

                SetCardSprite(cardObject, unoBackSprite);
                HideLegacyTexts(cardObject);
            }
        }
        else
        {
            float startY = ((count - 1) * botSpacing) * 0.5f;
            float rotation = side == BotSide.Left ? 90f : -90f;

            for (int i = 0; i < count; i++)
            {
                GameObject cardObject = Instantiate(backCardPrefab, container);
                cardObject.name = side + "BotCard_" + i;

                RectTransform rect = cardObject.GetComponent<RectTransform>();

                if (rect != null)
                {
                    rect.sizeDelta = botCardSize;
                    rect.anchoredPosition = new Vector2(0f, startY - i * botSpacing);
                    rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
                    rect.localScale = Vector3.one;
                }

                SetCardSprite(cardObject, unoBackSprite);
                HideLegacyTexts(cardObject);
            }
        }
    }

    private void RefreshDrawPileVisual()
    {
        ClearChildren(drawPileContainer);

        if (drawPileContainer == null || backCardPrefab == null || drawPile.Count <= 0)
        {
            return;
        }

        GameObject cardObject = Instantiate(backCardPrefab, drawPileContainer);
        cardObject.name = "DrawPileTop";

        RectTransform rect = cardObject.GetComponent<RectTransform>();

        if (rect != null)
        {
            rect.sizeDelta = drawPileCardSize;
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        SetCardSprite(cardObject, unoBackSprite);
        HideLegacyTexts(cardObject);
    }

    private void RefreshDiscardPileVisual()
    {
        ClearChildren(discardPileContainer);

        if (discardPileContainer == null || faceCardPrefab == null || discardPile.Count <= 0)
        {
            return;
        }

        if (discardPile.Count >= 2)
        {
            UnoCard underCard = discardPile[discardPile.Count - 2];

            GameObject underObject = Instantiate(faceCardPrefab, discardPileContainer);
            underObject.name = "DiscardUnder";

            RectTransform underRect = underObject.GetComponent<RectTransform>();

            if (underRect != null)
            {
                underRect.sizeDelta = discardCardSize;
                underRect.anchoredPosition = discardUnderOffset;
                underRect.localRotation = Quaternion.identity;
                underRect.localScale = Vector3.one;
            }

            SetCardSprite(underObject, underCard.sprite);
            HideLegacyTexts(underObject);

            Button underButton = underObject.GetComponent<Button>();
            if (underButton != null)
            {
                underButton.onClick.RemoveAllListeners();
            }
        }

        UnoCard topCard = discardPile[discardPile.Count - 1];

        GameObject topObject = Instantiate(faceCardPrefab, discardPileContainer);
        topObject.name = "DiscardTop";

        RectTransform topRect = topObject.GetComponent<RectTransform>();

        if (topRect != null)
        {
            topRect.sizeDelta = discardCardSize;
            topRect.anchoredPosition = discardTopOffset;
            topRect.localRotation = Quaternion.identity;
            topRect.localScale = Vector3.one;
        }

        SetCardSprite(topObject, topCard.sprite);
        HideLegacyTexts(topObject);

        Button topButton = topObject.GetComponent<Button>();
        if (topButton != null)
        {
            topButton.onClick.RemoveAllListeners();
        }
    }

    private void UpdateCurrentColorRing()
    {
        if (currentColorRing == null)
        {
            return;
        }

        currentColorRing.color = GetUnityColor(currentColor);
    }

    private Color GetUnityColor(CardColor color)
    {
        switch (color)
        {
            case CardColor.Red:
                return new Color(0.95f, 0.05f, 0.04f, 1f);

            case CardColor.Yellow:
                return new Color(1f, 0.75f, 0.02f, 1f);

            case CardColor.Green:
                return new Color(0.1f, 0.75f, 0.1f, 1f);

            case CardColor.Blue:
                return new Color(0.05f, 0.35f, 1f, 1f);

            default:
                return Color.white;
        }
    }

    private void SetCardSprite(GameObject cardObject, Sprite sprite)
    {
        if (cardObject == null || sprite == null)
        {
            return;
        }

        Image image = cardObject.GetComponent<Image>();

        if (image == null)
        {
            image = cardObject.GetComponentInChildren<Image>(true);
        }

        if (image != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = true;
        }
    }

    private void HideLegacyTexts(GameObject obj)
    {
        if (obj == null)
        {
            return;
        }

        TMPro.TMP_Text[] tmpTexts = obj.GetComponentsInChildren<TMPro.TMP_Text>(true);

        for (int i = 0; i < tmpTexts.Length; i++)
        {
            tmpTexts[i].gameObject.SetActive(false);
        }

        Text[] oldTexts = obj.GetComponentsInChildren<Text>(true);

        for (int i = 0; i < oldTexts.Length; i++)
        {
            oldTexts[i].gameObject.SetActive(false);
        }
    }

    private Sprite FindSpriteForCard(CardColor color, CardValue value)
    {
        string[] exactKeys = GetPossibleKeys(color, value);

        for (int i = 0; i < exactKeys.Length; i++)
        {
            string key = NormalizeName(exactKeys[i]);

            if (spriteLookup.ContainsKey(key))
            {
                return spriteLookup[key];
            }
        }

        foreach (KeyValuePair<string, Sprite> pair in spriteLookup)
        {
            if (IsSpriteMatch(pair.Key, color, value))
            {
                return pair.Value;
            }
        }

        Debug.LogError("Không tìm thấy sprite cho lá: " + color + " - " + value);
        return null;
    }

    private string[] GetPossibleKeys(CardColor color, CardValue value)
    {
        string colorKey = ColorToKey(color);
        string valueKey = ValueToKey(value);

        if (color == CardColor.Wild)
        {
            if (value == CardValue.Wild)
            {
                return new string[]
                {
                    "wild",
                    "joker",
                    "black_wild"
                };
            }

            return new string[]
            {
                "wild_draw4",
                "wilddraw4",
                "draw4",
                "plus4",
                "wild_plus4"
            };
        }

        return new string[]
        {
            colorKey + "_" + valueKey,
            colorKey + valueKey,
            colorKey + "-" + valueKey
        };
    }

    private bool IsSpriteMatch(string spriteName, CardColor color, CardValue value)
    {
        string name = NormalizeName(spriteName);

        if (color == CardColor.Wild)
        {
            if (value == CardValue.Wild)
            {
                return name.Contains("wild") &&
                       !name.Contains("draw4") &&
                       !name.Contains("plus4");
            }

            return (name.Contains("wild") && name.Contains("draw4")) ||
                   (name.Contains("wild") && name.Contains("plus4")) ||
                   name.Contains("wilddraw4") ||
                   name.Contains("draw4");
        }

        string colorKey = ColorToKey(color);
        string valueKey = ValueToKey(value);

        return name.Contains(colorKey) && name.Contains(valueKey);
    }

    private string NormalizeName(string value)
    {
        string s = value.ToLower().Trim();

        s = s.Replace(" ", "_");
        s = s.Replace("-", "_");

        s = s.Replace("+2", "draw2");
        s = s.Replace("plus2", "draw2");
        s = s.Replace("draw_2", "draw2");

        s = s.Replace("+4", "draw4");
        s = s.Replace("plus4", "draw4");
        s = s.Replace("draw_4", "draw4");

        while (s.Contains("__"))
        {
            s = s.Replace("__", "_");
        }

        return s;
    }

    private string ColorToKey(CardColor color)
    {
        switch (color)
        {
            case CardColor.Red:
                return "red";

            case CardColor.Yellow:
                return "yellow";

            case CardColor.Green:
                return "green";

            case CardColor.Blue:
                return "blue";

            default:
                return "wild";
        }
    }

    private bool CheckGameOverAfterPlay(List<UnoCard> hand)
    {
        if (hand == null)
        {
            return false;
        }

        if (hand.Count > 0)
        {
            return false;
        }

        EndGame(currentTurnIndex);
        return true;
    }

    private void EndGame(int winnerIndex)
    {
        Debug.Log(GetPlayerName(winnerIndex) + " đã hết bài. Game kết thúc!");

        gameStarted = false;
        actionInProgress = true;
        waitingForColorChoice = false;

        StopAllCoroutines();

        if (turnTimerRingManager != null)
        {
            turnTimerRingManager.StopTimer();
            turnTimerRingManager.HideAllRings();
        }

        if (colorChoosePanel != null)
        {
            colorChoosePanel.SetActive(false);
        }

        PlayerPrefs.SetInt("WinnerIndex", winnerIndex);
        PlayerPrefs.SetString("WinnerName", GetPlayerName(winnerIndex));

        PlayerPrefs.SetInt("PlayerCardCount", playerHand.Count);
        PlayerPrefs.SetInt("Bot1CardCount", leftBotHand.Count);
        PlayerPrefs.SetInt("Bot2CardCount", topBotHand.Count);
        PlayerPrefs.SetInt("Bot3CardCount", rightBotHand.Count);

        PlayerPrefs.Save();

        SceneManager.LoadScene(resultSceneName);
    }

    private string ValueToKey(CardValue value)
    {
        switch (value)
        {
            case CardValue.Zero:
                return "0";

            case CardValue.One:
                return "1";

            case CardValue.Two:
                return "2";

            case CardValue.Three:
                return "3";

            case CardValue.Four:
                return "4";

            case CardValue.Five:
                return "5";

            case CardValue.Six:
                return "6";

            case CardValue.Seven:
                return "7";

            case CardValue.Eight:
                return "8";

            case CardValue.Nine:
                return "9";

            case CardValue.Skip:
                return "skip";

            case CardValue.Reverse:
                return "reverse";

            case CardValue.DrawTwo:
                return "draw2";

            case CardValue.Wild:
                return "wild";

            case CardValue.WildDrawFour:
                return "wild_draw4";

            default:
                return "";
        }
    }
}