using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;   // New Input System

[RequireComponent(typeof(Collider2D))]
public class SprinklerInteractive : MonoBehaviour
{
    [Header("Move (Left-drag on body) / Rotate (Left-drag near nozzle)")]
    [SerializeField] float rotateSpeed = 8f;
    [SerializeField] float minAngle = -0.34f;   // your “up” pose (Z)
    [SerializeField] float maxAngle = 26.93f;   // your “down” pose (Z)

    [Header("Movement bounds")]
    [SerializeField] float minX = -9f;
    [SerializeField] float maxX =  9f;
    [SerializeField] float minY = -2f;          // NEW: vertical limits
    [SerializeField] float maxY =  2f;

    [Header("Rotation hit test")]
    [Tooltip("World units radius around the nozzle that counts as ROTATE.")]
    [SerializeField] float rotateRadius = 0.7f;

    [Header("Art Alignment")]
    [Tooltip("Spout direction relative to the object's +X. Right=0, Up=90, Left=180, Down=-90.")]
    [SerializeField] float outAngleDeg = 180f;

    [Header("Drip Gate (must face DOWN)")]
    [Tooltip("How downward the spout must be before dripping (0..1). Lower triggers sooner.")]
    [SerializeField, Range(0f, 1f)] float minDownY = 0.6f;

    [Header("Water Drip (slow, instant first drop)")]
    [SerializeField] Transform nozzlePos;     // at mouth of spout
    [SerializeField] GameObject dropPrefab;
    [SerializeField] float dripInterval = 0.32f;
    [SerializeField] float launchSpeed = 0.6f;
    [SerializeField] float dropGravity = 0.7f;
    [SerializeField] float dropDrag = 0.7f;
    [SerializeField] bool instantFirstDrop = true;

    [Header("Stop At Grass")]
    [SerializeField] bool useGroundStop = true;
    [SerializeField] Transform groundMarker;  // empty placed on grass Y

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

        // PRESS: pick & decide mode based on proximity to nozzle
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            bool hitSelf = Physics2D.OverlapPointAll(mouseWorld, myLayerMask).Any(h => h == myCol);
            selected = hitSelf;

            if (selected)
            {
                mode = DragMode.Move;
                if (nozzlePos && Vector2.Distance(nozzlePos.position, mouseWorld) <= rotateRadius)
                    mode = DragMode.Rotate;
            }
        }

        // RELEASE
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            selected = false;
            mode = DragMode.None;
        }

        // DRAG
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
                // Move in BOTH X and Y, clamped to bounds
                float newX = Mathf.Clamp(mouseWorld.x, minX, maxX);
                float newY = Mathf.Clamp(mouseWorld.y, minY, maxY);
                transform.position = new Vector3(newX, newY, transform.position.z);
            }
        }

        // DRIP only when the spout faces strongly DOWN
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
