using System.Collections.Generic;
using UnityEngine;

public class BubbleSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public Bubble smallPrefab;
    public Bubble bigPrefab;

    [Header("Counts")]
    public int smallCount = 10;
    public int bigCount   = 6;

    [Header("Scale Ranges (uniform)")]
    public Vector2 smallScaleRange = new Vector2(0.85f, 1.10f);
    public Vector2 bigScaleRange   = new Vector2(0.95f, 1.25f);

    [Header("Overlap Control")]
    public float padding = 0.05f;          // extra gap between bubbles (world units)
    public int maxPlacementTries = 100;

    [Header("Area & Layers")]
    public BoxCollider2D spawnArea;        // assign a Trigger box
    public LayerMask bubbleLayer;          // set to "Bubble" layer

    [Header("Progress Hook")]
    public DogCleanliness dog;

    int totalToSpawn;
    int poppedSoFar;
    readonly List<Bubble> live = new();

    void OnEnable()  { Bubble.OnAnyPopped += HandleBubblePopped; }
    void OnDisable() { Bubble.OnAnyPopped -= HandleBubblePopped; }

    void Start()
    {
        if (!spawnArea) spawnArea = GetComponent<BoxCollider2D>();
        RespawnInternal();
    }

    void HandleBubblePopped(Bubble b)
    {
        poppedSoFar++;
        live.Remove(b);
        if (dog)
        {
            dog.ReportPopped(poppedSoFar);
            // optional: if you also do local reveals
            // dog.RevealAt(b.transform.position, b.GetRadiusWorld());
            if (poppedSoFar >= totalToSpawn) dog.AllClean();
        }
    }

    void SpawnAll()
    {
        live.Clear();
        SpawnType(bigPrefab, bigCount, bigScaleRange);
        SpawnType(smallPrefab, smallCount, smallScaleRange);
    }

    void SpawnType(Bubble prefab, int count, Vector2 scaleRange)
    {
        for (int i = 0; i < count; i++)
            TrySpawn(prefab, scaleRange);
    }

    void TrySpawn(Bubble prefab, Vector2 scaleRange)
    {
        if (!prefab) return;
        var prefabCol = prefab.GetComponent<CircleCollider2D>();
        if (!prefabCol)
        {
            Debug.LogWarning("Bubble prefab missing CircleCollider2D.");
            return;
        }

        // base scale baked into the prefab (usually 1,1,1)
        float baseScale = Mathf.Max(prefab.transform.localScale.x, prefab.transform.localScale.y);
        int tries = 0;

        while (tries++ < maxPlacementTries)
        {
            // pick a random uniform scale for THIS attempt
            float s = Random.Range(scaleRange.x, scaleRange.y);
            float testRadius = prefabCol.radius * baseScale * s;

            Vector2 p = RandomPointIn(spawnArea.bounds);
            var hit = Physics2D.OverlapCircle(p, testRadius + padding, bubbleLayer);
            if (hit) continue;

            // place it
            var b = Instantiate(prefab, p, Quaternion.identity, transform);
            b.transform.localScale = prefab.transform.localScale * s; // apply chosen scale
            b.gameObject.layer = LayerMask.NameToLayer("Bubble");
            live.Add(b);
            return;
        }

        Debug.LogWarning($"Bubble placement failed after {maxPlacementTries} tries. " +
                         $"Consider enlarging area, reducing counts, shrinking colliders, or ranges.");
    }

    Vector2 RandomPointIn(Bounds b)
    {
        float x = Random.Range(b.min.x, b.max.x);
        float y = Random.Range(b.min.y, b.max.y);
        return new Vector2(x, y);
    }

    [ContextMenu("Respawn Bubbles")]
    void RespawnMenu() => RespawnInternal();

    void RespawnInternal()
    {
        foreach (Transform c in transform) DestroyImmediate(c.gameObject);
        poppedSoFar = 0;
        SpawnAll();
        totalToSpawn = live.Count;
        if (dog) { dog.SetTotal(totalToSpawn); dog.ReportPopped(0); }
    }
}
