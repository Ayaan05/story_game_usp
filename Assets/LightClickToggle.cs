using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class LightClickToggle : MonoBehaviour, IPointerDownHandler
{
    [Header("Sprites")]
    public Sprite offSprite;
    public Sprite onSprite;
    public bool startOn = false;

    [Header("Optional SFX")]
    public AudioSource sfx;
    public AudioClip clickClip;

    SpriteRenderer sr;
    bool isOn;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        isOn = startOn;
        ApplySprite();
    }

    void OnEnable()
    {
        ApplySprite();
    }

    // Legacy mouse path (works without EventSystem)
    void OnMouseDown()
    {
        Toggle();
    }

    // EventSystem path (works with new Input System + Physics2DRaycaster on Camera)
    public void OnPointerDown(PointerEventData eventData)
    {
        Toggle();
    }

    void Toggle()
    {
        isOn = !isOn;
        ApplySprite();
        if (sfx && clickClip) sfx.PlayOneShot(clickClip);
    }

    void ApplySprite()
    {
        if (!sr) sr = GetComponent<SpriteRenderer>();
        if (sr) sr.sprite = isOn ? onSprite : offSprite;
    }
}