using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class SprinklerInteractive : MonoBehaviour
{
    public enum SprayMode { Forward, VerticalDown }

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
    [SerializeField] bool tiltAlwaysForward = true;

    [Header("Spray")]
    [SerializeField] Transform nozzle;
    [SerializeField] GameObject dropletsPrefab;    // ParticleSystem or RB2D prefab
    [SerializeField] SprayMode mode = SprayMode.VerticalDown; // << choose here
    [SerializeField] float spraySpeedThreshold = 0.25f;

    // RB2D spawning
    [SerializeField] float dropsPerSecond = 10f;    // slower stream
    [SerializeField] float dropletSpeed   = 6f;     // initial speed
    [SerializeField] float dropLifetime   = 2.5f;
    [SerializeField] float spread         = 0f;     // 0 for vertical stream
    [SerializeField] Transform grassKillLine;

    [Header("Droplet Appearance")]
    [SerializeField] float dropletScale = 1.8f;     // RB2D size multiplier
    [SerializeField] float particleSizeMult = 1.8f; // ParticleSystem size multiplier
    [SerializeField] float particleRate = 8f;       // ParticleSystem rate

    [Header("Safety")]
    [SerializeField] float safeZ = 0f;

    [SerializeField] Vector2 clampMin = new Vector2(-9f, -2f);
    [SerializeField] Vector2 clampMax = new Vector2( 9f,  2f);
    [Header("Spray FX Tuning")]
    [SerializeField] bool flipNozzleKick = false;   // tick in Inspector if the spit goes backward

    
    [Header("Projectile Spray")]
    [SerializeField] float launchSpeed = 7.5f;     // how far it throws
    [SerializeField] float gravityAccel = 9.0f;    // how fast it drops
    [SerializeField] bool  projectileToLeft = true;// true = throw left, false = right

    [Header("Single-drop mode (ParticleSystem)")]
    [SerializeField] bool singleDrops = true;        // turn ON to emit one-at-a-time
    [SerializeField] float dropsPerSecondPS = 6f;    // spacing between single drops
    float emitBucketPS;





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
        lastPos   = transform.position;

        if (ps != null && ps.gameObject != null)
            Destroy(ps.gameObject);

        if (dropletsPrefab && nozzle)
        {
            var prefabPS = dropletsPrefab.GetComponentInChildren<ParticleSystem>(true);
            if (prefabPS)
            {
                var go = Instantiate(dropletsPrefab);
                go.transform.position = nozzle.position;
                go.transform.localScale = Vector3.one;

                ps = go.GetComponentInChildren<ParticleSystem>(true);

                // ----- Main -----
                var main = ps.main;
                main.loop            = true;
                main.playOnAwake     = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World; // critical
                main.scalingMode     = ParticleSystemScalingMode.Shape;
                main.stopAction      = ParticleSystemStopAction.None;
                main.maxParticles    = Mathf.Max(main.maxParticles, 5000);
    #if UNITY_2021_2_OR_NEWER
                main.ringBufferMode  = ParticleSystemRingBufferMode.PauseUntilReplaced;
    #endif
                main.startSizeMultiplier *= Mathf.Max(0.01f, particleSizeMult);

                // start idle; HandleSpray() toggles emission
                var emission = ps.emission;
                emission.enabled = false;          // MUST be off (we emit manually)
                emission.rateOverTime = 0f;        // no continuous stream
                emission.rateOverDistance = 0f;    // no distance-based emission

                var iv = ps.inheritVelocity; iv.enabled = false;

                var velOver   = ps.velocityOverLifetime; velOver.enabled = false;
                var forceOver = ps.forceOverLifetime;   forceOver.enabled = false;

                // -------------------- MODE SETUP --------------------
                if (mode == SprayMode.VerticalDown)
                {
                    // your non-projectile stream
                    main.startSpeed      = 0f;
                    main.gravityModifier = 0f;

                    velOver.enabled = true;
                    velOver.space   = ParticleSystemSimulationSpace.World;

                    // tiny spit then steady fall (keep if you still want this mode)
                    float xSign = (Vector2.Dot(nozzle.right, Vector2.right) >= 0f) ? 1f : -1f;
                    var xCurve = new AnimationCurve(
                        new Keyframe(0f, 5.5f * xSign),
                        new Keyframe(0.28f, 5.5f * xSign),
                        new Keyframe(0.75f, 0f),
                        new Keyframe(1f, 0f)
                    );
                    velOver.x = new ParticleSystem.MinMaxCurve(1f, xCurve);

                    float fall = -Mathf.Abs(dropletSpeed);
                    var yCurve = new AnimationCurve(
                        new Keyframe(0f, -0.02f),
                        new Keyframe(0.55f, fall),
                        new Keyframe(1f, fall)
                    );
                    velOver.y = new ParticleSystem.MinMaxCurve(1f, yCurve);

                    velOver.z = new ParticleSystem.MinMaxCurve(1f,
                        new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f)));
                }
                else // SprayMode.Forward => PROJECTILE (throw then arc)
                {
                    // 1) no start speed/gravity in Main
                    main.startSpeed      = 0f;
                    main.gravityModifier = 0f;

                    // 2) horizontal initial velocity ONLY, along nozzle.right (flip side if needed)
                    float side = projectileToLeft ? -1f : 1f;
                    Vector2 dir = ((Vector2)nozzle.right * side).normalized;

                    Vector2 v0 = new Vector2(dir.x * Mathf.Max(0.01f, launchSpeed), 0f); // Y=0!

                    velOver.enabled = true;
                    velOver.space   = ParticleSystemSimulationSpace.World;
                    // flat (constant) curves = constant initial velocity
                    velOver.x = new ParticleSystem.MinMaxCurve(1f,
                        new AnimationCurve(new Keyframe(0f, v0.x), new Keyframe(1f, v0.x)));
                    velOver.y = new ParticleSystem.MinMaxCurve(1f,
                        new AnimationCurve(new Keyframe(0f, 0f),    new Keyframe(1f, 0f)));
                    velOver.z = new ParticleSystem.MinMaxCurve(1f,
                        new AnimationCurve(new Keyframe(0f, 0f),    new Keyframe(1f, 0f)));

                    // 3) gravity as constant downward acceleration
                    forceOver.enabled = true;
                    forceOver.space   = ParticleSystemSimulationSpace.World;
                    forceOver.x       = new ParticleSystem.MinMaxCurve(0f);
                    forceOver.y       = new ParticleSystem.MinMaxCurve(-Mathf.Abs(gravityAccel));
                    forceOver.z       = new ParticleSystem.MinMaxCurve(0f);
                }

                // idle until movement
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }










    void Update()
    {
        HandlePointerNewInput();

        // Move & clamp
        Vector3 pos = transform.position;
        pos = Vector3.Lerp(pos, targetPos, 1f - Mathf.Exp(-moveLerp * Time.deltaTime));
        transform.position = ClampToBounds(pos);

        // Velocity estimate
        Vector3 newVel = (transform.position - lastPos) / Mathf.Max(Time.deltaTime, 1e-5f);
        vel = Vector3.Lerp(vel, newVel, 0.5f);
        lastPos = transform.position;

        // Tilt
        float speed = new Vector2(vel.x, vel.y).magnitude;
        float desiredDelta = tiltAlwaysForward
            ? Mathf.Clamp(speed * tiltPerUnitSpeed, 0f, maxTilt)
            : Mathf.Clamp(-vel.x * tiltPerUnitSpeed, -maxTilt, maxTilt);

        float desiredAngle = (dragging && speed > 0.01f) ? restAngleZ + desiredDelta : restAngleZ;
        float lerp = (dragging && speed > 0.01f) ? tiltLerp : returnLerp;
        float z = Mathf.LerpAngle(transform.eulerAngles.z, desiredAngle, 1f - Mathf.Exp(-lerp * Time.deltaTime));
        transform.rotation = Quaternion.Euler(0, 0, z);

        // Keep particle system anchored at the nozzle
        if (ps)
        {
            ps.transform.position = nozzle.position;
            ps.transform.rotation = (mode == SprayMode.Forward) ? nozzle.rotation : Quaternion.identity;
        }

        // Spray
        bool shouldSpray = speed >= spraySpeedThreshold && nozzle && dropletsPrefab;
        HandleSpray(shouldSpray);
    }

    // ---------- Input ----------
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

    // ---------- Clamp ----------
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
        // ParticleSystem path: manual single-particle emission
        if (ps)
        {
            if (!singleDrops)
            {
                // fallback: simple on/off stream (your old behavior)
                var emission = ps.emission;
                if (on)
                {
                    if (!emission.enabled) emission.enabled = true;
                    if (!ps.isEmitting) ps.Play(true);
                }
                else
                {
                    if (emission.enabled) emission.enabled = false;
                    emitBucketPS = 0f;
                }
                return;
            }

            // --- singleDrops == true ---
            // keep the system playing (so it can accept Emit calls), but with emission module OFF
            if (!ps.isPlaying) ps.Play(true);

            if (on)
            {
                emitBucketPS += Time.deltaTime * Mathf.Max(0.01f, dropsPerSecondPS);
                while (emitBucketPS >= 1f)
                {
                    emitBucketPS -= 1f;
                    ps.Emit(1); // <-- exactly one rectangle
                }
            }
            else
            {
                emitBucketPS = 0f;
            }
            return;
        }

        // Rigidbody2D path (unchanged)
        if (!on) { dropTimer = 0f; return; }

        dropTimer += Time.deltaTime * dropsPerSecond;
        while (dropTimer >= 1f)
        {
            dropTimer -= 1f;

            Vector2 dir;
            if (mode == SprayMode.VerticalDown)
            {
                dir = Vector2.down;
            }
            else
            {
                dir = nozzle ? (Vector2)nozzle.right : Vector2.right;
                if (spread != 0f)
                    dir = Quaternion.Euler(0, 0, Random.Range(-spread, spread)) * dir;
            }

            var drop = Instantiate(dropletsPrefab, nozzle.position, Quaternion.identity);
            drop.transform.localScale *= dropletScale;

            var wd = drop.GetComponent<WaterDrop>();
            if (wd != null && grassKillLine != null) wd.SetKillLine(grassKillLine, 0f, true);

            var rb = drop.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 addVel = (mode == SprayMode.VerticalDown) ? Vector2.zero : (Vector2)vel * 0.25f;
                rb.linearVelocity = dir.normalized * dropletSpeed + addVel;
            }

            if (dropLifetime > 0f) Destroy(drop, dropLifetime);
        }
    }


}
