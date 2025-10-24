using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class SceneChanger : NetworkBehaviour
{
    // Call this from host only (e.g. button click)
    [ContextMenu("Change Scene")] // adds right-click option in Inspector for testing
    public void ChangeScene()
    {
        if (IsServer) // Only server/host controls scene changes
        {
            // Example: load a scene called "GameScene"
            NetworkManager.SceneManager.LoadScene("Multiplayer", LoadSceneMode.Single);
        }
        else
        {
            Debug.LogWarning("Only the server/host can change scenes!");
        }
    }
}
