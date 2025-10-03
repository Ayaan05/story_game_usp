using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;     // IPointerClickHandler
using UnityEngine.SceneManagement;  // SceneManager
using System.IO;                    // Path for name extraction

/// <summary>
/// Click (pointer/touch) to load a scene after an optional SFX + delay.
/// Works on UI or world objects:
///  - UI: needs EventSystem + GraphicRaycaster (on the Canvas), and the UI Graphic's Raycast Target ON.
///  - World 2D/3D: needs EventSystem + Physics2DRaycaster or PhysicsRaycaster on the camera, and a Collider2D/Collider on this object.
/// Add the target scene to Build Settings (File → Build Settings… → Scenes In Build).
/// </summary>
[DisallowMultipleComponent]
public class SceneClickLoader : MonoBehaviour, IPointerClickHandler
{
    [Header("Load Settings")]
    [Tooltip("Name of the scene to load when clicked (must be in Build Settings).")]
    public string sceneToLoad;

    [Tooltip("Delay in seconds before loading (lets SFX finish).")]
    public float loadDelay = 0.18f;

    [Header("Optional SFX")]
    public AudioSource sfxSource;
    public AudioClip clickClip;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Input")]
    [Tooltip("Only respond to left mouse / primary pointer.")]
    public bool restrictToLeftClick = true;

    // === EventSystem entry point ===
    public void OnPointerClick(PointerEventData eventData)
    {
        if (restrictToLeftClick && eventData.button != PointerEventData.InputButton.Left)
            return;

        LoadTargetScene();
    }

    /// <summary>Public method you can also call from other scripts.</summary>
    public void LoadTargetScene()
    {
        Debug.Log($"SceneClickLoader: LoadTargetScene invoked on '{gameObject.name}' → '{sceneToLoad}'");
        StartCoroutine(LoadSceneCoroutine());
    }

    IEnumerator LoadSceneCoroutine()
    {
        // Play SFX (without being cut off by scene load)
        if (clickClip != null)
        {
            if (sfxSource == null)
            {
                // Try to find any non-looping AudioSource in the scene
                var srcs = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
                foreach (var s in srcs) { if (!s.loop) { sfxSource = s; break; } }
                if (sfxSource == null && srcs.Length > 0) sfxSource = srcs[0];
            }

            if (sfxSource != null) sfxSource.PlayOneShot(clickClip, sfxVolume);
            else Debug.LogWarning("SceneClickLoader: No AudioSource found to play clickClip.");
        }

        if (loadDelay > 0f)
            yield return new WaitForSecondsRealtime(loadDelay);

        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogWarning("SceneClickLoader: 'sceneToLoad' is empty. Set it in the Inspector.");
            yield break;
        }

        if (!IsSceneInBuildByName(sceneToLoad))
        {
            Debug.LogError($"SceneClickLoader: Scene '{sceneToLoad}' is not in Build Settings.");
            yield break;
        }

        SceneManager.LoadScene(sceneToLoad);
    }

    /// <summary>
    /// Runtime-safe check that a scene name exists in Build Settings.
    /// (Avoids using obsolete Application.CanStreamedLevelBeLoaded.)
    /// </summary>
    static bool IsSceneInBuildByName(string sceneName)
    {
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);            // e.g. "Assets/Scenes/Menu.unity"
            string name = Path.GetFileNameWithoutExtension(path);              // "Menu"
            if (string.Equals(name, sceneName, System.StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
