using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SparklesPop : MonoBehaviour
{
    [Header("Who to animate")]
    public bool includeChildren = true;   // true = animate all child SpriteRenderers; false = only this GO's SR
    public bool playOnEnable = false;     // auto-play if object is enabled

    [Header("Timing")]
    public float upTime   = 0.10f;        // grow time
    public float downTime = 0.08f;        // settle time
    public float stagger  = 0.03f;        // delay between each child's start

    [Header("Scale (relative to each element's localScale)")]
    public float startScale     = 0.5f;   // spawn small
    public float overshootScale = 1.2f;   // pop past final
    public float endScale       = 1.0f;   // settle here

    [Header("Fade")]
    public float fadeInTime = 0.10f;      // alpha 0→1

    struct Elem { public Transform t; public SpriteRenderer sr; public Vector3 baseScale; }
    readonly List<Elem> elems = new();

    void Awake()
    {
        Collect();
        // prep hidden + small (so Play() animates in)
        foreach (var e in elems)
        {
            if (!e.sr) continue;
            var c = e.sr.color; c.a = 0f; e.sr.color = c;
            e.t.localScale = e.baseScale * startScale;
        }
    }

    void OnEnable()
    {
        if (playOnEnable) Play();
    }

    public void Play()
    {
        gameObject.SetActive(true);     // ensure visible
        StopAllCoroutines();
        StartCoroutine(PlayCo());
    }

    void Collect()
    {
        elems.Clear();
        if (includeChildren)
        {
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!sr) continue;
                elems.Add(new Elem { t = sr.transform, sr = sr, baseScale = sr.transform.localScale });
            }
        }
        else
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr) elems.Add(new Elem { t = sr.transform, sr = sr, baseScale = sr.transform.localScale });
        }
    }

    IEnumerator PlayCo()
    {
        for (int i = 0; i < elems.Count; i++)
        {
            var e = elems[i];
            StartCoroutine(PopOne(e));
            if (stagger > 0f) yield return new WaitForSeconds(stagger);
        }
    }

    IEnumerator PopOne(Elem e)
    {
        if (!e.sr) yield break;

        // up (scale + fade in)
        float t = 0f;
        var c = e.sr.color;
        while (t < upTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / upTime);
            e.t.localScale = Vector3.Lerp(e.baseScale * startScale, e.baseScale * overshootScale, k);
            c.a = Mathf.Lerp(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.0001f, fadeInTime)));
            e.sr.color = c;
            yield return null;
        }

        // down (settle)
        t = 0f;
        while (t < downTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / downTime);
            e.t.localScale = Vector3.Lerp(e.baseScale * overshootScale, e.baseScale * endScale, k);
            yield return null;
        }

        // ✅ leave permanently: full alpha, final scale, no spin
        c.a = 1f; e.sr.color = c;
        e.t.localScale = e.baseScale * endScale;
    }

    // Optional helpers
    [ContextMenu("Show Instant")]
    public void ShowInstant()
    {
        foreach (var e in elems)
        {
            if (!e.sr) continue;
            var c = e.sr.color; c.a = 1f; e.sr.color = c;
            e.t.localScale = e.baseScale * endScale;
        }
    }

    [ContextMenu("Reset Hidden")]
    public void ResetHidden()
    {
        foreach (var e in elems)
        {
            if (!e.sr) continue;
            var c = e.sr.color; c.a = 0f; e.sr.color = c;
            e.t.localScale = e.baseScale * startScale;
        }
    }
}
