using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class HandBounce : MonoBehaviour
{
    [Header("Bounce")]
    public float amplitude = 0.2f;       // world units
    public float frequency = 2f;         // bounces per second

    [Header("Timing")]
    public float lifetimeSeconds = 3f;   // bounce duration before fade
    public float fadeTime = 0.2f;        // fade-out duration

    private Vector3 basePos;
    private float t;
    private float life;
    private SpriteRenderer sr;
    private bool fading;

    void Awake()
    {
        basePos = transform.position;
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // Bounce while alive or during fade
        t += Time.deltaTime;
        float y = basePos.y + Mathf.Sin(t * Mathf.PI * 2f * frequency) * amplitude;
        transform.position = new Vector3(basePos.x, y, basePos.z);

        // Lifetime countdown
        if (!fading)
        {
            life += Time.deltaTime;
            if (life >= lifetimeSeconds)
            {
                StartCoroutine(FadeThenDestroy());
            }
        }
    }

    private System.Collections.IEnumerator FadeThenDestroy()
    {
        fading = true;
        if (sr == null || fadeTime <= 0f)
        {
            Destroy(gameObject);
            yield break;
        }

        Color c0 = sr.color;
        float t0 = 0f;
        while (t0 < fadeTime)
        {
            t0 += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t0 / fadeTime);
            sr.color = new Color(c0.r, c0.g, c0.b, c0.a * k);
            yield return null;
        }
        Destroy(gameObject);
    }
}
