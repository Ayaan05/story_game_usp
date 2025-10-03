using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SparkleNextScene : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas uiCanvas;              // Canvas that holds the sparkle button
    [SerializeField] private Button sparkleButton;         // The sparkle as a UI Button (Image/TMP child ok)

    [Header("Target & Placement")]
    [SerializeField] private Transform worldTarget;        // Optional: place sparkle near this world object
    [SerializeField] private Vector2 worldOffset = new Vector2(0f, 2.0f); // offset in world units from target

    [Header("Scene Navigation")]
    [SerializeField] private string nextSceneName = "NextScene";

    [Header("Interactivity")]
    [SerializeField] private bool hideAtStart = true;      // hide sparkle until ShowSparkle() is called

    void Awake()
    {
        // Basic sanity: auto-find a Canvas if not assigned
        if (!uiCanvas)
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            if (canvases.Length == 1)
            {
                uiCanvas = canvases[0];
                Debug.Log($"SparkleNextScene: auto-assigned uiCanvas to {uiCanvas.name}");
            }
            else if (canvases.Length > 1)
            {
                Debug.LogWarning("SparkleNextScene: multiple Canvases found; please assign the correct Canvas.");
            }
            else
            {
                Debug.LogWarning("SparkleNextScene: no Canvas found in scene.");
            }
        }

        // Wire click -> load next scene
        if (sparkleButton)
        {
            sparkleButton.onClick.RemoveAllListeners();
            sparkleButton.onClick.AddListener(OnSparkleClicked);

            // Hide until called, mirroring the hidden-buttons architecture
            if (hideAtStart)
            {
                sparkleButton.gameObject.SetActive(false);
                sparkleButton.interactable = false;
            }
        }
        else
        {
            Debug.LogWarning("SparkleNextScene: sparkleButton is not assigned.");
        }
    }

    // Public method: call this when you want to reveal and place the sparkle
    public void ShowSparkle()
    {
        if (!sparkleButton)
        {
            Debug.LogWarning("SparkleNextScene.ShowSparkle: sparkleButton missing.");
            return;
        }

        // Place the sparkle relative to a world target if provided
        if (uiCanvas && worldTarget)
        {
            // Convert world position + offset to screen space, then to Canvas local position
            Vector3 worldPos = worldTarget.position + (Vector3)worldOffset;
            Vector3 screenPos = Camera.main ? Camera.main.WorldToScreenPoint(worldPos) : Vector3.zero;

            RectTransform canvasRect = uiCanvas.transform as RectTransform;
            RectTransform sparkRect = sparkleButton.transform as RectTransform;

            if (canvasRect && sparkRect)
            {
                // Screen space to Canvas local space
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPos,
                    uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCanvas.worldCamera,
                    out localPoint
                );
                sparkRect.anchoredPosition = localPoint;
            }
        }

        sparkleButton.gameObject.SetActive(true);
        sparkleButton.interactable = true;
    }

    // Optional: hide again if needed
    public void HideSparkle()
    {
        if (!sparkleButton) return;
        sparkleButton.interactable = false;
        sparkleButton.gameObject.SetActive(false);
    }

    private void OnSparkleClicked()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogWarning("SparkleNextScene: nextSceneName is empty; not loading.");
            return;
        }
        // Load the next scene
        SceneManager.LoadScene(nextSceneName);
    }
}
