using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;   // <- gives you TouchControl


[RequireComponent(typeof(Collider2D))]
public class SprinklerInteractive : MonoBehaviour
{
    public enum SprayMode { Forward, VerticalDown }
    // put near activeTouchId etc.
    Coroutine returnCR;


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
    [SerializeField] Transform topEdgeRef;     // ← assign your LID here
    [SerializeField] Transform bottomEdgeRef;  // ← child at sprinkler’s bottom
    [SerializeField] bool  clampYToCamera = true;
    [SerializeField] float yScreenPadding = 0.15f;   // world units
    int activeTouchId = -1;   // which finger is dragging (-1 = none)





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

    [Header("Return-to-origin")]
    [SerializeField] bool returnToPickupPoint = true;
    [SerializeField] float returnDelay = 0f;      // optional pause before returning
    [SerializeField] float arrivedEpsilon = 0.02f; // how close is “close enough”
    [SerializeField] float returnMoveLerp = 4f;  // slower than moveLerp
    Vector3 pickupPos;
    bool    hasPickupPos;
    bool isReturning;







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
        float rate = isReturning ? returnMoveLerp : moveLerp;       // << slow when returning
        pos = Vector3.Lerp(pos, targetPos, 1f - Mathf.Exp(-rate * Time.deltaTime));
        transform.position = ClampToBounds(pos);

        // stop "return" once we’ve arrived
        if (isReturning && (transform.position - targetPos).sqrMagnitude <= arrivedEpsilon * arrivedEpsilon)
        {
            isReturning = false;
            hasPickupPos = false;
        }


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
        // Only spray while the user is actively dragging
        bool shouldSpray = dragging && speed >= spraySpeedThreshold && nozzle && dropletsPrefab;

        HandleSpray(shouldSpray);
    }

    // ---------- Input ----------
    void HandlePointerNewInput()
    {
        var cam = Camera.main; if (!cam) return;

        Vector2 screenPos = default;
        bool down = false, held = false, up = false;

        // -------- Touch (mobile) --------
        if (Touchscreen.current != null)
        {
            foreach (var t in Touchscreen.current.touches)
            {
                int id = t.touchId.ReadValue();

                if (activeTouchId < 0)
                {
                    if (!t.press.wasPressedThisFrame) continue;

                    float zDist = Mathf.Abs(cam.transform.position.z - transform.position.z);
                    var wp = cam.ScreenToWorldPoint(new Vector3(t.position.ReadValue().x, t.position.ReadValue().y, zDist));
                    wp.z = safeZ;

                    if (col != null && col.OverlapPoint(wp))
                    {
                        activeTouchId = id;
                        screenPos = t.position.ReadValue();
                        down = true; held = true;
                        break;
                    }
                }
                else if (id == activeTouchId)
                {
                    screenPos = t.position.ReadValue();
                    down = t.press.wasPressedThisFrame;
                    held = t.press.isPressed;
                    up   = t.press.wasReleasedThisFrame;
                    break;
                }
            }
        }
        // -------- Mouse (editor/desktop) --------
        else if (Mouse.current != null)
        {
            screenPos = Mouse.current.position.ReadValue();
            down = Mouse.current.leftButton.wasPressedThisFrame;
            held = Mouse.current.leftButton.isPressed;
            up   = Mouse.current.leftButton.wasReleasedThisFrame;

            if (down)
            {
                float zDist = Mathf.Abs(cam.transform.position.z - transform.position.z);
                var wp = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDist));
                wp.z = safeZ;
                if (!(col != null && col.OverlapPoint(wp))) down = false; // reject off-target clicks
            }
        }

        // Convert chosen position to world
        float z = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector3 pw = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));
        pw.z = safeZ;

        // ---- Begin drag (only if started on us) ----
        if (down && col != null && col.OverlapPoint(pw))
        {
            dragging = true;
            grabOffset = transform.position - pw;
            targetPos = ClampToBounds(transform.position);

            // remember pickup + cancel any pending return
            pickupPos = transform.position;
            hasPickupPos = true;
            isReturning = false;
            if (returnCR != null) { StopCoroutine(returnCR); returnCR = null; }
        }

        // ---- Continue drag ----
        if (dragging && held)
            targetPos = ClampToBounds(pw + grabOffset);

        // ---- Release (touch lifts or mouse up) ----
        bool released = (!held && dragging) || (up && (activeTouchId < 0 || dragging));
        if (released)
        {
            dragging = false;
            activeTouchId = -1;
            // hard stop spray now
            HandleSpray(false);
            if (ps && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            dropTimer = 0f;   // RB2D path safety

            if (returnToPickupPoint && hasPickupPos)
            {
                if (returnDelay > 0f)
                {
                    if (returnCR != null) StopCoroutine(returnCR);
                    returnCR = StartCoroutine(ReturnSoon());
                }
                else
                {
                    targetPos = ClampToBounds(pickupPos);
                    isReturning = true;  // slow glide back
                }
            }
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

        var cam = Camera.main;
        if (!cam)
            return p;

        float vert = cam.orthographicSize;
        float horz = vert * cam.aspect;
        Vector3 c  = cam.transform.position;

        // Proposed delta this frame
        float dx = p.x - transform.position.x;
        float dy = p.y - transform.position.y;

        // ---------- HORIZONTAL (nozzle left, handle right) ----------
        if (clampXToCamera)
        {
            float camLeft  = c.x - horz + xScreenPadding;
            float camRight = c.x + horz - xScreenPadding;

            // Predict where anchors will be after moving by dx
            float leftPred  = leftEdgeRef  ? (leftEdgeRef.position.x  + dx) : (sr ? sr.bounds.min.x + dx : float.NaN);
            float rightPred = rightEdgeRef ? (rightEdgeRef.position.x + dx) : (sr ? sr.bounds.max.x + dx : float.NaN);

            if (!float.IsNaN(leftPred) && leftPred < camLeft)
                p.x += (camLeft - leftPred);       // nudge right to keep nozzle inside

            if (!float.IsNaN(rightPred) && rightPred > camRight)
                p.x -= (rightPred - camRight);     // nudge left to keep handle inside
        }

        // Recompute dy after possible x nudge (not needed for Y, but harmless)
        dy = p.y - transform.position.y;

        // ---------- VERTICAL (lid top, bottom of sprinkler bottom) ----------
        if (clampYToCamera)
        {
            float camBottom = c.y - vert + yScreenPadding;
            float camTop    = c.y + vert - yScreenPadding;

            float topPred    = topEdgeRef    ? (topEdgeRef.position.y    + dy) : (sr ? sr.bounds.max.y + dy : float.NaN);
            float bottomPred = bottomEdgeRef ? (bottomEdgeRef.position.y + dy) : (sr ? sr.bounds.min.y + dy : float.NaN);

            if (!float.IsNaN(topPred) && topPred > camTop)
                p.y -= (topPred - camTop);         // push down so lid stays inside

            if (!float.IsNaN(bottomPred) && bottomPred < camBottom)
                p.y += (camBottom - bottomPred);   // push up so bottom stays inside
        }

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

    System.Collections.IEnumerator ReturnSoon()
    {
        yield return new WaitForSeconds(returnDelay);
        if (!dragging && hasPickupPos)
            targetPos = ClampToBounds(pickupPos);
            isReturning = true;  // start slow return after the delay
    }



}
