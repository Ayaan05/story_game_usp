using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class BoxDragTilt2D : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Follow")]
    public float followSpeed = 12f;
    public float maxSpeed = 20f;

    [Header("Tilt (right-only, 0..+max)")]
    public float tiltPerPixel = 0.25f;     // degrees per horizontal pixel dragged
    public float maxTiltDeg = 120f;        // allowed tilt range is [0, maxTiltDeg]
    public float tiltDamping = 14f;

    [Header("Auto recenter when released")]
    public float returnPosSpeed = 6f;
    public float returnRotSpeed = 10f;

    private Camera cam;
    private bool dragging;

    private Vector3 startPos;
    private float startTiltDeg;

    private Vector3 targetPos;
    private float targetTilt;              // desired Z rotation in degrees (clamped 0..max)
    private float currentTilt;             // smoothed Z rotation in degrees (clamped 0..max)

    void Awake()
    {
        cam = Camera.main;

        startPos = transform.position;
        startTiltDeg = Normalize0To360(transform.eulerAngles.z);    // 0..360

        targetPos = startPos;

        // Initialize in 0..180 domain, clamp to [0, max]
        currentTilt = Mathf.Clamp(To0To180(startTiltDeg), 0f, maxTiltDeg);
        targetTilt  = currentTilt;
        transform.rotation = Quaternion.Euler(0f, 0f, currentTilt);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        dragging = true;
        targetPos = transform.position;
    }

    public void OnDrag(PointerEventData e)
    {
        // Follow the pointer (world space)
        var w = cam.ScreenToWorldPoint(e.position);
        w.z = transform.position.z;
        targetPos = w;

        // Map horizontal drag so dragging RIGHT increases positive (clockwise) tilt.
        // If you want LEFT to increase tilt instead, change + to - below.
        float deltaTilt = +e.delta.x * tiltPerPixel;

        // Right-only: ignore any delta that tries to go left (negative)
        if (deltaTilt < 0f) deltaTilt = 0f;

        targetTilt += deltaTilt;
        targetTilt = Mathf.Clamp(targetTilt, 0f, maxTiltDeg);
    }

    public void OnEndDrag(PointerEventData e)
    {
        dragging = false;
    }

    void Update()
    {
        if (dragging)
        {
            // Position damping with speed cap
            Vector3 next = Vector3.Lerp(transform.position, targetPos, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            Vector3 delta = next - transform.position;
            float maxStep = maxSpeed * Time.deltaTime;
            if (delta.magnitude > maxStep) next = transform.position + delta.normalized * maxStep;
            transform.position = next;

            // Smooth rotation toward target
            currentTilt = Mathf.LerpAngle(currentTilt, targetTilt, 1f - Mathf.Exp(-tiltDamping * Time.deltaTime));
            currentTilt = Mathf.Clamp(To0To180(currentTilt), 0f, maxTiltDeg);
            transform.rotation = Quaternion.Euler(0f, 0f, currentTilt);
        }
        else
        {
            // Auto recentre position toward start
            transform.position = Vector3.Lerp(transform.position, startPos, 1f - Mathf.Exp(-returnPosSpeed * Time.deltaTime));

            // Spring target rotation toward upright (0), never go below 0
            targetTilt = Mathf.Lerp(targetTilt, 0f, 1f - Mathf.Exp(-returnRotSpeed * Time.deltaTime));
            targetTilt = Mathf.Clamp(targetTilt, 0f, maxTiltDeg);

            // Smooth rotation toward target
            currentTilt = Mathf.LerpAngle(currentTilt, targetTilt, 1f - Mathf.Exp(-returnRotSpeed * Time.deltaTime));
            currentTilt = Mathf.Clamp(To0To180(currentTilt), 0f, maxTiltDeg);
            transform.rotation = Quaternion.Euler(0f, 0f, currentTilt);
        }
    }

    // Normalize 0..360
    private float Normalize0To360(float deg)
    {
        deg %= 360f;
        if (deg < 0f) deg += 360f;
        return deg;
    }

    // Convert any angle to its 0..180 equivalent (treats >180 as rotation back from 360)
    private float To0To180(float deg)
    {
        float a = Normalize0To360(deg);
        if (a > 180f) a = 360f - a; // mirror into 0..180
        return a;
    }

    // External read for detectors that expect 0..180 magnitude
    public float CurrentTiltDeg => currentTilt; // 0 (upright) .. +max (clockwise/down)
}
