using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PopFX : MonoBehaviour
{
    public float duration = 0.15f;
    public float startScale = 0.1f;
    public float endScale   = 0.3f;

    SpriteRenderer sr;
    float _mul = 1f;
    float _base = 1f; // computed to fit sprite to a target radius

    void Awake() { sr = GetComponent<SpriteRenderer>(); }

    // targetRadiusWorld: if > 0, we compute scale so sprite half-width == radius
    public void Play(Sprite sprite, float scaleMul = 1f, float targetRadiusWorld = -1f)
    {
        if (sprite) sr.sprite = sprite;

        // compute base scale from sprite bounds so size is resolution-agnostic
        _base = 1f;
        if (targetRadiusWorld > 0f && sr.sprite)
        {
            float halfWidthAtScale1 = sr.sprite.bounds.extents.x; // world units at scale 1
            if (halfWidthAtScale1 > 0f)
                _base = targetRadiusWorld / halfWidthAtScale1;
        }

        _mul = Mathf.Max(0.0001f, scaleMul);
        StartCoroutine(PlayCo());
    }

    IEnumerator PlayCo()
    {
        float t = 0f;
        var c = sr.color; c.a = 1f; sr.color = c;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);

            float s = Mathf.Lerp(startScale, endScale, k) * _base * _mul;
            transform.localScale = Vector3.one * s;

            c.a = 1f - k;
            sr.color = c;
            yield return null;
        }
        Destroy(gameObject);
    }
}
