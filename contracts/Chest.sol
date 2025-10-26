// SPDX-License-Identifier: MIT
pragma solidity ^0.8.28;

import "@openzeppelin/contracts/security/ReentrancyGuard.sol";

interface IEntropy {
    function getFeeV2() external view returns (uint256);
    function requestV2() external payable returns (uint64);
}
interface IChestNFT {
    function mintWithMetadata(
        address to,
        string memory name,
        string memory prize,
        string memory image
    ) external returns (uint256);
}


contract ChestOpener is ReentrancyGuard {
    IEntropy public entropy;
    IChestNFT public chestNFT;  

    uint256 public constant REQUEST_EXPIRY = 0 minutes;

    struct ChestRequest {
        address requester;
        bool fulfilled;
        uint256 timestamp;
        uint256 coins;
        bool usedFallback;
        string randomnessSource; // e.g. "entropy" or "onchain"
    }

    // sequenceNumber => ChestRequest
    mapping(uint64 => ChestRequest) public chestRequests;
    // user => coin balance
    mapping(address => uint256) public coins;
    // user => array of sequenceNumbers requested by that user
    mapping(address => uint64[]) public userRequests;

/* ---------------- NFT Shop ---------------- */
    struct ShopItem {
    string name;         // name of the NFT
    string prize;        // prize description
    string image;        // ipfs link
    uint256 price;       // cost in coins
    bool available;      // is this item purchasable?
    }
    mapping(uint256 => ShopItem) public shop; // itemId => ShopItem
    uint256 public nextItemId;

    /* EVENTS */
    event ChestRequested(uint64 indexed sequenceNumber, address indexed user, uint256 timestamp, uint256 fee);
    event ChestFulfilled(
        uint64 indexed sequenceNumber,
        address indexed user,
        uint256 coinsWon,
        uint256 totalCoins,
        string randomnessSource
    );
    event FallbackActivated(
        uint64 indexed sequenceNumber,
        address indexed user,
        uint256 coinsWon,
        uint256 totalCoins,
        string reason,
        string randomnessSource
    );
    event CoinsSpent(address indexed user, uint256 amount, uint256 remaining);
    // Fixed event declarations to match the emit statements
    event ShopItemAdded(uint256 indexed itemId, string name, string prize, string image, uint256 price);
    event ShopItemUpdated(uint256 indexed itemId, string name, string prize, string image, uint256 price, bool available);
    event NFTPurchased(address indexed user, uint256 itemId, uint256 tokenId, uint256 price);

    /* MODIFIERS */
    modifier onlyEntropy() {
        require(msg.sender == address(entropy), "Caller is not entropy provider");
        _;
    }

    // constructor(address _entropy) {
    //     require(_entropy != address(0), "Invalid entropy address");
    //     entropy = IEntropy(_entropy);
    // }
    address public owner;

    modifier onlyOwner() {
        require(msg.sender == owner, "Not owner");
        _;
    }
    constructor(address _entropy, address _chestNFT) {
        require(_entropy != address(0), "Invalid entropy address");
        require(_chestNFT != address(0), "Invalid NFT address");
        entropy = IEntropy(_entropy);
        chestNFT = IChestNFT(_chestNFT);
        owner = msg.sender;
    }
    /* ---------------- Shop Management ---------------- */
    function addShopItem(
    string memory name,
    string memory prize,
    string memory image,
    uint256 price
)    external onlyOwner {
    shop[nextItemId] = ShopItem(name, prize, image, price, true);
    emit ShopItemAdded(nextItemId, name, prize, image, price);
    nextItemId++;
}

    function updateShopItem(
    uint256 itemId,
    string memory name,
    string memory prize,
    string memory image,
    uint256 price,
    bool available
) external onlyOwner {
    require(itemId < nextItemId, "Invalid itemId");
    shop[itemId] = ShopItem(name, prize, image, price, available);
    emit ShopItemUpdated(itemId, name, prize, image, price, available);
}
    function getAllShopItems() external view returns (ShopItem[] memory) {
    ShopItem[] memory items = new ShopItem[](nextItemId);
    for (uint256 i = 0; i < nextItemId; i++) {
        items[i] = shop[i];
    }
    return items;
    }

/// Buy NFT and remove it from shop
function buyNFT(uint256 itemId) external nonReentrant {
    ShopItem memory item = shop[itemId];
    require(item.available, "Item not available");
    require(coins[msg.sender] >= item.price, "Not enough coins");

    // Deduct coins
    coins[msg.sender] -= item.price;

    // Mark item as unavailable (sold)
    shop[itemId].available = false;

    // Mint NFT with metadata
    uint256 tokenId = chestNFT.mintWithMetadata(msg.sender, item.name, item.prize, item.image);

    emit CoinsSpent(msg.sender, item.price, coins[msg.sender]);
    emit NFTPurchased(msg.sender, itemId, tokenId, item.price);
}



    /// Request a chest opening. Caller must send at least the entropy fee.
    /// Forwards the exact fee to entropy.requestV2() and refunds any extra.
    function requestChestOpening() external payable nonReentrant returns (uint64 sequenceNumber) {
        uint256 fee = entropy.getFeeV2();
        require(msg.value >= fee, "Insufficient entropy fee");

        // forward exactly the fee
        sequenceNumber = entropy.requestV2{value: fee}();

        // basic validation: treat sequenceNumber == 0 as invalid
        require(sequenceNumber != 0, "Invalid sequence number from entropy");

        chestRequests[sequenceNumber] = ChestRequest({
            requester: msg.sender,
            fulfilled: false,
            timestamp: block.timestamp,
            coins: 0,
            usedFallback: false,
            randomnessSource: ""
        });

        userRequests[msg.sender].push(sequenceNumber);

        // refund any extra sent by the caller
        if (msg.value > fee) {
            payable(msg.sender).transfer(msg.value - fee);
        }

        emit ChestRequested(sequenceNumber, msg.sender, block.timestamp, fee);
    }

    /// Entropy contract calls this once randomness is ready.
    /// Only callable by the configured entropy contract.
    function entropyCallback(
        uint64 sequenceNumber,
        address /* provider */,
        bytes32 randomNumber
    ) external nonReentrant onlyEntropy {
        _handleEntropyCallback(sequenceNumber, randomNumber);
    }

    /// Internal handler for entropy callback
    function _handleEntropyCallback(uint64 sequenceNumber, bytes32 randomNumber) internal {
        ChestRequest storage request = chestRequests[sequenceNumber];

        // ignore if no request or already fulfilled
        if (request.requester == address(0) || request.fulfilled) return;

        // ignore if expired — allow fallback instead
        if (block.timestamp > request.timestamp + REQUEST_EXPIRY) return;

        uint256 random = uint256(randomNumber);
        // Map randomness to coins: [100..200] (inclusive)
        uint256 coinsWon = 100 + (random % 101);

        request.fulfilled = true;
        request.coins = coinsWon;
        request.usedFallback = false;
        request.randomnessSource = "entropy";

        coins[request.requester] += coinsWon;

        emit ChestFulfilled(sequenceNumber, request.requester, coinsWon, coins[request.requester], "entropy");
    }

    /// Activate on-chain fallback for a user's earliest unfulfilled request, but only after expiry.
    /// Generates on-chain pseudo-randomness and resolves the request with fallback rewards [50..100].
    function activateFallback() external nonReentrant {
        uint64[] storage userSeqs = userRequests[msg.sender];
        require(userSeqs.length > 0, "No requests found for user");

        // Find the first unfulfilled request
        uint64 sequenceNumber = 0;
        uint256 seqIndex = type(uint256).max;
        for (uint256 i = 0; i < userSeqs.length; i++) {
            uint64 seq = userSeqs[i];
            if (!chestRequests[seq].fulfilled) {
                sequenceNumber = seq;
                seqIndex = i;
                break;
            }
        }
        require(sequenceNumber != 0, "No unfulfilled request available");

        ChestRequest storage request = chestRequests[sequenceNumber];
        require(!request.fulfilled, "Request already fulfilled");

        // Only allow fallback after the request expiry
        require(block.timestamp > request.timestamp + REQUEST_EXPIRY, "Request not expired yet");

        // Generate on-chain pseudo-randomness (not secure for high-value use)
        uint256 randomSeed = uint256(
            keccak256(
                abi.encodePacked(
                    block.timestamp,
                    block.prevrandao,
                    msg.sender,
                    sequenceNumber,
                    blockhash(block.number - 1)
                )
            )
        );
        uint256 coinsWon = 50 + (randomSeed % 51); // 50..100

        // Update request + user coins
        request.fulfilled = true;
        request.coins = coinsWon;
        request.usedFallback = true;
        request.randomnessSource = "onchain";

        coins[msg.sender] += coinsWon;

        emit FallbackActivated(sequenceNumber, msg.sender, coinsWon, coins[msg.sender], "Manual fallback (expired request)", "onchain");
        emit ChestFulfilled(sequenceNumber, msg.sender, coinsWon, coins[msg.sender], "onchain");
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

    /// Get unfulfilled chest requests for a user
    function getUnfulfilledRequests(address user) external view returns (uint64[] memory) {
        uint64[] memory allRequests = userRequests[user];
        uint256 count;

        for (uint256 i = 0; i < allRequests.length; i++) {
            if (!chestRequests[allRequests[i]].fulfilled) {
                count++;
            }
        }

        uint64[] memory pending = new uint64[](count);
        uint256 index;
        for (uint256 i = 0; i < allRequests.length; i++) {
            if (!chestRequests[allRequests[i]].fulfilled) {
                pending[index++] = allRequests[i];
            }
        }

        return pending;
    }
}
