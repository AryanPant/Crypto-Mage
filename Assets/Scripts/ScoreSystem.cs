using UnityEngine;
using Unity.Netcode;

public class ScoreSystem : NetworkBehaviour
{
    private LeaderboardUI leaderboardUI;
    private MatchResultsWeb3Sender web3Sender;
    public NetworkVariable<int> Kills = new NetworkVariable<int>(0);
    public NetworkVariable<int> Deaths = new NetworkVariable<int>(0);

    public void AddKill() => Kills.Value++;
    public void AddDeath() => Deaths.Value++;

    private void Start()
    {
        leaderboardUI = Object.FindFirstObjectByType<LeaderboardUI>();
        web3Sender = Object.FindFirstObjectByType<MatchResultsWeb3Sender>();
    }

    // Call this when a player kills another player  
    public void RegisterKill(ulong killerId, ulong victimId)
    {
        if (leaderboardUI == null) leaderboardUI = Object.FindFirstObjectByType<LeaderboardUI>();
        if (web3Sender == null) web3Sender = Object.FindFirstObjectByType<MatchResultsWeb3Sender>();
        if (killerId == NetworkManager.Singleton.LocalClientId)
            AddKill();

        if (victimId == NetworkManager.Singleton.LocalClientId)
            AddDeath();

        // ✅ Update local scores  
        leaderboardUI.UpdatePlayerScore(killerId,
            leaderboardUI.GetKills(killerId) + 1,
            leaderboardUI.GetDeaths(killerId));

        leaderboardUI.UpdatePlayerScore(victimId,
            leaderboardUI.GetKills(victimId),
            leaderboardUI.GetDeaths(victimId) + 1);

        // ✅ Get corresponding wallet addresses  
        string killerAddress = leaderboardUI.GetWalletAddress(killerId);
        string victimAddress = leaderboardUI.GetWalletAddress(victimId);

        // ✅ Send kill data to blockchain  
        if (IsServer && web3Sender != null)
        {
            string[] pair = new string[] { killerAddress, victimAddress };
            web3Sender.SendKillData(pair);
        }

        Debug.Log($"💀 Kill registered | Killer: {killerAddress} | Victim: {victimAddress}");
    }
}
