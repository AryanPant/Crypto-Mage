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

public class SimpleLobbyManager : MonoBehaviour
{
    bool staked = false;
    public int click = 0;
    private string lobbyAddress = "0xf9caD84FC8d2FC68996Baf3fA550f823B0aBeF3D";
    private string chestAddress = "0xb79d56D7C5707588737C3efb718b7406232bd3FF";

    [SerializeField] private ulong chainId = 84532; // Base Sepolia
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
    //[SerializeField] private Button RequestChestOpeningbuttton;
    //[SerializeField] private Button ActivateFallbackbutton;
    //[SerializeField] private TMP_Text statusText;
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
            Debug.LogError("Failed to initialize ThirdwebManager within the timeout period.");
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
                Debug.Log("ThirdwebManager.Instance found!");
                isInitialized = true;
                yield break;
            }

            Debug.Log($"Waiting for ThirdwebManager... ({elapsedTime:F1}s/{maxWaitTime}s)");
            yield return new WaitForSeconds(checkInterval);
            elapsedTime += checkInterval;
        }

        Debug.LogError($"ThirdwebManager.Instance is still null after {maxWaitTime} seconds!");
        isInitialized = false;
    }

    private IEnumerator InitializeContractsCoroutine()
    {
        var task = InitializeContracts();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted)
        {
            Debug.LogError($"Contract initialization failed: {task.Exception}");
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
            }
        ]";

        string ChestAbi = @"[
    {
        ""inputs"": [{ ""internalType"": ""uint64"", ""name"": ""sequenceNumber"", ""type"": ""uint64""}],
        ""name"": ""activateFallback"",
        ""outputs"": [],
        ""stateMutability"": ""nonpayable"",
        ""type"": ""function""
    },
    {
        ""inputs"": [],
        ""name"": ""requestChestOpening"",
        ""outputs"": [{ ""internalType"": ""uint64"", ""name"": ""sequenceNumber"", ""type"": ""uint64""}],
        ""stateMutability"": ""payable"",
        ""type"": ""function""
    },
    {
        ""inputs"": [{ ""internalType"": ""address"", ""name"": ""user"", ""type"": ""address""}],
        ""name"": ""getUserChestRequests"",
        ""outputs"": [
            { ""internalType"": ""uint64[]"", ""name"": ""sequenceNumbers"", ""type"": ""uint64[]""},
            { ""internalType"": ""bool[]"", ""name"": ""fulfilled"", ""type"": ""bool[]""},
            { ""internalType"": ""uint256[]"", ""name"": ""coinsWon"", ""type"": ""uint256[]""},
            { ""internalType"": ""uint256[]"", ""name"": ""timestamps"", ""type"": ""uint256[]""},
            { ""internalType"": ""string[]"", ""name"": ""status"", ""type"": ""string[]""},
            { ""internalType"": ""string[]"", ""name"": ""randomnessSource"", ""type"": ""string[]""}
        ],
        ""stateMutability"": ""view"",
        ""type"": ""function""
    },
    {
        ""inputs"": [{ ""internalType"": ""uint64"", ""name"": ""sequenceNumber"", ""type"": ""uint64""}],
        ""name"": ""canActivateFallback"",
        ""outputs"": [{ ""internalType"": ""bool"", ""name"": """", ""type"": ""bool""}],
        ""stateMutability"": ""view"",
        ""type"": ""function""
    }
]";

        try
        {
            contractlobby = await ThirdwebManager.Instance.GetContract(lobbyAddress, chainId, lobbyAbi);
            contractchest = await ThirdwebManager.Instance.GetContract(chestAddress, chainId, ChestAbi);

            if (contractlobby == null)
            {
                Debug.LogError("Failed to initialize lobby contract");
            }
            else
            {
                Debug.Log("Lobby contract initialized successfully");
            }

            if (contractchest == null)
            {
                Debug.LogError("Failed to initialize chest contract");
            }
            else
            {
                Debug.Log("Chest contract initialized successfully");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error initializing contracts: {e.Message}");
        }

        //joinLobbyButton.onClick.AddListener(JoinLobby);
        //RequestChestOpeningbuttton.onClick.AddListener(RequestChestOpening);
        //ActivateFallbackbutton.onClick.AddListener(ActivateFallback);
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
            Debug.LogError("SimpleLobbyManager is not fully initialized. Cannot set username.");
            return;
        }

        // Null and empty string checks
        if (string.IsNullOrEmpty(kutta))
        {
            Debug.LogError("Username cannot be null or empty");
            return;
        }

        //statusText.text = "Setting username...";
        try
        {
            var wallet = ThirdwebManager.Instance.GetActiveWallet();
            if (wallet == null)
            {
                Debug.LogError("No active wallet found");
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
                Debug.LogError("Failed to prepare setUsername transaction");
                return;
            }

            var usernameTransactionHash = await ThirdwebTransaction.Send(setUsernameTx);

            if (string.IsNullOrEmpty(usernameTransactionHash))
            {
                Debug.LogError("Transaction hash is null or empty");
                return;
            }

            Debug.Log($"Username set successfully! TX: {usernameTransactionHash}");
            // statusText.text = $"Username set! TX: {usernameTransactionHash}";
        }
        catch (System.Exception e)
        {
            //  statusText.text = $"Error: {e.Message}";
            Debug.LogError($"SetUsername error: {e}");
        }
    }

    public async void JoinLobby()
    {
        // Check if fully initialized
        if (!IsFullyInitialized())
        {
            Debug.LogError("SimpleLobbyManager is not fully initialized. Cannot join lobby.");
            click = 0;
            staked = false;
            return;
        }

        try
        {
            var wallet = ThirdwebManager.Instance.GetActiveWallet();
            if (wallet == null)
            {
                Debug.LogError("No active wallet found");
                click = 0;
                staked = false;
                return;
            }

            BigInteger weiValue = BigInteger.Parse("246000000000000");

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
                Debug.LogError("Failed to prepare stakeAndJoin transaction");
                click = 0;
                staked = false;
                return;
            }

            var joinTransactionHash = await ThirdwebTransaction.Send(stakeAndJoinTx);

            if (string.IsNullOrEmpty(joinTransactionHash))
            {
                Debug.LogError("Join transaction hash is null or empty");
                click = 0;
                staked = false;
                return;
            }

            staked = true;
            Debug.Log($"Joined lobby successfully! TX: {joinTransactionHash}");
            // statusText.text = $"Joined lobby! TX: {joinTransactionHash}";
        }
        catch (System.Exception e)
        {
            //   statusText.text = $"Error: {e.Message}";
            Debug.LogError($"JoinLobby error: {e}");
            staked = false;
            click = 0;
        }
    }

    public async void RequestChestOpening()
    {
        // Check if fully initialized
        if (!IsFullyInitialized())
        {
            Debug.LogError("SimpleLobbyManager is not fully initialized. Cannot request chest opening.");
            return;
        }

        //statusText.text = "Requesting chest opening...";
        try
        {
            var wallet = ThirdwebManager.Instance.GetActiveWallet();
            if (wallet == null)
            {
                Debug.LogError("No active wallet found");
                return;
            }

            Debug.Log(wallet);
            // Example: 0.015 ETH in Wei (match your server value)
            BigInteger valueWei = BigInteger.Parse("15000000000001");

            // Prepare transaction for requestChestOpening (payable function)
            var chestOpeningTx = await contractchest.Prepare(
                wallet: wallet,
                method: "requestChestOpening",
                weiValue: valueWei
            );

            if (chestOpeningTx == null)
            {
                Debug.LogError("Failed to prepare requestChestOpening transaction");
                return;
            }

            Debug.Log(chestOpeningTx);
            // Send transaction
            var txHash = await ThirdwebTransaction.Send(chestOpeningTx);

            if (string.IsNullOrEmpty(txHash))
            {
                Debug.LogError("Chest opening transaction hash is null or empty");
                return;
            }

            //statusText.text = $"Chest opening requested! TX: {txHash}";
            Debug.Log($"Chest opening transaction: {txHash}");
        }
        catch (System.Exception e)
        {
            //statusText.text = $"Error: {e.Message}";
            Debug.LogError($"RequestChestOpening error: {e}");
        }
    }

    public async void ActivateFallback()
    {
        // Check if fully initialized
        if (!IsFullyInitialized())
        {
            Debug.LogError("SimpleLobbyManager is not fully initialized. Cannot activate fallback.");
            return;
        }

        //statusText.text = "Auto-detecting sequence number...";
        try
        {
            var wallet = ThirdwebManager.Instance.GetActiveWallet();
            if (wallet == null)
            {
                Debug.LogError("No active wallet found");
                return;
            }

            var userAddress = await wallet.GetAddress();
            if (string.IsNullOrEmpty(userAddress))
            {
                Debug.LogError("Failed to get user address");
                return;
            }

            Debug.Log($"User address: {userAddress}");

            // First, get user's chest requests to find unfulfilled ones
            var getUserRequestsResult = await contractchest.Read<object>(
                method: "getUserChestRequests",
                parameters: new object[] { userAddress }
            );

            // getUserRequestsResult is usually an object[] containing 6 arrays
            if (getUserRequestsResult == null)
            {
                //statusText.text = "No chest requests found.";
                Debug.Log("getUserChestRequests returned null");
                return;
            }

            Debug.Log(getUserRequestsResult);

            if (!(getUserRequestsResult is object[] resultArrays))
            {
                Debug.LogError("getUserChestRequests result is not an object array");
                return;
            }

            Debug.Log($"Result contains {resultArrays.Length} arrays");

            if (resultArrays.Length < 2)
            {
                Debug.LogError("Insufficient arrays in getUserChestRequests result");
                return;
            }

            // Null check for individual arrays
            if (resultArrays[0] == null || resultArrays[1] == null)
            {
                Debug.LogError("Sequence numbers or fulfilled array is null");
                return;
            }

            // Cast each array separately with null checks
            var sequenceNumbersArray = resultArrays[0] as object[];
            var fulfilledArray = resultArrays[1] as object[];

            if (sequenceNumbersArray == null || fulfilledArray == null)
            {
                Debug.LogError("Failed to cast sequence numbers or fulfilled arrays");
                return;
            }

            if (sequenceNumbersArray.Length == 0)
            {
                Debug.Log("No chest requests found for this user");
                return;
            }

            if (sequenceNumbersArray.Length != fulfilledArray.Length)
            {
                Debug.LogError("Sequence numbers and fulfilled arrays have different lengths");
                return;
            }

            var sequenceNumbers = sequenceNumbersArray.Select(x => x != null ? Convert.ToUInt64(x) : 0UL).ToArray();
            var fulfilled = fulfilledArray.Select(x => x != null ? Convert.ToBoolean(x) : false).ToArray();

            Debug.Log("Chest request arrays retrieved successfully");
            Debug.Log($"Found {sequenceNumbers.Length} chest requests");

            // Find the latest unfulfilled request that can activate fallback
            ulong? targetSequenceNumber = null;

            for (int i = sequenceNumbers.Length - 1; i >= 0; i--)
            {
                // Convert sequence number to ulong
                var seqNum = sequenceNumbers[i];
                var isFulfilled = fulfilled[i];

                if (!isFulfilled && seqNum > 0)
                {
                    Debug.Log($"Checking if fallback can be activated for sequence: {seqNum}");

                    // Check if fallback can be activated for this sequence number
                    try
                    {
                        var canFallback = await contractchest.Read<bool>(
                            method: "canActivateFallback",
                            parameters: new object[] { seqNum }
                        );

                        if (canFallback)
                        {
                            targetSequenceNumber = seqNum;
                            Debug.Log($"✅ Found fallback-eligible sequence number: {targetSequenceNumber}");
                            break;
                        }
                    }
                    catch (System.Exception checkError)
                    {
                        Debug.LogWarning($"Could not check fallback eligibility for sequence {seqNum}: {checkError.Message}");
                        continue;
                    }
                }
            }

            if (!targetSequenceNumber.HasValue)
            {
                throw new System.Exception("No fallback-eligible requests found for this user");
            }

            //statusText.text = $"Activating fallback for sequence {targetSequenceNumber}...";

            // Prepare transaction for activateFallback
            var activateFallbackTx = await contractchest.Prepare(
                wallet: wallet,
                method: "activateFallback",
                weiValue: BigInteger.Zero, // Non-payable function
                parameters: new object[] { targetSequenceNumber.Value }
            );

            if (activateFallbackTx == null)
            {
                Debug.LogError("Failed to prepare activateFallback transaction");
                return;
            }

            Debug.Log($"Prepared fallback transaction for sequence: {targetSequenceNumber}");

            // Send transaction
            var txHash = await ThirdwebTransaction.Send(activateFallbackTx);

            if (string.IsNullOrEmpty(txHash))
            {
                Debug.LogError("Activate fallback transaction hash is null or empty");
                return;
            }

            //statusText.text = $"Fallback activated! Sequence: {targetSequenceNumber}, TX: {txHash}";
            Debug.Log($"Activate fallback transaction: {txHash}");
        }
        catch (System.Exception e)
        {
            //statusText.text = $"Error: {e.Message}";
            Debug.LogError($"ActivateFallback error: {e}");
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
            }
            else
            {
                Debug.LogWarning("buttonStaked GameObject is null");
            }

            if (waiting != null)
            {
                waiting.SetActive(true);
            }
            else
            {
                Debug.LogWarning("waiting GameObject is null");
            }
        }
    }
}