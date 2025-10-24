using StarterAssets;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerAttack : NetworkBehaviour
{
    public Animator anim;
    private StarterAssetsInputs _input;
    [SerializeField] private Camera cam;
    [SerializeField] private Transform spawnPoint;
    public GameObject chargeSpherePrefab;   // MUST have NetworkObject
    public AudioSource shootSound;
    public float speed;
    public float spawnDistance = 1.4f;
    public float spawnHeight = 1f;
    private bool attacking;
    private bool comboAllowed = false;
    private bool Incombo;
    private MeleeWeapon1 meleeWeapon;
    public ThirdPersonController thirdPersonController;
    [Header("Layer Blend Settings")]
    public int attackLayerIndex = 1;
    public float layerBlendSpeed = 5f;

    private float targetLayerWeight = 0f;

    private void Start()
    {
        _input = GetComponent<StarterAssetsInputs>();
        meleeWeapon = GetComponentInChildren<MeleeWeapon1>(true);

        if (IsOwner && meleeWeapon != null)
        {
            meleeWeapon.Init(OwnerClientId);
        }
        attacking = false;
    }

    private void Update()
    {
        // Only process input if this is OUR local player
        if (!IsOwner) return;

        if (Mouse.current.rightButton.wasPressedThisFrame && !attacking)
        {
            StartAttack("Charge");
        }
        else if (Mouse.current.leftButton.wasPressedThisFrame && !attacking)
        {
            StartAttack("Meele1");
        }
        else if (Mouse.current.leftButton.wasPressedThisFrame && comboAllowed)
        {
            anim.SetTrigger("Meele2");
            Incombo = true;
            anim.SetBool("Incombo", Incombo);
            comboAllowed = false;

            targetLayerWeight = 1f;
        }

        float currentWeight = anim.GetLayerWeight(attackLayerIndex);
        float newWeight = Mathf.Lerp(currentWeight, targetLayerWeight, Time.deltaTime * layerBlendSpeed);
        anim.SetLayerWeight(attackLayerIndex, newWeight);
    }

    private void StartAttack(string triggerName)
    {
        targetLayerWeight = 1f;
        anim.SetTrigger(triggerName);
        Incombo = false;
        anim.SetBool("Incombo", Incombo);
        attacking = true;
    }

    // Called by animation event
    private void Attack()
    {
        if (!IsOwner) return; // only owner spawns projectile

        Vector3 sphereSpawnPosition = spawnPoint.position;
        Vector3 direction = cam.transform.forward; // fallback

        // Cast a ray from the center of the screen
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            // Aim toward hit point
            direction = (hit.point - sphereSpawnPosition).normalized;
        }
        else
        {
            // No hit → shoot forward
            direction = ray.direction.normalized;
        }

        // Call server to spawn projectile with this direction
        SpawnChargeSphereServerRpc(sphereSpawnPosition, direction);

        if (shootSound != null) shootSound.Play();
    }

    [ServerRpc]
    private void SpawnChargeSphereServerRpc(Vector3 spawnPos, Vector3 forward, ServerRpcParams rpcParams = default)
    {
        GameObject chargeSphere = Instantiate(chargeSpherePrefab, spawnPos, Quaternion.identity);
        var netObj = chargeSphere.GetComponent<NetworkObject>();
        netObj.Spawn(true);

        // init projectile with owner info
        var proj = chargeSphere.GetComponent<Projectile>();
        if (proj != null)
            proj.Init(OwnerClientId, forward, speed);

        StartCoroutine(DestroyChargeSphereAfterDelay(chargeSphere, 4f));
    }


    private IEnumerator DestroyChargeSphereAfterDelay(GameObject chargeSphere, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (chargeSphere != null && chargeSphere.TryGetComponent(out NetworkObject netObj))
        {
            if (netObj.IsSpawned)
                netObj.Despawn();
            Destroy(chargeSphere);
        }
    }

    public void ResetAnimation()
    {
        ResetAllTriggers();
        attacking = false;
        Incombo = false;
        targetLayerWeight = 0f;
    }

    public void EnableCombo() => comboAllowed = true;

    public void DisableCombo()
    {
        comboAllowed = false;
        ResetAllTriggers();
    }

    public void ResetAfterCombo()
    {
        attacking = false;
        targetLayerWeight = 0f;
        ResetAllTriggers();
    }

    private void ResetAllTriggers()
    {
        anim.ResetTrigger("Charge");
        anim.ResetTrigger("Meele1");
        anim.ResetTrigger("Meele2");
    }
}
