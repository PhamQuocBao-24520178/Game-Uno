using UnityEngine;
using UnityEngine.UI;

public class SoundToggleButton : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button soundButton;
    [SerializeField] private Image soundIcon;

    [Header("Icons")]
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;

    private bool isSoundOn = true;

    private void Awake()
    {
        if (soundButton == null)
            soundButton = GetComponent<Button>();

        if (soundIcon == null)
            soundIcon = GetComponent<Image>();

        if (soundButton != null)
            soundButton.onClick.AddListener(ToggleIcon);
    }

    private void Start()
    {
        UpdateIcon();
    }

    private void ToggleIcon()
    {
        Debug.Log("Clicked Sound Button");

        isSoundOn = !isSoundOn;
        UpdateIcon();
    }

    private void UpdateIcon()
    {
        if (soundIcon == null)
        {
            Debug.LogError("Sound Icon is missing");
            return;
        }

        soundIcon.sprite = isSoundOn ? soundOnSprite : soundOffSprite;
    }
}