using System;
using UnityEngine;
using Newtonsoft.Json;
using System.Numerics;
using Thirdweb.Unity;
using Thirdweb;
using System.Collections;

public class SmartContractManager : MonoBehaviour
{
    [Header("Configuration")]
    public string serverURL = "http://localhost:3000";
    public string userWalletAddress;

    [Header("Contract Settings")]
    private string gameContractAddress = "0x91ADeF47103B72f9C771f14eDf5f4BDB88da0b2d";
    private ulong chainId = 296; // Base Sepolia

    [Header("Initialization Settings")]
    [SerializeField] private float maxWaitTime = 10f;
    [SerializeField] private float checkInterval = 0.5f;

    private ThirdwebContract gameContract;
    private bool isInitialized = false;

    void Start()
    {
        StartCoroutine(InitializationCoroutine());
    }

    void Update()
    {
        // Update the wallet address if it hasn't been set yet
        if (string.IsNullOrEmpty(userWalletAddress) && addressStorage.Instance != null)
        {
            userWalletAddress = addressStorage.Instance.Getaddress();
        }
    }

    private IEnumerator InitializationCoroutine()
    {
        yield return StartCoroutine(WaitForThirdwebManager());

        if (!isInitialized)
        {
            UnityEngine.Debug.LogError("Failed to initialize ThirdwebManager within the timeout period.");
            yield break;
        }

        yield return StartCoroutine(InitializeContractsCoroutine());
    }

    private IEnumerator WaitForThirdwebManager()
    {
        float elapsedTime = 0f;

        while (elapsedTime < maxWaitTime)
        {
            if (ThirdwebManager.Instance != null)
            {
                UnityEngine.Debug.Log("ThirdwebManager.Instance found!");
                isInitialized = true;
                yield break;
            }

            UnityEngine.Debug.Log($"Waiting for ThirdwebManager... ({elapsedTime:F1}s/{maxWaitTime}s)");
            yield return new WaitForSeconds(checkInterval);
            elapsedTime += checkInterval;
        }

        UnityEngine.Debug.LogError($"ThirdwebManager.Instance is still null after {maxWaitTime} seconds!");
        isInitialized = false;
    }

