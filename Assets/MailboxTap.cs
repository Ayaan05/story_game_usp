using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class MailboxTap : MonoBehaviour, IPointerClickHandler
{
    [Header("Rotate this Transform as the lid")]
    [SerializeField] private Transform lidTransform;

    [Header("Animation")]
    [SerializeField, Range(1f, 20f)] private float wiggleAngle = 8f;
    [SerializeField, Range(0.05f, 1f)] private float wiggleTime = 0.25f;
    [SerializeField, Range(1, 5)] private int wiggleCycles = 2;
    [SerializeField, Range(-120f, 120f)] private float lidOpenAngle = -35f; // Z rotation (negative = clockwise)
    [SerializeField, Range(0.05f, 0.6f)] private float lidOpenTime = 0.18f;
    [SerializeField] private bool toggleOnEveryTap = true;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource sfxSource; // 2D AudioSource on the Mailbox
    [SerializeField] private AudioClip wiggleClip;  // pen-click-2-411631
    [SerializeField] private AudioClip openClip;    // opening-door-411632
    [SerializeField] private AudioClip closeClip;   // pen-click-2-411631
    [SerializeField, Range(0f,1f)] private float sfxVolume = 1f;

    bool isOpen = false;
    bool busy = false;
    Quaternion lidClosedRot;

    void Awake()
    {
        if (!lidTransform) Debug.LogWarning("MailboxTap: lidTransform not assigned.");
        if (lidTransform) lidClosedRot = lidTransform.localRotation;
    }

    public void OnPointerClick(PointerEventData e)
    {
        // Only if THIS object (the Mailbox parent) was hit
        if (e.pointerCurrentRaycast.gameObject != gameObject) return;
        if (!busy) StartCoroutine(TapRoutine());
    }

    System.Collections.IEnumerator TapRoutine()
    {
        busy = true;

        // quick wiggle
        Play(wiggleClip);
        yield return StartCoroutine(Wiggle());

        // then open/close
        if (!isOpen)
        {
            Play(openClip);
            yield return StartCoroutine(SetOpen(true));
            if (!toggleOnEveryTap) { busy = false; yield break; }
        }
        else if (toggleOnEveryTap)
        {
            Play(closeClip);
            yield return StartCoroutine(SetOpen(false));
        }

        busy = false;
    }

    void Play(AudioClip clip)
    {
        if (!clip) return;

        EnsureSfxSource();
        if (sfxSource) sfxSource.PlayOneShot(clip, sfxVolume);
    }

    System.Collections.IEnumerator Wiggle()
    {
        float half = wiggleTime / (wiggleCycles * 2f);
        for (int i = 0; i < wiggleCycles; i++)
        {
            yield return RotateParent(+wiggleAngle, half);
            yield return RotateParent(-wiggleAngle * 2f, half * 2f);
            yield return RotateParent(+wiggleAngle, half);
        }
        // settle parent back to zero Z-rotation
        var e = transform.eulerAngles; e.z = 0f; transform.eulerAngles = e;
    }

    System.Collections.IEnumerator RotateParent(float deltaZ, float dur)
    {
        float t = 0f;
        float start = transform.eulerAngles.z;
        float end = start + deltaZ;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float z = Mathf.Lerp(start, end, t / dur);
            var e = transform.eulerAngles; e.z = z; transform.eulerAngles = e;
            yield return null;
        }
    }

    System.Collections.IEnumerator SetOpen(bool open)
    {
        isOpen = open;
        if (!lidTransform) yield break;

        Quaternion from = lidTransform.localRotation;
        Quaternion to = open ? Quaternion.Euler(0, 0, lidOpenAngle) : lidClosedRot;

        float t = 0f;
        while (t < lidOpenTime)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / lidOpenTime);
            lidTransform.localRotation = Quaternion.Slerp(from, to, u);
            yield return null;
        }
        lidTransform.localRotation = to;
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
