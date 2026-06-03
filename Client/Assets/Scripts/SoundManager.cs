using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Audio Source")]
    [SerializeField] private AudioSource sfxAudioSource;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip drawCardClip;
    [SerializeField] private AudioClip playCardClip;
    [SerializeField] private AudioClip unoClip;
    [SerializeField] private AudioClip errorClip;

    private const string SoundOnKey = "SoundOn";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (sfxAudioSource == null)
        {
            sfxAudioSource = GetComponent<AudioSource>();
        }
    }

    public void PlayButtonClick()
    {
        PlaySfx(buttonClickClip);
    }

    public void PlayDrawCard()
    {
        Debug.Log("PlayDrawCard được gọi");

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

    public void PlaySfx(AudioClip clip)
    {
        Debug.Log("PlaySfx chạy. Clip = " + (clip != null ? clip.name : "NULL"));

        if (clip == null)
        {
            Debug.LogError("Clip bị NULL.");
            return;
        }

        if (sfxAudioSource == null)
        {
            Debug.LogError("Sfx Audio Source bị NULL.");
            return;
        }

        bool isSoundOn = PlayerPrefs.GetInt(SoundOnKey, 1) == 1;
        Debug.Log("SoundOn = " + isSoundOn);

        if (!isSoundOn)
        {
            Debug.LogWarning("Sound đang OFF nên không phát.");
            return;
        }

        sfxAudioSource.PlayOneShot(clip);
    }
}