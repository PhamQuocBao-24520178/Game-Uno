using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TurnTimerRingManager : MonoBehaviour
{
    [Header("Timer Rings")]
    [SerializeField] private Image playerTimeRing;
    [SerializeField] private Image bot1TimeRing;
    [SerializeField] private Image bot2TimeRing;
    [SerializeField] private Image bot3TimeRing;

    [Header("Settings")]
    [SerializeField] private float showDuration = 10f;

    private Coroutine timerCoroutine;
    private Action onTimerFinished;

    private void Awake()
    {
        SetupRing(playerTimeRing);
        SetupRing(bot1TimeRing);
        SetupRing(bot2TimeRing);
        SetupRing(bot3TimeRing);

        HideAllRings();
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

        // Quan trọng:
        // Vì fillAmount đang giảm từ 1 về 0,
        // để nhìn như đếm ngược theo chiều kim đồng hồ thì để false.
        ring.fillClockwise = false;

        ring.fillAmount = 1f;
        ring.raycastTarget = false;
    }

    public void StartTurnTimer(int turnIndex, float duration, Action finishCallback)
    {
        StopTimer();

        onTimerFinished = finishCallback;

        Image activeRing = GetRingByTurnIndex(turnIndex);

        HideAllRings();

        if (activeRing == null)
        {
            return;
        }

        SetupRing(activeRing);

        activeRing.gameObject.SetActive(true);
        activeRing.fillAmount = 1f;

        timerCoroutine = StartCoroutine(CountdownRoutine(activeRing, duration));
    }

    public void StopTimer()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        onTimerFinished = null;
    }

    public void HideAllRings()
    {
        HideRing(playerTimeRing);
        HideRing(bot1TimeRing);
        HideRing(bot2TimeRing);
        HideRing(bot3TimeRing);
    }

    private void HideRing(Image ring)
    {
        if (ring == null)
        {
            return;
        }

        ring.fillAmount = 1f;
        ring.gameObject.SetActive(false);
    }

    private IEnumerator CountdownRoutine(Image activeRing, float duration)
    {
        float timeLeft = duration;

        while (timeLeft > 0f)
        {
            timeLeft -= Time.deltaTime;

            float percent = Mathf.Clamp01(timeLeft / duration);
            activeRing.fillAmount = percent;

            yield return null;
        }

        activeRing.fillAmount = 0f;
        activeRing.gameObject.SetActive(false);

        timerCoroutine = null;

        Action callback = onTimerFinished;
        onTimerFinished = null;

        callback?.Invoke();
    }

    private Image GetRingByTurnIndex(int turnIndex)
    {
        switch (turnIndex)
        {
            case 0:
                return playerTimeRing;

            case 1:
                return bot1TimeRing;

            case 2:
                return bot2TimeRing;

            case 3:
                return bot3TimeRing;

            default:
                return null;
        }
    }
}