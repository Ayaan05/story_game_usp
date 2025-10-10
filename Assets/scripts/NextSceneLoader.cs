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

    [Header("Activation")]
    [SerializeField] private bool allowTapAnywhere = true; // allow tapping anywhere once sparkle is shown

    [Header("Interactivity")]
    [SerializeField] private bool hideAtStart = true;      // hide sparkle until ShowSparkle() is called

    private bool globalTapArmed = false;
    private bool sceneTriggered = false;

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

    void Update()
    {
        if (!globalTapArmed) return;

#if ENABLE_INPUT_SYSTEM
        bool tapped = false;
        if (UnityEngine.InputSystem.Mouse.current != null &&
            UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            tapped = true;
        else if (UnityEngine.InputSystem.Touchscreen.current != null &&
                 UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            tapped = true;
        if (!tapped) return;
#else
        if (!Input.GetMouseButtonDown(0) &&
            !(Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            return;
#endif

        OnSparkleClicked();
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
        globalTapArmed = allowTapAnywhere;
        sceneTriggered = false;
    }

    // Optional: hide again if needed
    public void HideSparkle()
    {
        if (!sparkleButton) return;
        sparkleButton.interactable = false;
        sparkleButton.gameObject.SetActive(false);
        globalTapArmed = false;
        sceneTriggered = false;
    }

    private void OnSparkleClicked()
    {
        if (sceneTriggered) return;

        if (!globalTapArmed && sparkleButton && !sparkleButton.gameObject.activeInHierarchy)
            return;

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogWarning("SparkleNextScene: nextSceneName is empty; not loading.");
            return;
        }
        sceneTriggered = true;
        // Prefer fade controller when available
        if (SceneFadeController.Instance)
        {
            SceneFadeController.Instance.FadeOutAndLoad(nextSceneName);
        }
        else
        {
            SceneManager.LoadScene(nextSceneName);
        }

        globalTapArmed = false;
    }
}
