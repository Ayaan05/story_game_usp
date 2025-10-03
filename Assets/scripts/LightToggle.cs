using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Simple tap/click toggle for the string lights. Swaps sprites (SpriteRenderer or UI Image)
/// and optionally plays different SFX when turning on/off. Works in edit mode so you can
/// preview offsets by toggling Start On.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Collider2D))]
public class LightToggle : MonoBehaviour, IPointerClickHandler
{
    [Header("Sprites")]
    [Tooltip("Sprite shown when the lights are OFF.")]
    [SerializeField] private Sprite offSprite;
    [Tooltip("Sprite shown when the lights are ON.")]
    [SerializeField] private Sprite onSprite;

    [Header("Sprite Offsets (optional)")]
    [Tooltip("Local position offset applied when lights are OFF.")]
    [SerializeField] private Vector2 offOffset;
    [Tooltip("Local position offset applied when lights are ON.")]
    [SerializeField] private Vector2 onOffset;

    [Header("Startup State")]
    [SerializeField] private bool startOn = false;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip toggleOnClip;
    [SerializeField] private AudioClip toggleOffClip;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Header("Input")]
    [SerializeField] private bool restrictToLeftClick = true;

    SpriteRenderer spriteRenderer;
    Image image;
    bool isOn;
    Vector3 baseLocalPos;
    Vector3 lastAppliedPosition;
    bool initialized;

    void Awake()
    {
        CacheRenderers();
        EnsureAudioSource();
        InitializeState(startOn);
    }

    void OnEnable()
    {
        CacheRenderers();
        InitializeState(startOn);
        ApplyState(startOn, true, true);
    }

    void Start()
    {
        ApplyState(startOn, true, true);
    }

    void OnValidate()
    {
        CacheRenderers();
        InitializeState(startOn);
        ApplyState(startOn, true, true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (restrictToLeftClick && eventData.button != PointerEventData.InputButton.Left)
            return;

        Toggle();
    }

    public void Toggle()
    {
        SetState(!isOn, false);
    }

    void SetState(bool on, bool instant, bool suppressSfx = false)
    {
        if (!instant && isOn == on) return;
        isOn = on;
        ApplyState(on, instant, suppressSfx);
    }

    void ApplyState(bool on, bool instant, bool suppressSfx)
    {
        if (!initialized)
        {
            CaptureBaseFromCurrent();
        }

        Sprite target = on ? onSprite : offSprite;
        if (target != null)
        {
            if (spriteRenderer) spriteRenderer.sprite = target;
            if (image) image.sprite = target;
        }

        Vector3 targetPos = baseLocalPos + (Vector3)(on ? onOffset : offOffset);
        if (!initialized || transform.localPosition != targetPos)
        {
            transform.localPosition = targetPos;
            lastAppliedPosition = targetPos;
        }

        if (!instant && !suppressSfx)
        {
            PlayToggleSfx(on);
        }
    }

    void CacheRenderers()
    {
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        if (!image) image = GetComponent<Image>();

        if (!spriteRenderer && !image)
        {
            Debug.LogWarning("LightToggle: attach this to a SpriteRenderer or Image based object.");
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
        {
            sfxSource = sources[0];
        }
    }

    void InitializeState(bool currentState)
    {
        isOn = currentState;

        if (!initialized)
        {
            CaptureBaseFromCurrent();
        }
        else if (transform.localPosition != lastAppliedPosition)
        {
            // user moved the object in edit mode → update base reference based on current state
            CaptureBaseFromCurrent();
        }
    }

    void CaptureBaseFromCurrent()
    {
        Vector2 currentOffset = isOn ? onOffset : offOffset;
        baseLocalPos = transform.localPosition - (Vector3)currentOffset;
        lastAppliedPosition = transform.localPosition;
        initialized = true;
    }

    void PlayToggleSfx(bool turningOn)
    {
        if (!Application.isPlaying) return;
        var clip = turningOn ? toggleOnClip : toggleOffClip;
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }
}