    private IEnumerator InitializeContractsCoroutine()
    {
        var task = InitializeContracts();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted)
        {
            UnityEngine.Debug.LogError($"Contract initialization failed: {task.Exception}");
        }
    }

    private async System.Threading.Tasks.Task InitializeContracts()
    {
        string gameAbi = @"[
            {
                ""inputs"": [
                    {
                        ""internalType"": ""address[]"",
                        ""name"": ""leaderboard"",
                        ""type"": ""address[]""
                    }
                ],
                ""name"": ""distributeRewards"",
                ""outputs"": [],
                ""stateMutability"": ""nonpayable"",
                ""type"": ""function""
            }
        ]";

        try
        {
            gameContract = await ThirdwebManager.Instance.GetContract(gameContractAddress, chainId, gameAbi);

            if (gameContract == null)
            {
                UnityEngine.Debug.LogError("Failed to initialize game contract");
            }
            else
            {
                UnityEngine.Debug.Log("Game contract initialized successfully");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Error initializing contracts: {e.Message}");
        }
    }

    private bool IsFullyInitialized()
    {
        return isInitialized &&
               ThirdwebManager.Instance != null &&
               gameContract != null;
    }

    // ===== GAME CONTRACT METHODS =====
    public void SendKillData(string killerAddress, string victimAddress)
    {
        if (string.IsNullOrEmpty(killerAddress) || string.IsNullOrEmpty(victimAddress))
        {
            UnityEngine.Debug.LogError("Killer or victim address is null or empty");
            return;
        }

        UnityEngine.Debug.Log($"Sending kill data: killer={killerAddress}, victim={victimAddress}");

        // Just log for debugging - no actual contract call
        OnKillDataResult(new KillDataResult
        {
            success = true,
            txHash = "DEBUG_MODE",
            killerAddress = killerAddress,
            victimAddress = victimAddress
        });
    }

    public void GenerateGameLeaderboard()
    {
        UnityEngine.Debug.Log("Requesting generate-game-leaderboard...");

        // Just log for debugging - no actual contract call
        OnGenerateLeaderboardResult(new GenerateLeaderboardResult
        {
            success = true,
            txHash = "DEBUG_MODE",
            leaderboard = new string[] { }
        });
    }

    public async void DistributeRewards(string[] leaderboard)
    {
        if (!IsFullyInitialized())
        {
            UnityEngine.Debug.LogError("SmartContractManager is not fully initialized. Cannot distribute rewards.");
            return;
        }

        if (leaderboard == null || leaderboard.Length == 0)
        {
            UnityEngine.Debug.LogError("Leaderboard is null or empty");
            return;
        }

        try
        {
            var wallet = ThirdwebManager.Instance.GetActiveWallet();
            if (wallet == null)
            {
                UnityEngine.Debug.LogError("No active wallet found");
                return;
            }

            UnityEngine.Debug.Log($"Requesting distribute rewards for {leaderboard.Length} players...");
            UnityEngine.Debug.Log($"Leaderboard: {JsonConvert.SerializeObject(leaderboard)}");

            var distributeRewardsTx = await gameContract.Prepare(
                wallet: wallet,
                method: "distributeRewards",
                weiValue: BigInteger.Zero,
                parameters: new object[] { leaderboard }
            );

            if (distributeRewardsTx == null)
            {
                UnityEngine.Debug.LogError("Failed to prepare distributeRewards transaction");
                return;
            }

            var txHash = await ThirdwebTransaction.Send(distributeRewardsTx);

            if (string.IsNullOrEmpty(txHash))
            {
                UnityEngine.Debug.LogError("Distribute rewards transaction hash is null or empty");
                return;
            }

            UnityEngine.Debug.Log($"✅ Rewards distributed successfully! TxHash: {txHash}");

            OnDistributeRewardsResult(new DistributeRewardsResult
            {
                success = true,
                txHash = txHash,
                leaderboard = leaderboard
            });
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"DistributeRewards error: {e}");

            OnDistributeRewardsResult(new DistributeRewardsResult
            {
                success = false,
                error = e.Message,
                leaderboard = leaderboard
            });
        }
    }

    // ===== RESPONSE HANDLERS =====
    private void OnKillDataResult(KillDataResult result)
    {
        if (result.success)
        {
            UnityEngine.Debug.Log($"🎯 Kill recorded successfully! TxHash: {result.txHash}");
            // Handle successful kill recording
        }
        else
        {
            UnityEngine.Debug.LogError($"❌ Failed to record kill: {result.error}");
            // Handle kill recording error
        }
    }

    private void OnGenerateLeaderboardResult(GenerateLeaderboardResult result)
    {
        if (result.success)
        {
            UnityEngine.Debug.Log($"🏆 Leaderboard generated successfully!");
            UnityEngine.Debug.Log($"Leaderboard: {JsonConvert.SerializeObject(result.leaderboard)}");
            // Handle successful leaderboard generation
            // You can now use result.leaderboard for UI updates
        }
        else
        {
            UnityEngine.Debug.LogError($"❌ Failed to generate leaderboard: {result.error}");
            // Handle leaderboard generation error
        }
    }

    private void OnDistributeRewardsResult(DistributeRewardsResult result)
    {
        if (result.success)
        {
            UnityEngine.Debug.Log($"💰 Rewards distributed successfully! TxHash: {result.txHash}");
            // Handle successful reward distribution
        }
        else
        {
            UnityEngine.Debug.LogError($"❌ Failed to distribute rewards: {result.error}");
            // Handle reward distribution error
        }
    }

    void OnDestroy()
    {
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