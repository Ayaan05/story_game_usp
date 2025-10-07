using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FitCameraToSprite : MonoBehaviour
{
    [Tooltip("Drag the background SpriteRenderer here")]
    public SpriteRenderer targetSprite;

    public enum FitMode { Cover, Contain }

    [Tooltip("Cover = fill screen (may crop). Contain = show whole sprite (may letterbox/pillarbox).")]
    public FitMode fitMode = FitMode.Cover;

    [Range(0f, 0.2f)]
    public float padding = 0.02f; // small zoom/overscan tweak

    [Tooltip("Keep the camera centred on the sprite's bounds every frame.")]
    public bool lockToSpriteCenter = true;

    [Tooltip("Automatically refit every frame. Disable to tweak the camera manually.")]
    public bool autoRefit = true;

    [Tooltip("Fine-tune how the camera is offset from the sprite center (world units).")]
    public Vector2 frameOffset = Vector2.zero;

    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
    }

    void Start()
    {
        if (autoRefit) Refit();
    }

    void OnValidate()
    {
        if (autoRefit && Application.isPlaying)
            Refit();
    }
    void Update()
    {
        // Keep it responsive if the Game window/device size changes
        if (autoRefit && cam != null)
            Refit();
    }

    void Refit()
    {
        if (targetSprite == null) return;

        // Sprite size in world units
        Vector2 s = targetSprite.bounds.size;

        // Orthographic size needed to show full height
        float sizeForHeight = s.y * 0.5f;

        // Orthographic size needed to show full width, given camera aspect
        float sizeForWidth  = (s.x * 0.5f) / cam.aspect;

        float coverSize = Mathf.Min(sizeForHeight, sizeForWidth);
        float containSize = Mathf.Max(sizeForHeight, sizeForWidth);

        // Choose contain or cover
        float orthoSize = fitMode == FitMode.Cover ? coverSize : containSize;

        // Small padding so edges never peek through or clip
        if (padding > 0f)
        {
            if (fitMode == FitMode.Cover)
                orthoSize = Mathf.Max(0.0001f, orthoSize * (1f - padding));
            else
                orthoSize *= (1f + padding);
        }

        cam.orthographicSize = orthoSize;

        if (lockToSpriteCenter)
        {
            Vector3 center = targetSprite.bounds.center;
            Vector3 pos = cam.transform.position;
            cam.transform.position = new Vector3(center.x + frameOffset.x, center.y + frameOffset.y, pos.z);
        }
    }
}
