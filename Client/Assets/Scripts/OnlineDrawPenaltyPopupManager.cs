using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class OnlineDrawPenaltyPopupManager : MonoBehaviour
{
    [Header("Game State Source")]
    [SerializeField]
    private OnlineGameSetupController onlineGameSetupController;

    [Header("My Popup")]
    [SerializeField] private GameObject myPenaltyPopup;
    [SerializeField] private TMP_Text myPenaltyText;

    [Header("Left Popup")]
    [SerializeField] private GameObject leftPenaltyPopup;
    [SerializeField] private TMP_Text leftPenaltyText;

    [Header("Top Popup")]
    [SerializeField] private GameObject topPenaltyPopup;
    [SerializeField] private TMP_Text topPenaltyText;

    [Header("Right Popup")]
    [SerializeField] private GameObject rightPenaltyPopup;
    [SerializeField] private TMP_Text rightPenaltyText;

    [Header("Popup Time")]
    [SerializeField] private float showDuration = 1.8f;

    private int lastPenaltyAmount;
    private string lastPenaltyTargetPlayerId = "";

    private readonly Dictionary<GameObject, Coroutine> hideCoroutines =
        new Dictionary<GameObject, Coroutine>();

    private void Awake()
    {
        HidePopupNow(myPenaltyPopup);
        HidePopupNow(leftPenaltyPopup);
        HidePopupNow(topPenaltyPopup);
        HidePopupNow(rightPenaltyPopup);
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

        HandlePenaltyPopup(state);
    }

    private void HandlePenaltyPopup(OnlineGameStateResponse state)
    {
        int currentPenalty = state.pendingDrawPenalty;
        string currentTarget = state.currentTurnPlayerId ?? "";

        // Không có penalty thì reset dữ liệu theo dõi.
        if (currentPenalty <= 0 || string.IsNullOrEmpty(currentTarget))
        {
            lastPenaltyAmount = 0;
            lastPenaltyTargetPlayerId = "";
            return;
        }

        bool isNewPenalty =
            currentPenalty != lastPenaltyAmount ||
            currentTarget != lastPenaltyTargetPlayerId;

        if (!isNewPenalty)
        {
            return;
        }

        lastPenaltyAmount = currentPenalty;
        lastPenaltyTargetPlayerId = currentTarget;

        ShowPenaltyForPlayer(
            state,
            currentTarget,
            currentPenalty
        );
    }

    private void ShowPenaltyForPlayer(
        OnlineGameStateResponse state,
        string penalizedPlayerId,
        int penaltyAmount)
    {
        string myPlayerId =
            PlayerPrefs.GetString("OnlinePlayerId", "");

        // Người bị phạt là chính mình: popup ở dưới.
        if (penalizedPlayerId == myPlayerId)
        {
            ShowPopup(
                myPenaltyPopup,
                myPenaltyText,
                penaltyAmount
            );

            return;
        }

        List<OnlineGamePlayerStateResponse> opponents =
            GetOpponentsInRendererOrder(state, myPlayerId);

        int totalPlayers =
            state.players != null
                ? state.players.Length
                : 0;

        // 2 người:
        // Mình dưới, đối thủ ở trên.
        if (totalPlayers == 2)
        {
            if (opponents.Count > 0 &&
                opponents[0].playerId == penalizedPlayerId)
            {
                ShowPopup(
                    topPenaltyPopup,
                    topPenaltyText,
                    penaltyAmount
                );
            }

            return;
        }

        // 3 người:
        // Mình dưới, đối thủ trái và phải.
        if (totalPlayers == 3)
        {
            if (opponents.Count > 0 &&
                opponents[0].playerId == penalizedPlayerId)
            {
                ShowPopup(
                    leftPenaltyPopup,
                    leftPenaltyText,
                    penaltyAmount
                );

                return;
            }

            if (opponents.Count > 1 &&
                opponents[1].playerId == penalizedPlayerId)
            {
                ShowPopup(
                    rightPenaltyPopup,
                    rightPenaltyText,
                    penaltyAmount
                );
            }

            return;
        }

        // 4 người:
        // Mình dưới, đối thủ trái, trên, phải.
        if (totalPlayers >= 4)
        {
            if (opponents.Count > 0 &&
                opponents[0].playerId == penalizedPlayerId)
            {
                ShowPopup(
                    leftPenaltyPopup,
                    leftPenaltyText,
                    penaltyAmount
                );

                return;
            }

            if (opponents.Count > 1 &&
                opponents[1].playerId == penalizedPlayerId)
            {
                ShowPopup(
                    topPenaltyPopup,
                    topPenaltyText,
                    penaltyAmount
                );

                return;
            }

            if (opponents.Count > 2 &&
                opponents[2].playerId == penalizedPlayerId)
            {
                ShowPopup(
                    rightPenaltyPopup,
                    rightPenaltyText,
                    penaltyAmount
                );
            }
        }
    }

    private List<OnlineGamePlayerStateResponse>
        GetOpponentsInRendererOrder(
            OnlineGameStateResponse state,
            string myPlayerId)
    {
        List<OnlineGamePlayerStateResponse> opponents =
            new List<OnlineGamePlayerStateResponse>();

        if (state.players == null)
        {
            return opponents;
        }

        for (int i = 0; i < state.players.Length; i++)
        {
            OnlineGamePlayerStateResponse player =
                state.players[i];

            if (player == null ||
                player.playerId == myPlayerId)
            {
                continue;
            }

            opponents.Add(player);
        }

        return opponents;
    }

    private void ShowPopup(
        GameObject popup,
        TMP_Text popupText,
        int penaltyAmount)
    {
        if (popup == null)
        {
            return;
        }

        if (popupText != null)
        {
            popupText.text =
                "RÚT\n<color=#FFD43B>+" +
                penaltyAmount +
                "</color>";
        }

        if (hideCoroutines.TryGetValue(
            popup,
            out Coroutine oldCoroutine))
        {
            StopCoroutine(oldCoroutine);
        }

        popup.SetActive(true);

        hideCoroutines[popup] =
            StartCoroutine(HideAfterDelay(popup));
    }

    private IEnumerator HideAfterDelay(GameObject popup)
    {
        yield return new WaitForSeconds(showDuration);

        HidePopupNow(popup);
    }

    private void HidePopupNow(GameObject popup)
    {
        if (popup == null)
        {
            return;
        }

        popup.SetActive(false);

        if (hideCoroutines.ContainsKey(popup))
        {
            hideCoroutines.Remove(popup);
        }
    }

}