using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnoCardUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Button cardButton;

    private UnoCardData cardData;
    private System.Action<UnoCardData, UnoCardUI> onCardClicked;

    public void Setup(UnoCardData data, System.Action<UnoCardData, UnoCardUI> clickCallback)
    {
        cardData = data;
        onCardClicked = clickCallback;

        if (cardButton == null)
        {
            cardButton = GetComponent<Button>();
        }

        UpdateVisual();

        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();

            if (onCardClicked != null)
            {
                cardButton.onClick.AddListener(OnClickCard);
            }
        }
    }

    private void UpdateVisual()
    {
        if (cardData == null)
        {
            return;
        }

        if (backgroundImage != null)
        {
            // Không dùng sprite cố định +4 nữa.
            // Để null để lá bài hiển thị bằng màu theo data.
            backgroundImage.sprite = null;
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.color = GetUnityColor(cardData.color);
            backgroundImage.preserveAspect = false;
        }

        if (valueText != null)
        {
            valueText.text = cardData.GetDisplayText();
            valueText.color = GetTextColor(cardData.color);
            valueText.alignment = TextAlignmentOptions.Center;
            valueText.fontSize = GetFontSize(cardData);
        }
    }

    private Color GetUnityColor(UnoColor color)
    {
        switch (color)
        {
            case UnoColor.Red:
                return new Color(0.95f, 0.08f, 0.06f, 1f);

            case UnoColor.Yellow:
                return new Color(1f, 0.78f, 0.04f, 1f);

            case UnoColor.Green:
                return new Color(0.05f, 0.65f, 0.15f, 1f);

            case UnoColor.Blue:
                return new Color(0.05f, 0.3f, 0.95f, 1f);

            case UnoColor.Wild:
                return Color.black;

            default:
                return Color.white;
        }
    }

    private Color GetTextColor(UnoColor color)
    {
        if (color == UnoColor.Yellow)
        {
            return Color.black;
        }

        return Color.white;
    }

    private float GetFontSize(UnoCardData data)
    {
        if (data.type == UnoCardType.Number)
        {
            return 52f;
        }

        if (data.type == UnoCardType.Wild || data.type == UnoCardType.WildDrawFour)
        {
            return 34f;
        }

        return 32f;
    }

    private void OnClickCard()
    {
        onCardClicked?.Invoke(cardData, this);
    }
}