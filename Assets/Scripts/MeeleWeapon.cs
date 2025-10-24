using UnityEngine;
using Unity.Netcode;

public class MeleeWeapon : NetworkBehaviour
{
    public int damage = 20;
    private ulong ownerId;

    public void Init(ulong attackerId)
    {
        ownerId = attackerId;
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return; // damage only on server

        if (collision.gameObject.TryGetComponent(out Health health))
        {
            // don’t damage self
            if (health.OwnerClientId != ownerId)
            {
                health.TakeDamage(damage, ownerId);
            }
        }
    }
    
}
