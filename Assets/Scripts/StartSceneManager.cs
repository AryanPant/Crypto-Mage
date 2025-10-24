using UnityEngine;
using UnityEngine.SceneManagement;

public class StartSceneManager : MonoBehaviour
{
    public void StartGame()
    {
        SceneManager.LoadScene("Lobby");
    }
    public void QuitButton()
    {
        Application.Quit();
    }
    public void Shop(){
        SceneManager.LoadScene("Shop");
    }
}
