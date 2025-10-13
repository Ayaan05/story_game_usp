using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class DogCleanliness : MonoBehaviour
{
    // ===== Sprite workflow (optional) =====
    [Header("Sprite Mode (optional)")]
    public bool useSpriteMasks = false;
    public SpriteRenderer cleanSprite;
    public SpriteRenderer dirtySprite;
    public Transform maskContainer;
    public bool autoCollectMasks = true;
    public List<SpriteMask> revealMasks = new List<SpriteMask>();
    public int popsPerMask = 2;
    public GameObject sparklesParent;

    // ===== Video blend workflow =====
    [Header("Video Blend Mode")]
    public bool useVideoBlend = true;
    public Material dogBlendMat;               // Shader has _Cleanliness
    public GameObject blendedRawImageGO;       // RawImage using dogBlendMat

    [Header("Idle Video Players (same motion)")]
    public VideoPlayer dirtyIdlePlayer;        // -> DirtyDogRT
    public VideoPlayer cleanIdlePlayer;        // -> CleanDogRT
    public bool playIdleOnlyOnFirstPop = true;

    [Header("Happy / Celebration")]
    public GameObject happyDogRawImageGO;      // RawImage for happy video (inactive at start)
    public VideoPlayer happyPlayer;            // celebration player

    [Header("Progress")]
    public int totalBubbles = 24;
    public int popped = 0;

    // ▶ PlayAgainButton integration
    [Header("Scene Transition (via PlayAgainButton)")]
    [Tooltip("If true, DogCleanliness will call PlayAgainButton.TriggerReloadExternal() instead of loading scenes directly.")]
    public bool usePlayAgainButton = true;
    [Tooltip("Assign the PlayAgainButton that your screen uses. If left null, we will try to FindObjectOfType at runtime.")]
    public PlayAgainButton playAgainButton;
    [Tooltip("Extra delay before triggering PlayAgainButton after happy video ends.")]
    public float postHappyDelay = 0.0f;
    [Tooltip("If no happy video, delay before triggering PlayAgainButton on completion.")]
    public float immediateCompleteDelay = 0.0f;

    // fallback (legacy) if you decide not to use PlayAgainButton
    [Header("Legacy Fallback (only used if usePlayAgainButton = false)")]
    public string mainMenuScene = "MainMenu";

    // internal
    static readonly int CleanlinessID = Shader.PropertyToID("_Cleanliness");
    bool _idleStarted, _completed;

    void Awake()
    {
        // Sprite mode init
        if (useSpriteMasks)
        {
            if (cleanSprite) cleanSprite.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            if (dirtySprite) dirtySprite.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;

            if (autoCollectMasks) CollectMasks();
            SetActiveMaskCount(0);
        }
        if (sparklesParent) sparklesParent.SetActive(false);

        // Video blend init
        if (useVideoBlend)
        {
            if (happyDogRawImageGO) happyDogRawImageGO.SetActive(false);
            if (blendedRawImageGO) blendedRawImageGO.SetActive(true);

            ApplyCleanlinessToMaterial(0f);

            PrepPause(dirtyIdlePlayer);
            PrepPause(cleanIdlePlayer);

            if (happyPlayer) happyPlayer.playOnAwake = false;
        }

        // ▶ Auto-find PlayAgainButton if not assigned
        if (usePlayAgainButton && playAgainButton == null)
            playAgainButton = FindObjectOfType<PlayAgainButton>();
    }

    void CollectMasks()
    {
        revealMasks.Clear();
        if (!maskContainer)
        {
            var t = transform.Find("MaskContainer");
            if (t) maskContainer = t;
        }
        if (maskContainer)
        {
            var masks = maskContainer.GetComponentsInChildren<SpriteMask>(true);
            revealMasks.AddRange(masks);
            foreach (var m in revealMasks) if (m) m.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("DogCleanliness: No maskContainer assigned/found.");
        }
    }

    void PrepPause(VideoPlayer vp)
    {
        if (!vp) return;
        vp.playOnAwake = false;
        vp.isLooping = false;
        vp.waitForFirstFrame = true;
        vp.prepareCompleted += OnPreparedPauseFirstFrame;
        vp.Prepare();
    }

    void OnPreparedPauseFirstFrame(VideoPlayer vp)
    {
        vp.prepareCompleted -= OnPreparedPauseFirstFrame;
        vp.time = 0.0;
        vp.Play();
        vp.Pause();
    }

    // ========= External API =========

    public void SetTotal(int total)
    {
        totalBubbles = Mathf.Max(1, total);
        UpdateReveal();
        UpdateCleanlinessFromProgress();
    }

    public void ReportPopped(int poppedCount)
    {
        popped = Mathf.Clamp(poppedCount, 0, totalBubbles);

        if (useVideoBlend && playIdleOnlyOnFirstPop && !_idleStarted && popped > 0)
            StartIdlePlayback();

        UpdateReveal();
        UpdateCleanlinessFromProgress();
    }

    public void AllClean()
    {
        popped = totalBubbles;
        if (useSpriteMasks) SetActiveMaskCount(revealMasks.Count);

        if (sparklesParent) sparklesParent.SetActive(true);
        if (dirtySprite) { var c = dirtySprite.color; c.a = 0f; dirtySprite.color = c; }

        if (useVideoBlend) CompleteAndCelebrate();
        else TriggerSceneTransition(); // fallback if only sprites used
    }

    // =================================

    void UpdateReveal()
    {
        if (useSpriteMasks)
        {
            int maskCount = revealMasks.Count;
            if (maskCount == 0 || popsPerMask <= 0) return;

            int active = Mathf.Min(popped / popsPerMask, maskCount);
            SetActiveMaskCount(active);
        }
    }

    void SetActiveMaskCount(int k)
    {
        for (int i = 0; i < revealMasks.Count; i++)
        {
            var m = revealMasks[i];
            if (!m) continue;
            bool on = (i < k);
            if (m.gameObject.activeSelf != on) m.gameObject.SetActive(on);
        }
    }

    void UpdateCleanlinessFromProgress()
    {
        if (!useVideoBlend) return;

        float t = Mathf.Clamp01(totalBubbles > 0 ? (float)popped / totalBubbles : 0f);
        ApplyCleanlinessToMaterial(t);

        if (!_idleStarted && !playIdleOnlyOnFirstPop && t > 0f)
            StartIdlePlayback();

        if (t >= 1f && !_completed)
            CompleteAndCelebrate();
    }

    void ApplyCleanlinessToMaterial(float v01)
    {
        if (dogBlendMat) dogBlendMat.SetFloat(CleanlinessID, Mathf.Clamp01(v01));
    }

    void StartIdlePlayback()
    {
        _idleStarted = true;
        if (dirtyIdlePlayer && !dirtyIdlePlayer.isPlaying) dirtyIdlePlayer.Play();
        if (cleanIdlePlayer && !cleanIdlePlayer.isPlaying) cleanIdlePlayer.Play();
    }

    void CompleteAndCelebrate()
    {
        if (_completed) return;
        _completed = true;

        // stop idle
        if (dirtyIdlePlayer) dirtyIdlePlayer.Stop();
        if (cleanIdlePlayer) cleanIdlePlayer.Stop();

        // swap UI: hide blended, show happy
        if (blendedRawImageGO) blendedRawImageGO.SetActive(false);
        if (happyDogRawImageGO) happyDogRawImageGO.SetActive(true);

        // play happy and trigger transition at end
        if (happyPlayer)
        {
            happyPlayer.isLooping = false;
            happyPlayer.time = 0.0;
            happyPlayer.loopPointReached += OnHappyEnded;
            happyPlayer.Play();
        }
        else
        {
            // no happy video — trigger transition immediately (with optional delay)
            Invoke(nameof(TriggerSceneTransition), Mathf.Max(0f, immediateCompleteDelay));
        }
    }

    void OnHappyEnded(VideoPlayer vp)
    {
        vp.loopPointReached -= OnHappyEnded;
        if (postHappyDelay > 0f)
            Invoke(nameof(TriggerSceneTransition), postHappyDelay);
        else
            TriggerSceneTransition();
    }

    // ▶ Centralized scene transition using PlayAgainButton if available
    void TriggerSceneTransition()
    {
        if (usePlayAgainButton && playAgainButton != null)
        {
            // Trigger the exact same path as tapping the full-screen Play Again button
            playAgainButton.TriggerReloadExternal(skipDelay: true);
        }
        else
        {
            // Fallback legacy path (only used if you opt out of PlayAgainButton)
            if (!string.IsNullOrEmpty(mainMenuScene))
                SceneManager.LoadScene(mainMenuScene);
            else
                Debug.LogWarning("DogCleanliness: No PlayAgainButton assigned and no mainMenuScene set.");
        }
    }
}
