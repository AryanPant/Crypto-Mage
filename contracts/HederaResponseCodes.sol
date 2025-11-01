// SPDX-License-Identifier: Apache-2.0
pragma solidity ^0.8.27;

/**
 * @dev Hedera Response Codes
 * Reference: https://github.com/hashgraph/hedera-smart-contracts
 */
library HederaResponseCodes {
    int32 internal constant SUCCESS = 22;
    int32 internal constant INVALID_TOKEN_ID = 85;
    int32 internal constant INVALID_TOKEN_NFT_SERIAL_NUMBER = 86;
    int32 internal constant INVALID_ACCOUNT_ID = 50;
    int32 internal constant INVALID_CONTRACT_ID = 51;
    int32 internal constant INVALID_TRANSACTION_ID = 52;
    int32 internal constant RECEIPT_NOT_FOUND = 53;
    int32 internal constant RECORD_NOT_FOUND = 54;
    int32 internal constant INVALID_SOLIDITY_ADDRESS = 55;
    int32 internal constant CONTRACT_FILE_EMPTY = 56;
    int32 internal constant CONTRACT_FILE_LARGE = 57;
    int32 internal constant INVALID_TOKEN_ID_IN_CUSTOM_FEES = 58;
    int32 internal constant INVALID_FEE_COLLECTOR_ACCOUNT_ID = 59;
    int32 internal constant TOKEN_NOT_ASSOCIATED_TO_ACCOUNT = 166;
    int32 internal constant TOKEN_ALREADY_ASSOCIATED_TO_ACCOUNT = 167;
    int32 internal constant INVALID_TOKEN_AMOUNT = 168;
    int32 internal constant ACCOUNT_AMOUNT_TRANSFERS_ONLY_ALLOWED_FOR_FUNGIBLE_COMMON = 179;
}
