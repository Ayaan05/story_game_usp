using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
// testing

public class DoghouseSequence : MonoBehaviour, IPointerClickHandler
{

    private enum Step { Closed, Opened, SaidHi, AskedHow, ChooseFeeling, Done }

    [Header("Sprites")]
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openSprite;

    [Header("Scene Refs")]
    [SerializeField] private Canvas uiCanvas;           // Your UI Canvas
    [SerializeField] private Transform dogTransform;    // If null, will use this.transform
    [SerializeField] private Transform girlTransform;   // Optional; if null, uses this.transform

    [Header("UI Prefabs (RectTransform with Image/TMP)")]
    [SerializeField] private RectTransform bubbleDogHiPrefab;     // blue "HI"
    [SerializeField] private RectTransform bubbleGirlHiPrefab;    // orange "HI"
    [SerializeField] private RectTransform bubbleGirlHowPrefab;   // "HOW ARE YOU?"

    [Header("Choice Buttons (SCENE objects under Canvas, disabled at start)")]
    [SerializeField] private Button hungryButton;   // Button (TMP) in scene under Canvas
    [SerializeField] private Button stinkyButton;   // Button (TMP) in scene under Canvas

    [Header("Scenes To Load")]
    [SerializeField] private string hungrySceneName = "FoodScene";
    [SerializeField] private string stinkySceneName = "BathScene";

    [Header("World Offsets (relative to target in WORLD units)")]
    [SerializeField] private Vector2 dogBubbleOffset = new Vector2(0f, 2.0f);
    [SerializeField] private Vector2 girlBubbleOffset = new Vector2(0f, 2.1f);
    [SerializeField] private Vector2 hungryBtnOffset = new Vector2(-0.6f, 2.4f);
    [SerializeField] private Vector2 stinkyBtnOffset = new Vector2(0.4f, 2.4f);

    [Header("Animation")]
    [SerializeField, Range(0.05f, 0.6f)] private float fadeTime = 0.18f;
    [SerializeField, Range(0.75f, 1.25f)] private float popScale = 1.0f;


