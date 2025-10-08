using UnityEngine;

/// Gentle, tight motion that RESPECTS the scale set by your spawner.
/// Critical: baseline (pos/scale/rot) is captured in Start(), not Awake/OnEnable.
[DisallowMultipleComponent]
public class BubbleFloat : MonoBehaviour
{
    [Header("Amplitudes (world units & degrees) — tight defaults")]
    public float bobAmplitude   = 0.06f;
    public float swayAmplitude  = 0.04f;
    public float rotAmplitude   = 2f;
    public float breatheAmount  = 0.01f;   // ±1% scale

    [Header("Speeds (cycles per second)")]
    public float bobSpeed     = 0.22f;
    public float swaySpeed    = 0.18f;
    public float rotSpeed     = 0.16f;
    public float breatheSpeed = 0.20f;

    [Header("Variance (per-instance randomness)")]
    public Vector2 ampJitter   = new Vector2(0.90f, 1.10f);
    public Vector2 speedJitter = new Vector2(0.90f, 1.10f);

    [Header("Clamp motion near spawn")]
    public float maxDriftRadius = 0.08f;              // hard cap (world units)
    public bool  limitByBubbleRadius = true;          // also cap by % of radius
    [Range(0.05f, 0.5f)] public float maxOffsetAsRadiusFraction = 0.15f;

    [Header("Type-based tweak (optional)")]
    public bool autoTuneFromBubble = true;
    public float bigAmpMul   = 1.1f;
    public float smallAmpMul = 0.95f;

    Vector3 basePos, baseScale;
    Quaternion baseRot;
    float aMul = 1f, sMul = 1f;
    float pBob, pSway, pRot, pBreath;   // random phases
    Bubble bubble;
    float bubbleRadiusWorld = 0.5f;
    bool initialized;

    // DO NOT capture baseline in Awake/OnEnable — spawner hasn't finished scaling yet.
    void Awake()
    {
        bubble = GetComponent<Bubble>();

        // randomize phases + per-instance variance
        pBob    = Random.value * 100f;
        pSway   = Random.value * 100f;
        pRot    = Random.value * 100f;
        pBreath = Random.value * 100f;

        aMul = Random.Range(ampJitter.x,   ampJitter.y);
        sMul = Random.Range(speedJitter.x, speedJitter.y);

        if (autoTuneFromBubble && bubble != null)
            aMul *= (bubble.type == BubbleType.Big) ? bigAmpMul : smallAmpMul;
    }

    void Start()
    {
        // Capture the FINAL transform set by the spawner (position + scale already applied)
        basePos   = transform.position;
        baseScale = transform.localScale;
        baseRot   = transform.localRotation;

        if (bubble != null) bubbleRadiusWorld = Mathf.Max(0.0001f, bubble.GetRadiusWorld());
        initialized = true;
    }

    void Update()
    {
        if (!initialized) return; // wait until Start captured the baseline

        float t = Time.time;

        // cycles/sec → radians
        float y = Mathf.Sin((pBob    + t) * (Mathf.PI * 2f) * (bobSpeed     * sMul)) * (bobAmplitude   * aMul);
        float x = Mathf.Sin((pSway   + t) * (Mathf.PI * 2f) * (swaySpeed    * sMul)) * (swayAmplitude  * aMul);
        float r = Mathf.Sin((pRot    + t) * (Mathf.PI * 2f) * (rotSpeed     * sMul)) * (rotAmplitude   * aMul);
        float b = Mathf.Sin((pBreath + t) * (Mathf.PI * 2f) * (breatheSpeed * sMul)) * (breatheAmount  * aMul);

        // clamp positional drift
        Vector2 offset = new Vector2(x, y);
        float clampR = maxDriftRadius;
        if (limitByBubbleRadius)
            clampR = Mathf.Min(clampR, bubbleRadiusWorld * maxOffsetAsRadiusFraction);
        offset = Vector2.ClampMagnitude(offset, clampR);

        // apply relative to captured baseline
        transform.position      = basePos + (Vector3)offset;
        transform.localRotation = Quaternion.Euler(0f, 0f, r) * baseRot;
        transform.localScale    = baseScale * (1f + b);
    }

    /// Call this if you ever manually resize/reposition a bubble AFTER Start (rare).
    public void RebaseToCurrent()
    {
        basePos   = transform.position;
        baseScale = transform.localScale;
        baseRot   = transform.localRotation;
        if (bubble != null) bubbleRadiusWorld = Mathf.Max(0.0001f, bubble.GetRadiusWorld());
        initialized = true;
    }
}
