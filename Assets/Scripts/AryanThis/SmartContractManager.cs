using System;
using UnityEngine;
using SocketIOClient;
using Newtonsoft.Json;

public class SmartContractManager : MonoBehaviour
{
    private SocketIOUnity socket;

    [Header("Configuration")]
    public string serverURL = "http://localhost:3000";
    public string userWalletAddress;

    void Start()
    {
        InitializeSocket();
        if (addressStorage.Instance == null)
        {
            Debug.LogWarning("AddressStorage instance is not available.");
        }
    }

    void Update()
    {
        // Update the wallet address if it hasn't been set yet
        if (string.IsNullOrEmpty(userWalletAddress) && addressStorage.Instance != null)
        {
            userWalletAddress = addressStorage.Instance.Getaddress();
        }
    }

    void InitializeSocket()
    {
        var uri = new Uri(serverURL);
        socket = new SocketIOUnity(uri);

        socket.OnConnected += (sender, e) =>
        {
            Debug.Log("Connected to server!");
        };

        // GAME EVENT RESPONSES
        socket.On("kill-data-result", (response) =>
        {
            var result = JsonConvert.DeserializeObject<KillDataResult>(response.ToString());
            OnKillDataResult(result);
        });

        socket.On("generate-game-leaderboard-result", (response) =>
        {
            var result = JsonConvert.DeserializeObject<GenerateLeaderboardResult>(response.ToString());
            OnGenerateLeaderboardResult(result);
        });

        socket.On("distribute-rewards-result", (response) =>
        {
            var result = JsonConvert.DeserializeObject<DistributeRewardsResult>(response.ToString());
            OnDistributeRewardsResult(result);
        });

        socket.Connect();
    }

    // ===== GAME CONTRACT METHODS =====

    public void SendKillData(string killerAddress, string victimAddress)
    {
        var data = new { killerAddress = killerAddress, victimAddress = victimAddress };
        socket.Emit("send-kill-data", data);
        Debug.Log($"Sending kill data: killer={killerAddress}, victim={victimAddress}");
    }

    public void GenerateGameLeaderboard()
    {
        socket.Emit("generate-game-leaderboard");
        Debug.Log("Requesting generate-game-leaderboard...");
    }

    public void DistributeRewards(string[] leaderboard)
    {
        var data = new { leaderboard = leaderboard };
        socket.Emit("distribute-rewards", data);
        Debug.Log("Requesting distribute rewards...");
    }

    // ===== RESPONSE HANDLERS =====

    private void OnKillDataResult(KillDataResult result)
    {
        if (result.success)
        {
            Debug.Log($"? Kill recorded successfully! TxHash: {result.txHash}");
            // Handle successful kill recording
        }
        else
        {
            Debug.LogError($"? Failed to record kill: {result.error}");
            // Handle kill recording error
        }
    }

    private void OnGenerateLeaderboardResult(GenerateLeaderboardResult result)
    {
        if (result.success)
        {
            Debug.Log($"? Leaderboard generated successfully! TxHash: {result.txHash}");
            Debug.Log($"Leaderboard: {JsonConvert.SerializeObject(result.leaderboard)}");
            // Handle successful leaderboard generation
            // You can now use result.leaderboard for UI updates
        }
        else
        {
            Debug.LogError($"? Failed to generate leaderboard: {result.error}");
            // Handle leaderboard generation error
        }
    }

    private void OnDistributeRewardsResult(DistributeRewardsResult result)
    {
        if (result.success)
        {
            Debug.Log($"? Rewards distributed successfully! TxHash: {result.txHash}");
            // Handle successful reward distribution
        }
        else
        {
            Debug.LogError($"? Failed to distribute rewards: {result.error}");
            // Handle reward distribution error
        }
    }

    void OnDestroy()
    {
        if (socket != null)
        {
            socket.Disconnect();
            socket.Dispose();
        }
    }
}

// ===== DATA MODELS =====

[System.Serializable]
public class KillDataResult
{
    public bool success;
    public string txHash;
    public string killerAddress;
    public string victimAddress;
    public string error;
}

[System.Serializable]
public class GenerateLeaderboardResult
{
    public bool success;
    public string txHash;
    public string[] leaderboard;
    public string error;
}

[System.Serializable]
public class DistributeRewardsResult
{
    public bool success;
    public string txHash;
    public string[] leaderboard;
    public string error;
}