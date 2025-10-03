using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum MiniGame { None, Bath, Food }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public const string MAIN_SCENE = "Main_Scene";
    public const string BATH_SCENE = "Bath_Scene";
    public const string FOOD_SCENE = "Food_Scene";
    public const string END_SCENE  = "end_scene";

    private readonly HashSet<MiniGame> completed = new HashSet<MiniGame>();

    public bool showOptionsOnMain { get; private set; }
    public MiniGame lastCompleted { get; private set; } = MiniGame.None;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != MAIN_SCENE) return;

        if (completed.Count >= 2)
        {
            Debug.LogWarning("GameManager: Main_Scene loaded with all mini games still marked complete. Auto-resetting progress.");
            ResetProgress();
        }

        if (completed.Count == 0)
        {
            showOptionsOnMain = false;
            lastCompleted = MiniGame.None;
        }
    }

    public void CompleteMiniGame(MiniGame miniGame)
    {
        if (miniGame == MiniGame.None)
        {
            Debug.LogWarning("GameManager.CompleteMiniGame called with MiniGame.None");
            return;
        }

        completed.Add(miniGame);
        lastCompleted = miniGame;

        bool allDone = completed.Count >= 2;
        showOptionsOnMain = !allDone;

        string targetScene = allDone ? END_SCENE : MAIN_SCENE;
        Debug.Log($"GameManager: mini game '{miniGame}' complete. Loading '{targetScene}'");
        SceneManager.LoadScene(targetScene);
    }

    public bool IsMiniGameComplete(MiniGame miniGame) => completed.Contains(miniGame);

    public bool ConsumeShowOptionsFlag()
    {
        bool value = showOptionsOnMain;
        showOptionsOnMain = false;
        return value;
    }

    public void ResetProgress()
    {
        completed.Clear();
        showOptionsOnMain = false;
        lastCompleted = MiniGame.None;
        Debug.Log("GameManager: progress reset");
    }
}
