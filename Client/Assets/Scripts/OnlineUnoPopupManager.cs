using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OnlineUnoPopupManager : MonoBehaviour
{
    [Header("Source")]
    [SerializeField]
    private OnlineGameSetupController onlineGameSetupController;

    [Header("UNO Popup Objects")]
    [SerializeField] private GameObject myUnoPopup;
    [SerializeField] private GameObject leftUnoPopup;
    [SerializeField] private GameObject topUnoPopup;
    [SerializeField] private GameObject rightUnoPopup;

    [Header("Time")]
    [SerializeField] private float showDuration = 1.5f;

    private readonly HashSet<string> previousUnoPlayers =
        new HashSet<string>();

    private readonly Dictionary<GameObject, Coroutine> hideCoroutines =
        new Dictionary<GameObject, Coroutine>();

    private bool receivedFirstState;

    private void Awake()
    {
        HidePopupNow(myUnoPopup);
        HidePopupNow(leftUnoPopup);
        HidePopupNow(topUnoPopup);
        HidePopupNow(rightUnoPopup);
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

        HashSet<string> currentUnoPlayers =
            GetCurrentUnoPlayers(state);

        // Không hiện popup cũ ngay khi vừa load scene.
        if (!receivedFirstState)
        {
            CopySet(currentUnoPlayers, previousUnoPlayers);
            receivedFirstState = true;
            return;
        }

        foreach (string playerId in currentUnoPlayers)
        {
            if (!previousUnoPlayers.Contains(playerId))
            {
                ShowPopupForPlayer(state, playerId);
            }
        }

        CopySet(currentUnoPlayers, previousUnoPlayers);
    }

    private HashSet<string> GetCurrentUnoPlayers(
        OnlineGameStateResponse state)
    {
        HashSet<string> result = new HashSet<string>();

        if (state.declaredUnoPlayerIds == null)
        {
            return result;
        }

        for (int i = 0; i < state.declaredUnoPlayerIds.Length; i++)
        {
            string playerId = state.declaredUnoPlayerIds[i];

            if (!string.IsNullOrEmpty(playerId))
            {
                result.Add(playerId);
            }
        }

        return result;
    }

    private void ShowPopupForPlayer(
        OnlineGameStateResponse state,
        string declaredPlayerId)
    {
        string myPlayerId =
            PlayerPrefs.GetString("OnlinePlayerId", "");

        // Popup của chính mình luôn ở phía dưới.
        if (declaredPlayerId == myPlayerId)
        {
            ShowPopup(myUnoPopup);
            return;
        }

        List<OnlineGamePlayerStateResponse> opponents =
            GetOpponentsInRendererOrder(state, myPlayerId);

        int totalPlayers =
            state.players != null
                ? state.players.Length
                : 0;

        // 2 người: mình dưới, đối thủ trên.
        if (totalPlayers == 2)
        {
            if (opponents.Count > 0 &&
                opponents[0].playerId == declaredPlayerId)
            {
                ShowPopup(topUnoPopup);
            }

            return;
        }

        // 3 người: mình dưới, trái, phải.
        if (totalPlayers == 3)
        {
            if (opponents.Count > 0 &&
                opponents[0].playerId == declaredPlayerId)
            {
                ShowPopup(leftUnoPopup);
                return;
            }

            if (opponents.Count > 1 &&
                opponents[1].playerId == declaredPlayerId)
            {
                ShowPopup(rightUnoPopup);
            }

            return;
        }

        // 4 người: mình dưới, trái, trên, phải.
        if (totalPlayers >= 4)
        {
            if (opponents.Count > 0 &&
                opponents[0].playerId == declaredPlayerId)
            {
                ShowPopup(leftUnoPopup);
                return;
            }

            if (opponents.Count > 1 &&
                opponents[1].playerId == declaredPlayerId)
            {
                ShowPopup(topUnoPopup);
                return;
            }

            if (opponents.Count > 2 &&
                opponents[2].playerId == declaredPlayerId)
            {
                ShowPopup(rightUnoPopup);
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

    private void ShowPopup(GameObject popup)
    {
        if (popup == null)
        {
            return;
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

    private void CopySet(
        HashSet<string> from,
        HashSet<string> to)
    {
        to.Clear();

        foreach (string playerId in from)
        {
            to.Add(playerId);
        }
    }

}