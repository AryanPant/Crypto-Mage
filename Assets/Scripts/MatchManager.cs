using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using TMPro;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance;

    [Header("Match Settings")]
    public float matchDuration = 300f; // 5 minutes
    private float timer;
    private bool matchOver = false;

    [Header("UI References")]
    public TextMeshProUGUI timerText;   // optional countdown
    public GameObject resultsPanel;     // overlay panel for final results
    [SerializeField] private LeaderboardUI leaderboard;  // reference to your leaderboard
    [SerializeField] private MatchResultsWeb3Sender matchResultsWeb3Sender;
    //[SerializeField] private 
    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public override void OnNetworkSpawn()
    {
    leaderboard = FindFirstObjectByType<LeaderboardUI>();

        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        if (IsServer)
        {
            timer = matchDuration;
            InvokeRepeating(nameof(ServerTick), 1f, 1f); // tick every second
        }
    }

    private void ServerTick()
    {
        if (matchOver) return;

        timer -= 1f;
        UpdateTimerClientRpc(timer);

        if (timer <= 0f)
        {
            EndMatch();
        }
    }

    [ClientRpc]
    private void UpdateTimerClientRpc(float newTime)
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(newTime / 60);
            int seconds = Mathf.FloorToInt(newTime % 60);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    // Add this updated EndMatch method to your MatchManager class

    private void EndMatch()
    {
        matchOver = true;
        CancelInvoke(nameof(ServerTick));

        Debug.Log("🏁 Match ended! Processing results...");

        // 1️⃣ Despawn all player objects before showing results
        if (IsServer)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject != null)
                {
                    client.PlayerObject.Despawn(true); // true = destroy instance
                }
            }
        }

        // 2️⃣ Show final leaderboard/results to all clients
        ShowFinalResultsClientRpc();

        // 3️⃣ Send results to Web3 (only on server to avoid duplicates)
        if (IsServer)
        {
            var web3Sender = matchResultsWeb3Sender;
            if (web3Sender != null)
            {
                Debug.Log("🔗 Initiating Web3 results submission...");
                web3Sender.SendMatchResults();
            }
            else
            {
                Debug.LogError("❌ MatchResultsWeb3Sender not found! Web3 integration failed.");
            }
        }

        // 4️⃣ After longer delay to allow Web3 transactions, return to main scene
        Invoke(nameof(ReturnToMainScene), 10f); // Increased delay for Web3 processing
    }
    [ClientRpc]
    private void ShowFinalResultsClientRpc()
    {
        if (resultsPanel != null)
            resultsPanel.SetActive(true);

        if (leaderboard != null)
        {
            // Force leaderboard visible so players see scores
            leaderboard.leaderboardPanel.SetActive(true);

            // Manually call refresh (even if leaderboard UI was hidden before)
            leaderboard.SendMessage("RefreshLeaderboard", SendMessageOptions.DontRequireReceiver);
        }

        // Optional: Add "GAME OVER" text on results panel
        var title = resultsPanel.transform.Find("TitleText");
        if (title != null)
        {
            var text = title.GetComponent<TextMeshProUGUI>();
            if (text != null)
                text.text = "GAME OVER";
        }
    }

    private void ReturnToMainScene()
    {
        if (IsServer)
        {
            NetworkManager.SceneManager.LoadScene("Main Scene", LoadSceneMode.Single);
        }
    }
}
