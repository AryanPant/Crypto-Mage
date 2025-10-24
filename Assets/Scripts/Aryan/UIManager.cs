using UnityEngine;
using Unity.Netcode;

public class UIManager : MonoBehaviour
{
    private void OnGUI()
    {
        // Make sure NetworkManager exists
        if (NetworkManager.Singleton == null)
            return;

        // Only show buttons if not yet connected
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUI.Button(new Rect(10, 10, 100, 40), "Host"))
            {
                NetworkManager.Singleton.StartHost();
                Debug.Log("Host started");
            }
            if (GUI.Button(new Rect(10, 60, 100, 40), "Client"))
            {
                NetworkManager.Singleton.StartClient();
                Debug.Log("Client started");
            }
            if (GUI.Button(new Rect(10, 110, 100, 40), "Server"))
            {
                NetworkManager.Singleton.StartServer();
                Debug.Log("Server started");
            }
        }
        else
        {
            GUI.Label(new Rect(10, 10, 200, 40), "Connected as " +
                      (NetworkManager.Singleton.IsHost ? "Host" :
                      NetworkManager.Singleton.IsServer ? "Server" : "Client"));
        }
    }
}
