using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Tap : MonoBehaviour
{
    [Header("Assign these in the Inspector")]
    public GameObject bye_girl;
    public GameObject bye_dog;
    public GameObject play_again;

    private int tapStage = 0; // 0 = nothing, 1 = options, 2 = play_again

    [Header("Animation & Vibe")]
    [Tooltip("Fade/pop duration when showing/hiding objects")]
    public float vibeDuration = 0.24f;
    [Tooltip("Scale multiplier for the pop effect")]
    public float popScale = 1.08f;
    [Tooltip("Stagger delay (seconds) between showing multiple items")]
    public float stagger = 0.06f;

    [Header("Audio (optional)")]
    public AudioSource sfxSource;
    public AudioClip appearClip;     // when options appear
    public AudioClip revealClip;     // when play_again appears
    public AudioClip clickClip;      // when pressing play_again
    [Range(0f,1f)] public float sfxVolume = 1f;

    [Header("Debug")]
    [Tooltip("Enable debug logs for tap events and state changes.")]
    public bool debugLogs = false;

    [Header("Play Again Flow")]
    [Tooltip("If true, play_again loads a specific scene (defaults to Main) and resets GameManager.")]
    public bool useGameManagerOnPlayAgain = false;
    [Tooltip("Scene to load when useGameManagerOnPlayAgain is true.")]
    public string playAgainSceneName = GameManager.MAIN_SCENE;

    void Start()
    {
        // Make sure everything starts hidden
        SetActiveSafe(bye_girl, false);
        SetActiveSafe(bye_dog, false);
        SetActiveSafe(play_again, false);

        // Add reload functionality to play_again
        if (play_again != null)
        {
            Button btn = play_again.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    if (debugLogs) Debug.Log("TapCatcher: play_again clicked");
                    PlaySfx(clickClip);
                    if (useGameManagerOnPlayAgain)
                    {
                        string target = string.IsNullOrEmpty(playAgainSceneName) ? GameManager.MAIN_SCENE : playAgainSceneName;
                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.ResetProgress();
                        }
                        SceneManager.LoadScene(target);
                    }
                    else
                    {
                        var scene = SceneManager.GetActiveScene();
                        SceneManager.LoadScene(scene.buildIndex);
                    }
                });
            }
        }
    }

    public void OnTap()
    {
        if (tapStage == 0)
        {
            // First tap → show the two option buttons
            // Show options with vibe
            StartCoroutine(ShowSequence(new GameObject[] { bye_girl, bye_dog }, appearClip));
            tapStage = 1;
        }
        else if (tapStage == 1)
        {
            // Second tap → hide options and show play_again
            // Hide options then reveal play_again
            StartCoroutine(HideThenReveal(new GameObject[] { bye_girl, bye_dog }, play_again, revealClip));
            tapStage = 2;
        }
    }

    IEnumerator ShowSequence(GameObject[] gos, AudioClip clip)
    {
        if (gos == null || gos.Length == 0) yield break;
        PlaySfx(clip);
        for (int i = 0; i < gos.Length; i++)
        {
            var go = gos[i];
            if (go == null) continue;
            SetActiveSafe(go, true);
            yield return StartCoroutine(ShowSmooth(go, vibeDuration, popScale));
            if (stagger > 0f) yield return new WaitForSecondsRealtime(stagger);
        }
    }

    IEnumerator HideThenReveal(GameObject[] toHide, GameObject reveal, AudioClip revealClip)
    {
        // hide all
        if (toHide != null)
        {
            for (int i = 0; i < toHide.Length; i++)
            {
                var go = toHide[i];
                if (go == null) continue;
                yield return StartCoroutine(HideSmooth(go, vibeDuration));
                SetActiveSafe(go, false);
            }
        }

        // reveal play_again
        if (reveal != null)
        {
            SetActiveSafe(reveal, true);
            // ensure it's rendered above other UI and is interactable
            var rt = reveal.GetComponent<RectTransform>();
            if (rt != null) rt.SetAsLastSibling();
            var btn = reveal.GetComponent<Button>();
            if (btn != null) btn.interactable = true;
            Debug.Log("TapCatcher: revealed " + reveal.name + " and brought to front");
            PlaySfx(revealClip);
            yield return StartCoroutine(ShowSmooth(reveal, vibeDuration, popScale));
        }
    }

    IEnumerator ShowSmooth(GameObject go, float dur, float targetScale)
    {
        if (go == null) yield break;
        // prepare base
        Vector3 baseScale = go.transform.localScale;
        float startS = 0.92f, endS = targetScale;

        // ensure CanvasGroup or renderer exists
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();

        if (cg == null && img == null && sr == null)
        {
            // add CanvasGroup for UI elements if none
            cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
        }

        if (cg != null) cg.alpha = 0f;
        else if (img != null) { var c = img.color; c.a = 0f; img.color = c; }
        else if (sr != null) { var c = sr.color; c.a = 0f; sr.color = c; }

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float s = Mathf.Lerp(startS, endS, u);
            go.transform.localScale = baseScale * s;
            if (cg != null) cg.alpha = u;
            else if (img != null) { var c = img.color; c.a = u; img.color = c; }
            else if (sr != null) { var c = sr.color; c.a = u; sr.color = c; }
            yield return null;
        }
        go.transform.localScale = baseScale * endS;
        if (cg != null) cg.alpha = 1f;
    }

    IEnumerator HideSmooth(GameObject go, float dur)
    {
        if (go == null) yield break;
        Vector3 baseScale = go.transform.localScale;
        float startS = 1f, endS = 0.92f;

        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        UnityEngine.UI.Image img = go.GetComponent<UnityEngine.UI.Image>();
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float s = Mathf.Lerp(startS, endS, u);
            go.transform.localScale = baseScale * s;
            float alpha = 1f - u;
            if (cg != null) cg.alpha = alpha;
            else if (img != null) { var c = img.color; c.a = alpha; img.color = c; }
            else if (sr != null) { var c = sr.color; c.a = alpha; sr.color = c; }
            yield return null;
        }
        if (cg != null) cg.alpha = 0f;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        if (sfxSource == null)
        {
            var srcs = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (var s in srcs)
            {
                if (!s.loop && !s.gameObject.name.ToLower().Contains("music"))
                {
                    sfxSource = s;
                    break;
                }
            }
            if (sfxSource == null && srcs.Length > 0) sfxSource = srcs[0];
        }
        if (sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, sfxVolume);
        }
    }

    private void RestartScene()
    {
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    private void SetActiveSafe(GameObject go, bool state)
    {
        if (go != null && go.activeSelf != state)
            go.SetActive(state);
    }
}
