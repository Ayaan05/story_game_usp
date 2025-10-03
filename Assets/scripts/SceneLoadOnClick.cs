using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Button))]
public class SceneLoadOnClick : MonoBehaviour
{
    [Tooltip("Exact scene name in Build Settings")]
    public string sceneName = "Main";

    void Awake()
    {
        var btn = GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("SceneLoadOnClick: sceneName is empty.");
                return;
            }
            SceneManager.LoadScene(sceneName);
        });
    }
}
