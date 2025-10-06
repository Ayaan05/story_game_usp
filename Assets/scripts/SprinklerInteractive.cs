using UnityEngine;
using UnityEngine.InputSystem;   // New Input System

[RequireComponent(typeof(Collider2D))]
public class SprinklerInteractive : MonoBehaviour
{
    [Header("Dragging")]
    [SerializeField] float moveLerp = 18f;

    [Header("Clamp")]
    [SerializeField] bool clampToCamera = true;
    [SerializeField] float screenPadding = 0.2f;

    [Header("Tilt")]
    [SerializeField] float restAngleZ = 0f;
    [SerializeField] float maxTilt = 25f;
    [SerializeField] float tiltPerUnitSpeed = 10f;
    [SerializeField] float tiltLerp = 14f;
    [SerializeField] float returnLerp = 6f;

    [Header("Spray")]
    [SerializeField] Transform nozzle;              // spout tip; +X should face out
    [SerializeField] GameObject dropletsPrefab;     // ParticleSystem OR RB2D drop prefab
    [SerializeField] float spraySpeedThreshold = 0.25f;
    [SerializeField] float dropsPerSecond = 20f;    // RB2D spawn rate
    [SerializeField] float dropletSpeed = 6f;       // RB2D initial speed
    [SerializeField] float dropLifetime = 2.5f;     // fallback auto-destroy
    [SerializeField] float spread = 12f;            // RB2D angle jitter (deg)
    [Tooltip("Scene object at the grass height. Assign your Grass Line here.")]
    [SerializeField] Transform grassKillLine;       // passed to WaterDrop

    [Header("Droplet Appearance")]
    [SerializeField] float dropletScale = 1.6f;     // RB2D prefab size multiplier
    [SerializeField] float particleSizeMult = 1.6f; // Particle System Start Size multiplier

    [Header("Safety")]
    [SerializeField] float safeZ = 0f;

    // Optional fixed numeric bounds if clampToCamera=false
    [SerializeField] Vector2 clampMin = new Vector2(-9f, -2f);
    [SerializeField] Vector2 clampMax = new Vector2( 9f,  2f);

    bool dragging;
    Vector3 grabOffset;
    Vector3 targetPos, lastPos, vel;
    float dropTimer;
    ParticleSystem ps;
    SpriteRenderer sr;
    Collider2D col;

    void OnEnable()
    {
        sr  = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        var p = transform.position; p.z = safeZ; transform.position = p;
        targetPos = ClampToBounds(transform.position);
        lastPos = transform.position;

        // If dropletsPrefab is a ParticleSystem, instantiate once under the nozzle
        if (dropletsPrefab && nozzle)
        {
            var maybePS = dropletsPrefab.GetComponent<ParticleSystem>();
            if (maybePS)
            {
                var go = Instantiate(dropletsPrefab, nozzle.position, nozzle.rotation, nozzle);
                ps = go.GetComponent<ParticleSystem>();

                // ► Make particles bigger
                var main = ps.main;
                main.startSizeMultiplier *= particleSizeMult;

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    void Update()
    {
        HandlePointerNewInput();

        // Move and clamp
        Vector3 pos = transform.position;
        pos = Vector3.Lerp(pos, targetPos, 1f - Mathf.Exp(-moveLerp * Time.deltaTime));
        transform.position = ClampToBounds(pos);

        // Velocity estimate
        Vector3 newVel = (transform.position - lastPos) / Mathf.Max(Time.deltaTime, 1e-5f);
        vel = Vector3.Lerp(vel, newVel, 0.5f);
        lastPos = transform.position;

        // Tilt
        float speed = new Vector2(vel.x, vel.y).magnitude;
        float desiredDelta = Mathf.Clamp(-vel.x * tiltPerUnitSpeed, -maxTilt, maxTilt);
        float desiredAngle = (dragging && speed > 0.01f) ? restAngleZ + desiredDelta : restAngleZ;
        float lerp = (dragging && speed > 0.01f) ? tiltLerp : returnLerp;
        float z = Mathf.LerpAngle(transform.eulerAngles.z, desiredAngle, 1f - Mathf.Exp(-lerp * Time.deltaTime));
        transform.rotation = Quaternion.Euler(0, 0, z);

        // Spray
        bool shouldSpray = speed >= spraySpeedThreshold && nozzle && dropletsPrefab;
        HandleSpray(shouldSpray);
    }

    // ---------- Input (New Input System) ----------
    void HandlePointerNewInput()
    {
        var cam = Camera.main; if (!cam) return;

        Vector2 screenPos = Vector2.zero;
        bool down = false, held = false, up = false;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            var t = Touchscreen.current.primaryTouch;
            screenPos = t.position.ReadValue();
            down = t.press.wasPressedThisFrame;
            held = t.press.isPressed;
            up   = t.press.wasReleasedThisFrame;
        }
        else if (Mouse.current != null)
        {
            screenPos = Mouse.current.position.ReadValue();
            down = Mouse.current.leftButton.wasPressedThisFrame;
            held = Mouse.current.leftButton.isPressed;
            up   = Mouse.current.leftButton.wasReleasedThisFrame;
        }
        else if (Pointer.current != null)
        {
            screenPos = Pointer.current.position.ReadValue();
            down = Pointer.current.press.wasPressedThisFrame;
            held = Pointer.current.press.isPressed;
            up   = Pointer.current.press.wasReleasedThisFrame;
        }

        float zDist = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector3 pw = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDist));
        pw.z = safeZ;

        if (down)
        {
            var hit = Physics2D.Raycast(pw, Vector2.zero, 0f);
            if (hit.collider && hit.collider == col)
            {
                dragging = true;
                grabOffset = transform.position - pw;
                targetPos = ClampToBounds(transform.position);
            }
        }
        if (up) dragging = false;

        if (dragging && held)
        {
            targetPos = ClampToBounds(pw + grabOffset);
        }
    }

