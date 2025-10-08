using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class ContinuousPourInteractive : MonoBehaviour
{
    [Header("Continuous Pouring Settings")]
    public Transform foodAnchor;                 // The target anchor (same as BoxPourSpawn)
    public GameObject continuousPourPrefab;      // Prefab to spawn continuously
    public float spawnInterval = 0.2f;           // Interval between spawns
    public Vector3 continuousPourOffset = Vector3.zero; // Base spawn offset

    [Header("Randomization")]
    [Tooltip("Min X value for random horizontal offset (e.g., -0.5).")]
    public float minRandomOffsetX = -0.1f;
    [Tooltip("Max X value for random horizontal offset (e.g., 0.5).")]
    public float maxRandomOffsetX = 0.1f;
    
    [Header("Pour Condition Reference")]
    [Tooltip("Absolute tilt required (degrees) to pour.")]
    public float pourTiltThresholdDeg = 100f;
    // Increased detection area to make it easier to trigger
    public Vector2 bowlAreaHalfExtents = new Vector2(2.0f, 2.0f); 
    
    // Internal references
    private BoxDragTilt2D tiltSource;
    private Coroutine continuousPourCoroutine = null; 

    void Awake()
    {
        // Assumes BoxDragTilt2D is on the same GameObject
        tiltSource = GetComponent<BoxDragTilt2D>();
    }

    void Update()
    {
        if (foodAnchor == null || continuousPourPrefab == null || tiltSource == null) return;

        // --- Calculate Conditions ---
        Vector2 center = foodAnchor.position;
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        
        // Calculate world position check based on box collider/transform
        Vector2 boxHalfSize = boxCollider.size / 2.0f;
        Vector2 localTopLeft = boxCollider.offset + new Vector2(-boxHalfSize.x, boxHalfSize.y);
        Vector2 worldTopLeftCorner = transform.TransformPoint(localTopLeft);

        Vector2 d = worldTopLeftCorner - center;
        bool aboveBowl = Mathf.Abs(d.x) <= bowlAreaHalfExtents.x && Mathf.Abs(d.y) <= bowlAreaHalfExtents.y;
        float leftTiltMagnitude = Mathf.Abs(tiltSource.CurrentTiltDeg);
        bool tiltedEnough = leftTiltMagnitude >= pourTiltThresholdDeg;

        // --- Manage Continuous Pour Coroutine ---
        if (aboveBowl && tiltedEnough)
        {
            // Condition met: Start the continuous pour if it's not already running
            if (continuousPourCoroutine == null)
            {
                continuousPourCoroutine = StartCoroutine(PouringLoop());
            }
        }
        else
        {
            // Condition NOT met: Stop the continuous pour if it IS running
            if (continuousPourCoroutine != null)
            {
                StopCoroutine(continuousPourCoroutine);
                continuousPourCoroutine = null;
            }
        }
    }

    private IEnumerator PouringLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(spawnInterval);
        
        while(true)
        {
            SpawnPrefab();
            yield return wait;
        }
    }

    private void SpawnPrefab()
    {
        // 1. Calculate the random horizontal offset
        float randomX = UnityEngine.Random.Range(minRandomOffsetX, maxRandomOffsetX);
        
        // 2. Apply the random offset to the base continuousPourOffset
        Vector3 finalOffset = continuousPourOffset + new Vector3(randomX, 0f, 0f);

        var spawnPos = (foodAnchor != null ? foodAnchor.position : transform.position) + finalOffset;
        var newFood = Instantiate(continuousPourPrefab, spawnPos, Quaternion.identity);
        
        if (foodAnchor != null)
        {
            // Parent the spawned item to the anchor
            newFood.transform.SetParent(foodAnchor, worldPositionStays: true);
        }
        
        // Correct destruction: The spawned prefab is destroyed after 0.2 seconds.
        Destroy(newFood, 0.3f);
    }
}
