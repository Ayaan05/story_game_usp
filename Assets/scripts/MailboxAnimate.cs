using UnityEngine;
using UnityEngine.EventSystems;   // for IPointerDownHandler

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class MailboxClickToggle : MonoBehaviour, IPointerDownHandler
{
    [Header("Sprites")]
    public Sprite closedSprite;          // mailbox_closed
    public Sprite openSprite;            // mailbox_open
    public bool startClosed = true;

    [Header("Optional SFX")]
    public AudioSource sfx;
    public AudioClip openClip;
    public AudioClip closeClip;

    SpriteRenderer sr;
    bool isOpen;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        isOpen = !startClosed;
        ApplySprite();
    }

    void OnEnable()
    {
        ApplySprite();
    }

    // Path 1: legacy mouse input
    void OnMouseDown()
    {
        Toggle();
    }

    // Path 2: EventSystem input
    public void OnPointerDown(PointerEventData eventData)
    {
        Toggle();
    }

    public void Toggle()
    {
        isOpen = !isOpen;
        ApplySprite();

        if (sfx != null)
        {
            var clip = isOpen ? openClip : closeClip;
            if (clip != null) sfx.PlayOneShot(clip);
        }
    }

    void ApplySprite()
    {
        if (!sr) sr = GetComponent<SpriteRenderer>();
        if (!sr) return;

        sr.sprite = isOpen ? openSprite : closedSprite;
    }
}
