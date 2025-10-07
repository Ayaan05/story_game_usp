using System.Collections.Generic;
using UnityEngine;

public class DogCleanliness : MonoBehaviour
{
    [Header("Dog Renderers")]
    public SpriteRenderer cleanSprite;   // CleanDog
    public SpriteRenderer dirtySprite;   // DirtyDog
    public TailWag tailWag; 
    public DogBlinkOneSprite blink; // assign in Inspector


    [Header("Mask Control")]
    public Transform maskContainer;      // parent with your SpriteMasks (all disabled at start)
    public bool autoCollectMasks = true; // auto-grab masks from maskContainer (hierarchy order)
    public List<SpriteMask> revealMasks = new List<SpriteMask>();

    [Header("Reveal cadence")]
    [Tooltip("How many bubble pops per mask reveal. With 12 masks & 24 bubbles, set to 2.")]
    public int popsPerMask = 2;          // <- keep this at 2

    [Header("Finish FX (optional)")]
    public GameObject sparklesParent;    // turns on when all masks revealed

    // progress
    int totalBubbles = 24;               // spawner sets this (should be 24)
    int popped = 0;

    void Awake()
    {
        // correct mask interactions so masks reveal clean & cut dirt
        if (cleanSprite) cleanSprite.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        if (dirtySprite) dirtySprite.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;

        if (autoCollectMasks) CollectMasks();
        SetActiveCount(0);
        if (sparklesParent) sparklesParent.SetActive(false);
    }

    void CollectMasks()
    {
        revealMasks.Clear();
        if (!maskContainer)
        {
            var t = transform.Find("MaskContainer");
            if (t) maskContainer = t;
        }
        if (maskContainer)
        {
            var masks = maskContainer.GetComponentsInChildren<SpriteMask>(true);
            revealMasks.AddRange(masks);
            foreach (var m in revealMasks) if (m) m.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("DogCleanliness: No maskContainer assigned/found.");
        }
    }

    // -------- called by BubbleSpawner --------
    public void SetTotal(int total)
    {
        totalBubbles = Mathf.Max(1, total);
        UpdateReveal();
    }

    public void ReportPopped(int poppedCount)
    {
        popped = Mathf.Clamp(poppedCount, 0, totalBubbles);
        UpdateReveal();
        // inside ReportPopped(int p)
if (blink) blink.SetProgress01(totalBubbles > 0 ? (float)popped / totalBubbles : 0f);

    }

    public void AllClean()
    {
        popped = totalBubbles;
        SetActiveCount(revealMasks.Count);
        if (sparklesParent) sparklesParent.SetActive(true);
        if (dirtySprite) { var c = dirtySprite.color; c.a = 0f; dirtySprite.color = c; } // optional: snap dirt off
        if (tailWag) tailWag.SetHappy();  // stays at happySpeed forever

    }
    // ----------------------------------------

    void UpdateReveal()
    {
        int maskCount = revealMasks.Count;
        if (maskCount == 0 || popsPerMask <= 0) return;

        // 2 pops → 1 mask, 24 pops → 12 masks, etc.
        int active = Mathf.Min(popped / popsPerMask, maskCount);
        SetActiveCount(active);
    }

    void SetActiveCount(int k)
    {
        for (int i = 0; i < revealMasks.Count; i++)
        {
            var m = revealMasks[i];
            if (!m) continue;
            bool on = (i < k);
            if (m.gameObject.activeSelf != on)
                m.gameObject.SetActive(on);
        }
    }

    // handy debug buttons
    [ContextMenu("Disable All Masks")] void _AllOff() => SetActiveCount(0);
    [ContextMenu("Enable All Masks")]  void _AllOn()  => SetActiveCount(revealMasks.Count);
}
