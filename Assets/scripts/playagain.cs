using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;

public class PlayAgainButton : MonoBehaviour
{
    [Header("Auto-wire")]
    public bool autoWire = true;
    public bool replaceExistingListeners = true;
    [Header("Behaviour")]
    [Tooltip("Delay before reloading the scene (seconds). Useful to let click SFX play.")]
    public float reloadDelay = 0.12f;
    public enum ReloadMode { CurrentScene, SceneName, BuildIndex }
    [Tooltip("Which scene to load when the button is pressed.")]
    public ReloadMode reloadMode = ReloadMode.CurrentScene;
    [Tooltip("If ReloadMode is SceneName, use this name (must be added to Build Settings).")]
    public string sceneName = "";
    [Tooltip("If ReloadMode is BuildIndex, use this build index.")]
    public int sceneBuildIndex = 0;
    [Header("Debug")]
    public bool debugLogs = true;

    [Header("GameManager Integration")]
    [Tooltip("If true, will notify GameManager that a mini game finished instead of loading via reloadMode.")]
    public bool notifyGameManager = false;
    public MiniGame miniGame = MiniGame.None;
    [Tooltip("If true, GameManager.ResetProgress() is called before loading. Use on Play Again buttons.")]
    public bool resetGameManager = false;

    private Button wiredButton;

    void OnEnable()
    {
        if (!autoWire) return;

        // Find a Button on this GameObject or children (works even if the object was inactive at Start)
        wiredButton = GetComponent<Button>();
        if (wiredButton == null)
            wiredButton = GetComponentInChildren<Button>(true);

        if (wiredButton == null)
        {
            if (debugLogs) Debug.LogWarning("PlayAgainButton: No Button component found to wire on " + gameObject.name);
            return;
        }

        if (replaceExistingListeners)
            wiredButton.onClick.RemoveAllListeners();

        wiredButton.onClick.AddListener(OnClicked);
        wiredButton.interactable = true;
        if (debugLogs) Debug.Log("PlayAgainButton: wired OnClicked to Button '" + wiredButton.name + "'");
    }

    void OnDisable()
    {
        if (wiredButton != null)
        {
            wiredButton.onClick.RemoveListener(OnClicked);
            wiredButton = null;
        }
    }

    private void OnClicked()
    {
        if (debugLogs) Debug.Log("PlayAgainButton: clicked - reloading scene in " + reloadDelay + "s");
        StartCoroutine(ReloadAfterDelay(reloadDelay));
    }

    IEnumerator ReloadAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        var gm = GameManager.Instance;

        if (resetGameManager && gm != null)
        {
            gm.ResetProgress();
        }

        if (notifyGameManager)
        {
            if (gm == null)
            {
                Debug.LogWarning("PlayAgainButton: notifyGameManager is enabled but no GameManager exists.");
            }
            else
            {
                MiniGame target = miniGame != MiniGame.None ? miniGame : GuessMiniGameFromReloadTarget();
                if (target == MiniGame.None)
                {
                    Debug.LogWarning("PlayAgainButton: notifyGameManager is enabled but miniGame is None and could not be inferred.");
                }
                else
                {
                    gm.CompleteMiniGame(target);
                    yield break;
                }
            }
        }

        switch (reloadMode)
        {
            case ReloadMode.CurrentScene:
                var scene = SceneManager.GetActiveScene();
                if (SceneFadeController.Instance != null)
                    SceneFadeController.Instance.FadeOutAndLoad(scene.name);
                else
                    SceneManager.LoadScene(scene.buildIndex);
                break;
            case ReloadMode.SceneName:
                if (string.IsNullOrEmpty(sceneName))
                {
                    Debug.LogError("PlayAgainButton: reloadMode is SceneName but sceneName is empty.");
                }
                else
                {
                    if (!Application.CanStreamedLevelBeLoaded(sceneName))
                        Debug.LogWarning("PlayAgainButton: scene '" + sceneName + "' not found in Build Settings.");
                    if (SceneFadeController.Instance != null)
                        SceneFadeController.Instance.FadeOutAndLoad(sceneName);
                    else
                        SceneManager.LoadScene(sceneName);
                }
                break;
            case ReloadMode.BuildIndex:
                if (sceneBuildIndex < 0)
                {
                    Debug.LogError("PlayAgainButton: invalid sceneBuildIndex: " + sceneBuildIndex);
                }
                else
                {
                    if (SceneFadeController.Instance != null)
                    {
                        string name = SceneUtility.GetScenePathByBuildIndex(sceneBuildIndex);
                        if (!string.IsNullOrEmpty(name))
                        {
                            string sceneNameOnly = System.IO.Path.GetFileNameWithoutExtension(name);
                            SceneFadeController.Instance.FadeOutAndLoad(sceneNameOnly);
                        }
                        else
                        {
                            SceneManager.LoadScene(sceneBuildIndex);
                        }
                    }
                    else
                    {
                        SceneManager.LoadScene(sceneBuildIndex);
                    }
                }
                break;
        }
    }

    // Diagnostic helper: call from Console or inspector to check why the button may not be clickable
    [ContextMenu("Check Button Clickable")] 
    public void CheckClickable()
    {
        if (wiredButton == null)
        {
            Debug.LogWarning("PlayAgainButton: no wired Button to check");
            return;
        }

        Debug.Log("PlayAgainButton: wiredButton.interactable=" + wiredButton.interactable);
        // walk up parents to find any CanvasGroup that might block
        var cg = wiredButton.GetComponentInParent<CanvasGroup>();
        if (cg != null)
        {
            Debug.Log("PlayAgainButton: nearest CanvasGroup - blocksRaycasts=" + cg.blocksRaycasts + ", interactable=" + cg.interactable + ", alpha=" + cg.alpha);
        }
        else
        {
            Debug.Log("PlayAgainButton: no CanvasGroup on parents");
        }
    }

    MiniGame GuessMiniGameFromReloadTarget()
    {
        switch (reloadMode)
        {
            case ReloadMode.SceneName:
                return GuessMiniGameFromSceneName(sceneName);
            case ReloadMode.BuildIndex:
                if (sceneBuildIndex >= 0 && sceneBuildIndex < SceneManager.sceneCountInBuildSettings)
                {
                    string path = SceneUtility.GetScenePathByBuildIndex(sceneBuildIndex);
                    string name = System.IO.Path.GetFileNameWithoutExtension(path);
                    return GuessMiniGameFromSceneName(name);
                }
                break;
            case ReloadMode.CurrentScene:
                var scene = SceneManager.GetActiveScene();
                return GuessMiniGameFromSceneName(scene.name);
        }
        return MiniGame.None;
    }

    MiniGame GuessMiniGameFromSceneName(string candidate)
    {
        if (string.IsNullOrEmpty(candidate)) return MiniGame.None;
        if (candidate == GameManager.BATH_SCENE || candidate == "BathScene") return MiniGame.Bath;
        if (candidate == GameManager.FOOD_SCENE || candidate == "FoodScene") return MiniGame.Food;
        return MiniGame.None;
    }
}
