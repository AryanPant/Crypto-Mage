using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterSpawnManager : NetworkBehaviour
{
    [Header("Assign unique character prefabs here")]
    [SerializeField] private GameObject[] characterPrefabs;

    [Header("Assign spawn points here (same size as prefabs or larger)")]
    [SerializeField] private Transform[] spawnPoints;

    private List<int> usedPrefabIndices = new List<int>();
    private List<int> usedSpawnIndices = new List<int>();
    private float fallThresholdY = -10f; // Y position threshold for respawn
    public static CharacterSpawnManager Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Debug.Log("[CharacterSpawnManager] Server started. Setting up spawning.");
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.SceneManager.OnLoadComplete += HandleSceneLoaded;

            // Spawn existing clients after scene load
            StartCoroutine(SpawnExistingClients());
        }
        else
        {
            Debug.Log("[CharacterSpawnManager] Client started. Waiting for server to assign prefab...");
        }
    }

    private IEnumerator SpawnExistingClients()
    {
        yield return new WaitForEndOfFrame();

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
            {
                HandleClientConnected(client.ClientId);
            }
        }
    }

    private void HandleSceneLoaded(ulong clientId, string sceneName, LoadSceneMode mode)
    {
        if (!IsServer) return;

        StartCoroutine(DelayedSpawnAfterSceneLoad(clientId));
    }

    private IEnumerator DelayedSpawnAfterSceneLoad(ulong clientId)
    {
        yield return new WaitForSeconds(0.5f);

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject == null)
            {
                HandleClientConnected(clientId);
            }
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.SceneManager.OnLoadComplete -= HandleSceneLoaded;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        int prefabIndex = GetNextAvailablePrefabIndex();
        if (prefabIndex == -1)
        {
            Debug.LogWarning("[CharacterSpawnManager] No unique prefabs left!");
            return;
        }

        int spawnIndex = GetNextAvailableSpawnIndex();
        if (spawnIndex == -1)
        {
            Debug.LogWarning("[CharacterSpawnManager] No spawn points left! Reusing...");
            spawnIndex = Random.Range(0, spawnPoints.Length);
        }

        GameObject prefab = characterPrefabs[prefabIndex];
        Transform spawnPoint = spawnPoints[spawnIndex];

        GameObject player = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);

        Debug.Log($"[CharacterSpawnManager] Spawned {prefab.name} for Client {clientId} at {spawnPoint.position}");
    }

    // Respawn logic  
    public void RespawnPlayer(GameObject player)
    {
        if (!IsServer) return;

        int spawnIndex = GetNextAvailableSpawnIndex();
        if (spawnIndex == -1)
            spawnIndex = Random.Range(0, spawnPoints.Length);

        Transform spawnPoint = spawnPoints[spawnIndex];

        var controller = player.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        player.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        if (controller != null)
            controller.enabled = true;

        var netObj = player.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            Debug.Log($"[CharacterSpawnManager] Respawned Player {netObj.OwnerClientId} at {spawnPoint.position}");
            RespawnClientRpc(netObj.OwnerClientId, spawnPoint.position, spawnPoint.rotation);
        }
        else
        {
            Debug.LogWarning("[CharacterSpawnManager] Respawned player has no NetworkObject!");
        }
    }
    public void RespawnPlayerAtSameSpot(GameObject player, float yOffset = 1f)
    {
        if (!IsServer) return;

        Vector3 newPos = player.transform.position;
        newPos.y = fallThresholdY + yOffset;

        var controller = player.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        player.transform.SetPositionAndRotation(newPos, player.transform.rotation);

        if (controller != null)
            controller.enabled = true;

        var netObj = player.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            Debug.Log($"[CharacterSpawnManager] Nudged Player {netObj.OwnerClientId} to {newPos}");
            RespawnClientRpc(netObj.OwnerClientId, newPos, player.transform.rotation);
        }
    }

    [ClientRpc]
    private void RespawnClientRpc(ulong clientId, Vector3 position, Quaternion rotation)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayer != null)
            {
                var controller = localPlayer.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;

                localPlayer.transform.SetPositionAndRotation(position, rotation);

                if (controller != null) controller.enabled = true;

                Debug.Log($"[CharacterSpawnManager] Client {clientId} synced to respawn at {position}");
            }
        }
    }

    // Helpers
    private int GetNextAvailablePrefabIndex()
    {
        for (int i = 0; i < characterPrefabs.Length; i++)
        {
            if (!usedPrefabIndices.Contains(i))
            {
                usedPrefabIndices.Add(i);
                return i;
            }
        }
        return -1;
    }

    private int GetNextAvailableSpawnIndex()
    {
        if (usedSpawnIndices.Count >= spawnPoints.Length)
            usedSpawnIndices.Clear(); // reset pool if all used  

        List<int> freeIndices = new List<int>();
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (!usedSpawnIndices.Contains(i))
                freeIndices.Add(i);
        }

        if (freeIndices.Count == 0) return -1;

        int index = freeIndices[Random.Range(0, freeIndices.Count)];
        usedSpawnIndices.Add(index);
        return index;
    }
}