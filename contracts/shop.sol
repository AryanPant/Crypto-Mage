// SPDX-License-Identifier: MIT
pragma solidity ^0.8.28;

import "@openzeppelin/contracts/token/ERC721/extensions/ERC721Enumerable.sol";
import "@openzeppelin/contracts/access/Ownable.sol";

contract ChestNFT is ERC721Enumerable, Ownable {
    uint256 public nextTokenId;

    struct NFTMetadata {
        string name;
        string prize;
        string image; // IPFS CID or full ipfs:// link
    }

    mapping(uint256 => NFTMetadata) public tokenMetadata;
    mapping(address => bool) public minters;

    modifier onlyMinter() {
        require(minters[msg.sender], "Not a minter");
        _;
    }

    constructor() ERC721("ChestNFT", "CNFT") Ownable(msg.sender) {}

    function setMinter(address minter, bool allowed) external onlyOwner {
        minters[minter] = allowed;
    }

    function mintWithMetadata(
        address to,
        string memory name,
        string memory prize,
        string memory image
    ) external onlyMinter returns (uint256) {
        uint256 tokenId = nextTokenId;
        _safeMint(to, tokenId);
        tokenMetadata[tokenId] = NFTMetadata(name, prize, image);
        nextTokenId++;
        return tokenId;
    }

    /// Fetch all NFTs owned by a user
    function getUserNFTs(address user) external view returns (NFTMetadata[] memory) {
        uint256 count = balanceOf(user);
        NFTMetadata[] memory items = new NFTMetadata[](count);
        for (uint256 i = 0; i < count; i++) {
            uint256 tokenId = tokenOfOwnerByIndex(user, i);
            items[i] = tokenMetadata[tokenId];
        }
        return items;
    }
}
