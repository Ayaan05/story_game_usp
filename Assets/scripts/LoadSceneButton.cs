using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneButton : MonoBehaviour
{
    [Tooltip("Exact name of the scene to load (must be in Build Settings)")]
    public string sceneName = "MainMenu";

    // This must be public, parameterless, instance method
    public void LoadTarget()
    {
        SceneManager.LoadScene(sceneName);
    }
}
