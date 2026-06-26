using System;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    public static event Action<bool, bool> OnAudioSettingChanged;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicAudioSource;
    [SerializeField] private AudioSource sfxAudioSource;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip drawCardClip;
    [SerializeField] private AudioClip playCardClip;
    [SerializeField] private AudioClip unoClip;
    [SerializeField] private AudioClip errorClip;
    [SerializeField] private AudioClip winClip;
    [SerializeField] private AudioClip loseClip;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.35f;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 0.8f;

    private const string MusicEnabledKey = "MusicEnabled";
    private const string SfxEnabledKey = "SfxEnabled";

    private bool isMusicEnabled;
    private bool isSfxEnabled;

    public bool IsMusicEnabled => isMusicEnabled;
    public bool IsSfxEnabled => isSfxEnabled;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        AutoFindAudioSources();

        isMusicEnabled =
            PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;

        isSfxEnabled =
            PlayerPrefs.GetInt(SfxEnabledKey, 1) == 1;

        ApplyAllSettings();
    }

    private void Start()
    {
        NotifySettingsChanged();
    }

    private void AutoFindAudioSources()
    {
        AudioSource[] sources =
            GetComponents<AudioSource>();

        if (musicAudioSource == null &&
            sources.Length > 0)
        {
            musicAudioSource = sources[0];
        }

        if (sfxAudioSource == null &&
            sources.Length > 1)
        {
            sfxAudioSource = sources[1];
        }
    }

    public void ToggleMusic()
    {
        SetMusicEnabled(!isMusicEnabled);
    }

    public void ToggleSfx()
    {
        SetSfxEnabled(!isSfxEnabled);
    }

    public void SetMusicEnabled(bool enabled)
    {
        isMusicEnabled = enabled;

        PlayerPrefs.SetInt(
            MusicEnabledKey,
            isMusicEnabled ? 1 : 0
        );

        PlayerPrefs.Save();

        ApplyMusicSetting();
        NotifySettingsChanged();
    }

    public void SetSfxEnabled(bool enabled)
    {
        isSfxEnabled = enabled;

        PlayerPrefs.SetInt(
            SfxEnabledKey,
            isSfxEnabled ? 1 : 0
        );

        PlayerPrefs.Save();

        ApplySfxSetting();
        NotifySettingsChanged();
    }

    public void PlayButtonClick()
    {
        PlaySfx(buttonClickClip);
    }

    public void PlayDrawCard()
    {
        PlaySfx(drawCardClip);
    }

    public void PlayPlayCard()
    {
        PlaySfx(playCardClip);
    }

    public void PlayUno()
    {
        PlaySfx(unoClip);
    }

    public void PlayError()
    {
        PlaySfx(errorClip);
    }

    public void PlayWin()
    {
        PlaySfx(winClip);
    }

    public void PlayLose()
    {
        PlaySfx(loseClip);
    }

    public void PlayClick()
    {
        PlayButtonClick();
    }

    public void PlayCard()
    {
        PlayPlayCard();
    }

    private void PlaySfx(AudioClip clip)
    {
        if (!isSfxEnabled ||
            sfxAudioSource == null ||
            clip == null)
        {
            return;
        }

        sfxAudioSource.PlayOneShot(
            clip,
            sfxVolume
        );
    }

    private void ApplyAllSettings()
    {
        ApplyMusicSetting();
        ApplySfxSetting();
    }

    private void ApplyMusicSetting()
    {
        if (musicAudioSource == null)
        {
            Debug.LogWarning(
                "SoundManager chưa có Music Audio Source."
            );

            return;
        }

        // Đây là dòng làm nhạc nền tắt thật.
        musicAudioSource.mute = !isMusicEnabled;

        musicAudioSource.volume = musicVolume;

        if (isMusicEnabled &&
            musicAudioSource.clip != null &&
            !musicAudioSource.isPlaying)
        {
            musicAudioSource.Play();
        }
    }

    private void ApplySfxSetting()
    {
        if (sfxAudioSource == null)
        {
            Debug.LogWarning(
                "SoundManager chưa có SFX Audio Source."
            );

            return;
        }

        sfxAudioSource.mute = !isSfxEnabled;
        sfxAudioSource.volume = sfxVolume;
    }

    private void NotifySettingsChanged()
    {
        OnAudioSettingChanged?.Invoke(
            isMusicEnabled,
            isSfxEnabled
        );
    }
}