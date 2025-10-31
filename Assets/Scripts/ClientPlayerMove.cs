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

    private void Awake()
    {
        // Disable until we know ownership
        TogglePlayerControl(false);
        TogglePlayerPOV(false);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            // Local player keeps controls and POV
            TogglePlayerControl(true);
            TogglePlayerPOV(true);

            CleanupOtherPOVs();
        }
        else
        {
            // Remote players → just disable local-only POV
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
        if (cameraHolder) cameraHolder.SetActive(state);
        if (playerCamera) playerCamera.enabled = state;
        if (cinemachineBrain) cinemachineBrain.enabled = state;
        if (audioListener) audioListener.enabled = state;
    }

    private void CleanupOtherPOVs()
    {
        // Disable cameras not belonging to this player
        Camera[] allCameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cam in allCameras)
        {
            if (cam != playerCamera)
                cam.enabled = false;
        }

        CinemachineBrain[] allBrains = FindObjectsByType<CinemachineBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var brain in allBrains)
        {
            if (brain != cinemachineBrain)
                brain.enabled = false;
        }

        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var listener in listeners)
        {
            if (listener != audioListener)
                listener.enabled = false;
        }
    }
}
