using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class button : MonoBehaviour
{
    [Header("Load Settings")]
    [Tooltip("Name of scene to load when this button is pressed. Add the scene to Build Settings.")]
    public string sceneToLoad;

    [Tooltip("Optional delay (seconds) before loading the scene. Useful to let SFX play.")]
    public float loadDelay = 0.18f;

    [Header("Optional SFX")]
    public AudioSource sfxSource;
    public AudioClip clickClip;
    [Range(0f,1f)] public float sfxVolume = 1f;

    [Header("Auto-wire")]
    [Tooltip("If true, the script will find a Button component on this GameObject (or children) and wire OnClick to LoadTargetScene.")]
    public bool autoWireButton = true;
    [Tooltip("If true, existing OnClick listeners will be removed when auto-wiring.")]
    public bool replaceExistingListeners = true;

    [Header("GameManager Integration")]
    [Tooltip("If true, will notify GameManager that a mini game has been completed instead of loading sceneToLoad directly.")]
    public bool notifyGameManager = false;
    public MiniGame miniGame = MiniGame.None;
    [Tooltip("If true, GameManager.ResetProgress() will be called before loading the scene (useful for play again buttons).")]
    public bool resetGameManager = false;

    void Awake()
    {
        if (!autoWireButton) return;

        var btn = GetComponentInChildren<UnityEngine.UI.Button>(true);
        if (btn == null)
        {
            Debug.LogWarning("button: autoWireButton is enabled but no Button component found on or under " + gameObject.name);
            return;
        }

        if (replaceExistingListeners)
        {
            btn.onClick.RemoveAllListeners();
        }

        btn.onClick.AddListener(LoadTargetScene);
        Debug.Log("button: auto-wired LoadTargetScene to Button '" + btn.name + "' on " + gameObject.name);
    }

    // Public method you can assign to the Button onClick in the Inspector
    public void LoadTargetScene()
    {
        Debug.Log("button: LoadTargetScene invoked on " + gameObject.name);
        StartCoroutine(LoadSceneCoroutine());
    }

    IEnumerator LoadSceneCoroutine()
    {
        if (clickClip != null)
        {
            if (sfxSource == null)
            {
                // try to auto-find a non-looping audio source
                var srcs = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
                foreach (var s in srcs)
                {
                    if (!s.loop)
                    {
                        sfxSource = s;
                        break;
                    }
                }
                if (sfxSource == null && srcs.Length > 0) sfxSource = srcs[0];
            }

            if (sfxSource != null)
                sfxSource.PlayOneShot(clickClip, sfxVolume);
            else
                Debug.LogWarning("button: no AudioSource found to play clickClip");
        }

        yield return new WaitForSecondsRealtime(loadDelay);

        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogWarning("button: sceneToLoad is empty. Set the target scene in the Inspector.");
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneToLoad))
        {
            Debug.LogError("button: scene not in Build Settings: " + sceneToLoad);
        }

        var gm = GameManager.Instance;

        if (resetGameManager && gm != null)
        {
            gm.ResetProgress();
        }

        if (notifyGameManager)
        {
            if (gm == null)
            {
                Debug.LogWarning("button: notifyGameManager is enabled but no GameManager is present.");
            }
            else
            {
                MiniGame target = miniGame != MiniGame.None ? miniGame : GuessMiniGameFromScene(sceneToLoad);
                if (target == MiniGame.None)
                {
                    Debug.LogWarning("button: notifyGameManager is enabled but miniGame is None and could not be inferred from sceneToLoad.");
                }
                else
                {
                    gm.CompleteMiniGame(target);
                    yield break;
                }
            }
        }

        var controller = SceneFadeController.Instance;
        if (controller != null)
            controller.FadeOutAndLoad(sceneToLoad);
        else
            SceneManager.LoadScene(sceneToLoad);
    }

    MiniGame GuessMiniGameFromScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return MiniGame.None;
        if (sceneName == GameManager.BATH_SCENE || sceneName == "BathScene") return MiniGame.Bath;
        if (sceneName == GameManager.FOOD_SCENE || sceneName == "FoodScene") return MiniGame.Food;
        return MiniGame.None;
    }
}
