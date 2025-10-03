using UnityEngine;

public class PlaySfxOnStart : MonoBehaviour
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;

    [Tooltip("Play only this many seconds. <= 0 means play full clip.")]
    public float maxSeconds = 0f;

    void Start()
    {
        if (!clip) return;

        // Make a throwaway AudioSource we can stop early
        var go = new GameObject("SFX_" + clip.name);
        go.transform.position = transform.position;

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        // Set spatialBlend to 0 for 2D feel (optional)
        // src.spatialBlend = 0f;

        src.Play();

        // Destroy the temporary source after maxSeconds (or full length)
        float life = (maxSeconds > 0f) ? maxSeconds : clip.length;
        Object.Destroy(go, life);
    }
}
