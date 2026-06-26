using UnityEngine;
using UnityEngine.UI;

public class SettingsSoundButtons : MonoBehaviour
{
    [Header("Music UI")]
    [SerializeField] private Image musicToggleImage;
    [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite musicOffSprite;

    [Header("SFX UI")]
    [SerializeField] private Image sfxToggleImage;
    [SerializeField] private Sprite sfxOnSprite;
    [SerializeField] private Sprite sfxOffSprite;

    private void OnEnable()
    {
        SoundManager.OnAudioSettingChanged +=
            UpdateToggleVisual;

        RefreshVisual();
    }

    private void OnDisable()
    {
        SoundManager.OnAudioSettingChanged -=
            UpdateToggleVisual;
    }

    public void ToggleMusic()
    {
        if (SoundManager.Instance == null)
        {
            Debug.LogWarning(
                "Chưa có SoundManager."
            );

            return;
        }

        SoundManager.Instance.ToggleMusic();
    }

    public void ToggleSfx()
    {
        if (SoundManager.Instance == null)
        {
            Debug.LogWarning(
                "Chưa có SoundManager."
            );

            return;
        }

        SoundManager.Instance.ToggleSfx();
    }

    private void RefreshVisual()
    {
        if (SoundManager.Instance == null)
        {
            return;
        }

        UpdateToggleVisual(
            SoundManager.Instance.IsMusicEnabled,
            SoundManager.Instance.IsSfxEnabled
        );
    }

    private void UpdateToggleVisual(
        bool isMusicEnabled,
        bool isSfxEnabled)
    {
        if (musicToggleImage != null)
        {
            musicToggleImage.sprite = isMusicEnabled
                ? musicOnSprite
                : musicOffSprite;
        }

        if (sfxToggleImage != null)
        {
            sfxToggleImage.sprite = isSfxEnabled
                ? sfxOnSprite
                : sfxOffSprite;
        }
    }
}