using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Linq;

public class MatchResultsWeb3Sender : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private LeaderboardUI leaderboard;
    [SerializeField] private SmartContractManager smartContractManager;

    [Header("Settings")]
    [SerializeField] private float delayBetweenCalls = 2f; // Delay between each Web3 call
    [SerializeField] private bool enableDebugLogs = true;

    // Track kill events during the match
    private List<KillEvent> matchKillEvents = new List<KillEvent>();

    // Network variable to track if match has ended
    private NetworkVariable<bool> matchEnded = new NetworkVariable<bool>(false);

    private void Awake()
    {
        // Try to find components if not assigned
        if (leaderboard == null)
            leaderboard = FindAnyObjectByType<LeaderboardUI>();

        if (smartContractManager == null)
            smartContractManager = FindAnyObjectByType<SmartContractManager>();

        if (leaderboard == null)
        {
            Debug.LogError("LeaderboardUI not found in scene!");
        }

        if (smartContractManager == null)
        {
            Debug.LogError("SmartContractManager not found in scene!");
        }
    }

    private void Start()
    {
        // Subscribe to network events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    public override void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Reset match state when clients connect to a new match
        if (IsServer && matchEnded.Value)
        {
            matchEnded.Value = false;
            matchKillEvents.Clear();
        }
    }

    /// <summary>
    /// Record a kill event during the match (call this from your kill system)
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RecordKillEventServerRpc(ulong killerClientId, ulong victimClientId)
    {
        if (!IsServer) return;

        // Get wallet addresses from the leaderboard
        string killerWallet = GetWalletAddressForClient(killerClientId);
        string victimWallet = GetWalletAddressForClient(victimClientId);

        if (!string.IsNullOrEmpty(killerWallet) && !string.IsNullOrEmpty(victimWallet))
        {
            var killEvent = new KillEvent
            {
                killerAddress = killerWallet,
                victimAddress = victimWallet,
                timestamp = Time.time
            };

            matchKillEvents.Add(killEvent);

            if (enableDebugLogs)
                Debug.Log($"🔫 Kill recorded: {killerWallet} killed {victimWallet}");
        }
    }

    /// <summary>
    /// Get wallet address for a specific client ID
    /// </summary>
    private string GetWalletAddressForClient(ulong clientId)
    {
        if (leaderboard == null) return null;

        var playerScores = leaderboard.GetPlayerScores();
        var playerData = playerScores.FirstOrDefault(p => p.ClientId == clientId);

        return playerData?.PlayerName; // Assuming PlayerName contains wallet address
    }

    /// <summary>
    /// Call this at the end of the match to send results to Web3.
    /// This will trigger the sequence: kill-data → leaderboard → rewards
    /// </summary>
    public void SendMatchResults()
    {
        if (!IsServer)
        {
            Debug.LogWarning("SendMatchResults can only be called on server!");
            return;
        }

        if (matchEnded.Value)
        {
            Debug.LogWarning("Match results have already been sent!");
            return;
        }

        if (leaderboard == null || smartContractManager == null)
        {
            Debug.LogError("Missing required components for Web3 integration!");
            return;
        }

        matchEnded.Value = true;

        if (enableDebugLogs)
            Debug.Log("🚀 Starting Web3 match results submission sequence...");

        StartCoroutine(SendMatchResultsSequence());
    }

    /// <summary>
    /// Sends match results to Web3 in the correct sequence with delays
    /// </summary>
    private IEnumerator SendMatchResultsSequence()
    {
        // Get current player scores from leaderboard (this has the updated kills/deaths)
        var allPlayers = new List<LeaderboardUI.PlayerScoreData>(leaderboard.GetPlayerScores());

        if (allPlayers.Count == 0)
        {
            Debug.LogWarning("No player data found to send to Web3!");
            yield break;
        }

        // Log current scores for debugging
        if (enableDebugLogs)
        {
            Debug.Log("📊 Current Player Scores:");
            foreach (var player in allPlayers)
            {
                Debug.Log($"Player: {player.PlayerName}, Kills: {player.Kills}, Deaths: {player.Deaths}");
            }
        }

        // Step 1: Send all kill data (use recorded kill events if available, otherwise generate from scores)
        if (enableDebugLogs)
            Debug.Log("📊 Step 1: Sending kill data to blockchain...");

        yield return StartCoroutine(SendAllKillData(allPlayers));

        // Wait before next step
        yield return new WaitForSeconds(delayBetweenCalls);

        // Step 2: Generate leaderboard
        if (enableDebugLogs)
            Debug.Log("🏆 Step 2: Generating game leaderboard...");

        smartContractManager.GenerateGameLeaderboard();

        // Wait before final step
        yield return new WaitForSeconds(delayBetweenCalls);

        // Step 3: Distribute rewards based on leaderboard
        if (enableDebugLogs)
            Debug.Log("💰 Step 3: Distributing rewards...");

        string[] sortedWalletAddresses = GetSortedWalletAddresses(allPlayers);
        smartContractManager.DistributeRewards(sortedWalletAddresses);

        if (enableDebugLogs)
            Debug.Log("✅ Web3 match results submission sequence completed!");
    }

    /// <summary>
    /// Sends kill data for all players to the blockchain
    /// </summary>
    private IEnumerator SendAllKillData(List<LeaderboardUI.PlayerScoreData> players)
    {
        List<KillEvent> killsToSend = new List<KillEvent>();

        // Use recorded kill events if we have them
        if (matchKillEvents.Count > 0)
        {
            killsToSend = new List<KillEvent>(matchKillEvents);
            if (enableDebugLogs)
                Debug.Log($"Using {matchKillEvents.Count} recorded kill events");
        }
        else
        {
            // Fallback: Generate kill events from final scores
            if (enableDebugLogs)
                Debug.Log("No recorded kill events found, generating from final scores...");

            killsToSend = GenerateKillEventsFromScores(players);
        }

        // Send each kill event to the blockchain
        foreach (var killEvent in killsToSend)
        {
            if (enableDebugLogs)
                Debug.Log($"Sending kill data: {killEvent.killerAddress} killed {killEvent.victimAddress}");

            smartContractManager.SendKillData(killEvent.killerAddress, killEvent.victimAddress);

            // Small delay between kill submissions to avoid overwhelming the network
            yield return new WaitForSeconds(0.5f);
        }
    }

    /// <summary>
    /// Generate kill events from final player scores (fallback method)
    /// </summary>
    private List<KillEvent> GenerateKillEventsFromScores(List<LeaderboardUI.PlayerScoreData> players)
    {
        List<KillEvent> generatedKills = new List<KillEvent>();

        // Create a weighted list of potential victims based on their death count
        List<string> victimPool = new List<string>();

        foreach (var player in players)
        {
            for (int i = 0; i < player.Deaths; i++)
            {
                victimPool.Add(player.PlayerName);
            }
        }

        // For each player's kills, assign random victims
        foreach (var player in players)
        {
            for (int i = 0; i < player.Kills && victimPool.Count > 0; i++)
            {
                // Get a random victim (but not themselves)
                List<string> availableVictims = victimPool.Where(v => v != player.PlayerName).ToList();

                if (availableVictims.Count > 0)
                {
                    int randomIndex = Random.Range(0, availableVictims.Count);
                    string victim = availableVictims[randomIndex];

                    generatedKills.Add(new KillEvent
                    {
                        killerAddress = player.PlayerName,
                        victimAddress = victim,
                        timestamp = Time.time
                    });

                    // Remove this victim from the pool
                    victimPool.Remove(victim);
                }
            }
        }

        return generatedKills;
    }
    public void SendKillData(string killerAddress, string victimAddress)
    {
        if (enableDebugLogs)
            Debug.Log($"🔗 Sending individual kill to blockchain: Killer={killerAddress}, Victim={victimAddress}");

        // Store in matchKillEvents list
        matchKillEvents.Add(new KillEvent { killerAddress = killerAddress, victimAddress = victimAddress, timestamp = Time.time });

        // Call SmartContractManager to send immediately
        smartContractManager?.SendKillData(killerAddress, victimAddress);
    }

    /// <summary>
    /// Gets wallet addresses sorted by performance (kills - deaths)
    /// </summary>
    private string[] GetSortedWalletAddresses(List<LeaderboardUI.PlayerScoreData> players)
    {
        // Sort players by performance (kills - deaths) in descending order
        players.Sort((a, b) => {
            int scoreA = a.Kills - a.Deaths;
            int scoreB = b.Kills - b.Deaths;
            return scoreB.CompareTo(scoreA); // Descending order
        });

        // Limit to max 4 players and extract wallet addresses
        int maxPlayers = Mathf.Min(4, players.Count);
        string[] sortedAddresses = new string[maxPlayers];

        for (int i = 0; i < maxPlayers; i++)
        {
            sortedAddresses[i] = players[i].PlayerName; // Assuming PlayerName contains wallet address

            if (enableDebugLogs)
                Debug.Log($"Leaderboard position {i + 1}: {sortedAddresses[i]} (Score: {players[i].Kills - players[i].Deaths}, Kills: {players[i].Kills}, Deaths: {players[i].Deaths})");
        }

        return sortedAddresses;
    }

    /// <summary>
    /// Alternative method: Send detailed match results matrix (your original approach)
    /// Call this instead of SendMatchResults() if you prefer the matrix approach
    /// </summary>
    public void SendMatchResultsAsMatrix()
    {
        if (leaderboard == null) return;
        if (!IsServer) return;

        // Get all player scores with current values
        var allPlayers = new List<LeaderboardUI.PlayerScoreData>(leaderboard.GetPlayerScores());

        // Limit to max 4 players
        int maxPlayers = Mathf.Min(4, allPlayers.Count);

        // Build matrix [N x 3] -> [wallet/ENS, kills, deaths]
        object[,] resultsMatrix = new object[maxPlayers, 3];

        for (int i = 0; i < maxPlayers; i++)
        {
            var player = allPlayers[i];
            resultsMatrix[i, 0] = player.PlayerName; // wallet address / ENS
            resultsMatrix[i, 1] = player.Kills;
            resultsMatrix[i, 2] = player.Deaths;
        }

        // Log the matrix
        LogMatchResultsMatrix(resultsMatrix);
    }

    /// <summary>
    /// Logs the match results matrix for debugging
    /// </summary>
    private void LogMatchResultsMatrix(object[,] matrix)
    {
        if (!enableDebugLogs) return;

        Debug.Log("📊 Match Results Matrix:");
        Debug.Log("Format: [Wallet Address, Kills, Deaths]");

        for (int i = 0; i < matrix.GetLength(0); i++)
        {
            Debug.Log($"Player {i + 1}: {matrix[i, 0]}, Kills: {matrix[i, 1]}, Deaths: {matrix[i, 2]}");
        }
    }

    /// <summary>
    /// Get current match statistics for debugging
    /// </summary>
    public void LogCurrentMatchStats()
    {
        if (!enableDebugLogs) return;
        if (leaderboard == null) return;

        var players = leaderboard.GetPlayerScores();
        Debug.Log($"=== CURRENT MATCH STATS ===");
        Debug.Log($"Recorded Kill Events: {matchKillEvents.Count}");
        Debug.Log($"Active Players: {players.Count}");

        foreach (var player in players)
        {
            Debug.Log($"Player: {player.PlayerName} | Kills: {player.Kills} | Deaths: {player.Deaths}");
        }
    }

    /// <summary>
    /// Force refresh leaderboard data (useful for testing)
    /// </summary>
    [ContextMenu("Refresh Player Data")]
    public void RefreshPlayerData()
    {
        if (!IsServer) return;

        // Force update leaderboard with current ScoreSystem values
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                var scoreSystem = client.PlayerObject.GetComponent<ScoreSystem>();
                if (scoreSystem != null && leaderboard != null)
                {
                    leaderboard.UpdatePlayerScore(client.ClientId, scoreSystem.Kills.Value, scoreSystem.Deaths.Value);
                }
            }
        }
    }
    public void SendKillData(string[] killerVictimPair)
    {
        if (killerVictimPair.Length != 2) return;

        string killer = killerVictimPair[0];
        string victim = killerVictimPair[1];

        if (enableDebugLogs)
            Debug.Log($"🔗 Sending individual kill to blockchain: Killer={killer}, Victim={victim}");

        // Call your SmartContractManager method (or your existing blockchain logic)
        smartContractManager?.SendKillData(killer, victim);

        // Also optionally store in matchKillEvents
        matchKillEvents.Add(new KillEvent { killerAddress = killer, victimAddress = victim, timestamp = Time.time });
    }

}

/// <summary>
/// Helper class to represent a kill event
/// </summary>
[System.Serializable]
public class KillEvent
{
    public string killerAddress;
    public string victimAddress;
    public float timestamp; // When the kill happened

    public KillEvent()
    {
        timestamp = Time.time;
    }
}