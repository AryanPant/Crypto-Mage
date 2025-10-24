using UnityEngine;
using UnityEngine.SceneManagement;

public class CursorManager : MonoBehaviour
{
    private void Start()
    {
        UpdateCursorState();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateCursorState();
    }

    private void UpdateCursorState()
    {
        string currentScene = SceneManager.GetActiveScene().name;

        if (currentScene == "")
        {
            Cursor.lockState = CursorLockMode.Locked;  // lock in center
            Cursor.visible = false;                   // hide cursor
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;   // free cursor
            Cursor.visible = true;                    // show cursor
        }

        Debug.Log($"Cursor updated for scene: {currentScene}");
    }
}
