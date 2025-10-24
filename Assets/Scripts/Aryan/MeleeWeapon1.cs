using UnityEngine;
using Unity.Netcode;

public class MeleeWeapon1 : NetworkBehaviour
{
    private ulong ownerId;

    public void Init(ulong id) => ownerId = id;
    [SerializeField] private int MeeleDamage=25;
    // This is called by Animation Event at the "hit frame"

    public void PerformHit()
    {
        // Only the local player should detect hits (client-side authority for detection)
        if (!IsOwner)
        {
            Debug.Log("[PerformHit] Skipped because not local player");
            return;
        }

        Debug.Log("[PerformHit] Local player performing melee hit!");

        Vector3 hitCenter = transform.position + transform.forward * 1.5f;
        float hitRadius = 1.2f;

        Collider[] hits = Physics.OverlapSphere(hitCenter, hitRadius);
        foreach (var col in hits)
        {
            if (col.TryGetComponent(out Health health))
            {
                if (health.OwnerClientId != OwnerClientId)
                {
                    Debug.Log("Melee hit check performed!");
                    ApplyMeleeDamageServerRpc(health.OwnerClientId, MeeleDamage);
                }
            }
        }

    }

    [ServerRpc(RequireOwnership = false)]
    private void ApplyMeleeDamageServerRpc(ulong targetId, int damage)
    {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(targetId, out var client))
        {
            Debug.LogWarning($"[ServerRpc] No client found for {targetId}");
            return;
        }

        var playerObj = client.PlayerObject;
        var health = playerObj.GetComponentInChildren<Health>();
        if (health != null)
        {
            Debug.Log($"[ServerRpc] Applying {damage} dmg to {targetId} from {OwnerClientId}");
            health.TakeDamage(damage, OwnerClientId);
        }
        else
        {
            Debug.LogWarning($"[ServerRpc] No Health on {playerObj.name}");
        }
    }

    // Debug gizmo to see hit range in Scene view
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward * 1.5f, 1.2f);
    }
}
