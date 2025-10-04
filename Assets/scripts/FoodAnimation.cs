using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFadeAlphaOnly : MonoBehaviour
{
    public float duration = 0.6f;      // fade + fall time
    public float startAlpha = 0f;      // 0..1 at enable
    public float endAlpha = 1f;        // 0..1 target
    public AnimationCurve ease = AnimationCurve.EaseInOut(0,0, 1,1);

    [Header("Fall motion")]
    public float startHeight = 1.0f;   // how high above current Y to start

    private SpriteRenderer sr;
    private Color rgb;                 // preserve the original RGB exactly
    private bool running;

    // Movement state
    private Vector3 landPos;           // final landing pos (current position)
    private Vector3 startPos;          // starting pos (landPos + up * startHeight)

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        // Preserve RGB; only animate alpha
        var c = sr.color;
        rgb = new Color(c.r, c.g, c.b, 1f);

        // Initialize with start alpha
        float a0 = Mathf.Clamp01(startAlpha);
        sr.color = new Color(rgb.r, rgb.g, rgb.b, a0);
        sr.enabled = true;
    }

    void OnEnable()
    {
        // Set up positions
        landPos = transform.position;
        startPos = landPos + Vector3.up * startHeight;

        // Start above and at start alpha
        transform.position = startPos;
        sr.color = new Color(rgb.r, rgb.g, rgb.b, Mathf.Clamp01(startAlpha));

        StopAllCoroutines();
        StartCoroutine(FadeAndFall());
    }

    private System.Collections.IEnumerator FadeAndFall()
    {
        running = true;
        float t = 0f;

        float fromA = Mathf.Clamp01(sr.color.a);
        float toA   = Mathf.Clamp01(endAlpha);

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float e = ease.Evaluate(k);

            // Alpha
            float a = Mathf.LerpUnclamped(fromA, toA, e);
            sr.color = new Color(rgb.r, rgb.g, rgb.b, a);

            // Position (vertical fall)
            float y = Mathf.LerpUnclamped(startPos.y, landPos.y, e);
            transform.position = new Vector3(landPos.x, y, landPos.z);

            yield return null;
        }

        // Finalize
        sr.color = new Color(rgb.r, rgb.g, rgb.b, toA);
        transform.position = landPos;
        running = false;
    }
}