    // ---------- Clamp (camera and sprite aware) ----------
    Vector3 ClampToBounds(Vector3 p)
    {
        p.z = safeZ;

        float minX, maxX, minY, maxY;
        if (clampToCamera && Camera.main)
        {
            var cam = Camera.main;
            float vert = cam.orthographicSize;
            float horz = vert * cam.aspect;
            Vector3 c = cam.transform.position;

            Vector2 half = Vector2.zero;
            if (sr) { var e = sr.bounds.extents; half = new Vector2(e.x, e.y); }

            minX = c.x - horz + screenPadding + half.x;
            maxX = c.x + horz - screenPadding - half.x;
            minY = c.y - vert + screenPadding + half.y;
            maxY = c.y + vert - screenPadding - half.y;
        }
        else
        {
            minX = clampMin.x; maxX = clampMax.x;
            minY = clampMin.y; maxY = clampMax.y;
        }

        p.x = Mathf.Clamp(p.x, minX, maxX);
        p.y = Mathf.Clamp(p.y, minY, maxY);
        return p;
    }

    // ---------- Spray ----------
    void HandleSpray(bool on)
    {
        // ParticleSystem path
        if (ps)
        {
            if (on && !ps.isPlaying) ps.Play();
            else if (!on && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            return;
        }

        // Rigidbody2D drop spawner path
        if (!on) { dropTimer = 0f; return; }

        dropTimer += Time.deltaTime * dropsPerSecond;
        while (dropTimer >= 1f)
        {
            dropTimer -= 1f;

            Vector2 dir = nozzle ? (Vector2)nozzle.right : Vector2.right;
            float jitter = Random.Range(-spread, spread);
            dir = Quaternion.Euler(0, 0, jitter) * dir;

            var drop = Instantiate(dropletsPrefab, nozzle.position, Quaternion.identity);

            // ► make the drop bigger
            drop.transform.localScale *= dropletScale;

            // pass the Grass Line into the drop so it can self-kill by Y
            var wd = drop.GetComponent<WaterDrop>();
            if (wd != null && grassKillLine != null)
                wd.SetKillLine(grassKillLine, 0f, true);

            var rb = drop.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = dir.normalized * dropletSpeed + (Vector2)vel * 0.25f;

            if (dropLifetime > 0f) Destroy(drop, dropLifetime);
        }
    }
}
