using UnityEngine;
using UnityEngine.EventSystems;   // for IPointerDownHandler (EventSystem path)

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class MailboxClickToggle : MonoBehaviour, IPointerDownHandler
{
    [Header("Sprites")]
    public Sprite closedSprite;              // mailbox_closed
    public Sprite openSprite;                // mailbox_open
    public bool startClosed = true;

    [Header("Wiggle")]
    [Range(1f, 25f)]  public float wiggleAngleDeg = 8f;
    [Range(0.05f, 3f)] public float wiggleDuration = 0.8f;
    [Range(1, 5)]      public int   wiggleCycles   = 2;

    [Header("Optional SFX")]
    public AudioSource sfx;
    public AudioClip openClip;
    public AudioClip closeClip;

    SpriteRenderer sr;
    bool isOpen;
    bool animating;
    Quaternion baseRot;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        isOpen = !startClosed;
        ApplySprite(isOpen);
    }

    void OnEnable()
    {
        baseRot = transform.rotation;    // remember “resting” rotation each time it enables
        ApplySprite(isOpen);
    }

    // ------- INPUT PATH #1: legacy mouse ------------
    void OnMouseDown()
    {
        Toggle();
    }

    // ------- INPUT PATH #2: EventSystem -------------
    public void OnPointerDown(PointerEventData eventData)
    {
        Toggle();
    }

    // Public so you can wire this in the Inspector (Intercepted Events) if you want
    public void Toggle()
    {
        if (animating) return;
        StartCoroutine(WiggleThenSwap(!isOpen));
    }

    System.Collections.IEnumerator WiggleThenSwap(bool openNext)
    {
        animating = true;
        float T = Mathf.Max(0.05f, wiggleDuration);
        int cycles = Mathf.Max(1, wiggleCycles);

        float t = 0f;
        try
        {
            while (t < T)
            {
                t += Time.unscaledDeltaTime; // unaffected by timescale
                float u = Mathf.Clamp01(t / T);
                // ease in/out to avoid snappy jerk
                float ease = Mathf.SmoothStep(0f, 1f, u);
                float osc  = Mathf.Sin(ease * cycles * Mathf.PI * 2f);
                float ang  = osc * wiggleAngleDeg;

                transform.rotation = baseRot * Quaternion.Euler(0, 0, ang);
                yield return null;
            }
        }
        finally
        {
            // guarantee cleanup even if we exit early
            transform.rotation = baseRot;
            isOpen = openNext;
            ApplySprite(isOpen);

            if (sfx != null)
                sfx.PlayOneShot(isOpen ? openClip : closeClip);

            animating = false;
        }
    }

    void ApplySprite(bool openState)
    {
        if (!sr) sr = GetComponent<SpriteRenderer>();
        if (sr) sr.sprite = openState ? openSprite : closedSprite;
    }
}
