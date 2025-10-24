using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using Unity.Netcode;

public class LeaderboardUI : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] public GameObject leaderboardPanel;
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject playerEntryPrefab;

    private bool isVisible = false;

    // Existing dictionaries
    private Dictionary<ulong, PlayerScoreData> playerScores = new Dictionary<ulong, PlayerScoreData>();
    private Dictionary<ulong, string> clientAddresses = new Dictionary<ulong, string>();

    private addressStorage addrStorage;

    private void Start()
    {
        addrStorage = addressStorage.Instance;

        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(isVisible);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        if (IsServer && NetworkManager.Singleton != null)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                RegisterPlayer(client.ClientId);
        }
    }

    public override void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        RegisterPlayer(clientId);

        // Get wallet or ENS for this player
        string walletAddress = addrStorage != null ? addrStorage.Getaddress() : $"Player{clientId}";
        clientAddresses[clientId] = walletAddress;

        // Send wallet + score data to all clients
        SendPlayerDataClientRpc(clientId, walletAddress, 0, 0);
        UpdatePlayerAddressClientRpc(clientId, walletAddress);

        if (isVisible) RefreshLeaderboard();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (playerScores.ContainsKey(clientId))
            playerScores.Remove(clientId);
        if (clientAddresses.ContainsKey(clientId))
            clientAddresses.Remove(clientId);

        if (isVisible) RefreshLeaderboard();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            isVisible = true;
            leaderboardPanel.SetActive(true);
            RefreshLeaderboard();
        }
        else if (Input.GetKeyUp(KeyCode.Tab))
        {
            isVisible = false;
            leaderboardPanel.SetActive(false);
        }
    }

    public void UpdatePlayerScore(ulong clientId, int kills, int deaths)
    {
        if (!playerScores.ContainsKey(clientId))
            RegisterPlayer(clientId);

        playerScores[clientId].Kills = kills;
        playerScores[clientId].Deaths = deaths;

        if (isVisible)
            RefreshLeaderboard();
    }

    private void RefreshLeaderboard()
    {
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);

        var sortedPlayers = playerScores.Values
            .OrderByDescending(p => p.Kills)
            .ThenBy(p => p.Deaths);

        foreach (var player in sortedPlayers)
        {
            GameObject entry = Instantiate(playerEntryPrefab, contentParent);
            var nameText = entry.transform.Find("Name").GetComponent<TextMeshProUGUI>();
            var killsText = entry.transform.Find("Kills").GetComponent<TextMeshProUGUI>();
            var deathsText = entry.transform.Find("Deaths").GetComponent<TextMeshProUGUI>();

            string address = clientAddresses.ContainsKey(player.ClientId)
                ? clientAddresses[player.ClientId]
                : player.PlayerName;

            nameText.text = address;
            killsText.text = player.Kills.ToString();
            deathsText.text = player.Deaths.ToString();
        }
    }

    private void RegisterPlayer(ulong clientId)
    {
        if (!playerScores.ContainsKey(clientId))
        {
            string displayName = "Player" + clientId;
            if (addrStorage != null)
            {
                string wallet = addrStorage.Getaddress();
                displayName = wallet;
            }

            playerScores.Add(clientId, new PlayerScoreData(clientId, displayName));
        }
    }

    [ClientRpc]
    private void SendPlayerDataClientRpc(ulong clientId, string name, int kills, int deaths, ClientRpcParams clientRpcParams = default)
    {
        if (!playerScores.ContainsKey(clientId))
            playerScores.Add(clientId, new PlayerScoreData(clientId, name));

        playerScores[clientId].Kills = kills;
        playerScores[clientId].Deaths = deaths;

        if (!clientAddresses.ContainsKey(clientId))
            clientAddresses.Add(clientId, name);

        if (isVisible)
            RefreshLeaderboard();
    }

    [ClientRpc]
    private void UpdatePlayerAddressClientRpc(ulong clientId, string walletAddress)
    {
        if (clientAddresses.ContainsKey(clientId))
            clientAddresses[clientId] = walletAddress;
        else
            clientAddresses.Add(clientId, walletAddress);

        if (playerScores.ContainsKey(clientId))
            playerScores[clientId].PlayerName = walletAddress;

        if (isVisible)
            RefreshLeaderboard();
    }

    // ✅ Utility accessors
    public List<PlayerScoreData> GetPlayerScores() => new List<PlayerScoreData>(playerScores.Values);
    public string GetWalletAddress(ulong clientId) => clientAddresses.ContainsKey(clientId) ? clientAddresses[clientId] : "Unknown";
    public int GetKills(ulong clientId) => playerScores.ContainsKey(clientId) ? playerScores[clientId].Kills : 0;
    public int GetDeaths(ulong clientId) => playerScores.ContainsKey(clientId) ? playerScores[clientId].Deaths : 0;

    public class PlayerScoreData
    {
        public ulong ClientId;
        public string PlayerName;
        public int Kills;
        public int Deaths;

        public PlayerScoreData(ulong clientId, string name)
        {
            ClientId = clientId;
            PlayerName = name;
            Kills = 0;
            Deaths = 0;
        }
    }
}
