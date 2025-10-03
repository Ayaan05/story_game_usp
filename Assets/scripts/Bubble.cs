using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;   // 👈 needed for OnPointerClick

public enum BubbleType { Small, Big }

[RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
public class Bubble : MonoBehaviour, IPointerClickHandler
{
    public static Action<Bubble> OnAnyPopped;

    [Header("Setup")]
    public BubbleType type = BubbleType.Small;

    [Header("Pop FX")]
    public GameObject popFxPrefab;     // assign PopFX prefab
    public Sprite popSprite;           // small/big pop sprite
    public float popFxScale = 1f;      // per-type tweak

    [Header("Audio (optional)")]
    public AudioSource sfx;

    CircleCollider2D col;
    SpriteRenderer sr;
    bool popped;

    void Awake()
    {
        col = GetComponent<CircleCollider2D>();
        sr  = GetComponent<SpriteRenderer>();
        if (col) col.isTrigger = true; // keep as you had it
    }

    // PRIMARY: EventSystem click
    public void OnPointerClick(PointerEventData eventData) => TryPop();

    // optional fallback for editor convenience
    void OnMouseDown() { TryPop(); Debug.Log($"CLICK {name}"); }

    public void TryPop()
    {
        if (popped) return;
        popped = true;

        // disable interactions & hide bubble immediately
        if (col) col.enabled = false;
        if (sr)  sr.enabled = false;

        // spawn pop FX (independent object)
// spawn pop FX (independent object)
if (popFxPrefab)
{
    var fxGO = Instantiate(popFxPrefab, transform.position, Quaternion.identity);
    var fx = fxGO.GetComponent<PopFX>();
    if (fx) fx.Play(popSprite, popFxScale, GetRadiusWorld() * 0.8f); // 👈 key change
}


        // play pop sound without being cut off by Destroy
        if (sfx && sfx.clip) AudioSource.PlayClipAtPoint(sfx.clip, transform.position, sfx.volume);

        // notify & remove bubble
        OnAnyPopped?.Invoke(this);
        Destroy(gameObject);
    }

    public float GetRadiusWorld()
    {
        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        return col ? col.radius * scale : 0.5f * scale;
    }
}
