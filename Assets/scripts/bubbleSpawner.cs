using System.Collections.Generic;
using UnityEngine;

public class BubbleSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public Bubble smallPrefab;
    public Bubble bigPrefab;

    // --- EXACT VALUES YOU ASKED FOR ---
    [Header("Counts (INTENDED totals)")]
    public int smallCount = 14;  // ← exact
    public int bigCount   = 10;  // ← exact

    [Header("Reserve one BIG (for your hand/tutorial bubble)")]
    [Tooltip("We spawn one less BIG than 'bigCount', but still count full (small+big).")]
    public bool reserveOneBig = true;

    [Header("Exact WORLD radius ranges (units)")]
    public Vector2 smallRadiusRange = new Vector2(0.3f, 1.3f);  // ← exact
    public Vector2 bigRadiusRange   = new Vector2(1.3f, 2.5f);  // ← exact

    [Header("Spacing & Bounds")]
    public float paddingBetweenBubbles = 0.05f; // ← exact
    public float edgePadding           = 0.03f; // ← exact
    public int   maxPlacementTries     = 100;   // ← exact

    [Header("Spawn Area (choose ONE)")]
    [Tooltip("Preferred: PolygonCollider2D (IsTrigger = true).")]
    public PolygonCollider2D spawnPolygon;
    [Tooltip("Fallback: BoxCollider2D (IsTrigger = true).")]
    public BoxCollider2D     spawnBox;

    [Header("Layers")]
    [Tooltip("LayerMask used by OverlapCircle safety checks (must include 'Bubble' layer).")]
    public LayerMask bubbleMask;

    [Header("Progress Hook (optional)")]
    public DogCleanliness dog;

    // runtime
    readonly List<Vector2> centers = new();
    readonly List<float>   radii   = new();
    readonly List<Bubble>  live    = new();

    int intendedTotal;   // what DogCleanliness should expect (small+big)
    int poppedSoFar;

    void OnEnable()  { Bubble.OnAnyPopped += HandleBubblePopped; }
    void OnDisable() { Bubble.OnAnyPopped -= HandleBubblePopped; }

    void Awake()
    {
        if (!spawnPolygon && !spawnBox)
        {
            spawnPolygon = GetComponent<PolygonCollider2D>();
            if (!spawnPolygon) spawnBox = GetComponent<BoxCollider2D>();
        }
        if (!spawnPolygon && !spawnBox)
            Debug.LogError("BubbleSpawner: assign a PolygonCollider2D or BoxCollider2D as the spawn area (IsTrigger = true).");

        if (transform.lossyScale != Vector3.one)
            Debug.LogWarning("BubbleSpawner: parent has non-1 scale; world-radius math assumes (1,1,1).");
    }

    void Start() => RespawnInternal();

    void HandleBubblePopped(Bubble b)
    {
        poppedSoFar++;
        live.Remove(b); // OK if this bubble wasn't spawned by us (e.g., your manual one)
        if (dog)
        {
            dog.ReportPopped(poppedSoFar);
            if (poppedSoFar >= intendedTotal) dog.AllClean();
        }
    }

    [ContextMenu("Respawn Bubbles")]
    void RespawnMenu() => RespawnInternal();

    void RespawnInternal()
    {
        // clear existing children
        foreach (Transform c in transform)
        {
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
        centers.Clear(); radii.Clear(); live.Clear();
        poppedSoFar = 0;

        // what DogCleanliness should expect (FULL counts)
        intendedTotal = Mathf.Max(0, smallCount) + Mathf.Max(0, bigCount);
        if (dog) { dog.SetTotal(intendedTotal); dog.ReportPopped(0); }

        // what we actually spawn:
        int bigToSpawn   = Mathf.Max(0, bigCount - (reserveOneBig ? 1 : 0)); // ← one less big
        int smallToSpawn = Mathf.Max(0, smallCount);

        // place big first, then small
        PlaceBatch(bigPrefab,   bigToSpawn,   bigRadiusRange);
        PlaceBatch(smallPrefab, smallToSpawn, smallRadiusRange);
    }

    void PlaceBatch(Bubble prefab, int count, Vector2 radiusRange)
    {
        if (!prefab || count <= 0) return;

        var prefabCol = prefab.GetComponent<CircleCollider2D>();
        if (!prefabCol)
        {
            Debug.LogError($"Prefab {prefab.name} needs a CircleCollider2D.");
            return;
        }

        float basePrefabScale = Mathf.Max(prefab.transform.localScale.x, prefab.transform.localScale.y);
        float parentScale     = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);

        for (int i = 0; i < count; i++)
        {
            bool placed = false;

            for (int tries = 0; tries < maxPlacementTries; tries++)
            {
                // pick a target world radius
                float targetR = Random.Range(radiusRange.x, radiusRange.y);

                // compute local scale so collider's WORLD radius == targetR
                float denom = prefabCol.radius * Mathf.Max(0.0001f, basePrefabScale) * Mathf.Max(0.0001f, parentScale);
                float S = targetR / denom;

                // collider center offset in world
                Vector2 offsetWorld = prefabCol.offset * basePrefabScale * S * parentScale;

                // sample a circle center fully inside area (with edge padding)
                if (!TryGetRandomCenter(targetR, out var center))
                    break;

                // analytic non-overlap vs already placed
                if (!IsNonOverlapping(center, targetR)) continue;

                // safety: modern Physics2D.OverlapCircle
                float testR = targetR + paddingBetweenBubbles * 0.5f;
                if (Physics2D.OverlapCircle(center, testR, bubbleMask) != null) continue;

                // center -> transform.position
                Vector2 pos = center - offsetWorld;

                var inst = Instantiate(prefab, pos, Quaternion.identity, transform);
                inst.transform.localScale = prefab.transform.localScale * S;

                int bubbleIdx = LayerMask.NameToLayer("Bubble");
                if (bubbleIdx != -1) inst.gameObject.layer = bubbleIdx;

                centers.Add(center);
                radii.Add(targetR);
                live.Add(inst);
                placed = true;
                break;
            }

            if (!placed)
            {
                Debug.LogWarning($"Could not place a {prefab.name} after {maxPlacementTries} tries. Shrink radii/padding or enlarge area.");
                // continue trying remaining ones
            }
        }
    }

    bool TryGetRandomCenter(float r, out Vector2 center)
    {
        // polygon first
        if (spawnPolygon)
        {
            var b = spawnPolygon.bounds;
            for (int i = 0; i < maxPlacementTries; i++)
            {
                var p = new Vector2(Random.Range(b.min.x, b.max.x),
                                    Random.Range(b.min.y, b.max.y));
                if (!spawnPolygon.OverlapPoint(p)) continue;
                if (!InsidePolygonWithMargin(spawnPolygon, p, r + edgePadding)) continue;
                center = p;
                return true;
            }
            center = default;
            return false;
        }

        // box fallback
        if (spawnBox)
        {
            Bounds b = spawnBox.bounds;
            float xmin = b.min.x + r + edgePadding;
            float xmax = b.max.x - r - edgePadding;
            float ymin = b.min.y + r + edgePadding;
            float ymax = b.max.y - r - edgePadding;
            if (xmax <= xmin || ymax <= ymin) { center = default; return false; }

            center = new Vector2(Random.Range(xmin, xmax), Random.Range(ymin, ymax));
            return true;
        }

        center = default;
        return false;
    }

    // inside polygon AND at least 'margin' from edges
    bool InsidePolygonWithMargin(PolygonCollider2D poly, Vector2 worldP, float margin)
    {
        if (!poly.OverlapPoint(worldP)) return false;

        float minDist = float.PositiveInfinity;
        for (int path = 0; path < poly.pathCount; path++)
        {
            var pts = poly.GetPath(path); // local
            if (pts == null || pts.Length < 2) continue;

            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 a = poly.transform.TransformPoint(pts[i]);
                Vector2 b = poly.transform.TransformPoint(pts[(i + 1) % pts.Length]);
                float d = DistancePointSegment(worldP, a, b);
                if (d < minDist) minDist = d;
                if (minDist < margin) return false; // early out
            }
        }
        return minDist >= margin;
    }

    static float DistancePointSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude);
        t = Mathf.Clamp01(t);
        return Vector2.Distance(p, a + t * ab);
    }

    bool IsNonOverlapping(Vector2 c, float r)
    {
        for (int i = 0; i < centers.Count; i++)
        {
            float rr = r + radii[i] + paddingBetweenBubbles;
            if ((c - centers[i]).sqrMagnitude < rr * rr) return false;
        }
        return true;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0,1,1,0.10f);
        if (spawnPolygon) Gizmos.DrawCube(spawnPolygon.bounds.center, spawnPolygon.bounds.size);
        else if (spawnBox) Gizmos.DrawCube(spawnBox.bounds.center, spawnBox.bounds.size);

        Gizmos.color = new Color(1,1,1,0.45f);
        for (int i = 0; i < centers.Count; i++) Gizmos.DrawWireSphere(centers[i], radii[i]);
    }
#endif

    // quick button to reapply your exact preset values if the Inspector got changed
    [ContextMenu("Apply EXACT Preset Values")]
    void ApplyPreset()
    {
        smallCount = 14;
        bigCount   = 10;
        smallRadiusRange = new Vector2(0.3f, 1.3f);
        bigRadiusRange   = new Vector2(1.3f, 2.5f);
        paddingBetweenBubbles = 0.05f;
        edgePadding = 0.03f;
        maxPlacementTries = 100;
        reserveOneBig = true;
    }
}
