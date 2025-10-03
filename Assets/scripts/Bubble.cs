using System;
using System.Collections;
using UnityEngine;

public enum BubbleType { Small, Big }

public class Bubble : MonoBehaviour
{
    public static Action<Bubble> OnAnyPopped;

    [Header("Setup")]
    public BubbleType type = BubbleType.Small;
    public float popScale = 1.25f;
    public float popDuration = 0.12f;
    public AudioSource sfx;                 // optional

    CircleCollider2D col;
    Vector3 startScale;
    bool popped;

    void Awake()
    {
        col = GetComponent<CircleCollider2D>();
        startScale = transform.localScale;
    }

    void OnMouseDown()                      // works for mouse & touch (with collider)
    {
        if (!popped) StartCoroutine(PopCo());
    }

    IEnumerator PopCo()
    {
        popped = true;
        if (col) col.enabled = false;
        if (sfx) sfx.Play();

        // quick “pop” scale animation
        float t = 0f;
        while (t < popDuration)
        {
            t += Time.deltaTime;
            float k = t / popDuration;
            float s = Mathf.Lerp(1f, popScale, k);
            transform.localScale = startScale * s;
            yield return null;
        }

        OnAnyPopped?.Invoke(this);
        Destroy(gameObject);
    }

    public float GetRadiusWorld()
    {
        var r = col ? col.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) : 0.5f;
        return r;
    }
}
