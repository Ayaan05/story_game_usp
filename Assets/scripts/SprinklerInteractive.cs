using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;   // New Input System

[RequireComponent(typeof(Collider2D))]
public class SprinklerInteractive : MonoBehaviour
{
    [Header("Move (Left-drag anywhere)")]
    [SerializeField] float rotateSpeed = 8f;
    [SerializeField] float minAngle = -0.34f;   // your “up” pose (Z)
    [SerializeField] float maxAngle = 26.93f;   // your “down” pose (Z)

    [Header("Movement bounds")]
    [SerializeField] float minX = -9f;
    [SerializeField] float maxX =  9f;
    [SerializeField] float minY = -2f;          // NEW: vertical limits
    [SerializeField] float maxY =  2f;

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

    [Header("Drop Audio")]
    [SerializeField] AudioSource dropSfxSource;
    [SerializeField] AudioClip dropClip;
    [Range(0f, 1f)]
    [SerializeField] float dropSfxVolume = 1f;

    [Header("Stop At Grass")]
    [SerializeField] bool useGroundStop = true;
    [SerializeField] Transform groundMarker;  // empty placed on grass Y

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
                // pointer stays captured so rotation can follow drag immediately
            }
        }

        // RELEASE
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            selected = false;
        }

        // DRAG
        if (selected && Mouse.current.leftButton.isPressed)
        {
            // Move in BOTH X and Y, clamped to bounds
            float newX = Mathf.Clamp(mouseWorld.x, minX, maxX);
            float newY = Mathf.Clamp(mouseWorld.y, minY, maxY);
            Vector3 newPos = new Vector3(newX, newY, transform.position.z);

            Vector2 dragDelta = newPos - transform.position;

            transform.position = newPos;

            // Rotate based on drag direction when significant movement occurs
            if (dragDelta.sqrMagnitude > 0.0001f)
            {
                Vector2 dir = dragDelta.normalized;
                float targetZ = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                float z = Mathf.LerpAngle(transform.eulerAngles.z, targetZ, rotateSpeed * Time.deltaTime);
                z = ClampAngle(z, minAngle, maxAngle);
                transform.rotation = Quaternion.Euler(0f, 0f, z);
            }
            else if ((mouseWorld - transform.position).sqrMagnitude > 0.0001f)
            {
                Vector2 dir = (mouseWorld - transform.position).normalized;
                float targetZ = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                float z = Mathf.LerpAngle(transform.eulerAngles.z, targetZ, rotateSpeed * Time.deltaTime);
                z = ClampAngle(z, minAngle, maxAngle);
                transform.rotation = Quaternion.Euler(0f, 0f, z);
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

        PlayDropSfx();

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

    void PlayDropSfx()
    {
        if (dropClip == null) return;

        if (dropSfxSource == null)
        {
            var sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (var s in sources)
            {
                if (!s.loop)
                {
                    dropSfxSource = s;
                    break;
                }
            }
            if (dropSfxSource == null && sources.Length > 0)
            {
                dropSfxSource = sources[0];
            }
        }

        if (dropSfxSource != null)
        {
            dropSfxSource.PlayOneShot(dropClip, dropSfxVolume);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (nozzlePos)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(nozzlePos.position, nozzlePos.position + (Vector3)GetOutDirection() * 0.6f);
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
