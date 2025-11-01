// SPDX-License-Identifier: MIT
pragma solidity ^0.8.27;

import "@openzeppelin/contracts/utils/ReentrancyGuard.sol";
import "@openzeppelin/contracts/access/Ownable.sol";

interface IShopNFT {
    function mintWithMetadata(address to, string memory name, string memory prize, string memory image)
        external
        returns (int64); // HTS NFTs return serial numbers (int64) instead of tokenIds (uint256)
}

interface IChestOpener {
    function getCoins(address user) external view returns (uint256);
    function spendCoinsFrom(address user, uint256 amount) external;
}

interface IHederaPrng {
    function spinWheel(uint256 numSegments) external returns (uint256);
    function getRandomInRange(uint32 lo, uint32 hi) external returns (uint32);
}

contract SpinWheelHedera is ReentrancyGuard, Ownable {
    /// ---------- CONFIG ----------
    uint256 public spinCost = 50; // Cost in coins to spin the wheel
    uint256 public constant MAX_WHEEL_ITEMS = 6; // Maximum items on the wheel

    /// ---------- STATE ----------
    IHederaPrng public hederaPrng;
    IShopNFT public shopNFT;
    IChestOpener public chestOpener;

    // User coin balances (synced from chest contract)
    mapping(address => uint256) public userCoins;

    struct WheelItem {
        string name;
        string prize;
        string image;
        bool active;
    }

    // Array of wheel items
    WheelItem[] public wheelItems;

    // Track spins and results
    mapping(address => uint256) public userSpinCount;
    mapping(address => uint256[]) public userWonItems; // Array of wheel item indices won

    /// ---------- EVENTS ----------
    event WheelItemAdded(uint256 indexed itemIndex, string name);
    event WheelItemUpdated(uint256 indexed itemIndex, string name, bool active);
    event WheelSpun(address indexed user, uint256 selectedItemIndex, string itemName, uint256 coinsSpent);
    event NFTAwarded(address indexed user, int64 serialNumber, uint256 wheelItemIndex);
    event CoinsAdded(address indexed user, uint256 amount, uint256 newBalance);
    event CoinsSpent(address indexed user, uint256 amount, uint256 remainingBalance);
    event SpinCostUpdated(uint256 oldCost, uint256 newCost);

    /// ---------- ERRORS ----------
    error WheelEmpty();
    error InsufficientCoins();
    error InvalidItemIndex();
    error ItemNotActive();

    constructor(address _hederaPrng, address _shopNFT, address _chestOpener) Ownable(msg.sender) {
        require(_hederaPrng != address(0), "Invalid Hedera PRNG contract address");
        require(_shopNFT != address(0), "Invalid NFT contract address");
        require(_chestOpener != address(0), "Invalid chest opener contract address");

        hederaPrng = IHederaPrng(_hederaPrng);
        shopNFT = IShopNFT(_shopNFT);
        chestOpener = IChestOpener(_chestOpener);
    }

    /// ---------- WHEEL MANAGEMENT ----------

    // Add a new item to the spin wheel
    function addWheelItem(string memory name, string memory prize, string memory image) external onlyOwner {
        require(wheelItems.length < MAX_WHEEL_ITEMS, "Wheel is full");

        wheelItems.push(WheelItem({name: name, prize: prize, image: image, active: true}));

        emit WheelItemAdded(wheelItems.length - 1, name);
    }

    // Update an existing wheel item
    function updateWheelItem(
        uint256 itemIndex,
        string memory name,
        string memory prize,
        string memory image,
        bool active
    ) external onlyOwner {
        require(itemIndex < wheelItems.length, InvalidItemIndex());

        WheelItem storage item = wheelItems[itemIndex];

        item.name = name;
        item.prize = prize;
        item.image = image;
        item.active = active;

        emit WheelItemUpdated(itemIndex, name, active);
    }

    /// ---------- SPIN FUNCTIONALITY ----------

    // Sync helper to pull latest coins from the chest opener contract
    // Only updates if chest balance is higher (new coins added), prevents overwriting deductions
    function _syncCoinsFor(address user) internal {
        uint256 chestCoins = chestOpener.getCoins(user);
        uint256 currentBalance = userCoins[user];
        // Only sync if chest has more coins (new coins added), don't overwrite if chest has less
        if (chestCoins > currentBalance) {
            userCoins[user] = chestCoins;
            emit CoinsAdded(user, chestCoins - currentBalance, chestCoins);
        } else if (chestCoins < currentBalance) {
            // If chest has less, update to match (coins were spent elsewhere)
            userCoins[user] = chestCoins;
        }
    }

    // Spin the wheel using Hedera PRNG precompile through HederaPrng contract
    function spinWheel() external nonReentrant returns (uint256 selectedItemIndex) {
        require(wheelItems.length > 0, WheelEmpty());

        // Always sync coins from chest before checking balance
        _syncCoinsFor(msg.sender);
        require(userCoins[msg.sender] >= spinCost, InsufficientCoins());

        // Count active items
        uint256 activeCount = 0;
        for (uint256 i = 0; i < wheelItems.length; i++) {
            if (wheelItems[i].active) {
                activeCount++;
            }
        }
        require(activeCount > 0, "No active items on wheel");

        // Use HederaPrng to get an index in [0, activeCount)
        uint256 randomIndexAmongActive = hederaPrng.spinWheel(activeCount);

        // Map the active index to the concrete item index
        uint256 currentActive = 0;
        for (uint256 i = 0; i < wheelItems.length; i++) {
            if (wheelItems[i].active) {
                if (currentActive == randomIndexAmongActive) {
                    selectedItemIndex = i;
                    break;
                }
                currentActive++;
            }
        }

        // Deduct coins for spinning from both local tracking and chest contract
        userCoins[msg.sender] -= spinCost;
        chestOpener.spendCoinsFrom(msg.sender, spinCost);

        // Award the selected item
        _awardWheelItem(msg.sender, selectedItemIndex);

        // Update user stats
        userSpinCount[msg.sender]++;
        userWonItems[msg.sender].push(selectedItemIndex);

        emit CoinsSpent(msg.sender, spinCost, userCoins[msg.sender]);
        emit WheelSpun(msg.sender, selectedItemIndex, wheelItems[selectedItemIndex].name, spinCost);
    }

    // Award the selected wheel item to the user
    function _awardWheelItem(address user, uint256 itemIndex) internal {
        require(itemIndex < wheelItems.length, InvalidItemIndex());

        WheelItem memory item = wheelItems[itemIndex];
        require(item.active, ItemNotActive());

        // Mint HTS NFT with the wheel item metadata (returns serial number)
        int64 serialNumber = shopNFT.mintWithMetadata(user, item.name, item.prize, item.image);

        // Mark the item as inactive after it is awarded
        wheelItems[itemIndex].active = false;

        emit NFTAwarded(user, serialNumber, itemIndex);
    }

    /// ---------- COIN MANAGEMENT ----------

    // Sync coins from chest contract to wheel contract
    // Only updates if chest balance is higher (new coins added), prevents overwriting deductions
    function syncCoins(address user) external {
        uint256 chestCoins = chestOpener.getCoins(user);
        uint256 currentBalance = userCoins[user];
        // Only sync if chest has more coins (new coins added), don't overwrite if chest has less
        if (chestCoins > currentBalance) {
            userCoins[user] = chestCoins;
            emit CoinsAdded(user, chestCoins - currentBalance, chestCoins);
        } else if (chestCoins < currentBalance) {
            // If chest has less, update to match (coins were spent elsewhere)
            userCoins[user] = chestCoins;
        }
    }

    // Get user's coin balance in wheel contract
    function getCoins(address user) external returns (uint256) {
        _syncCoinsFor(msg.sender);
        return userCoins[user];
    }

    /// ---------- VIEW FUNCTIONS ----------

    // Get all wheel items
    function getAllWheelItems() external view returns (WheelItem[] memory) {
        return wheelItems;
    }

    // Get active wheel items only
    function getActiveWheelItems() external view returns (WheelItem[] memory) {
        uint256 activeCount = 0;

        // Count active items
        for (uint256 i = 0; i < wheelItems.length; i++) {
            if (wheelItems[i].active) {
                activeCount++;
            }
        }

        // Create array of active items
        WheelItem[] memory activeItems = new WheelItem[](activeCount);
        uint256 index = 0;

        for (uint256 i = 0; i < wheelItems.length; i++) {
            if (wheelItems[i].active) {
                activeItems[index] = wheelItems[i];
                index++;
            }
        }

        return activeItems;
    }

    // Get wheel item by index
    function getWheelItem(uint256 index) external view returns (WheelItem memory) {
        require(index < wheelItems.length, InvalidItemIndex());
        return wheelItems[index];
    }

    // Get user's spin statistics
    function getUserStats(address user) external view returns (uint256 spinCount, uint256[] memory wonItems) {
        return (userSpinCount[user], userWonItems[user]);
    }

    // Get total number of wheel items
    function getWheelItemCount() external view returns (uint256) {
        return wheelItems.length;
    }

    // Get number of active wheel items
    function getActiveWheelItemCount() external view returns (uint256) {
        uint256 count = 0;
        for (uint256 i = 0; i < wheelItems.length; i++) {
            if (wheelItems[i].active) {
                count++;
            }
        }
        return count;
    }

    /// ---------- ADMIN FUNCTIONS ----------

    // Withdraw contract balance (only owner)
    function withdrawFunds() external onlyOwner {
        uint256 balance = address(this).balance;
        require(balance > 0, "No funds to withdraw");

        payable(owner()).transfer(balance);
    }

    // Update spin cost (only owner)
    function setSpinCost(uint256 newCost) external onlyOwner {
        require(newCost > 0, "Spin cost must be greater than 0");

        uint256 oldCost = spinCost;
        spinCost = newCost;

        emit SpinCostUpdated(oldCost, newCost);
    }
}
