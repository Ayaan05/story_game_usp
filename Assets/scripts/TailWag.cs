using UnityEngine;

public class TailWag : MonoBehaviour
{
    [Header("Motion")]
    public float wagAmplitude = 15f;   // degrees
    public float baseSpeed    = 6f;    // idle wag
    public float happySpeed   = 12f;   // permanent speed after finish

    [Header("Boost logic")]
    public float boostSpeed         = 14f;  // speed while boosted
    public float holdAfterLastPop   = 2f;   // ⬅️ keep boost alive this long after the latest pop
    public float blendTimeToTarget  = 0.25f; // smoothing when switching speeds

    float t;
    float currentSpeed;
    float boostUntilTime = -1f;
    bool  lockedHappy;

    void Awake()
    {
        currentSpeed = baseSpeed;
    }

    void Update()
    {
        // pick target speed
        float target = baseSpeed;
        if (lockedHappy)                target = happySpeed;
        else if (Time.time <= boostUntilTime) target = boostSpeed;

        // smooth towards target
        if (blendTimeToTarget <= 0f) currentSpeed = target;
        else
        {
            float rate = Mathf.Abs(target - currentSpeed) / blendTimeToTarget;
            currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * Time.deltaTime);
        }

        // apply wag
        t += Time.deltaTime * currentSpeed;
        transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t) * wagAmplitude);
    }

    /// <summary>Call this on EVERY bubble pop. Extends the boost window.</summary>
    public void NotePop()
    {
        if (lockedHappy) return;
        boostUntilTime = Time.time + holdAfterLastPop;
    }

    /// <summary>Call when ALL bubbles are popped—stays fast forever.</summary>
    public void SetHappy()
    {
        lockedHappy = true;
        currentSpeed = happySpeed;
    }
}
