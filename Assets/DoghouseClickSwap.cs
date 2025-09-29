using UnityEngine;
using UnityEngine.EventSystems; // IMPORTANT

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class DoghouseClickSwap : MonoBehaviour, IPointerClickHandler
{
    [Header("Assign in Inspector")]
    [SerializeField] private Sprite closedSprite;   // doghouse_closed
    [SerializeField] private Sprite openSprite;     // doghouse_open
    [SerializeField] private bool toggle = true;

    private SpriteRenderer sr;
    private bool isOpen;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        // Auto-fit collider to sprite bounds, so clicks hit correctly
        var box = GetComponent<BoxCollider2D>();
        if (sr.sprite != null)
        {
            var b = sr.sprite.bounds; // local
            box.size = b.size;
            box.offset = b.center;
        }

        // Start state from current sprite (optional)
        isOpen = (sr.sprite == openSprite);
    }

    // Fired by EventSystem when you click/tap this object (because it has a collider)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!openSprite || !closedSprite) return;

        if (toggle)
        {
            isOpen = !isOpen;
            sr.sprite = isOpen ? openSprite : closedSprite;
        }
        else
        {
            sr.sprite = openSprite;
            enabled = false;
        }
    }
}
