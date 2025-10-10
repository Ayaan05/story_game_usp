using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Spawns a transparent full-screen button when this object activates, so the next tap anywhere advances the scene.
/// </summary>
public class TapAnywhereAdvance : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("PlayAgainButton that handles the actual scene change. If left null, searches in children.")]
    public PlayAgainButton targetButton;

    [Tooltip("Scene to load if no PlayAgainButton is found.")]
    public string fallbackSceneName = GameManager.END_SCENE;

    [Tooltip("Mini game to report as completed if using the fallback.")]
    public MiniGame fallbackMiniGame = MiniGame.Food;

    [Header("Timing")]
    [Tooltip("Wait this long before creating the overlay (seconds).")]
    public float activationDelay = 0f;

    [Tooltip("Require all pointers to release before listening, so the tap that spawned the sparkles doesn't immediately fire.")]
    public bool requirePointerRelease = false;

    [Header("Debug")]
    public bool debugLogs = false;

    private GameObject overlayRoot;
    private Button overlayButton;
    private bool overlayActive;

    void OnEnable()
    {
        if (targetButton == null)
            targetButton = GetComponentInChildren<PlayAgainButton>(true);

        if (debugLogs)
            Debug.Log($"{name}: TapAnywhereAdvance enabled (targetButton={(targetButton ? targetButton.name : "null")})");

        StartCoroutine(SetupOverlay());
    }

    void OnDisable()
    {
        DestroyOverlay();
    }

    IEnumerator SetupOverlay()
    {
        if (requirePointerRelease)
        {
            yield return null;
            while (IsPointerHeld())
                yield return null;
        }

        if (activationDelay > 0f)
            yield return new WaitForSecondsRealtime(activationDelay);

        CreateOverlay();
    }

    void CreateOverlay()
    {
        if (overlayActive)
            return;

        overlayRoot = new GameObject("TapAnywhereAdvanceOverlay");
        var canvas = overlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        canvas.overrideSorting = true;

        overlayRoot.AddComponent<CanvasScaler>();
        overlayRoot.AddComponent<GraphicRaycaster>();

        var buttonGO = new GameObject("AdvanceButton");
        buttonGO.transform.SetParent(overlayRoot.transform, false);

        var rect = buttonGO.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = buttonGO.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        overlayButton = buttonGO.AddComponent<Button>();
        overlayButton.onClick.AddListener(OnOverlayClicked);

        overlayActive = true;

        if (debugLogs)
            Debug.Log($"{name}: TapAnywhereAdvance overlay ready.");
    }

    void DestroyOverlay()
    {
        overlayActive = false;
        if (overlayButton != null)
        {
            overlayButton.onClick.RemoveListener(OnOverlayClicked);
            overlayButton = null;
        }
        if (overlayRoot != null)
        {
            Destroy(overlayRoot);
            overlayRoot = null;
        }
    }

    void OnOverlayClicked()
    {
        if (debugLogs)
            Debug.Log($"{name}: overlay tapped → advancing.");

        DestroyOverlay();

        if (targetButton != null)
        {
            targetButton.TriggerReloadExternal(true);
            return;
        }

        var gm = GameManager.Instance;
        if (gm != null && fallbackMiniGame != MiniGame.None)
        {
            gm.CompleteMiniGame(fallbackMiniGame);
            return;
        }

        string targetScene = string.IsNullOrEmpty(fallbackSceneName) ? GameManager.END_SCENE : fallbackSceneName;
        if (!string.IsNullOrEmpty(targetScene))
        {
            if (SceneFadeController.Instance != null)
                SceneFadeController.Instance.FadeOutAndLoad(targetScene);
            else
                SceneManager.LoadScene(targetScene);
        }
    }

    bool IsPointerHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return true;
#endif
        if (Input.GetMouseButton(0))
            return true;
        if (Input.touchCount > 0)
        {
            var phase = Input.GetTouch(0).phase;
            return phase == UnityEngine.TouchPhase.Began ||
                   phase == UnityEngine.TouchPhase.Moved ||
                   phase == UnityEngine.TouchPhase.Stationary;
        }
        return false;
    }
}
