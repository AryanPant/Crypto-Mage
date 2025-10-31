using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Numerics;
using Thirdweb.Unity;
using Thirdweb;
using System.Net.Http.Headers;
//using Unity.Android.Gradle.Manifest;
using System;
using System.Linq;
using Unity.Netcode;
using System.Collections;
using UnityEngine.SceneManagement;

public class SimpleLobbyManager : MonoBehaviour
{
    bool staked = false;
    public int click = 0;
    private string lobbyAddress = "0x91ADeF47103B72f9C771f14eDf5f4BDB88da0b2d";
    private string chestAddress = "0x61146B3Dd96e03B8fF0F7fcd2A53701d362C9Bd6";

    private ulong chainId = 296; // Base Sepolia
    [SerializeField] private GameObject waiting; // Base Sepolia
    //[SerializeField] private GameObject hostwaiting; // Base Sepolia
    [SerializeField] private GameObject buttonStaked; // Base Sepolia

    [Header("Initialization Settings")]
    [SerializeField] private float maxWaitTime = 10f; // Maximum time to wait for ThirdwebManager
    [SerializeField] private float checkInterval = 0.5f; // How often to check for ThirdwebManager

    //[SerializeField] private string stakeAmountEth = "0.01"; // Adjust as needed

    [Header("UI")]
    //[SerializeField] private Button joinLobbyButton;
    //[SerializeField] private Button Setusername;
    //[SerializeField] private Button OpenChestButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text coinwon;
    //[SerializeField] private TMP_Text stakeAmountText;

    private ThirdwebContract contractlobby;
    private ThirdwebContract contractchest;
    private bool isInitialized = false;

    private void Start()
    {
        // Start the initialization process
        StartCoroutine(InitializationCoroutine());
    }

    private IEnumerator InitializationCoroutine()
    {
        // Wait for ThirdwebManager to be initialized
        yield return StartCoroutine(WaitForThirdwebManager());

        if (!isInitialized)
        {
            UnityEngine.Debug.LogError("Failed to initialize ThirdwebManager within the timeout period.");
            yield break;
        }

        // Initialize contracts (we'll need to wrap the async call)
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
        string lobbyAbi = @"[
            {
                ""inputs"": [],
                ""name"": ""stakeAndJoin"",
                ""outputs"": [],
                ""stateMutability"": ""payable"",
                ""type"": ""function""
            },
            {
                ""inputs"": [{""internalType"": ""string"", ""name"": ""_name"", ""type"": ""string""}],
                ""name"": ""setUsername"",
                ""outputs"": [],
                ""stateMutability"": ""nonpayable"",
                ""type"": ""function""
            },
            {
                ""inputs"": [],
                ""name"": ""startGame"",
                ""outputs"": [],
                ""stateMutability"": ""nonpayable"",
                ""type"": ""function""
            }
        ]";

