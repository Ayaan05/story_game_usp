using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class SprinklerInteractive : MonoBehaviour
{
    public enum SprayMode { Forward, VerticalDown }

    [Header("Dragging")]
    [SerializeField] float moveLerp = 18f;

    [Header("Horizontal Clamp")]
    [SerializeField] bool clampXToCamera = true;   // keep inside camera horizontally
    [SerializeField] float xScreenPadding = 0.15f; // world units from screen edge
    [SerializeField] bool clampY = false;          // set true if you also want Y limits
    [SerializeField] Vector2 yLimits = new Vector2(-999f, 999f);
    [Header("Screen Clamp Anchors")]
    [SerializeField] Transform leftEdgeRef;   // e.g. NozzleTip
    [SerializeField] Transform rightEdgeRef;  // e.g. HandleEnd




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
    [Header("Nozzle width")]
    [SerializeField] float nozzleWidth = 0.6f;     // how wide the mouth is
    [SerializeField] float nozzleThickness = 0.05f; // how tall the slit is






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
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode     = ParticleSystemScalingMode.Shape;
                main.stopAction      = ParticleSystemStopAction.None;
                main.maxParticles    = Mathf.Max(main.maxParticles, 5000);
    #if UNITY_2021_2_OR_NEWER
                main.ringBufferMode  = ParticleSystemRingBufferMode.PauseUntilReplaced;
    #endif
                main.startSizeMultiplier *= Mathf.Max(0.01f, particleSizeMult);

                // ---- Emission: off (we emit manually / HandleSpray controls it)
                var emission = ps.emission;
                emission.enabled = false;
                emission.rateOverTime = 0f;
                emission.rateOverDistance = 0f;

                // ---- No inherit from emitter motion
                var iv = ps.inheritVelocity; iv.enabled = false;

                // ---- Shape: wide slit (Box); version-safe (no emitFrom)
                var shape = ps.shape;
                shape.enabled   = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.position  = Vector3.zero;
                shape.randomDirectionAmount = 0f;
                shape.randomPositionAmount  = 0f;
                shape.boxThickness = Vector3.zero; // full volume, not shell

                // Width across the mouth; thin slit
                if (mode == SprayMode.VerticalDown)
                {
                    // Throw is vertical; width should be local X, thin on Y
                    shape.scale = new Vector3(nozzleWidth, nozzleThickness, 0f);
                }
                else
                {
                    // Projectile/Forward: throw is along ±X; width across it → local Y
                    shape.scale = new Vector3(nozzleThickness, nozzleWidth, 0f);
                }

                var velOver   = ps.velocityOverLifetime; velOver.enabled = false;
                var forceOver = ps.forceOverLifetime;   forceOver.enabled = false;

                // -------------------- MODE SETUP --------------------
                if (mode == SprayMode.VerticalDown)
                {
                    main.startSpeed      = 0f;
                    main.gravityModifier = 0f;

                    velOver.enabled = true;
                    velOver.space   = ParticleSystemSimulationSpace.World;

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
                else // SprayMode.Forward => projectile (throw then arc)
                {
                    main.startSpeed      = 0f;
                    main.gravityModifier = 0f;

                    float side = projectileToLeft ? -1f : 1f;
                    Vector2 dir = ((Vector2)nozzle.right * side).normalized;

                    Vector2 v0 = new Vector2(dir.x * Mathf.Max(0.01f, launchSpeed), 0f); // Y=0
                    velOver.enabled = true;
                    velOver.space   = ParticleSystemSimulationSpace.World;
                    velOver.x = new ParticleSystem.MinMaxCurve(1f,
                        new AnimationCurve(new Keyframe(0f, v0.x), new Keyframe(1f, v0.x)));
                    velOver.y = new ParticleSystem.MinMaxCurve(1f,
                        new AnimationCurve(new Keyframe(0f, 0f),   new Keyframe(1f, 0f)));
                    velOver.z = new ParticleSystem.MinMaxCurve(1f,
                        new AnimationCurve(new Keyframe(0f, 0f),   new Keyframe(1f, 0f)));

                    forceOver.enabled = true;
                    forceOver.space   = ParticleSystemSimulationSpace.World;
                    forceOver.x = new ParticleSystem.MinMaxCurve(0f);
                    forceOver.y = new ParticleSystem.MinMaxCurve(-Mathf.Abs(gravityAccel));
                    forceOver.z = new ParticleSystem.MinMaxCurve(0f);
                }

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
    // World-space horizontal radii from the pivot to each sprite edge.
    // leftRadius: distance from pivot to left edge
    // rightRadius: distance from pivot to right edge
    void GetHorizontalRadii(out float leftRadius, out float rightRadius)
    {
        leftRadius = rightRadius = 0f;
        if (!sr || !sr.sprite) return;

        // Local-space sprite bounds relative to pivot
        var b  = sr.sprite.bounds;                 // center/size in local units
        float sx = Mathf.Abs(transform.lossyScale.x);

        // Distances from pivot to each edge (local), then scale to world
        leftRadius  = (b.extents.x + b.center.x) * sx;  // pivot -> left
        rightRadius = (b.extents.x - b.center.x) * sx;  // pivot -> right
    }

   Vector3 ClampToBounds(Vector3 p)
    {
        p.z = safeZ;

        if (clampXToCamera && Camera.main)
        {
            var cam  = Camera.main;
            float vert = cam.orthographicSize;
            float horz = vert * cam.aspect;
            Vector3 c  = cam.transform.position;

            float camLeft  = c.x - horz + xScreenPadding;
            float camRight = c.x + horz - xScreenPadding;

            // How far are we trying to move along X this frame?
            float dx = p.x - transform.position.x;

            // Predict where each anchor would be if we moved by dx.
            // (We just offset current world position by dx; rotation/scale don’t change here.)
            float leftPred  = leftEdgeRef  ? (leftEdgeRef.position.x  + dx) : float.NaN;
            float rightPred = rightEdgeRef ? (rightEdgeRef.position.x + dx) : float.NaN;

            // If an anchor isn't assigned, use sprite bounds for that side
            if (!leftEdgeRef && sr)  leftPred  = sr.bounds.min.x + dx;
            if (!rightEdgeRef && sr) rightPred = sr.bounds.max.x + dx;

            // Now push p.x so both anchors remain inside the camera rect.
            if (!float.IsNaN(leftPred) && leftPred < camLeft)
                p.x += (camLeft - leftPred);        // shift right so nozzle tip is inside

            if (!float.IsNaN(rightPred) && rightPred > camRight)
                p.x -= (rightPred - camRight);      // shift left so handle/back is inside
        }
        else
        {
            // Fallback: simple world-box clamp
            p.x = Mathf.Clamp(p.x, clampMin.x, clampMax.x);
        }

        // Y free (or clamp elsewhere if you want)
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
