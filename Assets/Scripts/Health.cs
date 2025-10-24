using UnityEngine;
using Unity.Netcode;
using System.Collections;
using StarterAssets;
using UnityEngine.UI;

public class Health : NetworkBehaviour
{
    [Header("Player")]
    [SerializeField] private int maxHealth = 100;

    [SerializeField] private Animator anim;
    private float targetLayerWeight = 0f;
    public float layerBlendSpeed = 5f;

    [Tooltip("Scripts to disable when the player is dead")]
    [SerializeField] private Behaviour[] disableOnDeath;
    [SerializeField] private ThirdPersonController controller;

    // Networked health
    public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(
        100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Health UI")]
    [SerializeField] private GameObject healthBarCanvas;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private float healthLerpSpeed = 5f;

    [Header("Kill Zone")]
    [SerializeField] private float fallThresholdY = -10f; // Y position threshold
    [SerializeField] private int fallDamage = 1000;       // Enough to kill instantly

    private float targetHealth;
    private Transform cam;
    private ScoreSystem scoreSystem;

    private bool canvasInitialized = false;

    // Guard to ensure Die() runs only once
    private bool isDead = false;

    // Sentinel for "no killer"
    private const ulong NO_KILLER = ulong.MaxValue;

    private void Awake()
    {
        if (anim == null)
            anim = GetComponent<Animator>();
        controller = GetComponent<ThirdPersonController>();
        scoreSystem = GetComponent<ScoreSystem>();
    }

    public override void OnNetworkSpawn()
    {
        cam = Camera.main != null ? Camera.main.transform : null;

        if (healthBarCanvas != null)
        {
            if (IsOwner)
                healthBarCanvas.SetActive(false);
            else
                healthBarCanvas.SetActive(true);
        }

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            targetHealth = CurrentHealth.Value;
            healthSlider.value = CurrentHealth.Value;
        }

        CurrentHealth.OnValueChanged += OnHealthChanged;

        if (cam != null)
            InitializeHealthCanvasVisibility();
        else
            canvasInitialized = false;

        // ensure isDead matches current health on spawn
        isDead = CurrentHealth.Value <= 0;
    }

