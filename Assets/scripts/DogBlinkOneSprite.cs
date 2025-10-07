using System.Collections;
using UnityEngine;

public class DogBlinkOneSprite : MonoBehaviour
{
    [Header("Eyes (single SR that shows both eyes open)")]
    public SpriteRenderer eyes;          // ← assign your one eyes sprite here

    [Header("Blink timing")]
    [Tooltip("How long the eyes turn OFF for each blink.")]
    public float closedTime = 0.10f;

    [Tooltip("Average time between blinks when idle (seconds).")]
    public float baseInterval = 7.0f;

    [Tooltip("± percent randomization of the interval (0.15 = ±15%).")]
    [Range(0f, 0.6f)] public float intervalJitter = 0.15f;

    [Header("Boost while popping")]
    [Tooltip("How much more frequently to blink during active popping (1.5 = 1.5x).")]
    public float frequencyBoostFactor = 1.5f;

    [Tooltip("How long after the latest pop to keep the boosted frequency (seconds).")]
    public float boostHoldSeconds = 2.0f;

    float lastPopTime = -999f;
    Coroutine loopCo;

    void Awake()
    {
        if (!eyes) Debug.LogWarning("DogBlinkOneSprite: assign the 'eyes' SpriteRenderer.");
        if (eyes) eyes.enabled = true; // eyes ON by default
    }

    void OnEnable()
    {
        Bubble.OnAnyPopped += OnAnyPop;
        if (loopCo != null) StopCoroutine(loopCo);
        loopCo = StartCoroutine(BlinkLoop());
    }

    void OnDisable()
    {
        Bubble.OnAnyPopped -= OnAnyPop;
        if (loopCo != null) StopCoroutine(loopCo);
        loopCo = null;
        if (eyes) eyes.enabled = true; // make sure we leave them open
    }

    void OnAnyPop(Bubble b)
    {
        lastPopTime = Time.time; // extend boost window
    }

    IEnumerator BlinkLoop()
    {
        while (true)
        {
            // pick interval
            bool boosted = (Time.time - lastPopTime) <= boostHoldSeconds;
            float interval = baseInterval;
            if (boosted && frequencyBoostFactor > 1f)
                interval /= frequencyBoostFactor; // more frequent = shorter interval

            // jitter it a bit
            float j = Mathf.Clamp01(intervalJitter);
            interval *= Random.Range(1f - j, 1f + j);

            yield return new WaitForSeconds(interval);

            // blink: eyes OFF briefly, then ON
            if (eyes)
            {
                eyes.enabled = false;
                yield return new WaitForSeconds(closedTime);
                eyes.enabled = true;
            }
        }
    }

    // Optional manual trigger
    public void BlinkNow()
    {
        if (gameObject.activeInHierarchy) StartCoroutine(BlinkOnce());
    }
    IEnumerator BlinkOnce()
    {
        if (!eyes) yield break;
        eyes.enabled = false;
        yield return new WaitForSeconds(closedTime);
        eyes.enabled = true;
    }

    // Back-compat no-op (safe if something else calls it)
    public void SetProgress01(float p) { /* not used in this version */ }
}
