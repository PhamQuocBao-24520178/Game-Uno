using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OnlineInitialDealAnimation : MonoBehaviour
{
    [Header("Source")]
    [SerializeField]
    private OnlineGameSetupController onlineGameSetupController;

    [SerializeField]
    private CardAnimationManager cardAnimationManager;

    [Header("Card Containers")]
    [SerializeField] private RectTransform myHandContainer;
    [SerializeField] private RectTransform leftOpponentHandContainer;
    [SerializeField] private RectTransform topOpponentHandContainer;
    [SerializeField] private RectTransform rightOpponentHandContainer;

    [Header("Animation")]
    [SerializeField, Range(0.01f, 0.2f)]
    private float delayBeforeDeal = 0.08f;

    private string animatedRoomCode = "";
    private bool isPlayingInitialDeal;

    private void Update()
    {
        if (isPlayingInitialDeal ||
            onlineGameSetupController == null ||
            cardAnimationManager == null)
        {
            return;
        }

        OnlineGameStateResponse state =
            onlineGameSetupController.CurrentGameState;

        if (state == null ||
            state.status != "Playing" ||
            state.players == null ||
            state.players.Length < 2 ||
            state.myHand == null ||
            state.myHand.Length != 7 ||
            string.IsNullOrEmpty(state.roomCode))
        {
            return;
        }

        // Mỗi phòng chỉ chạy animation chia bài một lần.
        if (animatedRoomCode == state.roomCode)
        {
            return;
        }

        StartCoroutine(PlayInitialDealRoutine(state));
    }

    private IEnumerator PlayInitialDealRoutine(
        OnlineGameStateResponse state)
    {
        isPlayingInitialDeal = true;

        // Chờ OnlineCardRenderer render đủ các lá bài thật.
        yield return null;
        yield return new WaitForSeconds(delayBeforeDeal);

        List<CardAnimationRequest> requests =
            BuildDealRequests(state);

        if (requests.Count == 0)
        {
            animatedRoomCode = state.roomCode;
            isPlayingInitialDeal = false;
            yield break;
        }

        HideAllTargetCards(requests);

        bool isFinished = false;

        cardAnimationManager.PlayDealSequence(
            requests,
            () =>
            {
                isFinished = true;
            }
        );

        while (!isFinished)
        {
            yield return null;
        }

        ShowAllTargetCards(requests);

        animatedRoomCode = state.roomCode;
        isPlayingInitialDeal = false;
    }

    private List<CardAnimationRequest> BuildDealRequests(
        OnlineGameStateResponse state)
    {
        List<CardAnimationRequest> requests =
            new List<CardAnimationRequest>();

        int totalPlayers = state.players.Length;

        List<RectTransform> myCards =
            GetChildren(myHandContainer);

        List<RectTransform> leftCards =
            GetChildren(leftOpponentHandContainer);

        List<RectTransform> topCards =
            GetChildren(topOpponentHandContainer);

        List<RectTransform> rightCards =
            GetChildren(rightOpponentHandContainer);

        // Mỗi vòng chia một lá cho từng người.
        // Tổng cộng 7 vòng.
        for (int cardIndex = 0; cardIndex < 7; cardIndex++)
        {
            // Bài của mình: lật mặt trước.
            if (cardIndex < myCards.Count)
            {
                Sprite mySprite =
                    GetMyCardSprite(state, cardIndex);

                requests.Add(
                    CreateRequest(
                        myCards[cardIndex],
                        mySprite,
                        true
                    )
                );
            }

            // 2 người:
            // mình dưới, đối thủ trên.
            if (totalPlayers == 2)
            {
                if (cardIndex < topCards.Count)
                {
                    requests.Add(
                        CreateRequest(
                            topCards[cardIndex],
                            null,
                            false
                        )
                    );
                }

                continue;
            }

            // 3 người:
            // mình dưới, trái, phải.
            if (totalPlayers == 3)
            {
                if (cardIndex < leftCards.Count)
                {
                    requests.Add(
                        CreateRequest(
                            leftCards[cardIndex],
                            null,
                            false
                        )
                    );
                }

                if (cardIndex < rightCards.Count)
                {
                    requests.Add(
                        CreateRequest(
                            rightCards[cardIndex],
                            null,
                            false
                        )
                    );
                }

                continue;
            }

            // 4 người:
            // mình dưới, trái, trên, phải.
            if (totalPlayers >= 4)
            {
                if (cardIndex < leftCards.Count)
                {
                    requests.Add(
                        CreateRequest(
                            leftCards[cardIndex],
                            null,
                            false
                        )
                    );
                }

                if (cardIndex < topCards.Count)
                {
                    requests.Add(
                        CreateRequest(
                            topCards[cardIndex],
                            null,
                            false
                        )
                    );
                }

                if (cardIndex < rightCards.Count)
                {
                    requests.Add(
                        CreateRequest(
                            rightCards[cardIndex],
                            null,
                            false
                        )
                    );
                }
            }
        }

        return requests;
    }

    private CardAnimationRequest CreateRequest(
        RectTransform target,
        Sprite faceSprite,
        bool faceUp)
    {
        return new CardAnimationRequest(
            target,
            faceSprite,
            faceUp,
            target.eulerAngles.z,
            0f,
            () => ShowCard(target)
        );
    }

    private Sprite GetMyCardSprite(
        OnlineGameStateResponse state,
        int index)
    {
        if (state.myHand == null ||
            index < 0 ||
            index >= state.myHand.Length)
        {
            return null;
        }

        RectTransform cardTarget =
            myHandContainer.GetChild(index)
                .GetComponent<RectTransform>();

        if (cardTarget == null)
        {
            return null;
        }

        UnityEngine.UI.Image cardImage =
            cardTarget.GetComponent<UnityEngine.UI.Image>();

        if (cardImage == null)
        {
            cardImage =
                cardTarget.GetComponentInChildren<
                    UnityEngine.UI.Image
                >(true);
        }

        return cardImage != null
            ? cardImage.sprite
            : null;
    }

    private List<RectTransform> GetChildren(
        RectTransform container)
    {
        List<RectTransform> result =
            new List<RectTransform>();

        if (container == null)
        {
            return result;
        }

        for (int i = 0; i < container.childCount; i++)
        {
            RectTransform child =
                container.GetChild(i)
                    .GetComponent<RectTransform>();

            if (child != null)
            {
                result.Add(child);
            }
        }

        return result;
    }

    private void HideAllTargetCards(
        List<CardAnimationRequest> requests)
    {
        for (int i = 0; i < requests.Count; i++)
        {
            if (requests[i] != null)
            {
                HideCard(requests[i].Target);
            }
        }
    }

    private void ShowAllTargetCards(
        List<CardAnimationRequest> requests)
    {
        for (int i = 0; i < requests.Count; i++)
        {
            if (requests[i] != null)
            {
                ShowCard(requests[i].Target);
            }
        }
    }

    private void HideCard(RectTransform card)
    {
        if (card == null)
        {
            return;
        }

        CanvasGroup group =
            card.GetComponent<CanvasGroup>();

        if (group == null)
        {
            group = card.gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    private void ShowCard(RectTransform card)
    {
        if (card == null)
        {
            return;
        }

        CanvasGroup group =
            card.GetComponent<CanvasGroup>();

        if (group == null)
        {
            group = card.gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = 1f;
        group.blocksRaycasts = true;
    }

}