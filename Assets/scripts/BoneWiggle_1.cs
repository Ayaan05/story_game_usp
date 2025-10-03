using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class BoneWiggle : MonoBehaviour, IPointerClickHandler
{
    [Header("Wiggle")]
    [SerializeField, Range(2f, 30f)] float wiggleAngle = 12f;   // degrees (± around Z)
    [SerializeField, Range(0.05f, 0.6f)] float wiggleTime = 0.25f;
    [SerializeField, Range(1, 5)] int cycles = 2;

    [Header("Optional pop")]
    [SerializeField, Range(0.9f, 1.2f)] float popScale = 1.04f; // tiny scale pop
    [SerializeField, Range(0.05f, 0.4f)] float popTime = 0.12f;

    [Header("Audio (optional)")]
    [SerializeField] AudioSource sfxSource;        // 2D source on this object or parent
    [SerializeField] AudioClip wiggleClip;         // e.g. pen-click-2-411631
    [SerializeField, Range(0f, 1f)] float sfxVolume = 1f;

    bool busy;

    void Reset()
    {
        // If you forget to add a collider, try to add one sized to the sprite.
        if (!TryGetComponent<Collider2D>(out var _))
        {
            var col = gameObject.AddComponent<BoxCollider2D>();
            var sr = GetComponent<SpriteRenderer>();
            if (sr && col is BoxCollider2D bc)
                bc.size = sr.bounds.size; // rough fit; tweak in Inspector
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Ensure we only react if THIS object was hit (not background doghouse).
        if (eventData.pointerCurrentRaycast.gameObject != gameObject) return;
        if (!busy) StartCoroutine(WiggleRoutine());
    }

    System.Collections.IEnumerator WiggleRoutine()
    {
        busy = true;

        // optional pop
        yield return StartCoroutine(Pop(popScale, popTime));

        // sfx
        if (wiggleClip)
        {
            EnsureSfxSource();
            if (sfxSource) sfxSource.PlayOneShot(wiggleClip, sfxVolume);
        }

        // wiggle around Z
        Quaternion startRot = transform.localRotation;
        float half = wiggleTime / (cycles * 2f);

        for (int i = 0; i < cycles; i++)
        {
            yield return StartCoroutine(RotateDelta(+wiggleAngle, half));
            yield return StartCoroutine(RotateDelta(-wiggleAngle * 2f, half * 2f));
            yield return StartCoroutine(RotateDelta(+wiggleAngle, half));
        }

        // settle back exactly
        transform.localRotation = startRot;
        busy = false;
    }

    System.Collections.IEnumerator RotateDelta(float deltaZ, float dur)
    {
        float t = 0f;
        float startZ = transform.localEulerAngles.z;
        float endZ = startZ + deltaZ;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float z = Mathf.Lerp(startZ, endZ, t / dur);
            var e = transform.localEulerAngles;
            e.z = z;
            transform.localEulerAngles = e;
            yield return null;
        }
    }

    System.Collections.IEnumerator Pop(float targetMul, float dur)
    {
        Vector3 baseScale = transform.localScale;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float s = Mathf.Lerp(1f, targetMul, u);
            transform.localScale = baseScale * s;
            yield return null;
        }
        // snap back
        transform.localScale = baseScale;
    }

    void EnsureSfxSource()
    {
        if (sfxSource != null) return;

        var srcs = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var s in srcs)
        {
            if (!s.loop)
            {
                sfxSource = s;
                return;
            }
        }
        if (srcs.Length > 0) sfxSource = srcs[0];
    }
}
