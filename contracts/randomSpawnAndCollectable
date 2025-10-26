// SPDX-License-Identifier: MIT
pragma solidity ^0.8.28;

import "@openzeppelin/contracts/security/ReentrancyGuard.sol";

interface IEntropy {
    function getFeeV2() external view returns (uint256);
    function requestV2() external payable returns (uint64);
}

contract RandomNo is ReentrancyGuard {
    IEntropy public entropy;
    
    struct RandomRequest {
        address requester;
        bool fulfilled;
        uint256 timestamp;
        uint256 randomNumber; // Will store the final random number (0-4)
    }

    // sequenceNumber => RandomRequest
    mapping(uint64 => RandomRequest) public randomRequests;
    // user => array of sequenceNumbers requested by that user
    mapping(address => uint64[]) public userRequests;
    
    // Events
    event RandomRequested(uint64 indexed sequenceNumber, address indexed user, uint256 timestamp, uint256 fee);
    event RandomFulfilled(uint64 indexed sequenceNumber, address indexed user, uint256 randomNumber);

    modifier onlyEntropy() {
        require(msg.sender == address(entropy), "Caller is not entropy provider");
        _;
    }

    constructor(address _entropy) {
        require(_entropy != address(0), "Invalid entropy address");
        entropy = IEntropy(_entropy);
    }

    /// Request a random number between 0-4. Caller must send at least the entropy fee.
    function getRandomNumber() external payable nonReentrant returns (uint64 sequenceNumber) {
        uint256 fee = entropy.getFeeV2();
        require(msg.value >= fee, "Insufficient entropy fee");

        // Forward exactly the fee to entropy
        sequenceNumber = entropy.requestV2{value: fee}();
        
        // Basic validation: treat sequenceNumber == 0 as invalid
        require(sequenceNumber != 0, "Invalid sequence number from entropy");

        // Store the request
        randomRequests[sequenceNumber] = RandomRequest({
            requester: msg.sender,
            fulfilled: false,
            timestamp: block.timestamp,
            randomNumber: 0
        });

        // Track user requests
        userRequests[msg.sender].push(sequenceNumber);

        // Refund any extra ETH sent by the caller
        if (msg.value > fee) {
            payable(msg.sender).transfer(msg.value - fee);
        }

        emit RandomRequested(sequenceNumber, msg.sender, block.timestamp, fee);
    }

    /// Entropy contract calls this once randomness is ready.
    /// Only callable by the configured entropy contract.
    function entropyCallback(
        uint64 sequenceNumber,
        address /* provider */,
        bytes32 randomNumber
    ) external nonReentrant onlyEntropy {
        RandomRequest storage request = randomRequests[sequenceNumber];

        // Ignore if no request or already fulfilled
        if (request.requester == address(0) || request.fulfilled) return;

        // Convert bytes32 to uint256 and map to 0-4 range
        uint256 random = uint256(randomNumber);
        uint256 finalRandomNumber = random % 5; // 0, 1, 2, 3, or 4

        // Update the request
        request.fulfilled = true;
        request.randomNumber = finalRandomNumber;

        emit RandomFulfilled(sequenceNumber, request.requester, finalRandomNumber);
    }

    /// Get the random number for a specific request (returns 0 if not fulfilled)
    function getRandomResult(uint64 sequenceNumber) external view returns (uint256) {
        RandomRequest memory request = randomRequests[sequenceNumber];
        require(request.requester != address(0), "Request does not exist");
        
        if (!request.fulfilled) {
            return type(uint256).max; // Indicates not yet fulfilled
        }
        
        return request.randomNumber;
    }

    /// Get all requests made by a user
    function getUserRequests(address user) external view returns (uint64[] memory) {
        return userRequests[user];
    }

    /// Get unfulfilled requests for a user
    function getUnfulfilledRequests(address user) external view returns (uint64[] memory) {
        uint64[] memory allRequests = userRequests[user];
        uint256 count;

        // Count unfulfilled requests
        for (uint256 i = 0; i < allRequests.length; i++) {
            if (!randomRequests[allRequests[i]].fulfilled) {
                count++;
            }
        }

        // Create array of unfulfilled requests
        uint64[] memory pending = new uint64[](count);
        uint256 index;
        for (uint256 i = 0; i < allRequests.length; i++) {
            if (!randomRequests[allRequests[i]].fulfilled) {
                pending[index++] = allRequests[i];
            }
        }

        return pending;
    }

    /// Check if a specific request is fulfilled
    function isRequestFulfilled(uint64 sequenceNumber) external view returns (bool) {
        return randomRequests[sequenceNumber].fulfilled;
    }

    /// Get request details
    function getRequestDetails(uint64 sequenceNumber) external view returns (
        address requester,
        bool fulfilled,
        uint256 timestamp,
        uint256 randomNumber
    ) {
        RandomRequest memory request = randomRequests[sequenceNumber];
        return (request.requester, request.fulfilled, request.timestamp, request.randomNumber);
    }

    /// Emergency function to withdraw contract balance (only if needed)
    function withdraw() external {
        require(msg.sender == address(this), "Not authorized");
        payable(msg.sender).transfer(address(this).balance);
    }
}
