// SPDX-License-Identifier: Apache-2.0
pragma solidity ^0.8.20;

/**
 * Hedera PRNG helper contract
 *
 * Implements access to the Hedera PRNG precompile (address 0x169) as described in the
 * official tutorial and HIP-351. Provides convenience methods to:
 *  - fetch a pseudorandom seed
 *  - derive a pseudorandom number within an inclusive-exclusive range [lo, hi)
 *  - compute a wheel index for a spin-the-wheel feature with N segments
 *
 * References:
 * - How to Generate a Random Number on Hedera:
 *   https://docs.hedera.com/hedera/tutorials/more-tutorials/how-to-generate-a-random-number-on-hedera#Generate%20Random%20Numbers%20Using%20Solidity
 */
contract HederaPrng {
    // Hedera PRNG precompile address (per HIP-351 and Hedera docs)
    address private constant PRNG_PRECOMPILE = address(0x169);

    // Cached last generated number for simple reads/verification flows
    uint32 private lastRandomNumber;

    event RandomNumberGenerated(bytes32 seed, uint32 randomNumber);
    event WheelSpun(uint256 numSegments, uint256 selectedIndex);

    /**
     * Calls the precompile to obtain a 256-bit pseudorandom seed.
     * The specific selector is defined by the precompile interface.
     */
    function getPseudorandomSeed() public returns (bytes32 randomSeed) {
        // selector for getPseudorandomSeed() on the PRNG precompile per HIP-351
        bytes4 selector = bytes4(keccak256("getPseudorandomSeed()"));
        (bool callSuccessful, bytes memory precompileResult) = PRNG_PRECOMPILE.call(abi.encodeWithSelector(selector));
        require(callSuccessful, "PRNG precompile call failed");
        randomSeed = abi.decode(precompileResult, (bytes32));
    }

    /**
     * Returns a pseudorandom uint32 in the inclusive-exclusive range [lo, hi).
     * Stores the value in `lastRandomNumber` and emits an event for off-chain consumers.
     */
    function getRandomInRange(uint32 lo, uint32 hi) public returns (uint32) {
        require(hi > lo, "invalid range");

        // Fetch a new seed from the precompile and derive a uint32
        bytes32 seed = getPseudorandomSeed();

        // Take the low-order 32 bits from the seed in a defined, safe way
        uint32 choice = uint32(uint256(seed));

        uint32 range = hi - lo;
        uint32 value = lo + (choice % range);
        lastRandomNumber = value;
        emit RandomNumberGenerated(seed, value);
        return value;
    }

    /**
     * Spin-the-wheel helper: maps randomness into an index in [0, numSegments).
     * Intended for use cases like selecting a prize slice on a wheel.
     */
    function spinWheel(uint256 numSegments) external returns (uint256) {
        require(numSegments > 0 && numSegments <= type(uint32).max, "invalid segments");
        uint32 value = getRandomInRange(0, uint32(numSegments));
        uint256 index = uint256(value);
        emit WheelSpun(numSegments, index);
        return index;
    }

    /**
     * Read the most recently generated random number for simple verification/UI.
     */
    function getLastRandomNumber() external view returns (uint32) {
        return lastRandomNumber;
    }
}
