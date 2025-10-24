using UnityEngine;
using Unity.Netcode;

public class Projectile : NetworkBehaviour
{
    public int damage = 25;
    private ulong ownerId;

    public void Init(ulong attackerId, Vector3 direction, float speed)
    {
        ownerId = attackerId;
        GetComponent<Rigidbody>().linearVelocity = direction.normalized * speed;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        if (collision.gameObject.TryGetComponent(out Health health))
        {
            // don’t damage yourself
            if (health.OwnerClientId != ownerId)
            {
                health.TakeDamage(damage, ownerId);
            }
        }

        // despawn across network
        if (TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
        Destroy(gameObject);
    }
}
