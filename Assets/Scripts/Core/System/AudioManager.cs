using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("SFX Clip")]
    public AudioClip groupHitSound;
    public AudioClip groupHitSound2;
    public AudioClip groupBreakSound;
    public AudioClip buttonClick;
    public AudioClip coinCollect;
    public AudioClip buySound;
    private bool useFirstHitSound = true;

    [Header("Audio Source")]
    public AudioSource sfxSource;
    public AudioSource musicSource;
   

    [Header("Volume")]
    [Range(0f, 1f)]
    public float sfxVolume = 1f;

    [Range(0f, 1f)]
    public float musicVolume = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        if (sfxSource == musicSource)
        {
            var sharedSource = sfxSource;
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.outputAudioMixerGroup = sharedSource.outputAudioMixerGroup;
            sfxSource.spatialBlend = sharedSource.spatialBlend;
        }

        sfxSource.playOnAwake = false;
        musicSource.playOnAwake = false;

        sfxSource.loop = false;
        musicSource.loop = true;

        if (PlayerPrefs.HasKey("SFX"))
            sfxVolume = PlayerPrefs.GetInt("SFX", 1) == 1 ? 1f : 0f;
        if (PlayerPrefs.HasKey("Mussic"))
            musicVolume = PlayerPrefs.GetInt("Mussic", 1) == 1 ? 1f : 0f;

        sfxSource.volume = sfxVolume;
        musicSource.volume = musicVolume;
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        if (sfxSource == null) return;

        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    public void PlaySFX(AudioClip clip, float volume)
    {
        if (clip == null) return;
        if (sfxSource == null) return;

        sfxSource.PlayOneShot(clip, volume * sfxVolume);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        if (musicSource == null) return;

        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource == null) return;

        musicSource.Stop();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);

        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume;
            if (sfxVolume <= 0f) sfxSource.Stop();
        }
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);

        if (musicSource != null) musicSource.volume = musicVolume;
    }
    public void PlayGroupHitSound()
    {
        AudioClip clip = useFirstHitSound ? groupHitSound : groupHitSound2;

        if (clip == null || sfxSource == null) return;

        sfxSource.PlayOneShot(clip, sfxVolume);

        useFirstHitSound = !useFirstHitSound;
    }
}