        string ChestAbi = @"[
            {
                ""inputs"": [],
                ""name"": ""openChest"",
                ""outputs"": [
                    {
                        ""internalType"": ""uint256"",
                        ""name"": ""coinsWon"",
                        ""type"": ""uint256""
                    }
                ],
                ""stateMutability"": ""nonpayable"",
                ""type"": ""function""
            },
            {
                ""inputs"": [
                    {
                        ""internalType"": ""address"",
                        ""name"": ""user"",
                        ""type"": ""address""
                    }
                ],
                ""name"": ""getCoins"",
                ""outputs"": [
                    {
                        ""internalType"": ""uint256"",
                        ""name"": """",
                        ""type"": ""uint256""
                    }
                ],
                ""stateMutability"": ""view"",
                ""type"": ""function""
            },
            {
                ""inputs"": [
                    {
                        ""internalType"": ""uint256"",
                        ""name"": ""amount"",
                        ""type"": ""uint256""
                    }
                ],
                ""name"": ""spendCoins"",
                ""outputs"": [],
                ""stateMutability"": ""nonpayable"",
                ""type"": ""function""
            }
        ]";

        try
        {
            contractlobby = await ThirdwebManager.Instance.GetContract(lobbyAddress, chainId, lobbyAbi);
            contractchest = await ThirdwebManager.Instance.GetContract(chestAddress, chainId, ChestAbi);

            if (contractlobby == null)
            {
                UnityEngine.Debug.LogError("Failed to initialize lobby contract");
            }
            else
            {
                UnityEngine.Debug.Log("Lobby contract initialized successfully");
            }

            if (contractchest == null)
            {
                UnityEngine.Debug.LogError("Failed to initialize chest contract");
            }
            else
            {
                UnityEngine.Debug.Log("Chest contract initialized successfully");
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Error initializing contracts: {e.Message}");
        }

        //joinLobbyButton.onClick.AddListener(JoinLobby);
        //OpenChestButton.onClick.AddListener(OpenChest);
        //Setusername.onClick.AddListener(setusername);
        //stakeAmountText.text = $"Stake: {stakeAmountEth} ETH";
    }

    // Helper method to check if everything is properly initialized
    private bool IsFullyInitialized()
    {
        return isInitialized &&
               ThirdwebManager.Instance != null &&
               contractlobby != null &&
               contractchest != null;
    }

    public async void setusername(string kutta)
    {
        // Check if fully initialized
        if (!IsFullyInitialized())
        {
            UnityEngine.Debug.LogError("SimpleLobbyManager is not fully initialized. Cannot set username.");
            return;
        }

        // Null and empty string checks
        if (string.IsNullOrEmpty(kutta))
        {
            UnityEngine.Debug.LogError("Username cannot be null or empty");
            return;
        }

        //statusText.text = "Setting username...";
        try
        {
            var wallet = ThirdwebManager.Instance.GetActiveWallet();
            if (wallet == null)
            {
                UnityEngine.Debug.LogError("No active wallet found");
                return;
            }

            // Set username using Prepare and Send pattern
            var setUsernameTx = await contractlobby.Prepare(
                wallet: wallet,
                method: "setUsername",
                weiValue: BigInteger.Zero,
                parameters: new object[] { kutta }
            );

            if (setUsernameTx == null)
            {
                UnityEngine.Debug.LogError("Failed to prepare setUsername transaction");
                return;
            }

            var usernameTransactionHash = await ThirdwebTransaction.Send(setUsernameTx);

            if (string.IsNullOrEmpty(usernameTransactionHash))
            {
                UnityEngine.Debug.LogError("Transaction hash is null or empty");
                return;
            }

            UnityEngine.Debug.Log($"Username set successfully! TX: {usernameTransactionHash}");
            // statusText.text = $"Username set! TX: {usernameTransactionHash}";
        }
        catch (System.Exception e)
        {
            //  statusText.text = $"Error: {e.Message}";
            UnityEngine.Debug.LogError($"SetUsername error: {e}");
        }
    }

    public async void JoinLobby()
    {
        // Check if fully initialized
        if (!IsFullyInitialized())
        {
            UnityEngine.Debug.LogError("SimpleLobbyManager is not fully initialized. Cannot join lobby.");
            click = 0;
            staked = false;
            return;
        }

        try
        {
            var wallet = ThirdwebManager.Instance.GetActiveWallet();
            if (wallet == null)
            {
                UnityEngine.Debug.LogError("No active wallet found");
                click = 0;
                staked = false;
                return;
            }

            BigInteger weiValue = BigInteger.Parse("1000000000000000000");

            // 2. Then stake and join
            //    statusText.text = "Joining lobby...";
            //var weiValue = Utils.ToWei(stakeAmountEth);

            var stakeAndJoinTx = await contractlobby.Prepare(
                wallet: wallet,
                method: "stakeAndJoin",
                weiValue: weiValue
            );

            if (stakeAndJoinTx == null)
            {
                UnityEngine.Debug.LogError("Failed to prepare stakeAndJoin transaction");
                click = 0;
                staked = false;
                return;
            }

            var joinTransactionHash = await ThirdwebTransaction.Send(stakeAndJoinTx);

            if (string.IsNullOrEmpty(joinTransactionHash))
            {
                UnityEngine.Debug.LogError("Join transaction hash is null or empty");
                click = 0;
                staked = false;
                return;
            }

            staked = true;
            UnityEngine.Debug.Log($"Joined lobby successfully! TX: {joinTransactionHash}");
            afterstaked();
            // statusText.text = $"Joined lobby! TX: {joinTransactionHash}";
        }
        catch (System.Exception e)
        {
            //   statusText.text = $"Error: {e.Message}";
            UnityEngine.Debug.LogError($"JoinLobby error: {e}");
            staked = false;
            click = 0;
        }
    }

    public async void StartGame()
    {
        // Check if fully initialized
        if (!IsFullyInitialized())
        {
            UnityEngine.Debug.LogError("SimpleLobbyManager is not fully initialized. Cannot start game.");
            return;
        }

        //statusText.text = "Starting game...";
        try
        {
            var wallet = ThirdwebManager.Instance.GetActiveWallet();
            if (wallet == null)
            {
                UnityEngine.Debug.LogError("No active wallet found");
                return;
            }

            // Set username using Prepare and Send pattern
            var startGameTx = await contractlobby.Prepare(
                wallet: wallet,
                method: "startGame",
                weiValue: BigInteger.Zero
            );

            if (startGameTx == null)
            {
                UnityEngine.Debug.LogError("Failed to prepare startGame transaction");
                return;
            }

            var startGameTransactionHash = await ThirdwebTransaction.Send(startGameTx);

            if (string.IsNullOrEmpty(startGameTransactionHash))
            {
                UnityEngine.Debug.LogError("Transaction hash is null or empty");
                return;
            }

            UnityEngine.Debug.Log($"Game Started without money! TX: {startGameTransactionHash}");
            // statusText.text = $"Game started! TX: {startGameTransactionHash}";
        }
        catch (System.Exception e)
        {
            //  statusText.text = $"Error: {e.Message}";
            UnityEngine.Debug.LogError($"Gamestart error: {e}");
        }
    }

    public async void OpenChest()
    {
        // Check if fully initialized
        if (!IsFullyInitialized())
        {
            UnityEngine.Debug.LogError("SimpleLobbyManager is not fully initialized. Cannot open chest.");
            return;
        }

        //statusText.text = "Opening chest...";
        try
        {
            var wallet = ThirdwebManager.Instance.GetActiveWallet();
            if (wallet == null)
            {
                UnityEngine.Debug.LogError("No active wallet found");
                return;
            }

            // Get balance before opening chest
            var userAddress = await wallet.GetAddress();
            var balanceBefore = await contractchest.Read<BigInteger>(
                method: "getCoins",
                parameters: new object[] { userAddress }
            );

            UnityEngine.Debug.Log($"Balance before opening chest: {balanceBefore}");
            UnityEngine.Debug.Log("Preparing openChest transaction...");

            // Prepare transaction for openChest
            var openChestTx = await contractchest.Prepare(
                wallet: wallet,
                method: "openChest",
                weiValue: BigInteger.Zero
            );

            if (openChestTx == null)
            {
                UnityEngine.Debug.LogError("Failed to prepare openChest transaction");
                return;
            }

            UnityEngine.Debug.Log("Sending openChest transaction...");

            // Send transaction
            var txHash = await ThirdwebTransaction.Send(openChestTx);

            if (string.IsNullOrEmpty(txHash))
            {
                UnityEngine.Debug.LogError("Open chest transaction hash is null or empty");
                return;
            }

            UnityEngine.Debug.Log($"Chest opened successfully! TX: {txHash}");
            UnityEngine.Debug.Log("Waiting for transaction to be mined...");

            // Wait a bit for the transaction to be mined
            await System.Threading.Tasks.Task.Delay(3000);

            // Get balance after opening chest
            var balanceAfter = await contractchest.Read<BigInteger>(
                method: "getCoins",
                parameters: new object[] { userAddress }
            );

            BigInteger coinsWon = balanceAfter - balanceBefore;
            UnityEngine.Debug.Log($"Balance after opening chest: {balanceAfter}");
            UnityEngine.Debug.Log($"🎉 Coins won from chest: {coinsWon}");

            // Update UI
            if (coinwon != null)
            {
                coinwon.text = $"{coinsWon}";
            }

            if (statusText != null)
            {
                statusText.text = $"{balanceAfter}";
            }
            GetMyCoins();

            //statusText.text = $"Chest opened! Won {coinsWon} coins! Total: {balanceAfter}";
        }
        catch (System.Exception e)
        {
            //statusText.text = $"Error: {e.Message}";
            UnityEngine.Debug.LogError($"OpenChest error: {e}");
        }
    }

    public async void GetCoins(string userAddress)
    {
        // Check if fully initialized
        if (!IsFullyInitialized())
        {
            UnityEngine.Debug.LogError("SimpleLobbyManager is not fully initialized. Cannot get coins.");
            return;
        }

        // Null and empty string checks
        if (string.IsNullOrEmpty(userAddress))
        {
            UnityEngine.Debug.LogError("User address cannot be null or empty");
            return;
        }

        try
        {
            UnityEngine.Debug.Log($"Getting coins for address: {userAddress}");

            // Read coins balance for the user
            var coinsBalance = await contractchest.Read<BigInteger>(
                method: "getCoins",
                parameters: new object[] { userAddress }
            );

            UnityEngine.Debug.Log($"Coins balance: {coinsBalance}");
            //statusText.text = $"Coins: {coinsBalance}";
        }
        catch (System.Exception e)
        {
            //statusText.text = $"Error: {e.Message}";
            UnityEngine.Debug.LogError($"GetCoins error: {e}");
        }
    }

    public async void GetMyCoins()
    {
        // Check if fully initialized
        if (!IsFullyInitialized())
        {
            UnityEngine.Debug.LogError("SimpleLobbyManager is not fully initialized. Cannot get coins.");
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

            var userAddress = await wallet.GetAddress();
            if (string.IsNullOrEmpty(userAddress))
            {
                UnityEngine.Debug.LogError("Failed to get user address");
                return;
            }

            UnityEngine.Debug.Log($"Getting coins for my address: {userAddress}");

            // Read coins balance for the current user
            var coinsBalance = await contractchest.Read<BigInteger>(
                method: "getCoins",
                parameters: new object[] { userAddress }
            );

            UnityEngine.Debug.Log($"My coins balance: {coinsBalance}");

            if (statusText != null)
            {
                statusText.text = $"{coinsBalance}";
            }
        }
        catch (System.Exception e)
        {
            //statusText.text = $"Error: {e.Message}";
            UnityEngine.Debug.LogError($"GetMyCoins error: {e}");
        }
    }

    public async void SpendCoins(string amount)
    {
        // Check if fully initialized
        if (!IsFullyInitialized())
        {
            UnityEngine.Debug.LogError("SimpleLobbyManager is not fully initialized. Cannot spend coins.");
            return;
        }

        // Null and empty string checks
        if (string.IsNullOrEmpty(amount))
        {
            UnityEngine.Debug.LogError("Amount cannot be null or empty");
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

            BigInteger amountToSpend = BigInteger.Parse(amount);

            UnityEngine.Debug.Log($"Preparing to spend {amountToSpend} coins...");

            // Prepare transaction for spendCoins
            var spendCoinsTx = await contractchest.Prepare(
                wallet: wallet,
                method: "spendCoins",
                weiValue: BigInteger.Zero,
                parameters: new object[] { amountToSpend }
            );

            if (spendCoinsTx == null)
            {
                UnityEngine.Debug.LogError("Failed to prepare spendCoins transaction");
                return;
            }

            UnityEngine.Debug.Log("Sending spendCoins transaction...");

            // Send transaction
            var txHash = await ThirdwebTransaction.Send(spendCoinsTx);

            if (string.IsNullOrEmpty(txHash))
            {
                UnityEngine.Debug.LogError("Spend coins transaction hash is null or empty");
                return;
            }

            UnityEngine.Debug.Log($"Coins spent successfully! TX: {txHash}");
            //statusText.text = $"Spent {amountToSpend} coins! TX: {txHash}";
        }
        catch (System.Exception e)
        {
            //statusText.text = $"Error: {e.Message}";
            UnityEngine.Debug.LogError($"SpendCoins error: {e}");
        }
    }

    public void afterstaked()
    {
        if (!staked && click == 0)
        {
            JoinLobby();
            click = 1;
        }
        if (staked)
        {
            // Null check for UI GameObjects
            if (buttonStaked != null)
            {
                buttonStaked.SetActive(false);
                UnityEngine.Debug.Log("buttonStaked GameObject set to inactive");
            }
            else
            {
                UnityEngine.Debug.LogWarning("buttonStaked GameObject is null");
            }

            if (waiting != null)
            {
                waiting.SetActive(true);
                UnityEngine.Debug.Log("waiting GameObject set to active");
            }
            else
            {
                UnityEngine.Debug.LogWarning("waiting GameObject is null");
            }
        }

    }

    public void BackToLobby()
    {
        SceneManager.LoadScene("Lobby");
    }
}