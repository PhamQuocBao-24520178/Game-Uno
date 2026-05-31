using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class CardAnimationRequest
{
    public RectTransform Target;
    public Sprite FaceSprite;
    public bool FaceUp;
    public float EndRotationZ;
    public float Duration;
    public Action OnArrive;

    public CardAnimationRequest(
        RectTransform target,
        Sprite faceSprite,
        bool faceUp,
        float endRotationZ = 0f,
        float duration = 0f,
        Action onArrive = null)
    {
        Target = target;
        FaceSprite = faceSprite;
        FaceUp = faceUp;
        EndRotationZ = endRotationZ;
        Duration = duration;
        OnArrive = onArrive;
    }
}

public class CardAnimationManager : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private RectTransform animationLayer;

    [Header("Pile References")]
    [SerializeField] private RectTransform drawPile;
    [SerializeField] private RectTransform discardPile;

    [Header("Sprites")]
    [SerializeField] private Sprite cardBackSprite;

    [Header("Card Size")]
    [SerializeField] private Vector2 flyCardSize = new Vector2(160f, 210f);

    [Header("Animation Timing")]
    [SerializeField] private float defaultDealDuration = 0.22f;
    [SerializeField] private float defaultDrawDuration = 0.25f;
    [SerializeField] private float defaultPlayDuration = 0.20f;
    [SerializeField] private float delayBetweenDealCards = 0.04f;

    [Header("Motion")]
    [SerializeField] private float arcHeight = 50f;
    [SerializeField] private float defaultScale = 1f;

    private Camera uiCamera;

    private void Awake()
    {
        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = rootCanvas.worldCamera;
        }
        else
        {
            uiCamera = null;
        }
    }

    // =========================
    // PUBLIC METHODS
    // =========================

    public void PlayDealSequence(List<CardAnimationRequest> requests, Action onComplete = null)
    {
        StartCoroutine(DealSequenceRoutine(requests, onComplete));
    }

    public void PlayDrawToTarget(RectTransform target, Sprite faceSprite, bool faceUp, Action onComplete = null)
    {
        StartCoroutine(FlyCardRoutine(
            fromRect: drawPile,
            toRect: target,
            sprite: faceUp ? faceSprite : cardBackSprite,
            duration: defaultDrawDuration,
            endRotationZ: 0f,
            onComplete: onComplete
        ));
    }

    public void PlayCardToDiscard(RectTransform sourceCardRect, Sprite faceSprite, Action onComplete = null)
    {
        StartCoroutine(FlyCardRoutine(
            fromRect: sourceCardRect,
            toRect: discardPile,
            sprite: faceSprite,
            duration: defaultPlayDuration,
            endRotationZ: 0f,
            onComplete: onComplete
        ));
    }

    public void PlayFromDrawPileToDiscard(Sprite faceSprite, Action onComplete = null)
    {
        StartCoroutine(FlyCardRoutine(
            fromRect: drawPile,
            toRect: discardPile,
            sprite: faceSprite,
            duration: defaultPlayDuration,
            endRotationZ: 0f,
            onComplete: onComplete
        ));
    }

    // =========================
    // DEAL SEQUENCE
    // =========================

    private IEnumerator DealSequenceRoutine(List<CardAnimationRequest> requests, Action onComplete)
    {
        if (requests == null || requests.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        for (int i = 0; i < requests.Count; i++)
        {
            CardAnimationRequest request = requests[i];

            if (request == null || request.Target == null)
            {
                continue;
            }

            Sprite spriteToUse = request.FaceUp ? request.FaceSprite : cardBackSprite;
            float duration = request.Duration > 0f ? request.Duration : defaultDealDuration;

            bool finished = false;

            yield return StartCoroutine(FlyCardRoutine(
                fromRect: drawPile,
                toRect: request.Target,
                sprite: spriteToUse,
                duration: duration,
                endRotationZ: request.EndRotationZ,
                onComplete: () =>
                {
                    request.OnArrive?.Invoke();
                    finished = true;
                }
            ));

            while (!finished)
            {
                yield return null;
            }

            if (delayBetweenDealCards > 0f)
            {
                yield return new WaitForSeconds(delayBetweenDealCards);
            }
        }

        onComplete?.Invoke();
    }

    // =========================
    // CORE FLY ROUTINE
    // =========================

    private IEnumerator FlyCardRoutine(
        RectTransform fromRect,
        RectTransform toRect,
        Sprite sprite,
        float duration,
        float endRotationZ,
        Action onComplete)
    {
        if (animationLayer == null || fromRect == null || toRect == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        GameObject tempObj = new GameObject("FlyCard");
        tempObj.transform.SetParent(animationLayer, false);

        Image tempImage = tempObj.AddComponent<Image>();
        RectTransform tempRect = tempObj.GetComponent<RectTransform>();

        tempImage.sprite = sprite;
        tempImage.preserveAspect = true;
        tempImage.raycastTarget = false;

        tempRect.anchorMin = new Vector2(0.5f, 0.5f);
        tempRect.anchorMax = new Vector2(0.5f, 0.5f);
        tempRect.pivot = new Vector2(0.5f, 0.5f);
        tempRect.sizeDelta = flyCardSize;
        tempRect.localScale = Vector3.one * defaultScale;

        Vector2 startPos = WorldToAnchoredPosition(fromRect.position);
        Vector2 endPos = WorldToAnchoredPosition(toRect.position);

        tempRect.anchoredPosition = startPos;
        tempRect.rotation = Quaternion.Euler(0f, 0f, fromRect.eulerAngles.z);

        float startRotation = tempRect.eulerAngles.z;
        float timer = 0f;

        Vector2 controlPoint = (startPos + endPos) * 0.5f + Vector2.up * arcHeight;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            float smoothT = EaseOutCubic(t);

            Vector2 currentPos = CalculateQuadraticBezierPoint(smoothT, startPos, controlPoint, endPos);
            tempRect.anchoredPosition = currentPos;

            float currentRot = Mathf.LerpAngle(startRotation, endRotationZ, smoothT);
            tempRect.rotation = Quaternion.Euler(0f, 0f, currentRot);

            yield return null;
        }

        tempRect.anchoredPosition = endPos;
        tempRect.rotation = Quaternion.Euler(0f, 0f, endRotationZ);

        Destroy(tempObj);

        onComplete?.Invoke();
    }

    // =========================
    // HELPERS
    // =========================

    private Vector2 WorldToAnchoredPosition(Vector3 worldPosition)
    {
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, worldPosition);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            animationLayer,
            screenPoint,
            uiCamera,
            out Vector2 localPoint
        );

        return localPoint;
    }

    private Vector2 CalculateQuadraticBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2)
    {
        float u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
    }

    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
}