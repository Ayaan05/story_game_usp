using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class BoneSpringBounce : MonoBehaviour, IPointerClickHandler
{
    public enum KickMode { Velocity, Height }

    [Header("Kick Mode")]
    [SerializeField] KickMode kickMode = KickMode.Height;

    [Header("Kick (Velocity mode)")]
    [SerializeField, Range(0.1f, 50f)] float kickVelocity = 6f;

    [Header("Kick (Height mode)")]
    [SerializeField, Range(0.01f, 1f)] float kickHeightPercent = 0.30f;

    [Header("Spring Back")]
    [SerializeField, Range(0.5f, 8f)]  float springFrequency = 2.5f;
    [SerializeField, Range(0f, 1.2f)]  float dampingRatio   = 0.35f;
    [SerializeField, Range(0.2f, 3f)]  float maxSettleTime  = 1.5f;

    [Header("Timing")]
    [Tooltip("Values >1 slow the motion; <1 speed it up.")]
    [SerializeField, Range(0.25f, 4f)] float timeStretch = 2f;

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

        if (kickMode == KickMode.Velocity)
        {
            v = kickVelocity;
        }
        else
        {
            float worldH = 1f;
            var sr = GetComponent<SpriteRenderer>();
            if (sr) worldH = Mathf.Max(0.0001f, sr.bounds.size.y);
            float localH = worldH / Mathf.Max(0.0001f, transform.lossyScale.y);
            y = kickHeightPercent * localH;
        }

        float w = 2f * Mathf.PI * Mathf.Max(0.01f, springFrequency);
        float k = w * w;
        float c = 2f * dampingRatio * w;

        float t = 0f;
        float allow = maxSettleTime * Mathf.Max(0.01f, timeStretch);

        while (t < allow)
        {
            // ↓ slow time progression by timeStretch
            float dt = Time.unscaledDeltaTime / Mathf.Max(0.01f, timeStretch);
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
