using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FitCameraToSprite : MonoBehaviour
{
    [Tooltip("Drag the background SpriteRenderer here")]
    public SpriteRenderer targetSprite;

    [Tooltip("Fill the screen (may crop) vs. show whole sprite (may letterbox/pillarbox).")]
    public bool coverScreen = true;

    [Range(0f, 0.2f)]
    public float padding = 0.02f; // small zoom to guarantee no edges

    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
    }

    void Start()  { Refit(); }
    void OnValidate() { if (Application.isPlaying) Refit(); }
    void Update()
    {
        // Keep it responsive if the Game window/device size changes
        if (cam != null) Refit();
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

        // Choose contain or cover
        cam.orthographicSize = coverScreen
            ? Mathf.Max(sizeForHeight, sizeForWidth)     // fill (may crop one axis)
            : Mathf.Min(sizeForHeight, sizeForWidth);    // contain (may show bars)

        // Small extra zoom so edges never peek through
        if (coverScreen && padding > 0f)
            cam.orthographicSize *= (1f + padding);
    }
}
