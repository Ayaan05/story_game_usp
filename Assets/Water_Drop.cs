using UnityEngine;

public class WaterDrop : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField] float life = 4f;

    [Header("Kill by Y (no physics)")]
    [Tooltip("Drag your Grass Line object here to kill when drop goes below its Y.")]
    [SerializeField] Transform killLine;      // assign Grass Line here
    [SerializeField] float offsetY = 0f;      // fine-tune height if needed
    [SerializeField] bool killWhenBelow = true;

    [Header("Kill by trigger (no layers)")]
    [Tooltip("Optional: specific collider to kill on contact (e.g., Grass Line's BoxCollider2D).")]
    [SerializeField] Collider2D killCollider; // drag Grass Line's BoxCollider2D here (optional)
    [Tooltip("Optional: tag to kill on contact (set this tag on Grass Line). Leave empty to skip.")]
    [SerializeField] string killTag = "GrassLine";

    // Legacy support: Init(groundY)
    float groundY = float.NegativeInfinity;
    bool useGround = false;
    public void Init(float groundY) { this.groundY = groundY; useGround = true; }

    void Start()
    {
        if (life > 0f) Destroy(gameObject, life);
    }

    void Update()
    {
        // Preferred: kill by the Grass Line's Y
        if (killLine)
        {
            float cut = killLine.position.y + offsetY;
            float y = transform.position.y;
            if (killWhenBelow ? (y <= cut) : (y >= cut))
                Destroy(gameObject);
            return; // no need to also check groundY
        }

        // Legacy numeric Y
        if (useGround && transform.position.y <= groundY)
            Destroy(gameObject);
    }

    // Trigger kill without layers: by collider or tag
    void OnTriggerEnter2D(Collider2D other)
    {
        if (killCollider && other == killCollider) { Destroy(gameObject); return; }
        if (!string.IsNullOrEmpty(killTag) && other.CompareTag(killTag)) Destroy(gameObject);
    }

    // in WaterDrop.cs
    public void SetKillLine(Transform line, float offset = 0f, bool below = true)
    {
        killLine = line;     // Transform field in your script
        offsetY  = offset;
        killWhenBelow = below;
    }

}
