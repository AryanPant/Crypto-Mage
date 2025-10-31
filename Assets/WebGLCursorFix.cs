using UnityEngine;

/// <summary>
/// ADD TO MULTIPLAYER SCENE - Fixes cursor locking in WebGL
/// </summary>
public class WebGLCursorFix : MonoBehaviour
{
    void Start()
    {
        Debug.Log("[CURSOR FIX] Started");
        
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            // WebGL needs user interaction to lock cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("[CURSOR FIX] Click anywhere to enable controls");
        }
    }

    void Update()
    {
        // In WebGL, click to lock cursor
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                Debug.Log("[CURSOR FIX] Cursor locked!");
            }
        }
        
        // ESC to unlock
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("[CURSOR FIX] Cursor unlocked");
        }
    }
}