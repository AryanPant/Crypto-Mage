using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Numerics;
using Thirdweb.Unity;
using Thirdweb;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

[System.Serializable]
public class ShopItem
{
    public string name;
    public string prize;
    public string image;
    public BigInteger price;
    public bool available;
    public uint itemId;
}

public class NFTShopManager : MonoBehaviour
{
    [Header("Contract Settings")]
    [SerializeField] private string contractAddress;
    [SerializeField] private ulong chainId = 84532; // Base Sepolia
    
    [Header("UI References")]
    [SerializeField] private GameObject shopItemPrefab;
    [SerializeField] private Transform shopContainer;
    [SerializeField] private Button refreshShopButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text userCoinsText;

    private ThirdwebContract contract;
    private List<ShopItem> shopItems = new List<ShopItem>();

    private string contractAbi = @"[
        {
            ""inputs"": [
                {""internalType"": ""string"", ""name"": ""name"", ""type"": ""string""},
                {""internalType"": ""string"", ""name"": ""prize"", ""type"": ""string""},
                {""internalType"": ""string"", ""name"": ""image"", ""type"": ""string""},
                {""internalType"": ""uint256"", ""name"": ""price"", ""type"": ""uint256""}
            ],
            ""name"": ""addShopItem"",
            ""outputs"": [],
            ""stateMutability"": ""nonpayable"",
            ""type"": ""function""
        },
        {
            ""inputs"": [{""internalType"": ""uint256"", ""name"": ""itemId"", ""type"": ""uint256""}],
            ""name"": ""buyNFT"",
            ""outputs"": [],
            ""stateMutability"": ""nonpayable"",
            ""type"": ""function""
        },
        {
            ""inputs"": [],
            ""name"": ""getAllShopItems"",
            ""outputs"": [
                {
                    ""components"": [
                        {""internalType"": ""string"", ""name"": ""name"", ""type"": ""string""},
                        {""internalType"": ""string"", ""name"": ""prize"", ""type"": ""string""},
                        {""internalType"": ""string"", ""name"": ""image"", ""type"": ""string""},
                        {""internalType"": ""uint256"", ""name"": ""price"", ""type"": ""uint256""},
                        {""internalType"": ""bool"", ""name"": ""available"", ""type"": ""bool""}
                    ],
                    ""internalType"": ""struct ChestOpener.ShopItem[]"",
                    ""name"": """",
                    ""type"": ""tuple[]""
                }
            ],
            ""stateMutability"": ""view"",
            ""type"": ""function""
        },
        {
            ""inputs"": [{""internalType"": ""address"", ""name"": ""user"", ""type"": ""address""}],
            ""name"": ""getCoins"",
            ""outputs"": [{""internalType"": ""uint256"", ""name"": """", ""type"": ""uint256""}],
            ""stateMutability"": ""view"",
            ""type"": ""function""
        },
        {
            ""inputs"": [],
            ""name"": ""requestChestOpening"",
            ""outputs"": [{""internalType"": ""uint64"", ""name"": ""sequenceNumber"", ""type"": ""uint64""}],
            ""stateMutability"": ""payable"",
            ""type"": ""function""
        }
    ]";

    private async void Start()
    {
        await InitializeContract();
        SetupUI();
        await LoadShopItems();
        await UpdateUserCoins();
    }

    private async Task InitializeContract()
    {
        try
        {
            if (string.IsNullOrEmpty(contractAddress))
            {
                throw new InvalidOperationException("Contract address not set");
            }

            if (ThirdwebManager.Instance == null)
            {
                throw new InvalidOperationException("ThirdwebManager instance not found");
            }

            contract = await ThirdwebManager.Instance.GetContract(contractAddress, chainId, contractAbi);
            UpdateStatus("Contract initialized successfully");
        }
        catch (Exception e)
        {
            var errorMessage = $"Contract initialization failed: {e.Message}";
            UpdateStatus(errorMessage);
            Debug.LogError($"NFTShopManager - {errorMessage}");
            throw;
        }
    }

    private void SetupUI()
    {
        ValidateRequiredComponents();

        if (refreshShopButton != null)
            refreshShopButton.onClick.AddListener(async () => await LoadShopItems());
    }

    private void ValidateRequiredComponents()
    {
        bool hasErrors = false;

        if (shopContainer == null)
        {
            Debug.LogError("NFTShopManager - Shop Container reference is missing. Assign this in the Unity Inspector.");
            hasErrors = true;
        }
        if (statusText == null)
        {
            Debug.LogError("NFTShopManager - Status Text reference is missing. Assign this in the Unity Inspector.");
            hasErrors = true;
        }
        if (userCoinsText == null)
        {
            Debug.LogError("NFTShopManager - User Coins Text reference is missing. Assign this in the Unity Inspector.");
            hasErrors = true;
        }
        if (shopItemPrefab == null)
        {
            Debug.LogError("NFTShopManager - Shop Item Prefab reference is missing. Assign this in the Unity Inspector.");
            hasErrors = true;
        }

        if (hasErrors)
        {
            UpdateStatus("Setup error: Some required components are missing. Check the console for details.");
        }
    }

    public async Task LoadShopItems()
    {
        UpdateStatus("Loading shop items...");
        try
        {
            var result = await contract.Read<object>("getAllShopItems");
            
            if (result != null)
            {
                shopItems.Clear();
                var itemsArray = (object[])result;
                
                for (int i = 0; i < itemsArray.Length; i++)
                {
                    var itemData = (object[])itemsArray[i];
                    
                    var shopItem = new ShopItem
                    {
                        itemId = (uint)i,
                        name = itemData[0].ToString(),
                        prize = itemData[1].ToString(),
                        image = itemData[2].ToString(),
                        price = BigInteger.Parse(itemData[3].ToString()),
                        available = Convert.ToBoolean(itemData[4])
                    };
                    
                    shopItems.Add(shopItem);
                }
                
                DisplayShopItems();
                UpdateStatus($"Loaded {shopItems.Count} shop items");
            }
        }
        catch (Exception e)
        {
            UpdateStatus($"Error loading shop items: {e.Message}");
            Debug.LogError($"LoadShopItems error: {e}");
        }
    }

    private void DisplayShopItems()
    {
        if (shopContainer == null || shopItemPrefab == null)
        {
            Debug.LogError("NFTShopManager - Cannot display shop items: Missing required references");
            return;
        }

        foreach (Transform child in shopContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var item in shopItems)
        {
            GameObject itemUI = Instantiate(shopItemPrefab, shopContainer);
            SetupShopItemUI(itemUI, item);
        }
    }

    private void SetupShopItemUI(GameObject itemUI, ShopItem item)
    {
        var nameText = itemUI.GetComponentInChildren<TMP_Text>();
        if (nameText != null)
            nameText.text = $"{item.name}\nPrize: {item.prize}\nPrice: {item.price} coins";

        var buyButton = itemUI.GetComponentInChildren<Button>();
        if (buyButton != null)
        {
            buyButton.interactable = item.available;
            buyButton.onClick.AddListener(async () => await BuyNFT(item.itemId));
        }
    }

    public async Task BuyNFT(uint itemId)
    {
        if (contract == null)
        {
            UpdateStatus("Cannot purchase NFT: Contract not initialized");
            return;
        }

        UpdateStatus($"Purchasing NFT with ID: {itemId}...");
        try
        {
            var wallet = ThirdwebManager.Instance?.GetActiveWallet();
            if (wallet == null)
            {
                UpdateStatus("Cannot purchase NFT: Wallet not connected");
                return;
            }

            var item = shopItems.FirstOrDefault(i => i.itemId == itemId);
            if (item == null)
            {
                UpdateStatus("Cannot purchase NFT: Invalid item ID");
                return;
            }

            if (!item.available)
            {
                UpdateStatus("Cannot purchase NFT: Item not available");
                return;
            }

            var userCoins = await GetUserCoins();
            if (userCoins < item.price)
            {
                UpdateStatus("Cannot purchase NFT: Insufficient coins");
                return;
            }
            
            var buyNFTTx = await contract.Prepare(
                wallet: wallet,
                method: "buyNFT",
                weiValue: BigInteger.Zero,
                parameters: new object[] { itemId }
            );

            var txHash = await ThirdwebTransaction.Send(buyNFTTx);
            UpdateStatus($"NFT purchased! TX: {txHash}");
            
            await UpdateUserCoins();
            await LoadShopItems();
        }
        catch (Exception e)
        {
            var errorMessage = $"Purchase failed: {e.Message}";
            UpdateStatus(errorMessage);
            Debug.LogError($"NFTShopManager - BuyNFT error: {e}");
        }
    }

    public async Task UpdateUserCoins()
    {
        if (contract == null)
        {
            Debug.LogError("NFTShopManager - Cannot update coins: Contract not initialized");
            return;
        }

        try
        {
            var wallet = ThirdwebManager.Instance?.GetActiveWallet();
            if (wallet == null)
            {
                Debug.LogError("NFTShopManager - Cannot update coins: Wallet not connected");
                if (userCoinsText != null)
                    userCoinsText.text = "Coins: 0";
                return;
            }

            var userAddress = await wallet.GetAddress();
            var coins = await contract.Read<BigInteger>("getCoins", userAddress);
            
            if (userCoinsText != null)
                userCoinsText.text = $"Coins: {coins}";
        }
        catch (Exception e)
        {
            var errorMessage = $"UpdateUserCoins error: {e.Message}";
            Debug.LogError($"NFTShopManager - {errorMessage}");
            if (userCoinsText != null)
                userCoinsText.text = "Coins: Error";
        }
    }

    public async Task RequestChestOpening()
    {
        if (contract == null)
        {
            UpdateStatus("Cannot request chest opening: Contract not initialized");
            return;
        }

        UpdateStatus("Requesting chest opening...");
        try
        {
            var wallet = ThirdwebManager.Instance?.GetActiveWallet();
            if (wallet == null)
            {
                UpdateStatus("Cannot request chest opening: Wallet not connected");
                return;
            }

            BigInteger fee = BigInteger.Parse("15000000000001"); // ~0.000015 ETH

            var requestTx = await contract.Prepare(
                wallet: wallet,
                method: "requestChestOpening",
                weiValue: fee
            );

            var txHash = await ThirdwebTransaction.Send(requestTx);
            UpdateStatus($"Chest opening requested! TX: {txHash}");
            
            await UpdateUserCoins();
        }
        catch (Exception e)
        {
            var errorMessage = $"Chest opening request failed: {e.Message}";
            UpdateStatus(errorMessage);
            Debug.LogError($"NFTShopManager - RequestChestOpening error: {e}");
        }
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
        
        Debug.Log($"NFTShopManager: {message}");
    }

    public async Task<BigInteger> GetUserCoins()
    {
        try
        {
            var wallet = ThirdwebManager.Instance.GetActiveWallet();
            var userAddress = await wallet.GetAddress();
            return await contract.Read<BigInteger>("getCoins", userAddress);
        }
        catch (Exception e)
        {
            Debug.LogError($"GetUserCoins error: {e}");
            return BigInteger.Zero;
        }
    }

    public void ExitShop()
    {
        SceneManager.LoadScene("StartScene");
    }
}