    public override void OnDestroy()
    {
        CurrentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void Update()
    {
        if (healthSlider != null)
            healthSlider.value = Mathf.Lerp(healthSlider.value, targetHealth, Time.deltaTime * healthLerpSpeed);

        if (cam == null && Camera.main != null)
        {
            cam = Camera.main.transform;
            InitializeHealthCanvasVisibility();
        }

        if (IsClient && healthBarCanvas != null && cam != null && !IsOwner)
        {
            if (!healthBarCanvas.activeSelf)
                healthBarCanvas.SetActive(true);

            Vector3 dir = cam.position - healthBarCanvas.transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                healthBarCanvas.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        // --- FALL DEATH HANDLING ---
        if (IsServer && !isDead && transform.position.y < fallThresholdY)
        {
            HandleFallDeath(); // CHANGED
        }

        float currentWeight = anim.GetLayerWeight(1);
        float newWeight = Mathf.Lerp(currentWeight, targetLayerWeight, Time.deltaTime * layerBlendSpeed);
        anim.SetLayerWeight(1, newWeight);
    }

    // NEW METHOD
    private void HandleFallDeath()
    {
        CharacterSpawnManager.Instance.RespawnPlayerAtSameSpot(gameObject, 1f);
        TakeDamage(fallDamage, NO_KILLER);
    }


    private void InitializeHealthCanvasVisibility()
    {
        if (healthBarCanvas == null) return;

        if (IsOwner)
            healthBarCanvas.SetActive(false);
        else
            healthBarCanvas.SetActive(true);

        canvasInitialized = true;
    }

    private void OnHealthChanged(int previous, int current)
    {
        targetHealth = current;
    }

    [ServerRpc]
    public void TakeDamageServerRpc(int amount, ulong attackerId)
    {
        TakeDamage(amount, attackerId);
    }

    public void TakeDamage(int amount, ulong attackerId)
    {
        if (!IsServer) return;
        if (isDead) return;  // <---- NEW GUARD
        if (CurrentHealth.Value <= 0) return;

        CurrentHealth.Value -= amount;

        if (CurrentHealth.Value <= 0)
            Die(attackerId);
        else
        {
            targetLayerWeight = 1f;
            anim.SetTrigger("Hurt");
        }
    }


    public void ResetHurtAnimation()
    {
        targetLayerWeight = 0f;
        anim.ResetTrigger("Hurt");
    }

    private void Die(ulong killerId)
    {
        // Ensure Die only runs once  
        if (isDead) return;
        isDead = true;

        // Reward killer if valid and present (ignore NO_KILLER sentinel)  
        if (killerId != NO_KILLER && NetworkManager.Singleton.ConnectedClients.TryGetValue(killerId, out var killer))
        {
            if (killer.PlayerObject.TryGetComponent(out ScoreSystem score))
                score.AddKill();
        }

        // Add victim death (server-side)  
        if (TryGetComponent(out ScoreSystem victimScore))
            victimScore.AddDeath();

        foreach (var script in disableOnDeath)
            if (script != null) script.enabled = false;

        if (IsServer)
        {
            var leaderboardUI = Object.FindFirstObjectByType<LeaderboardUI>();
            if (leaderboardUI != null)
            {
                ulong clientId = NetworkObject.OwnerClientId;

                // Increment death count, keep kills same  
                int currentKills = leaderboardUI.GetKills(clientId);
                int newDeaths = leaderboardUI.GetDeaths(clientId) + 1;

                leaderboardUI.UpdatePlayerScore(clientId, currentKills, newDeaths);
            }
        }

        StopMovementClientRpc(OwnerClientId);
        PlayDeathAnimationClientRpc();

        StartCoroutine(ServerRespawnAfterDelay(5f));
    }

    public void ApplyHeal(int amount)
    {
        if (!IsServer) return;

        // do nothing if dead
        if (isDead) return;

        CurrentHealth.Value = Mathf.Min(CurrentHealth.Value + amount, maxHealth);
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdateLeaderboardServerRpc()
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var scoreSys = client.PlayerObject.GetComponent<ScoreSystem>();
            if (scoreSys != null)
            {
                UpdateLeaderboardClientRpc(client.ClientId, scoreSys.Kills.Value, scoreSys.Deaths.Value);
            }
        }
    }

    [ClientRpc]
    private void UpdateLeaderboardClientRpc(ulong clientId, int kills, int deaths)
    {
        var leaderboard = Object.FindFirstObjectByType<LeaderboardUI>();
        if (leaderboard != null)
            leaderboard.UpdatePlayerScore(clientId, kills, deaths);
    }

    [ClientRpc]
    private void StopMovementClientRpc(ulong clientId)
    {
        // Apply only on the client that owns this player
        if (OwnerClientId == clientId)
            controller.CanMove = false;
    }

    [ClientRpc]
    private void StartMovementClientRpc(ulong clientId)
    {
        if (OwnerClientId == clientId)
            controller.CanMove = true;
    }

    [ClientRpc]
    private void PlayDeathAnimationClientRpc()
    {
        if (anim != null)
            anim.SetBool("Die", true);
    }

    private IEnumerator ServerRespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (IsServer)
            HandleRespawn();
    }

    private void HandleRespawn()
    {
        // Reset health while still "dead"
        CurrentHealth.Value = maxHealth;

        // Move player to safe spawn position BEFORE reviving
        CharacterSpawnManager.Instance.RespawnPlayer(gameObject);

        // Now clear dead state
        isDead = false;

        controller.CanMove = true;

        foreach (var script in disableOnDeath)
            if (script != null) script.enabled = true;

        StartMovementClientRpc(OwnerClientId);
        ResetAnimationClientRpc();
    }


    [ClientRpc]
    private void ResetAnimationClientRpc()
    {
        if (anim != null)
            anim.SetBool("Die", false);
    }
}