using UnityEngine;

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
        Vector2 pos = transform.position;
        Vector2 d = pos - center;
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

        var spawnPos = (foodAnchor != null ? foodAnchor.position : transform.position) + spawnOffset;
        var go = Instantiate(foodPrefab, spawnPos, Quaternion.identity);
        if (foodAnchor != null) go.transform.SetParent(foodAnchor, worldPositionStays: true);

        if (disableBoxAfterPour)
        {
            var col = GetComponent<Collider2D>(); if (col) col.enabled = false;

            var rb = GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.linearVelocity = Vector2.zero;            // modern API
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;     // modern API
            }

            var drag = GetComponent<BoxDragTilt2D>(); if (drag) drag.enabled = false;
        }
    }
}
