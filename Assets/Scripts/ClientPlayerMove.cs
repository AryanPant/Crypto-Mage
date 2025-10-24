using Unity.Netcode;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using Unity.Netcode.Components;

[RequireComponent(typeof(NetworkTransform))]
public class ClientPlayerMove : NetworkBehaviour
{
    [Header("Player Components")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private ThirdPersonController thirdPersonController;
    [SerializeField] private StarterAssetsInputs starterAssetsInputs;

    [Header("Player Camera Setup")]
    [SerializeField] private GameObject cameraHolder;      // Parent holding camera + brain + audio
    [SerializeField] private Camera playerCamera;
    [SerializeField] private CinemachineBrain cinemachineBrain;
    [SerializeField] private AudioListener audioListener;

    // Ensure we only clean once across all players
    private static bool cleanedUp = false;

    private void Awake()
    {
        TogglePlayerControl(false);
        TogglePlayerPOV(false);
    }

public override void OnNetworkSpawn()
{
    base.OnNetworkSpawn();

#if UNITY_WEBGL
    Application.targetFrameRate = 60;
#endif

    if (IsOwner)
    {
        TogglePlayerControl(true);
        TogglePlayerPOV(true);

        // Delay cleanup until after spawn stabilizes
        StartCoroutine(DelayedCleanup());
    }
    else
    {
        TogglePlayerPOV(false);
    }
}


    private void TogglePlayerControl(bool state)
    {
        if (playerInput) playerInput.enabled = state;
        if (thirdPersonController) thirdPersonController.enabled = state;
        if (starterAssetsInputs) starterAssetsInputs.enabled = state;
    }

    private void TogglePlayerPOV(bool state)
    {
        // Only toggle when necessary (avoid triggering GC in WebGL)
        if (cameraHolder && cameraHolder.activeSelf != state)
            cameraHolder.SetActive(state);

        if (playerCamera && playerCamera.enabled != state)
            playerCamera.enabled = state;

        if (cinemachineBrain && cinemachineBrain.enabled != state)
            cinemachineBrain.enabled = state;

        if (audioListener && audioListener.enabled != state)
            audioListener.enabled = state;
    }

private void CleanupOtherPOVs()
{
    if (cleanedUp) return;
    cleanedUp = true;

    foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        if (cam && cam.enabled && cam != playerCamera)
            cam.enabled = false;

    foreach (var brain in FindObjectsByType<CinemachineBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        if (brain && brain.enabled && brain != cinemachineBrain)
            brain.enabled = false;

    foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        if (listener && listener.enabled && listener != audioListener)
            listener.enabled = false;
}

    private System.Collections.IEnumerator DelayedCleanup()
{
    // Wait a few frames to let WebGL settle
    yield return null;
    yield return null;  // 2 frames
    yield return new WaitForSeconds(0.05f);

    CleanupOtherPOVs();
}

}
