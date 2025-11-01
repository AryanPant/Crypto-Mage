// SPDX-License-Identifier: Apache-2.0
pragma solidity ^0.8.27;

/**
 * @dev Hedera Token Service Interface
 * Reference: https://github.com/hashgraph/hedera-smart-contracts
 *
 * This is a minimal implementation for HTS operations used in the staking contract.
 * For full functionality, import from: https://github.com/hashgraph/hedera-smart-contracts
 */
import "./HederaResponseCodes.sol";

interface IHederaTokenService {
    function associateToken(address account, address token) external returns (int256 responseCode);

    function transferToken(address token, address sender, address receiver, int64 amount)
        external
        returns (int256 responseCode);

    function transferTokens(address token, address[] memory accountIds, int64[] memory amounts)
        external
        returns (int256 responseCode);

    function transferNFT(address token, address sender, address receiver, int64 serialNumber)
        external
        returns (int256 responseCode);

    function transferNFTs(
        address token,
        address[] memory senders,
        address[] memory receivers,
        int64[] memory serialNumbers
    ) external returns (int256 responseCode);

    function mintToken(address token, int64 amount, bytes[] memory metadata)
        external
        returns (int256 responseCode, int64[] memory serialNumbers);
}

/**
 * @dev Hedera Token Service
 * Wrapper around the precompiled Hedera Token Service system contract at address 0x167
 */
contract HederaTokenService {
    address constant PRECOMPILE_ADDRESS = address(0x167);

    /**
     * @dev Associates a Hedera Token Service token to an account
     * @param account The account to associate the token to
     * @param token The token address to associate
     * @return responseCode The response code for the operation
     */
    function associateToken(address account, address token) external returns (int256 responseCode) {
        (bool success, bytes memory result) =
            PRECOMPILE_ADDRESS.call(abi.encodeWithSelector(IHederaTokenService.associateToken.selector, account, token));
        require(success, "HTS precompile call failed");
        responseCode = abi.decode(result, (int256));
        return responseCode;
    }

    /**
     * @dev Transfers a fungible token from one account to another
     * @param token The token address to transfer
     * @param sender The account to transfer from
     * @param receiver The account to transfer to
     * @param amount The amount to transfer (as int64)
     * @return responseCode The response code for the operation
     */
    function transferToken(address token, address sender, address receiver, int64 amount)
        external
        returns (int256 responseCode)
    {
        (bool success, bytes memory result) = PRECOMPILE_ADDRESS.call(
            abi.encodeWithSelector(IHederaTokenService.transferToken.selector, token, sender, receiver, amount)
        );
        require(success, "HTS precompile call failed");
        responseCode = abi.decode(result, (int256));
        return responseCode;
    }

    /**
     * @dev Transfers multiple amounts of tokens between accounts
     * @param token The token address to transfer
     * @param accountIds Array of account addresses
     * @param amounts Array of amounts (positive for receiving, negative for sending)
     * @return responseCode The response code for the operation
     */
    function transferTokens(address token, address[] memory accountIds, int64[] memory amounts)
        external
        returns (int256 responseCode)
    {
        (bool success, bytes memory result) = PRECOMPILE_ADDRESS.call(
            abi.encodeWithSelector(IHederaTokenService.transferTokens.selector, token, accountIds, amounts)
        );
        require(success, "HTS precompile call failed");
        responseCode = abi.decode(result, (int256));
        return responseCode;
    }

    /**
     * @dev Transfers an NFT from one account to another
     * @param token The NFT token address
     * @param sender The account to transfer from
     * @param receiver The account to transfer to
     * @param serialNumber The serial number of the NFT
     * @return responseCode The response code for the operation
     */
    function transferNFT(address token, address sender, address receiver, int64 serialNumber)
        external
        returns (int256 responseCode)
    {
        (bool success, bytes memory result) = PRECOMPILE_ADDRESS.call(
            abi.encodeWithSelector(IHederaTokenService.transferNFT.selector, token, sender, receiver, serialNumber)
        );
        require(success, "HTS precompile call failed");
        responseCode = abi.decode(result, (int256));
        return responseCode;
    }

    /**
     * @dev Transfers multiple NFTs between accounts
     * @param token The NFT token address
     * @param senders Array of sender addresses
     * @param receivers Array of receiver addresses
     * @param serialNumbers Array of NFT serial numbers
     * @return responseCode The response code for the operation
     */
    function transferNFTs(
        address token,
        address[] memory senders,
        address[] memory receivers,
        int64[] memory serialNumbers
    ) external returns (int256 responseCode) {
        (bool success, bytes memory result) = PRECOMPILE_ADDRESS.call(
            abi.encodeWithSelector(IHederaTokenService.transferNFTs.selector, token, senders, receivers, serialNumbers)
        );
        require(success, "HTS precompile call failed");
        responseCode = abi.decode(result, (int256));
        return responseCode;
    }

    /**
     * @dev Mints new NFTs to the contract (for NFTs, amount should be 0)
     * @param token The HTS NFT token address (must already exist)
     * @param amount Amount to mint (0 for NFTs, positive for fungible tokens)
     * @param metadata Array of metadata bytes for each NFT to mint
     * @return responseCode The response code for the operation
     * @return serialNumbers Array of serial numbers of minted NFTs
     */
    function mintToken(address token, int64 amount, bytes[] memory metadata)
        external
        returns (int256 responseCode, int64[] memory serialNumbers)
    {
        (bool success, bytes memory result) = PRECOMPILE_ADDRESS.call(
            abi.encodeWithSelector(IHederaTokenService.mintToken.selector, token, amount, metadata)
        );
        require(success, "HTS precompile call failed");
        (responseCode, serialNumbers) = abi.decode(result, (int256, int64[]));
        return (responseCode, serialNumbers);
    }
}
