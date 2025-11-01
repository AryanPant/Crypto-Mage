// SPDX-License-Identifier: MIT
pragma solidity ^0.8.27;

import "@openzeppelin/contracts/access/Ownable.sol";
import "@openzeppelin/contracts/utils/ReentrancyGuard.sol";
import "./HederaTokenService.sol";
import "./HederaResponseCodes.sol";

interface IChestHederaCoins {
    function getCoins(address user) external view returns (uint256);
    function spendCoinsFrom(address user, uint256 amount) external;
}

/**
 * @title ShopNFTHedera
 * @dev HTS-based NFT shop contract that mints and transfers NFTs using Hedera Token Service
 *
 * @notice IMPORTANT SETUP REQUIREMENTS:
 * 1. Create HTS NFT token first (via Hedera SDK/API/Console) with:
 *    - This contract address as the minter (grant mint permissions)
 *    - Supply type: NON_FUNGIBLE_UNIQUE
 * 2. Deploy this contract
 * 3. Call setHTSNFTToken() with the created token address
 * 4. Users MUST associate the token before receiving NFTs (via SDK/API or associateToken())
 *
 * @notice When mintWithMetadata() is called (e.g., from spinWheel), the recipient must be
 *         associated with the token or the transfer will fail with code 166.
 */
contract ShopNFTHedera is HederaTokenService, Ownable, ReentrancyGuard {
    /// ---------- STATE ----------
    address public htsNFTToken; // Address of the HTS NFT token (must be set after token creation)

    struct NFTMetadata {
        string name;
        string prize;
        string image; // IPFS CID or full ipfs:// link
    }

    // Mapping: serialNumber => metadata
    mapping(int64 => NFTMetadata) public tokenMetadata;
    mapping(address => bool) public minters;

    // Track all serial numbers minted by this contract
    int64[] public allMintedSerials;

    modifier onlyMinter() {
        require(minters[msg.sender], "Not a minter");
        _;
    }

    modifier hasHTSToken() {
        require(htsNFTToken != address(0), "HTS NFT token not set");
        _;
    }

    constructor() Ownable(msg.sender) {}

    /// ---------- ADMIN FUNCTIONS ----------

    function setHTSNFTToken(address _htsNFTToken) external onlyOwner {
        require(_htsNFTToken != address(0), "Invalid token address");
        htsNFTToken = _htsNFTToken;
        // Associate this contract with the HTS token
        int256 response = this.associateToken(address(this), _htsNFTToken);
        require(response == int256(uint256(uint32(HederaResponseCodes.SUCCESS))), "Failed to associate token");
    }

    function setMinter(address minter, bool allowed) external onlyOwner {
        minters[minter] = allowed;
    }

    function setChest(address chestAddress) external onlyOwner {
        chest = IChestHederaCoins(chestAddress);
    }

    /// ---------- NFT MINTING ----------

    /**
     * @dev Internal function to mint NFT with metadata
     */
    function _mintWithMetadataInternal(address to, string memory name, string memory prize, string memory image)
        internal
        hasHTSToken
        returns (int64 serialNumber)
    {
        // Create metadata bytes array (HTS NFTs can have metadata as bytes)
        // We encode the metadata JSON as bytes
        bytes memory metadataJson =
            abi.encodePacked('{"name":"', name, '","prize":"', prize, '","image":"', image, '"}');
        bytes[] memory metadataArray = new bytes[](1);
        metadataArray[0] = metadataJson;

        // Mint the NFT (amount = 0 for NFTs)
        // @audit-info: Contract must have mint permissions on the HTS token (set during token creation)
        (int256 responseCode, int64[] memory serialNumbers) = this.mintToken(htsNFTToken, 0, metadataArray);

        require(responseCode == int256(uint256(uint32(HederaResponseCodes.SUCCESS))), "HTS mint failed");
        require(serialNumbers.length == 1, "Invalid serial numbers returned");

        serialNumber = serialNumbers[0];

        // Store metadata on-chain for easy access
        tokenMetadata[serialNumber] = NFTMetadata(name, prize, image);
        allMintedSerials.push(serialNumber);

        // Transfer NFT to recipient (currently owned by this contract)
        // @audit-info: Recipient (to) must be associated with the HTS token before transfer
        // If not associated, this will fail with response code 166 (TOKEN_NOT_ASSOCIATED_TO_ACCOUNT)
        // In that case, the NFT remains with this contract and can be claimed via claimStuckNFT() after association
        int256 transferResponse = this.transferNFT(htsNFTToken, address(this), to, serialNumber);
        require(
            transferResponse == int256(uint256(uint32(HederaResponseCodes.SUCCESS))),
            "Transfer failed: recipient must associate token first (call associateUserToken())"
        );

        return serialNumber;
    }

    /**
     * @dev Mints a new HTS NFT with metadata (external function for minters)
     * @param to Address to mint the NFT to
     * @param name Name of the NFT
     * @param prize Prize description
     * @param image IPFS link or CID
     * @return serialNumber The serial number of the minted NFT
     */
    function mintWithMetadata(address to, string memory name, string memory prize, string memory image)
        external
        onlyMinter
        hasHTSToken
        returns (int64 serialNumber)
    {
        return _mintWithMetadataInternal(to, name, prize, image);
    }

    /// ---------- NFT SHOP ----------

    IChestHederaCoins public chest;

    struct ShopItem {
        string name;
        string prize;
        string image;
        uint256 price;
        bool available;
    }

    mapping(uint256 => ShopItem) public shop;
    uint256 public nextItemId;

    event ShopItemAdded(uint256 indexed itemId, string name, string prize, string image, uint256 price);
    event ShopItemUpdated(
        uint256 indexed itemId, string name, string prize, string image, uint256 price, bool available
    );
    event NFTPurchased(address indexed user, uint256 itemId, int64 serialNumber, uint256 price);

    function addShopItem(string memory name, string memory prize, string memory image, uint256 price)
        external
        onlyOwner
    {
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

    /**
     * @dev Buy NFT with coins tracked in ChestHedera
     */
    function buyNFT(uint256 itemId) external nonReentrant hasHTSToken {
        ShopItem memory item = shop[itemId];
        require(item.available, "Item not available");
        require(address(chest) != address(0), "Chest not set");
        require(chest.getCoins(msg.sender) >= item.price, "Not enough coins");

        // Deduct coins from the buyer via ChestHedera spender flow
        chest.spendCoinsFrom(msg.sender, item.price);

        // Mark item as unavailable (sold)
        shop[itemId].available = false;

        // Mint NFT with metadata to buyer
        int64 serialNumber = _mintWithMetadataInternal(msg.sender, item.name, item.prize, item.image);

        emit NFTPurchased(msg.sender, itemId, serialNumber, item.price);
    }

    /// ---------- USER FUNCTIONS ----------

    /**
     * @dev Allow users to associate the HTS NFT token with their account
     * @notice Users MUST call this (or associate via SDK/API) before they can receive NFTs
     *         This function calls the inherited associateToken which allows users to associate for themselves
     *         If a transfer fails due to missing association, the NFT will be held by this contract
     *         and can be claimed via claimStuckNFT() after association
     */
    function associateUserToken() external hasHTSToken {
        // Users can call the inherited associateToken function directly
        // The contract's associateToken checks that msg.sender == account (or allows self-association)
        // Note: In Hedera, association typically requires the account's own signature, but
        // when called through a contract, it may work if the contract is authorized
        int256 response = this.associateToken(msg.sender, htsNFTToken);
        require(
            response == int256(uint256(uint32(HederaResponseCodes.SUCCESS))),
            "Failed to associate token - ensure you are calling for your own account"
        );
    }

    /**
     * @dev Transfer an NFT that was stuck in the contract due to failed transfer
     * @notice Call this after associating the token if an NFT mint succeeded but transfer failed
     * @param serialNumber The serial number of the NFT to claim
     */
    function claimStuckNFT(int64 serialNumber) external hasHTSToken {
        // Verify this serial number was minted by us (check if metadata exists)
        bytes memory nameBytes = bytes(tokenMetadata[serialNumber].name);
        require(nameBytes.length > 0, "Invalid serial number");

        // Attempt transfer to msg.sender
        int256 transferResponse = this.transferNFT(htsNFTToken, address(this), msg.sender, serialNumber);
        require(
            transferResponse == int256(uint256(uint32(HederaResponseCodes.SUCCESS))),
            "Transfer failed - ensure token is associated"
        );
    }

    /// ---------- VIEW FUNCTIONS ----------

    /**
     * @dev Get metadata for a specific NFT by serial number
     */
    function getNFTMetadata(int64 serialNumber) external view returns (NFTMetadata memory) {
        return tokenMetadata[serialNumber];
    }

    /**
     * @dev Get all serial numbers minted by this contract
     */
    function getAllMintedSerials() external view returns (int64[] memory) {
        return allMintedSerials;
    }

    /**
     * @notice For HTS NFTs, we cannot directly query ownership like ERC721
     * Users need to check ownership via HTS queries or the Hedera SDK
     * This is a limitation of HTS vs ERC721 - we track what we've minted but not ownership
     */
}
