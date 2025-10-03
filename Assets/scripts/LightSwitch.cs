using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Toggles between two child graphics (one for lights-off, one for lights-on) when tapped.
/// Attach this to a parent GameObject that holds both visuals.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LightSwitch : MonoBehaviour, IPointerClickHandler
{
    [Header("Graphics")]
    [Tooltip("GameObject that represents the lights OFF state.")]
    [SerializeField] private GameObject offGraphic;
    [Tooltip("GameObject that represents the lights ON state.")]
    [SerializeField] private GameObject onGraphic;

    [Header("Startup State")]
    [SerializeField] private bool startOn = false;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip toggleOnClip;
    [SerializeField] private AudioClip toggleOffClip;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    bool isOn;

    void Awake()
    {
        if (!offGraphic || !onGraphic)
            Debug.LogWarning("LightSwitch: assign both offGraphic and onGraphic.");

        EnsureAudioSource();
        ApplyState(startOn, instant: true);
    }

    void OnEnable()
    {
        ApplyState(isOn, instant: true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        ApplyState(!isOn, instant: false);
    }

    void ApplyState(bool turnOn, bool instant)
    {
        isOn = turnOn;

        if (offGraphic) offGraphic.SetActive(!isOn);
        if (onGraphic) onGraphic.SetActive(isOn);

        if (!instant)
        {
            var clip = isOn ? toggleOnClip : toggleOffClip;
            if (clip && sfxSource) sfxSource.PlayOneShot(clip, sfxVolume);
        }
    }

    void EnsureAudioSource()
    {
        if (sfxSource != null) return;

        var sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var src in sources)
        {
            if (!src.loop)
            {
                sfxSource = src;
                break;
            }
        }
        if (sfxSource == null && sources.Length > 0)
            sfxSource = sources[0];
    }
}
