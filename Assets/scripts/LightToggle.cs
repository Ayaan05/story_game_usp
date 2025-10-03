using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Simple tap/click toggle for the string lights. Swaps sprites (SpriteRenderer or UI Image)
/// and optionally plays different SFX when turning on/off.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LightToggle : MonoBehaviour, IPointerClickHandler
{
    [Header("Sprites")]
    [Tooltip("Sprite shown when the lights are OFF.")]
    [SerializeField] private Sprite offSprite;
    [Tooltip("Sprite shown when the lights are ON.")]
    [SerializeField] private Sprite onSprite;

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

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        image = GetComponent<Image>();

        if (!spriteRenderer && !image)
        {
            Debug.LogWarning("LightToggle: attach this to a SpriteRenderer or Image based object.");
        }

        if (sfxSource == null)
        {
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
    }

    void Start()
    {
        SetState(startOn, true);
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

    void SetState(bool on, bool instant)
    {
        if (isOn == on && !instant) return;
        isOn = on;

        Sprite target = isOn ? onSprite : offSprite;
        if (target != null)
        {
            if (spriteRenderer) spriteRenderer.sprite = target;
            if (image) image.sprite = target;
        }

        if (!instant)
        {
            PlayToggleSfx(isOn);
        }
    }

    void PlayToggleSfx(bool turningOn)
    {
        var clip = turningOn ? toggleOnClip : toggleOffClip;
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }
}
