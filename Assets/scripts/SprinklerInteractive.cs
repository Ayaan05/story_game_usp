using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;   // New Input System

[RequireComponent(typeof(Collider2D))]
public class SprinklerInteractive : MonoBehaviour
{
    [Header("Move (Left-drag on body) / Rotate (Left-drag near nozzle)")]
    [SerializeField] float rotateSpeed = 8f;
    [SerializeField] float minAngle = -0.34f;   // your “up” pose
    [SerializeField] float maxAngle = 26.93f;   // your “down” pose
    [SerializeField] float minX = -20f;         // <<< set your lateral bounds here
    [SerializeField] float maxX =  20f;

    [Header("Rotation hit test")]
    [Tooltip("Click distance (in world units) from nozzle to treat a drag as ROTATE.")]
    [SerializeField] float rotateRadius = 0.7f;

    [Header("Art Alignment")]
    [Tooltip("Spout direction relative to the object's +X. Right=0, Up=90, Left=180, Down=-90.")]
    [SerializeField] float outAngleDeg = 180f;

    [Header("Drip Gate (must face DOWN)")]
    [Tooltip("How downward the spout must be before dripping (0..1). Lower starts sooner.")]
    [SerializeField, Range(0f, 1f)] float minDownY = 0.6f;

    [Header("Water Drip (slow, instant first drop)")]
    [SerializeField] Transform nozzlePos;     // position at mouth
    [SerializeField] GameObject dropPrefab;   // water_drops prefab
    [SerializeField] float dripInterval = 0.32f;
    [SerializeField] float launchSpeed = 0.6f;
    [SerializeField] float dropGravity = 0.7f;
    [SerializeField] float dropDrag = 0.7f;
    [SerializeField] bool instantFirstDrop = true;

    [Header("Stop At Grass")]
    [SerializeField] bool useGroundStop = true;
    [SerializeField] Transform groundMarker;  // empty at grass Y

    enum DragMode { None, Move, Rotate }
    DragMode mode = DragMode.None;

    bool selected;
    Camera cam;
    Collider2D myCol;
    int myLayerMask;
    Coroutine dripCo;

    void Awake()
    {
        cam = Camera.main;
        myCol = GetComponent<Collider2D>();
        myLayerMask = 1 << gameObject.layer;
        if (!cam) Debug.LogWarning("SprinklerInteractive: Tag your camera 'MainCamera'.");
    }

    void Update()
    {
        if (cam == null || Mouse.current == null) return;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseWorld.z = 0f;

        // --- press: pick object and decide mode (rotate vs move) ---
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            selected = Physics2D.OverlapPointAll(mouseWorld, myLayerMask).Any(h => h == myCol);
            if (selected)
            {
                mode = DragMode.Move;
                if (nozzlePos && Vector2.Distance(nozzlePos.position, mouseWorld) <= rotateRadius)
                    mode = DragMode.Rotate;
            }
        }

        // --- release: clear mode/selection ---
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            selected = false;
            mode = DragMode.None;
        }

        // --- drag behavior ---
        if (selected && Mouse.current.leftButton.isPressed)
        {
            if (mode == DragMode.Rotate)
            {
                Vector2 dir = mouseWorld - transform.position;
                float targetZ = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                float z = Mathf.LerpAngle(transform.eulerAngles.z, targetZ, Time.deltaTime * rotateSpeed);
                z = ClampAngle(z, minAngle, maxAngle);
                transform.rotation = Quaternion.Euler(0f, 0f, z);
            }
            else if (mode == DragMode.Move)
            {
                float newX = Mathf.Clamp(mouseWorld.x, minX, maxX);
                transform.position = new Vector3(newX, transform.position.y, transform.position.z);
            }
        }

        // --- drip only when spout faces strongly DOWN ---
        Vector2 outDir = GetOutDirection();
        bool pointingDown = outDir.y <= -minDownY;
        if (pointingDown)
        {
            if (dripCo == null)
            {
                if (instantFirstDrop) SpawnDrop();
                dripCo = StartCoroutine(DripLoop());
            }
        }
        else if (dripCo != null)
        {
            StopCoroutine(dripCo);
            dripCo = null;
        }
    }

    IEnumerator DripLoop()
    {
        var wait = new WaitForSeconds(dripInterval);
        while (true) { SpawnDrop(); yield return wait; }
    }

    void SpawnDrop()
    {
        if (!dropPrefab || !nozzlePos) return;

        GameObject drop = Instantiate(dropPrefab, nozzlePos.position, Quaternion.identity);
        if (drop.TryGetComponent<Rigidbody2D>(out var rb))
        {
            Vector2 dir = (GetOutDirection() + Vector2.down * 0.12f).normalized;
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = dropGravity;
            rb.linearDamping = dropDrag;
            rb.AddForce(dir * launchSpeed, ForceMode2D.Impulse);
        }

        if (useGroundStop && groundMarker && drop.TryGetComponent<WaterDrop>(out var wd))
            wd.Init(groundMarker.position.y);
    }

    // ---------- helpers ----------
    Vector2 GetOutDirection()
    {
        Vector2 baseRight = transform.right; // object's +X in world
        Quaternion offset = Quaternion.Euler(0, 0, outAngleDeg);
        return ((Vector2)(offset * baseRight)).normalized;
    }

    static float ClampAngle(float z, float min, float max)
    {
        z = Mathf.Repeat(z + 180f, 360f) - 180f;
        return Mathf.Clamp(z, min, max);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (nozzlePos)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(nozzlePos.position, nozzlePos.position + (Vector3)GetOutDirection() * 0.6f);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
            Gizmos.DrawWireSphere(nozzlePos.position, rotateRadius); // rotate zone
        }
        if (useGroundStop && groundMarker)
        {
            Gizmos.color = new Color(0, 1, 0, 0.35f);
            Gizmos.DrawLine(new Vector3(-1000, groundMarker.position.y, 0),
                            new Vector3( 1000, groundMarker.position.y, 0));
        }
    }
#endif
}
