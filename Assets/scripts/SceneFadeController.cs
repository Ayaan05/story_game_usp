using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class SceneFadeController : MonoBehaviour
{
    private static SceneFadeController instance;
    private static bool creatingRuntimeInstance;

    public static SceneFadeController Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindExistingInstance();
                if (instance == null)
                {
                    instance = CreateRuntimeInstance();
                }
            }
            return instance;
        }
        private set => instance = value;
    }

    [Tooltip("Duration of fade in seconds.")]
    public float fadeDuration = 0.6f;
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private CanvasGroup canvasGroup;
    private bool runtimeGenerated = false;
    private int fadeVersion = 0;

    void Awake()
    {
        runtimeGenerated = creatingRuntimeInstance;

        if (instance != null && instance != this)
        {
            if (instance.runtimeGenerated)
            {
                Destroy(instance.gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        Instance = this;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = runtimeGenerated ? 0f : 1f;
        if (runtimeGenerated)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        else
        {
            StartFadeToClear();
        }
        EnsureRectCoversScreen();
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartFadeToClear();
    }

    public void FadeOutAndLoad(string sceneName)
    {
        StartCoroutine(FadeOutThenLoad(sceneName));
    }

    IEnumerator FadeOutThenLoad(string sceneName)
    {
        yield return Fade(0f, 1f);
        SceneManager.LoadScene(sceneName);
    }

    IEnumerator Fade(float from, float to)
    {
        if (canvasGroup == null)
            yield break;

        int token = ++fadeVersion;

        canvasGroup.alpha = from;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, fadeDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = fadeCurve.Evaluate(t);
            canvasGroup.alpha = Mathf.Lerp(from, to, eased);
            if (token != fadeVersion)
                yield break;
            yield return null;
        }
        canvasGroup.alpha = to;

        if (Mathf.Approximately(to, 0f))
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    void OnDestroy()
    {
        if (instance == this)
            Instance = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void StartFadeToClear()
    {
        if (canvasGroup == null)
            return;
        StartCoroutine(Fade(1f, 0f));
    }

    static SceneFadeController FindExistingInstance()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<SceneFadeController>(FindObjectsInactive.Include);
#else
        return FindObjectOfType<SceneFadeController>(true);
#endif
    }

    static SceneFadeController CreateRuntimeInstance()
    {
        creatingRuntimeInstance = true;
        var go = new GameObject("SceneFadeController (Auto)", typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        go.AddComponent<GraphicRaycaster>();

        var image = go.AddComponent<Image>();
        image.color = Color.black;

        var controller = go.AddComponent<SceneFadeController>();
        creatingRuntimeInstance = false;
        return controller;
    }

    void EnsureRectCoversScreen()
    {
        var rect = transform as RectTransform;
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (canvas.sortingOrder < short.MaxValue / 2)
                canvas.sortingOrder = short.MaxValue;
        }

        var image = GetComponent<Image>();
        if (image != null && image.color.a <= 0f)
            image.color = Color.black;
    }
}
