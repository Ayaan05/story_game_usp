using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns big + small bubbles (circles) inside a BoxCollider2D area:
/// - Picks a random WORLD radius per instance within your ranges
/// - Computes localScale so CircleCollider2D's *world* radius == target radius
/// - Keeps circles fully inside the area (edgePadding)
/// - Guarantees no overlap: uses analytic distance check + Physics2D.OverlapCircle (Unity docs)
/// - Places big first, then small (better packing)
/// - Reports progress to DogCleanliness
/// </summary>
public class BubbleSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public Bubble smallPrefab;
    public Bubble bigPrefab;

    [Header("Counts")]
    public int smallCount = 10;
    public int bigCount   = 6;

    [Header("Exact WORLD radius ranges (units)")]
    public Vector2 smallRadiusRange = new Vector2(0.30f, 0.40f);
    public Vector2 bigRadiusRange   = new Vector2(0.55f, 0.70f);

    [Header("Spacing & Bounds")]
    [Tooltip("Extra gap between any two circles (world units).")]
    public float paddingBetweenBubbles = 0.05f;
    [Tooltip("Gap between circle edge and the spawn area edge (world units).")]
    public float edgePadding = 0.03f;
    public int   maxPlacementTries = 200;

    [Header("Area & Layers")]
    [Tooltip("Axis-aligned BoxCollider2D that defines the spawn region (Is Trigger = true).")]
    public BoxCollider2D spawnArea;
    [Tooltip("LayerMask used only for OverlapCircle tests (e.g., include the 'Bubble' layer).")]
    public LayerMask bubbleMask;

    [Header("Progress Hook (optional)")]
    public DogCleanliness dog;

    // Runtime bookkeeping for analytic non-overlap checks
    readonly List<Vector2> centers = new(); // circle centers (world)
    readonly List<float>   radii   = new(); // circle radii  (world)
    readonly List<Bubble>  live    = new();

    int totalToSpawn;
    int poppedSoFar;

    void OnEnable()  { Bubble.OnAnyPopped += HandleBubblePopped; }
    void OnDisable() { Bubble.OnAnyPopped -= HandleBubblePopped; }

    void Awake()
    {
        if (!spawnArea) spawnArea = GetComponent<BoxCollider2D>();
        if (!spawnArea) Debug.LogError("BubbleSpawner needs a BoxCollider2D assigned to 'spawnArea'.");
        if (transform.lossyScale != Vector3.one)
            Debug.LogWarning("BubbleSpawner has non-1 scale; prefer (1,1,1) so world radius math is exact.");
    }

    void Start() => RespawnInternal();

    void HandleBubblePopped(Bubble b)
    {
        poppedSoFar++;
        live.Remove(b);
        if (dog)
        {
            dog.ReportPopped(poppedSoFar);
            if (poppedSoFar >= totalToSpawn) dog.AllClean();
        }
    }

    [ContextMenu("Respawn Bubbles")]
    void RespawnMenu() => RespawnInternal();

    void RespawnInternal()
    {
        // Clear children
        foreach (Transform c in transform)
        {
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
        centers.Clear(); radii.Clear(); live.Clear();
        poppedSoFar = 0;

        // Pack big first, then small (better)
        PlaceBatch(bigPrefab, bigCount, bigRadiusRange);
        PlaceBatch(smallPrefab, smallCount, smallRadiusRange);

        totalToSpawn = live.Count;
        if (dog) { dog.SetTotal(totalToSpawn); dog.ReportPopped(0); }
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

        // Cached scales
        float basePrefabScale = Mathf.Max(prefab.transform.localScale.x, prefab.transform.localScale.y);
        float parentScale     = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        Bounds b              = spawnArea.bounds;

        for (int i = 0; i < count; i++)
        {
            bool placed = false;

            for (int tries = 0; tries < maxPlacementTries; tries++)
            {
                // 1) Choose a target WORLD radius within range
                float targetR = Random.Range(radiusRange.x, radiusRange.y);

                // 2) Compute local scale so collider's WORLD radius == targetR
                //    CircleCollider2D.radius is in local units. WORLD radius = radius * prefabScale * parentScale.
                float denom = prefabCol.radius * Mathf.Max(0.0001f, basePrefabScale) * Mathf.Max(0.0001f, parentScale);
                float S = targetR / denom;

                // 3) Account for collider offset to get the actual circle center
                Vector2 offsetWorld = prefabCol.offset * basePrefabScale * S * parentScale;

                // 4) Sample a random circle center INSIDE the spawn bounds, contracted by radius + edge padding
                float xmin = b.min.x + targetR + edgePadding;
                float xmax = b.max.x - targetR - edgePadding;
                float ymin = b.min.y + targetR + edgePadding;
                float ymax = b.max.y - targetR - edgePadding;

                if (xmax <= xmin || ymax <= ymin)
                {
                    Debug.LogWarning("Spawn area too small for requested radii + edgePadding.");
                    return;
                }

                // Sample the *circle center* first, then derive transform.position from it
                Vector2 circleCenter = new Vector2(Random.Range(xmin, xmax), Random.Range(ymin, ymax));
                Vector2 pos = circleCenter - offsetWorld; // transform.position we will use

                // 5) Analytic non-overlap vs placed circles: distance >= r_i + r_j + padding
                if (!IsNonOverlapping(circleCenter, targetR))
                    continue;

                // 6) Physics2D preflight to avoid any other colliders (Unity docs: Physics2D.OverlapCircle)
                //    Signature: OverlapCircle(Vector2 point, float radius, int layerMask);
                //    Returns the first Collider2D overlapping the circle area, or null.
                float physicsTestR = targetR + paddingBetweenBubbles * 0.5f;
                Collider2D hit = Physics2D.OverlapCircle(circleCenter, physicsTestR, bubbleMask);
                if (hit != null) continue; // something else is there; retry

                // 7) Instantiate at the chosen position with computed scale
                var inst = Instantiate(prefab, pos, Quaternion.identity, transform);
                inst.transform.localScale = prefab.transform.localScale * S;

                // Single-layer assignment (index), not a bitmask
                int bubbleIdx = LayerMask.NameToLayer("Bubble");
                if (bubbleIdx != -1) inst.gameObject.layer = bubbleIdx;

                // 8) Commit bookkeeping (now that it's valid)
                centers.Add(circleCenter);
                radii.Add(targetR);
                live.Add(inst);
                placed = true;
                break;
            }

            if (!placed)
            {
                Debug.LogWarning($"Could not place a {prefab.name} after {maxPlacementTries} tries. " +
                                 $"Reduce counts/radii/padding or enlarge spawn area.");
                // continue to try the rest, or break; we continue to place as many as possible
            }
        }
    }

    /// <summary>
    /// True if candidate circle at 'candidateCenter' with radius 'r' does not overlap any placed circle,
    /// using distance-squared test: d^2 >= (r_i + r_j + padding)^2
    /// </summary>
    bool IsNonOverlapping(Vector2 candidateCenter, float r)
    {
        for (int i = 0; i < centers.Count; i++)
        {
            float rr = r + radii[i] + paddingBetweenBubbles;
            if ((candidateCenter - centers[i]).sqrMagnitude < rr * rr)
                return false;
        }
        return true;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!spawnArea) return;
        Gizmos.color = new Color(0,1,1,0.12f);
        Gizmos.DrawCube(spawnArea.bounds.center, spawnArea.bounds.size);

        // Visualize placed circles in Play Mode
        Gizmos.color = new Color(1,1,1,0.45f);
        for (int i = 0; i < centers.Count; i++)
            Gizmos.DrawWireSphere(centers[i], radii[i]);
    }
#endif
}
