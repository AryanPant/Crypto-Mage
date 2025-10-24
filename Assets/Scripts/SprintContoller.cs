using UnityEngine;
using Unity.Netcode;
using StarterAssets;
using System.Collections;

public class SprintController : NetworkBehaviour
{
    private ThirdPersonController controller;
    private float originalSprintSpeed;
    // Networked multiplier for sprint
    public NetworkVariable<float> SprintMultiplier = new NetworkVariable<float>(
        1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        controller = GetComponent<ThirdPersonController>();
        originalSprintSpeed = controller.SprintSpeed;
    }

    private void Update()
    {
        // Apply multiplier locally
        if (controller != null)
        {

            controller.SprintSpeed = originalSprintSpeed * SprintMultiplier.Value; // replace 5.335f with your base sprint
        }
    }

    public void ApplySprintBoost(float multiplier, float duration)
    {
        if (!IsServer) return;
        StartCoroutine(SprintBoostRoutine(multiplier, duration));
    }

    private IEnumerator SprintBoostRoutine(float multiplier, float duration)
    {
        SprintMultiplier.Value = multiplier;
        yield return new WaitForSeconds(duration);
        SprintMultiplier.Value = 1f; // reset
    }
}
