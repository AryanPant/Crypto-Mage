using UnityEngine;
using Unity.Netcode;
using System.Collections;

public enum PowerupType { Heal, Sprint }

public class Powerup : NetworkBehaviour
{
    [Header("Settings")]
    public PowerupType type;
    public int healAmount = 50;
    public float sprintBoost = 1.5f;
    public float sprintDuration = 5f;
    public float respawnTime = 10f; // seconds before respawn

    [Header("Visuals")]
    [SerializeField] private GameObject visuals;
    [SerializeField] private float rotationSpeed = 90f; // degrees per second

    [Header("Audio")]
    [SerializeField] private AudioClip healSound;
    [SerializeField] private AudioClip sprintSound;

    private Collider col;

    private void Awake()
    {
        col = GetComponent<Collider>();
    }

    private void Update()
    {
        // Rotate only visuals, not collider
        if (visuals.activeSelf)
        {
            visuals.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.TryGetComponent(out Health health))
        {
            var playerId = health.OwnerClientId;

            if (type == PowerupType.Heal)
            {
                health.ApplyHeal(healAmount);
                PlaySoundClientRpc(playerId, PowerupType.Heal);
            }
            else if (type == PowerupType.Sprint)
            {
                if (other.TryGetComponent(out SprintController sprint))
                {
                    sprint.ApplySprintBoost(sprintBoost, sprintDuration);
                    PlaySoundClientRpc(playerId, PowerupType.Sprint);
                }
            }

            // start respawn coroutine
            StartCoroutine(RespawnRoutine());
        }
    }

    private IEnumerator RespawnRoutine()
    {
        // Hide across all clients
        HideClientRpc();

        // disable collider while hidden
        col.enabled = false;

        yield return new WaitForSeconds(respawnTime);

        // Respawn
        ShowClientRpc();
        col.enabled = true;
    }

    [ClientRpc]
    private void HideClientRpc()
    {
        visuals.SetActive(false);
    }

    [ClientRpc]
    private void ShowClientRpc()
    {
        visuals.SetActive(true);
    }

    [ClientRpc]
    private void PlaySoundClientRpc(ulong targetClientId, PowerupType powerupType)
    {
        // only play sound on the client who picked it up
        if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

        AudioClip clip = null;
        if (powerupType == PowerupType.Heal) clip = healSound;
        else if (powerupType == PowerupType.Sprint) clip = sprintSound;

        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }
}
