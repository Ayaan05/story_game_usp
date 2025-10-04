using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFadeAlphaOnly : MonoBehaviour
{
    public float duration = 0.6f;      // fade time
    public float startAlpha = 0f;      // 0..1 at enable
    public float endAlpha = 1f;        // 0..1 target
    public AnimationCurve ease = AnimationCurve.EaseInOut(0,0, 1,1);

    private SpriteRenderer sr;
    private Color rgb;                 // preserve the original RGB exactly
    private bool running;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        // Sanity: leave material untouched; do NOT assign sharedMaterial = null here.
        // Sanity: read current RGB and keep it
        var c = sr.color;
        rgb = new Color(c.r, c.g, c.b, 1f);

        // Initialize with start alpha
        float a0 = Mathf.Clamp01(startAlpha);
        sr.color = new Color(rgb.r, rgb.g, rgb.b, a0);
        sr.enabled = true;
    }

    void OnEnable()
    {
        // Support pooling: restart fade from current alpha if needed
        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    private System.Collections.IEnumerator FadeIn()
    {
        running = true;
        float t = 0f;

        float fromA = Mathf.Clamp01(sr.color.a);
        float toA   = Mathf.Clamp01(endAlpha);

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float a = Mathf.LerpUnclamped(fromA, toA, ease.Evaluate(k));
            sr.color = new Color(rgb.r, rgb.g, rgb.b, a);
            yield return null;
        }

        sr.color = new Color(rgb.r, rgb.g, rgb.b, toA);
        running = false;
    }
}
