using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class OnlineCardAnimationController : MonoBehaviour
{
    [Header("Layer")]
    [SerializeField] private RectTransform animationLayer;

    [Header("Online Game Positions")]
    [SerializeField] private RectTransform drawPile;
    [SerializeField] private RectTransform discardPile;
    [SerializeField] private RectTransform myHandContainer;

    [Header("Card Visual")]
    [SerializeField] private Sprite unoBackSprite;

    [Header("Animation")]
    [SerializeField] private float playDuration = 0.32f;
    [SerializeField] private float drawDuration = 0.28f;

    [SerializeField] private float playScaleMultiplier = 1.08f;
    [SerializeField] private float drawScaleMultiplier = 0.92f;

    private void Awake()
    {
        if (animationLayer != null)
        {
            animationLayer.SetAsLastSibling();
        }
    }

    // Lá bài thật từ tay mình bay vào chồng bài giữa.
    public void PlayMyCardToDiscard(GameObject sourceCardObject)
    {
        if (sourceCardObject == null ||
            animationLayer == null ||
            discardPile == null)
        {
            return;
        }

        Image sourceImage =
            sourceCardObject.GetComponent<Image>();

        if (sourceImage == null)
        {
            sourceImage =
                sourceCardObject.GetComponentInChildren<Image>(true);
        }

        if (sourceImage == null ||
            sourceImage.sprite == null)
        {
            return;
        }

        StartCoroutine(
            AnimatePlayCard(
                sourceImage,
                sourceCardObject
            )
        );
    }

    // Lá bài úp bay từ chồng rút về tay mình.
    public void DrawCardToMyHand()
    {
        if (animationLayer == null ||
            drawPile == null ||
            myHandContainer == null ||
            unoBackSprite == null)
        {
            return;
        }

        StartCoroutine(AnimateDrawCard());
    }

    // Dùng cho bước sau: đối thủ đánh bài úp bay vào giữa.
    public void OpponentCardToDiscard(RectTransform opponentHand)
    {
        if (animationLayer == null ||
            opponentHand == null ||
            discardPile == null ||
            unoBackSprite == null)
        {
            return;
        }

        StartCoroutine(
            AnimateOpponentPlay(opponentHand)
        );
    }

    private IEnumerator AnimatePlayCard(
        Image sourceImage,
        GameObject sourceCardObject)
    {
        RectTransform sourceRect =
            sourceImage.rectTransform;

        Vector2 startPosition =
            ConvertWorldToAnimationLocal(
                sourceRect.position
            );

        Vector2 endPosition =
            ConvertWorldToAnimationLocal(
                discardPile.position
            );

        Vector2 startSize =
            GetSafeSize(sourceRect, new Vector2(160f, 210f));

        float startRotation =
            sourceRect.eulerAngles.z;

        Image animationCard =
            CreateAnimationCard(
                sourceImage.sprite,
                startPosition,
                startSize,
                startRotation
            );

        if (animationCard == null)
        {
            yield break;
        }

        CanvasGroup sourceGroup =
            sourceCardObject.GetComponent<CanvasGroup>();

        if (sourceGroup == null)
        {
            sourceGroup =
                sourceCardObject.AddComponent<CanvasGroup>();
        }

        float oldAlpha = sourceGroup.alpha;

        // Ẩn lá thật ngay lúc animation bắt đầu.
        sourceGroup.alpha = 0f;

        RectTransform animationRect =
            animationCard.rectTransform;

        Vector2 endSize =
            GetSafeSize(
                discardPile,
                new Vector2(160f, 210f)
            ) * playScaleMultiplier;

        float elapsed = 0f;

        while (elapsed < playDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / playDuration
            );

            float smoothT =
                Mathf.SmoothStep(0f, 1f, t);

            animationRect.anchoredPosition =
                Vector2.Lerp(
                    startPosition,
                    endPosition,
                    smoothT
                );

            animationRect.sizeDelta =
                Vector2.Lerp(
                    startSize,
                    endSize,
                    smoothT
                );

            animationRect.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Lerp(
                        startRotation,
                        0f,
                        smoothT
                    )
                );

            yield return null;
        }

        Destroy(animationCard.gameObject);

        // Renderer sẽ tạo lại bài thật sau khi GameState cập nhật.
        if (sourceCardObject != null)
        {
            sourceGroup.alpha = oldAlpha;
        }
    }

    private IEnumerator AnimateDrawCard()
    {
        Vector2 startPosition =
            ConvertWorldToAnimationLocal(
                drawPile.position
            );

        Vector2 endPosition =
            ConvertWorldToAnimationLocal(
                myHandContainer.position
            );

        Vector2 startSize =
            GetSafeSize(
                drawPile,
                new Vector2(160f, 210f)
            );

        Image animationCard =
            CreateAnimationCard(
                unoBackSprite,
                startPosition,
                startSize,
                0f
            );

        if (animationCard == null)
        {
            yield break;
        }

        RectTransform animationRect =
            animationCard.rectTransform;

        Vector2 endSize =
            startSize * drawScaleMultiplier;

        float elapsed = 0f;

        while (elapsed < drawDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / drawDuration
            );

            float smoothT =
                Mathf.SmoothStep(0f, 1f, t);

            animationRect.anchoredPosition =
                Vector2.Lerp(
                    startPosition,
                    endPosition,
                    smoothT
                );

            animationRect.sizeDelta =
                Vector2.Lerp(
                    startSize,
                    endSize,
                    smoothT
                );

            yield return null;
        }

        Destroy(animationCard.gameObject);
    }

    private IEnumerator AnimateOpponentPlay(
        RectTransform opponentHand)
    {
        Vector2 startPosition =
            ConvertWorldToAnimationLocal(
                opponentHand.position
            );

        Vector2 endPosition =
            ConvertWorldToAnimationLocal(
                discardPile.position
            );

        Vector2 startSize =
            GetSafeSize(
                opponentHand,
                new Vector2(110f, 145f)
            );

        Image animationCard =
            CreateAnimationCard(
                unoBackSprite,
                startPosition,
                startSize,
                0f
            );

        if (animationCard == null)
        {
            yield break;
        }

        RectTransform animationRect =
            animationCard.rectTransform;

        Vector2 endSize =
            GetSafeSize(
                discardPile,
                new Vector2(160f, 210f)
            );

        float elapsed = 0f;

        while (elapsed < playDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / playDuration
            );

            float smoothT =
                Mathf.SmoothStep(0f, 1f, t);

            animationRect.anchoredPosition =
                Vector2.Lerp(
                    startPosition,
                    endPosition,
                    smoothT
                );

            animationRect.sizeDelta =
                Vector2.Lerp(
                    startSize,
                    endSize,
                    smoothT
                );

            yield return null;
        }

        Destroy(animationCard.gameObject);
    }

    private Image CreateAnimationCard(
        Sprite sprite,
        Vector2 position,
        Vector2 size,
        float rotationZ)
    {
        if (animationLayer == null || sprite == null)
        {
            return null;
        }

        GameObject cardObject =
            new GameObject(
                "OnlineAnimationCard",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

        cardObject.transform.SetParent(
            animationLayer,
            false
        );

        RectTransform rect =
            cardObject.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;

        rect.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                rotationZ
            );

        Image image =
            cardObject.GetComponent<Image>();

        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;

        return image;
    }

    // Đây là phần sửa chính:
    // đổi world position sang local position của AnimationLayer,
    // không dùng ScreenPoint nên không bị lệch theo Canvas Scale.
    private Vector2 ConvertWorldToAnimationLocal(
        Vector3 worldPosition)
    {
        if (animationLayer == null)
        {
            return Vector2.zero;
        }

        Vector3 localPosition =
            animationLayer.InverseTransformPoint(
                worldPosition
            );

        return new Vector2(
            localPosition.x,
            localPosition.y
        );
    }

    private Vector2 GetSafeSize(
        RectTransform rect,
        Vector2 fallbackSize)
    {
        if (rect == null)
        {
            return fallbackSize;
        }

        Vector2 size = rect.rect.size;

        if (size.x <= 1f || size.y <= 1f)
        {
            return fallbackSize;
        }

        return size;
    }

}