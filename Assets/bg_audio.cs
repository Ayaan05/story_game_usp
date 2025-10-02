using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    private static MusicPlayer _instance;
    public static MusicPlayer Instance => _instance;

    private AudioSource audioSource;

    [Header("Music Settings")]
    [Tooltip("Start playing automatically on Awake if an AudioSource with a clip is present.")]
    public bool playOnAwake = true;
    [Tooltip("Volume for the persistent music AudioSource.")]
    [Range(0f,1f)] public float volume = 0.5f;

    void Awake()
    {
        // If another MusicPlayer already exists, destroy this one
        if (_instance != null && _instance != this)
        {
            Debug.Log("MusicPlayer: duplicate detected, destroying " + gameObject.name);
            Destroy(gameObject);
            return;
        }

        // Otherwise, set this as the instance and make it persistent
        _instance = this;
        // Make sure this MusicPlayer is a root GameObject before marking DontDestroyOnLoad
        if (transform.parent != null)
        {
            transform.SetParent(null);
            Debug.Log("MusicPlayer: unparented to make root before DontDestroyOnLoad");
        }
        DontDestroyOnLoad(gameObject);

        // Get or add the AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = volume;

        if (playOnAwake && audioSource.clip != null)
        {
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
                Debug.Log("MusicPlayer: started music " + audioSource.clip.name + " on " + gameObject.name);
            }
        }
        else if (playOnAwake && audioSource.clip == null)
        {
            // Try to find any AudioSource in the scene that already has a clip and adopt it
            var others = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (var o in others)
            {
                if (o != audioSource && o.clip != null)
                {
                    audioSource.clip = o.clip;
                    Debug.Log("MusicPlayer: adopted clip '" + o.clip.name + "' from " + o.gameObject.name);
                    audioSource.Play();
                    break;
                }
            }

            // If still no clip, attempt to load from Resources/BackgroundMusic (user can add a clip there)
            if (audioSource.clip == null)
            {
                var resClip = Resources.Load<AudioClip>("BackgroundMusic");
                if (resClip != null)
                {
                    audioSource.clip = resClip;
                    audioSource.Play();
                    Debug.Log("MusicPlayer: loaded clip 'BackgroundMusic' from Resources and started playing");
                }
                else
                {
                    Debug.LogWarning("MusicPlayer: no audio clip assigned and none found in scene. Put a clip on the AudioSource or add 'BackgroundMusic' to Resources, or call MusicPlayer.PlayClip(yourClip).");
                }
            }
        }
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            Debug.Log("MusicPlayer: instance destroyed");
        }
    }

    // Public helpers
    public void Play()
    {
        if (audioSource == null) return;
        if (!audioSource.isPlaying) audioSource.Play();
    }

    public void Stop()
    {
        if (audioSource == null) return;
        if (audioSource.isPlaying) audioSource.Stop();
    }

    public void SetVolume(float v)
    {
        volume = Mathf.Clamp01(v);
        if (audioSource != null) audioSource.volume = volume;
    }

    public void SetClip(AudioClip clip, bool playImmediately = true)
    {
        if (audioSource == null) return;
        audioSource.clip = clip;
        if (playImmediately && clip != null)
        {
            audioSource.Play();
        }
    }

    // Static helper to ensure music is playing from anywhere
    public static void PlayClipStatic(AudioClip clip, bool loop = true, float vol = 0.5f)
    {
        if (clip == null)
        {
            Debug.LogWarning("MusicPlayer.PlayClipStatic: clip is null");
            return;
        }

        if (_instance != null)
        {
            _instance.audioSource.clip = clip;
            _instance.audioSource.loop = loop;
            _instance.SetVolume(vol);
            _instance.audioSource.Play();
            Debug.Log("MusicPlayer: PlayClipStatic started clip " + clip.name);
            return;
        }

        // No instance yet: create a GameObject and attach
        var go = new GameObject("MusicPlayer_Auto");
        var mp = go.AddComponent<MusicPlayer>();
        mp.audioSource = go.GetComponent<AudioSource>();
        mp.audioSource.clip = clip;
        mp.audioSource.loop = loop;
        mp.SetVolume(vol);
        DontDestroyOnLoad(go);
        mp.audioSource.Play();
        _instance = mp;
        Debug.Log("MusicPlayer: PlayClipStatic created MusicPlayer_Auto and started clip " + clip.name);
    }
}
