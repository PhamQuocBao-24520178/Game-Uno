using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OnlineTurnTimerUI : MonoBehaviour
{
    [Header("Game State Source")]
    [SerializeField]
    private OnlineGameSetupController onlineGameSetupController;

    [Header("Timer Rings")]
    [SerializeField] private Image myTimerRing;
    [SerializeField] private Image leftTimerRing;
    [SerializeField] private Image topTimerRing;
    [SerializeField] private Image rightTimerRing;

    [Header("Optional Seconds Text")]
    [SerializeField] private TMP_Text myTimerText;
    [SerializeField] private TMP_Text leftTimerText;
    [SerializeField] private TMP_Text topTimerText;
    [SerializeField] private TMP_Text rightTimerText;

    [Header("Timer Colors")]
    [SerializeField]
    private Color normalColor =
        new Color(0.18f, 0.82f, 1f, 1f);

    [SerializeField]
    private Color warningColor =
        new Color(1f, 0.28f, 0.16f, 1f);

    [SerializeField, Range(1f, 15f)]
    private float warningAtSeconds = 10f;

    private long serverOffsetMs;
    private bool hasServerOffset;

    private void Awake()
    {
        SetupRing(myTimerRing);
        SetupRing(leftTimerRing);
        SetupRing(topTimerRing);
        SetupRing(rightTimerRing);

        HideRing(myTimerRing, myTimerText);
        HideRing(leftTimerRing, leftTimerText);
        HideRing(topTimerRing, topTimerText);
        HideRing(rightTimerRing, rightTimerText);
    }

    private void Update()
    {
        if (onlineGameSetupController == null)
        {
            return;
        }

        OnlineGameStateResponse state =
            onlineGameSetupController.CurrentGameState;

        if (state == null ||
            state.players == null ||
            state.players.Length == 0)
        {
            return;
        }

        if (state.status != "Playing" ||
            string.IsNullOrEmpty(state.currentTurnPlayerId))
        {
            HideAllRings();
            return;
        }

        UpdateServerOffset(state);

        float secondsLeft = GetSecondsLeft(state);

        ShowTimerForCurrentPlayer(
            state,
            state.currentTurnPlayerId,
            secondsLeft
        );
    }

    private void UpdateServerOffset(OnlineGameStateResponse state)
    {
        if (state.serverTimeUnixMs <= 0)
        {
            return;
        }

        long localNow = GetUtcNowUnixMs();

        serverOffsetMs =
            state.serverTimeUnixMs - localNow;

        hasServerOffset = true;
    }

    private float GetSecondsLeft(OnlineGameStateResponse state)
    {
        if (state.turnEndsAtUnixMs <= 0)
        {
            return 0f;
        }

        long localNow = GetUtcNowUnixMs();

        long estimatedServerNow =
            hasServerOffset
                ? localNow + serverOffsetMs
                : localNow;

        float seconds =
            (state.turnEndsAtUnixMs - estimatedServerNow) / 1000f;

        return Mathf.Clamp(
            seconds,
            0f,
            Mathf.Max(1, state.turnDurationSeconds)
        );
    }

    private void ShowTimerForCurrentPlayer(
        OnlineGameStateResponse state,
        string currentTurnPlayerId,
        float secondsLeft)
    {
        HideAllRings();

        string myPlayerId =
            PlayerPrefs.GetString("OnlinePlayerId", "");

        if (currentTurnPlayerId == myPlayerId)
        {
            ShowRing(
                myTimerRing,
                myTimerText,
                secondsLeft,
                state.turnDurationSeconds
            );

            return;
        }

        List<OnlineGamePlayerStateResponse> opponents =
            GetOpponentsInRendererOrder(state, myPlayerId);

        int totalPlayers = state.players.Length;

        // 2 người: đối thủ phía trên.
        if (totalPlayers == 2)
        {
            if (opponents.Count > 0 &&
                opponents[0].playerId == currentTurnPlayerId)
            {
                ShowRing(
                    topTimerRing,
                    topTimerText,
                    secondsLeft,
                    state.turnDurationSeconds
                );
            }

            return;
        }

        // 3 người: trái và phải.
        if (totalPlayers == 3)
        {
            if (opponents.Count > 0 &&
                opponents[0].playerId == currentTurnPlayerId)
            {
                ShowRing(
                    leftTimerRing,
                    leftTimerText,
                    secondsLeft,
                    state.turnDurationSeconds
                );

                return;
            }

            if (opponents.Count > 1 &&
                opponents[1].playerId == currentTurnPlayerId)
            {
                ShowRing(
                    rightTimerRing,
                    rightTimerText,
                    secondsLeft,
                    state.turnDurationSeconds
                );
            }

            return;
        }

        // 4 người: trái, trên, phải.
        if (totalPlayers >= 4)
        {
            if (opponents.Count > 0 &&
                opponents[0].playerId == currentTurnPlayerId)
            {
                ShowRing(
                    leftTimerRing,
                    leftTimerText,
                    secondsLeft,
                    state.turnDurationSeconds
                );

                return;
            }

            if (opponents.Count > 1 &&
                opponents[1].playerId == currentTurnPlayerId)
            {
                ShowRing(
                    topTimerRing,
                    topTimerText,
                    secondsLeft,
                    state.turnDurationSeconds
                );

                return;
            }

            if (opponents.Count > 2 &&
                opponents[2].playerId == currentTurnPlayerId)
            {
                ShowRing(
                    rightTimerRing,
                    rightTimerText,
                    secondsLeft,
                    state.turnDurationSeconds
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

    private void SetupRing(Image ring)
    {
        if (ring == null)
        {
            return;
        }

        ring.type = Image.Type.Filled;
        ring.fillMethod = Image.FillMethod.Radial360;
        ring.fillOrigin = (int)Image.Origin360.Top;
        ring.fillClockwise = false;
        ring.fillAmount = 0f;
    }

    private void ShowRing(
        Image ring,
        TMP_Text secondsText,
        float secondsLeft,
        int totalSeconds)
    {
        if (ring == null)
        {
            return;
        }

        float safeTotalSeconds =
            Mathf.Max(1f, totalSeconds);

        ring.gameObject.SetActive(true);

        ring.fillAmount =
            Mathf.Clamp01(secondsLeft / safeTotalSeconds);

        ring.color =
            secondsLeft <= warningAtSeconds
                ? warningColor
                : normalColor;

        if (secondsText != null)
        {
            secondsText.gameObject.SetActive(true);

            secondsText.text =
                Mathf.CeilToInt(secondsLeft).ToString();

            secondsText.color = ring.color;
        }
    }

    private void HideAllRings()
    {
        HideRing(myTimerRing, myTimerText);
        HideRing(leftTimerRing, leftTimerText);
        HideRing(topTimerRing, topTimerText);
        HideRing(rightTimerRing, rightTimerText);
    }

    private void HideRing(
        Image ring,
        TMP_Text secondsText)
    {
        if (ring != null)
        {
            ring.fillAmount = 0f;
            ring.gameObject.SetActive(false);
        }

        if (secondsText != null)
        {
            secondsText.gameObject.SetActive(false);
        }
    }

    private long GetUtcNowUnixMs()
    {
        return DateTimeOffset.UtcNow
            .ToUnixTimeMilliseconds();
    }

}