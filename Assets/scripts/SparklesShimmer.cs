using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SparklesShimmer : MonoBehaviour
{
    [Header("Shimmer")]
    public float alphaMin = 0.5f;      // lowest opacity
    public float alphaMax = 1.0f;      // highest opacity
    public float shimmerHz = 3f;       // twinkles per second

    [Header("Glow scale pulse (optional)")]
    public float scalePulse = 0.05f;   // 0.0 to disable; 0.05 = +/−5% size
    public float pulseHz = 2f;         // pulses per second

    [Header("Subtle drift (optional)")]
    public float driftRadius = 0.03f;  // world units of micro-movement
    public float driftHz = 0.4f;       // cycles per second

    private SpriteRenderer sr;
    private Color baseColor;
    private Vector3 basePos;
    private Vector3 baseScale;
    private float t;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseColor = sr.color;
        basePos = transform.position;
        baseScale = transform.localScale;
    }

    void Update()
    {
        t += Time.deltaTime;

        // Alpha shimmer (sin mapped to [alphaMin..alphaMax])
        float aMid = (alphaMin + alphaMax) * 0.5f;
        float aAmp = Mathf.Max(0f, (alphaMax - alphaMin) * 0.5f);
        float a = aMid + Mathf.Sin(t * Mathf.PI * 2f * shimmerHz) * aAmp;
        sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);

        // Scale pulse
        if (scalePulse > 0f)
        {
            float s = 1f + Mathf.Sin(t * Mathf.PI * 2f * pulseHz) * scalePulse;
            transform.localScale = baseScale * s;
        }

        // Subtle drift
        if (driftRadius > 0f)
        {
            float dx = Mathf.Sin(t * Mathf.PI * 2f * driftHz) * driftRadius;
            float dy = Mathf.Cos(t * Mathf.PI * 2f * driftHz * 0.8f) * driftRadius * 0.6f;
            transform.position = basePos + new Vector3(dx, dy, 0f);
        }
    }

    // Optional: call to reset back to initial pose/color
    public void ResetShimmer()
    {
        t = 0f;
        sr.color = baseColor;
        transform.position = basePos;
        transform.localScale = baseScale;
    }
}
