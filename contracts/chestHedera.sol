// SPDX-License-Identifier: MIT
pragma solidity ^0.8.27;

import "@openzeppelin/contracts/utils/ReentrancyGuard.sol";
import "./shopHedera.sol"; 

interface IHederaPrng {
    function getRandomInRange(uint32 lo, uint32 hi) external returns (uint32);
}

contract ChestHedera is ReentrancyGuard {
    IHederaPrng public hederaPrng;
    ShopNFTHedera public shop;

    mapping(address => uint256) public coins;
    mapping(address => bool) public authorizedSpenders;

    /* EVENTS */
    event ChestOpened(address indexed user, uint256 coinsWon, uint256 totalCoins, string randomnessSource);
    event CoinsSpent(address indexed user, uint256 amount, uint256 remaining);
    event AuthorizedSpenderSet(address indexed spender, bool authorized);

    address public owner;

    modifier onlyOwner() {
        require(msg.sender == owner, "Not owner");
        _;
    }

    modifier onlyAuthorizedSpender() {
        require(authorizedSpenders[msg.sender], "Not authorized spender");
        _;
    }

    constructor(address _hederaPrng, address _shop) {
        require(_hederaPrng != address(0), "Invalid Hedera PRNG address");
        require(_shop != address(0), "Invalid NFT address");
        hederaPrng = IHederaPrng(_hederaPrng);
        shop = ShopNFTHedera(_shop);
        owner = msg.sender;
    }

    /// Open chest using Hedera PRNG randomness and award coins
    /// Award range: [0..50] inclusive via [0, 51)
    function openChest() external nonReentrant returns (uint256 coinsWon) {
        uint32 rnd = hederaPrng.getRandomInRange(0, 51); // 0..50 inclusive
        coinsWon = uint256(rnd);
        coins[msg.sender] += coinsWon;
        emit ChestOpened(msg.sender, coinsWon, coins[msg.sender], "hedera-prng");
    }

    /// Get coin balance of any user
    function getCoins(address user) external view returns (uint256) {
        return coins[user];
    }

    /// Spend/remove coins from caller's balance
    function spendCoins(uint256 amount) external nonReentrant {
        require(coins[msg.sender] >= amount, "Not enough coins");
        coins[msg.sender] -= amount;
        emit CoinsSpent(msg.sender, amount, coins[msg.sender]);
    }

    /// Spend coins from a user's balance (can only be called by authorized spenders)
    function spendCoinsFrom(address user, uint256 amount) external nonReentrant onlyAuthorizedSpender {
        require(coins[user] >= amount, "Not enough coins");
        coins[user] -= amount;
        emit CoinsSpent(user, amount, coins[user]);
    }

    /// Set authorized spender (only owner can call)
    function setAuthorizedSpender(address spender, bool authorized) external onlyOwner {
        authorizedSpenders[spender] = authorized;
        emit AuthorizedSpenderSet(spender, authorized);
    }
}
