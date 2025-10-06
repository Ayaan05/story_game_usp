using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class BoneSpringBounce : MonoBehaviour, IPointerClickHandler
{
    public enum KickMode { Velocity, Height }

    [Header("Kick Mode")]
    [SerializeField] KickMode kickMode = KickMode.Height;

    [Header("Kick (Velocity mode)")]
    [SerializeField, Range(0.01f, 50f)] float kickVelocity = 6f;          // ↓ smaller min

    [Header("Kick (Height mode)")]
    [SerializeField, Range(0.001f, 1f)] float kickHeightPercent = 0.30f;  // ↓ smaller min

    [Header("Kick Ease (slows the initial pop)")]
    [Tooltip("Time to ease into the initial kick (seconds). 0 = instant.")]
    [SerializeField, Range(0f, 1f)] float kickEaseTime = 0.25f;
    [SerializeField] AnimationCurve kickEase = AnimationCurve.EaseInOut(0,0,1,1);

    [Header("Spring Back")]
    [SerializeField, Range(0.5f, 8f)]  float springFrequency = 2.5f;
    [SerializeField, Range(0f, 1.2f)]  float dampingRatio   = 0.35f;
    [SerializeField, Range(0.2f, 3f)]  float maxSettleTime  = 1.5f;

    [Header("Timing")]
    [Tooltip("Values >1 slow the entire bounce; <1 speeds it up.")]
    [SerializeField, Range(0.25f, 4f)] float timeStretch = 1.5f;

    [Header("Settle Thresholds")]
    [SerializeField, Range(0.0005f, 0.05f)] float posEps   = 0.002f;
    [SerializeField, Range(0.002f, 0.5f)]  float speedEps = 0.02f;

    [Header("Optional SFX")]
    [SerializeField] AudioSource sfx;
    [SerializeField] AudioClip clickClip;
    [SerializeField, Range(0f,1f)] float sfxVolume = 1f;

    Vector3 baseLocalPos;
    Coroutine anim;

    void OnEnable() => baseLocalPos = transform.localPosition;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.pointerCurrentRaycast.gameObject != gameObject) return;

        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(BounceRoutine());

        if (sfx && clickClip) sfx.PlayOneShot(clickClip, sfxVolume);
    }

    System.Collections.IEnumerator BounceRoutine()
    {
        float y = 0f, v = 0f;

        // --- Gentle kick-in phase (slows the initial pop) ---
        float wH = 1f;
        var sr = GetComponent<SpriteRenderer>();
        if (sr) wH = Mathf.Max(0.0001f, sr.bounds.size.y);
        float localH = wH / Mathf.Max(0.0001f, transform.lossyScale.y);

        float dt; float tEase = 0f;
        if (kickMode == KickMode.Height)
        {
            float targetY = Mathf.Clamp01(kickHeightPercent) * localH;

            if (kickEaseTime > 0f)
            {
                while (tEase < kickEaseTime)
                {
                    dt = Time.unscaledDeltaTime / Mathf.Max(0.01f, timeStretch);
                    tEase += dt;
                    float u = Mathf.Clamp01(tEase / kickEaseTime);
                    y = Mathf.Lerp(0f, targetY, kickEase.Evaluate(u));
                    var p = baseLocalPos; p.y += y; transform.localPosition = p;
                    yield return null;
                }
            }
            else
            {
                y = targetY;
            }
            v = 0f; // start spring from rest at displaced position
        }
        else // Velocity mode
        {
            float targetV = kickVelocity;
            if (kickEaseTime > 0f)
            {
                while (tEase < kickEaseTime)
                {
                    dt = Time.unscaledDeltaTime / Mathf.Max(0.01f, timeStretch);
                    tEase += dt;
                    float u = Mathf.Clamp01(tEase / kickEaseTime);
                    v = Mathf.Lerp(0f, targetV, kickEase.Evaluate(u));
                    y += v * dt;
                    var p = baseLocalPos; p.y += y; transform.localPosition = p;
                    yield return null;
                }
            }
            else
            {
                v = targetV;
            }
        }

        // --- Spring simulation ---
        float w = 2f * Mathf.PI * Mathf.Max(0.01f, springFrequency);
        float k = w * w;
        float c = 2f * dampingRatio * w;

        float t = 0f;
        float allow = maxSettleTime * Mathf.Max(0.01f, timeStretch);

        while (t < allow)
        {
            dt = Time.unscaledDeltaTime / Mathf.Max(0.01f, timeStretch);
            t += dt;

            float a = -k * y - c * v;
            v += a * dt;
            y += v * dt;

            var p = baseLocalPos; p.y += y;
            transform.localPosition = p;

            if (Mathf.Abs(y) < posEps && Mathf.Abs(v) < speedEps)
                break;

            yield return null;
        }

        transform.localPosition = baseLocalPos;
        anim = null;
    }
}

