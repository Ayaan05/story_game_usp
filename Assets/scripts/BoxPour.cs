using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class BoxPourSpawn : MonoBehaviour
{
    [Header("Spawn")]
    public Transform foodAnchor;                 // assign FoodAnchor
    public GameObject foodPrefab;                // assign Food prefab
    public Vector3 spawnOffset = Vector3.zero;   // optional fine-tune

    [Header("Pour condition")]
    [Tooltip("Absolute left tilt required (degrees) to pour; e.g., 100 means near upside-down left.")]
    public float pourTiltThresholdDeg = 100f;
    public Vector2 bowlAreaHalfExtents = new Vector2(0.8f, 0.4f);

    [Header("One-shot")]
    public bool oneShot = true;
    public bool disableBoxAfterPour = false;

    // Added fade-in prefabs and offsets
    [Header("Fade-in")]
    [Tooltip("The prefabs to fade in before the final foodPrefab.")]
    public GameObject[] fadePrefabs;
    public float fadeDuration = 0.5f;
    [Tooltip("Offsets for each fade prefab. Should match the size of fadePrefabs.")]
    public Vector3[] fadeOffsets;

    private bool poured;
    private BoxDragTilt2D tiltSource;

    void Awake()
    {
        tiltSource = GetComponent<BoxDragTilt2D>();
    }

    void Update()
    {
        if (poured && oneShot) return;
        if (foodAnchor == null || foodPrefab == null || tiltSource == null) return;

        // Position over bowl
        Vector2 center = foodAnchor.position;
        // Get the collider's half-size
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        Vector2 boxHalfSize = boxCollider.size / 2.0f;
        
        // Calculate the local position of the top-left corner
        // The collider's offset is also factored in here
        Vector2 localTopLeft = boxCollider.offset + new Vector2(-boxHalfSize.x, boxHalfSize.y);
        
        // Convert the local position to a world-space position
        Vector2 worldTopLeftCorner = transform.TransformPoint(localTopLeft);

        Vector2 d = worldTopLeftCorner - center;
        bool aboveBowl = Mathf.Abs(d.x) <= bowlAreaHalfExtents.x && Mathf.Abs(d.y) <= bowlAreaHalfExtents.y;

        // Left-only tilt magnitude: CurrentTiltDeg is <= 0 by design
        float leftTiltMagnitude = Mathf.Abs(tiltSource.CurrentTiltDeg);
        bool tiltedEnough = leftTiltMagnitude >= pourTiltThresholdDeg;

        if (aboveBowl && tiltedEnough)
            DoPour();
    }

    private void DoPour()
    {
        if (poured) return;
        poured = true;
        
        StartCoroutine(PourWithFade());

        if (disableBoxAfterPour)
        {
            var col = GetComponent<Collider2D>(); if (col) col.enabled = false;

            var rb = GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }

            var drag = GetComponent<BoxDragTilt2D>(); if (drag) drag.enabled = false;
        }
    }

    private IEnumerator PourWithFade()
    {
        var baseSpawnPos = (foodAnchor != null ? foodAnchor.position : transform.position) + spawnOffset;
        
        if (fadePrefabs != null && fadePrefabs.Length > 0)
        {
            // Step 1: Fade in all fade prefabs
            for (int i = 0; i < fadePrefabs.Length; i++)
            {
                Vector3 currentOffset = (fadeOffsets != null && i < fadeOffsets.Length) ? fadeOffsets[i] : Vector3.zero;
                Vector3 spawnPos = baseSpawnPos + currentOffset;

                var fadeGo = Instantiate(fadePrefabs[i], spawnPos, Quaternion.identity);
                if (foodAnchor != null) fadeGo.transform.SetParent(foodAnchor, worldPositionStays: true);

                SpriteRenderer spriteRenderer = fadeGo.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    // Fade in effect
                    float timer = 0f;
                    Color startColor = spriteRenderer.color;
                    startColor.a = 0f;
                    Color targetColor = startColor;
                    targetColor.a = 1f;

                    while (timer < fadeDuration)
                    {
                        timer += Time.deltaTime;
                        spriteRenderer.color = Color.Lerp(startColor, targetColor, timer / fadeDuration);
                        yield return null;
                    }
                    spriteRenderer.color = targetColor;
                }
            }
        }
        
        // Step 2: Fade in the final foodPrefab
        var finalGo = Instantiate(foodPrefab, baseSpawnPos, Quaternion.identity);
        if (foodAnchor != null) finalGo.transform.SetParent(foodAnchor, worldPositionStays: true);

        SpriteRenderer finalSpriteRenderer = finalGo.GetComponent<SpriteRenderer>();
        if (finalSpriteRenderer != null)
        {
            // Fade in effect for the final prefab
            float timer = 0f;
            Color startColor = finalSpriteRenderer.color;
            startColor.a = 0f;
            Color targetColor = startColor;
            targetColor.a = 1f;

            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                finalSpriteRenderer.color = Color.Lerp(startColor, targetColor, timer / fadeDuration);
                yield return null;
            }
            finalSpriteRenderer.color = targetColor;
        }
    }
}