    // === Audio ===
    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;        // drag the Audio Source here
    [SerializeField] private AudioClip doorOpenClip;        // opening-door-411632
    [SerializeField] private AudioClip textDingClip;        // ding-402325 (HI + HOW)
    [SerializeField] private AudioClip optionsAppearClip;   // pen-click-2-411631
    [SerializeField] private AudioClip optionClickClip;     // optional: pen-click-2-411631
    [SerializeField, Range(0f,1f)] private float sfxVolume = 1f;

    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
            sfxSource.PlayOneShot(clip, sfxVolume);
    }

    



    // internals
    private SpriteRenderer sr;
    private Step step = Step.Closed;

    // Track spawned bubbles to clear on next click
    private readonly List<GameObject> activeBubbles = new List<GameObject>();

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        // Fit collider to current sprite, so clicks register
        var box = GetComponent<BoxCollider2D>();
        if (sr.sprite != null)
        {
            var b = sr.sprite.bounds;
            box.size = b.size;
            box.offset = b.center;
        }

        // Defaults if not assigned
        if (!dogTransform) dogTransform = transform;
        if (!girlTransform) girlTransform = transform;

        // Try to auto-find a Canvas if not assigned to make setup more forgiving
        if (!uiCanvas)
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            if (canvases.Length == 1)
            {
                uiCanvas = canvases[0];
                Debug.Log("DoghouseSequence: auto-assigned uiCanvas to " + uiCanvas.name);
            }
            else if (canvases.Length > 1)
            {
                Debug.LogWarning("DoghouseSequence: multiple Canvases found; please assign the correct Canvas to uiCanvas in the Inspector.");
            }
            else
            {
                Debug.LogWarning("DoghouseSequence: no Canvas found in scene; UI bubbles will not be spawned until uiCanvas is assigned.");
            }
        }

        // Start closed sprite
        if (closedSprite) sr.sprite = closedSprite;
        step = Step.Closed;

        // Hide buttons at start + wire
        if (hungryButton)
        {
            hungryButton.gameObject.SetActive(false);
            hungryButton.onClick.RemoveAllListeners();
            hungryButton.onClick.AddListener(OnHungry);
        }
        if (stinkyButton)
        {
            stinkyButton.gameObject.SetActive(false);
            stinkyButton.onClick.RemoveAllListeners();
            stinkyButton.onClick.AddListener(OnStinky);
        }
    }

    public void OnPointerClick(PointerEventData e)
    {
        // Only react if THIS object was actually hit
        if (e.pointerCurrentRaycast.gameObject != gameObject) return;

        switch (step)
        {
            case Step.Closed:
                OpenHouse();
                step = Step.Opened;
                break;

            case Step.Opened:
                SayHi();
                step = Step.SaidHi;
                break;

            case Step.SaidHi:
                AskHow();
                step = Step.AskedHow;
                break;

            case Step.AskedHow:
                ShowFeelingChoices();
                step = Step.ChooseFeeling;
                break;

            case Step.ChooseFeeling:
                // Waiting for a button click
                break;

            case Step.Done:
                break;
        }
    }

    // ====== Sequence actions ======

    void OpenHouse()
    {
        if (openSprite) sr.sprite = openSprite;
        // lil' pop on open
        PlaySfx(doorOpenClip);  // 1st click: door opens
        StartCoroutine(SpritePop(sr.transform, fadeTime));
    }

    void SayHi()
    {
        StartCoroutine(ClearBubblesSmooth()); // remove previous (if any)
        SpawnBubbleOver(girlTransform, bubbleGirlHiPrefab, girlBubbleOffset);
        SpawnBubbleOver(dogTransform, bubbleDogHiPrefab, dogBubbleOffset);
        PlaySfx(textDingClip); // 2nd click: HI bubbles
    }

    void AskHow()
    {
        StartCoroutine(ClearBubblesSmooth());
        SpawnBubbleOver(girlTransform, bubbleGirlHowPrefab, girlBubbleOffset);
        PlaySfx(textDingClip); // 3rd click: HOW text
    }

    void ShowFeelingChoices()
    {
        StartCoroutine(ClearBubblesSmooth());

        // Place above dog and fade+pop in
        if (hungryButton)
        {
            var rt = hungryButton.transform as RectTransform;
            PlaceUIOverWorld(rt, dogTransform, hungryBtnOffset);
            hungryButton.gameObject.SetActive(true);
            StartCoroutine(FadeAndPopIn(rt, fadeTime));
        }

        if (stinkyButton)
        {
            var rt = stinkyButton.transform as RectTransform;
            PlaceUIOverWorld(rt, dogTransform, stinkyBtnOffset);
            stinkyButton.gameObject.SetActive(true);
            StartCoroutine(FadeAndPopIn(rt, fadeTime));
        }
        PlaySfx(optionsAppearClip); // 4th click: options cloud appears
    }

    void OnHungry()
    {
        PlaySfx(optionClickClip);   // optional click sound
        if (!string.IsNullOrEmpty(hungrySceneName))
            SceneManager.LoadScene(hungrySceneName);
        step = Step.Done;
    }

    void OnStinky()
    {
        PlaySfx(optionClickClip);   // optional click sound
        if (!string.IsNullOrEmpty(stinkySceneName))
            SceneManager.LoadScene(stinkySceneName);
        step = Step.Done;
    }

    // ====== Bubble helpers ======

    void SpawnBubbleOver(Transform target, RectTransform bubblePrefab, Vector2 worldOffset)
    {
        if (!target)
        {
            Debug.LogWarning("SpawnBubbleOver: target is null");
            return;
        }
        if (!bubblePrefab)
        {
            Debug.LogWarning("SpawnBubbleOver: bubblePrefab is null");
            return;
        }
        if (!uiCanvas)
        {
            Debug.LogWarning("SpawnBubbleOver: uiCanvas is not assigned on " + gameObject.name);
            return;
        }

        RectTransform inst = Instantiate(bubblePrefab, uiCanvas.transform);
        if (!inst)
        {
            Debug.LogError("SpawnBubbleOver: failed to Instantiate bubble prefab");
            return;
        }
        inst.gameObject.SetActive(true);
        // ensure it has a CanvasGroup so we can fade
        var cg = EnsureCanvasGroup(inst.gameObject);
        cg.alpha = 0f;
        // Respect prefab's base scale and apply a slight shrink relative to it
        inst.localScale = inst.localScale * 0.88f;

        PlaceUIOverWorld(inst, target, worldOffset);

        // debug: verify anchored position
        if (inst != null)
        {
            Debug.Log($"SpawnBubbleOver: spawned {inst.name} at anchoredPosition {inst.anchoredPosition} (canvas: {uiCanvas.name})");
        }

        activeBubbles.Add(inst.gameObject);
        // fade+pop in
        StartCoroutine(FadeAndPopIn(inst, fadeTime));
    }

    IEnumerator ClearBubblesSmooth()
    {
        if (activeBubbles.Count == 0) yield break;

        // fade out all, then destroy
        List<GameObject> toKill = new List<GameObject>(activeBubbles);
        activeBubbles.Clear();

        float t = 0f;
        Dictionary<GameObject, CanvasGroup> groups = new Dictionary<GameObject, CanvasGroup>();
        foreach (var go in toKill)
        {
            if (!go) continue;
            var cg = EnsureCanvasGroup(go);
            groups[go] = cg;
        }

        // remember each object's starting scale so we lerp *relative* to it
        Dictionary<GameObject, Vector3> startScales = new Dictionary<GameObject, Vector3>();
        foreach (var go in toKill)
        {
            if (!go) continue;
            startScales[go] = go.transform.localScale;
        }

        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / fadeTime);
            float s = Mathf.Lerp(1f, 0.88f, Mathf.Clamp01(t / fadeTime));
            foreach (var kv in groups)
            {
                if (!kv.Key) continue;
                kv.Value.alpha = a;
                if (startScales.TryGetValue(kv.Key, out var baseScale))
                    kv.Key.transform.localScale = baseScale * s;
                else
                    kv.Key.transform.localScale = Vector3.one * s;
            }
            yield return null;
        }

        foreach (var go in toKill)
        {
            if (go) Destroy(go);
        }
    }

    // ====== Positioning & animation utils ======

    void PlaceUIOverWorld(RectTransform uiRect, Transform worldTarget, Vector2 worldOffset)
    {
        if (!uiRect)
        {
            Debug.LogWarning("PlaceUIOverWorld: uiRect is null");
            return;
        }
        if (!worldTarget)
        {
            Debug.LogWarning("PlaceUIOverWorld: worldTarget is null");
            return;
        }
        if (!uiCanvas)
        {
            Debug.LogWarning("PlaceUIOverWorld: uiCanvas is not assigned");
            return;
        }

        // worldOffset is in WORLD units (same as sprites)
        Vector3 world = worldTarget.position + new Vector3(worldOffset.x, worldOffset.y, 0f);

        // choose camera: if overlay canvas, no camera; otherwise prefer canvas.worldCamera, fallback to Camera.main
        Camera cam = uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : (uiCanvas.worldCamera != null ? uiCanvas.worldCamera : Camera.main);
        if (uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay && cam == null)
        {
            Debug.LogWarning("PlaceUIOverWorld: canvas is not overlay and no camera found (assign Canvas.worldCamera or ensure Camera.main exists)");
        }

        Vector2 screen;
        if (cam != null)
            screen = cam.WorldToScreenPoint(world);
        else if (Camera.main != null)
            screen = Camera.main.WorldToScreenPoint(world);
        else
        {
            Debug.LogError("PlaceUIOverWorld: No camera available to convert world to screen point");
            return;
        }

        RectTransform canvasRect = uiCanvas.transform as RectTransform;
        bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screen,
            uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
            out Vector2 local
        );

        if (!ok)
        {
            // Fallback: use viewport coordinates and map to canvas size
            Debug.LogWarning($"PlaceUIOverWorld: Screen->Local conversion failed for screen {screen} on canvas {uiCanvas.name}. Trying viewport fallback.");
            Camera fallbackCam = cam != null ? cam : Camera.main;
            if (fallbackCam == null)
            {
                Debug.LogError("PlaceUIOverWorld: no camera available for fallback conversion.");
                return;
            }

            Vector3 viewport = fallbackCam.WorldToViewportPoint(world);
            // convert viewport (0..1) to canvas local point - account for pivot
            Vector2 canvasSize = canvasRect.sizeDelta;
            Vector2 pivotOffset = new Vector2((0.5f - canvasRect.pivot.x) * canvasSize.x, (0.5f - canvasRect.pivot.y) * canvasSize.y);
            local = new Vector2(viewport.x * canvasSize.x, viewport.y * canvasSize.y) - (canvasSize * 0.5f) + pivotOffset;
        }

        uiRect.anchoredPosition = local;
        Debug.Log($"PlaceUIOverWorld: placed {uiRect.name} over {worldTarget.name} at world {world} -> screen {screen} -> local {local}");
    }

    IEnumerator FadeAndPopIn(RectTransform rt, float dur)
    {
        if (!rt) yield break;
        var cg = EnsureCanvasGroup(rt.gameObject);
        float t = 0f;
        float startA = 0f, endA = 1f;
        float startS = 0.88f, endS = popScale;

        // use the current localScale as the base so we animate relative to it
        Vector3 baseScale = rt.localScale;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            cg.alpha = Mathf.Lerp(startA, endA, u);
            float s = Mathf.Lerp(startS, endS, u);
            rt.localScale = baseScale * s;
            yield return null;
        }

        // settle to the base scale (no absolute overwrites)
        rt.localScale = baseScale;
    }

    IEnumerator SpritePop(Transform tr, float dur)
    {
        if (!tr) yield break;
        float t = 0f;
        float start = 0.94f, end = 1.0f;

        // Animate relative to the current localScale so we don't overwrite non-1 scales
        Vector3 baseScale = tr.localScale;
        tr.localScale = baseScale * start;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float s = Mathf.Lerp(start, end, u);
            tr.localScale = baseScale * s;
            yield return null;
        }
        tr.localScale = baseScale;
    }

    CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (!cg) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }
}
