using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class EndSceneController : MonoBehaviour
{
    [Header("Tap Sequence Objects")]
    [Tooltip("Objects that appear on the first tap (e.g. bye bubbles).")]
    public GameObject[] firstTapObjects;

    [Header("Sprite Swap Targets")]
    public SpriteRenderer windowRenderer;
    public Sprite windowClosedSprite;
    public SpriteRenderer doghouseRenderer;
    public Sprite doghouseClosedSprite;

    [Header("Window Return")]
    public WindowReturnHandler windowReturnHandler;
    public bool resetGameOnReturn = true;
    public SceneFadeController sceneFadeController;

    [Header("Input")]
    [Tooltip("If true, taps will trigger even if the pointer is over UI objects.")]
    public bool ignoreUIBlocking = true;

    [Header("Transitions")]
    [Tooltip("Seconds for window/doghouse swap fade.")]
    [Range(0.05f, 1.5f)] public float swapDuration = 0.4f;

    [Header("Debug")]
    public bool debugLogs = false;

    private int tapStage = 0;
    private bool sequenceComplete = false;
    private Sprite originalWindowSprite;
    private Sprite originalDoghouseSprite;

    void Awake()
    {
        if (windowRenderer != null)
            originalWindowSprite = windowRenderer.sprite;
        if (doghouseRenderer != null)
            originalDoghouseSprite = doghouseRenderer.sprite;

        SetFirstTapObjectsActive(false);
        ConfigureWindowReturn(false);
    }

    void OnEnable()
    {
        tapStage = 0;
        sequenceComplete = false;
        SetFirstTapObjectsActive(false);
        if (windowRenderer != null)
        {
            windowRenderer.sprite = originalWindowSprite;
            windowRenderer.color = Color.white;
        }
        if (doghouseRenderer != null)
        {
            doghouseRenderer.sprite = originalDoghouseSprite;
            doghouseRenderer.color = Color.white;
        }
        ConfigureWindowReturn(false);
    }

    void Update()
    {
        if (sequenceComplete) return;

        bool tapped = false;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (ShouldProcessPointer(-1)) tapped = true;
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            if (ShouldProcessPointer(0)) tapped = true;
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            if (ShouldProcessPointer(-1)) tapped = true;
        }
        else if (Input.touchSupported && Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began && ShouldProcessPointer(touch.fingerId)) tapped = true;
        }
#endif

        if (!tapped) return;

        if (tapStage == 0)
        {
            if (debugLogs) Debug.Log("EndSceneController: first tap detected");
            SetFirstTapObjectsActive(true);
            tapStage = 1;
        }
        else if (tapStage == 1)
        {
            if (debugLogs) Debug.Log("EndSceneController: second tap detected");
            AdvanceSequence();
            tapStage = 2;
        }
    }

    void AdvanceSequence()
    {
        SetFirstTapObjectsActive(false);
        StartCoroutine(PerformSwapSequence());
    }

    void SetFirstTapObjectsActive(bool state)
    {
        if (firstTapObjects == null) return;
        for (int i = 0; i < firstTapObjects.Length; i++)
        {
            var go = firstTapObjects[i];
            if (go != null)
            {
                go.SetActive(state);
                if (state)
                    BoostVisualAlpha(go);
            }
        }
    }

    void ConfigureWindowReturn(bool enable)
    {
        if (windowReturnHandler != null)
            windowReturnHandler.Configure(this, enable);
    }

    public void HandleWindowReturn()
    {
        if (!sequenceComplete) return;

        if (resetGameOnReturn && GameManager.Instance != null)
            GameManager.Instance.ResetProgress();

        string targetScene = GameManager.MAIN_SCENE;
        if (sceneFadeController != null)
            sceneFadeController.FadeOutAndLoad(targetScene);
        else
            SceneManager.LoadScene(targetScene);
    }

    bool ShouldProcessPointer(int pointerId)
    {
        if (ignoreUIBlocking || EventSystem.current == null)
            return true;

        if (pointerId >= 0)
            return !EventSystem.current.IsPointerOverGameObject(pointerId);

        return !EventSystem.current.IsPointerOverGameObject();
    }

    IEnumerator PerformSwapSequence()
    {
        if (debugLogs) Debug.Log("EndSceneController: starting swap fade");

        float duration = Mathf.Max(0.05f, swapDuration);
        Coroutine windowFade = null;
        Coroutine dogFade = null;

        if (windowRenderer != null && windowClosedSprite != null)
            windowFade = StartCoroutine(CrossFadeSprite(windowRenderer, windowClosedSprite, duration));
        if (doghouseRenderer != null && doghouseClosedSprite != null)
            dogFade = StartCoroutine(CrossFadeSprite(doghouseRenderer, doghouseClosedSprite, duration));

        if (windowFade != null) yield return windowFade;
        if (dogFade != null) yield return dogFade;

        sequenceComplete = true;
        ConfigureWindowReturn(true);
    }

    IEnumerator CrossFadeSprite(SpriteRenderer renderer, Sprite targetSprite, float duration)
    {
        if (renderer == null || targetSprite == null)
            yield break;

        var tempObj = new GameObject(renderer.gameObject.name + "_SwapTemp");
        tempObj.transform.SetParent(renderer.transform.parent);
        tempObj.transform.position = renderer.transform.position;
        tempObj.transform.rotation = renderer.transform.rotation;
        tempObj.transform.localScale = renderer.transform.localScale;

        var tempRenderer = tempObj.AddComponent<SpriteRenderer>();
        tempRenderer.sprite = targetSprite;
        tempRenderer.sortingLayerID = renderer.sortingLayerID;
        tempRenderer.sortingOrder = renderer.sortingOrder + 1;
        tempRenderer.color = new Color(1f, 1f, 1f, 0f);

        Color baseColor = renderer.color;
        Color targetColor = tempRenderer.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float fadeIn = t;
            float fadeOut = 1f - t;
            renderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, fadeOut);
            tempRenderer.color = new Color(1f, 1f, 1f, fadeIn);
            yield return null;
        }

        renderer.sprite = targetSprite;
        renderer.color = baseColor;

        Destroy(tempObj);
    }

    void BoostVisualAlpha(GameObject go)
    {
        if (go == null) return;

        var graphic = go.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.color = new Color(1f, 1f, 1f, 1f);
        }
        else
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(1f, 1f, 1f, 1f);
            }
        }

        var cg = go.GetComponent<CanvasGroup>();
        if (cg != null && cg.alpha < 0.95f)
            cg.alpha = 1f;
    }
}
