// SPDX-License-Identifier: MIT
pragma solidity ^0.8.27;

/// @notice Wizard lobby contract for hackathon
/// - Each player must stake Sepolia-ETH worth >= $1 (via Pyth ETH/USD feed)
/// - Single lobby (2..4 players). Unity (off-chain) runs the game timer.
/// - Owner (game server) submits the final leaderboard and contract pays 60%/40% to top 2.

import "@openzeppelin/contracts/utils/ReentrancyGuard.sol";
import "@openzeppelin/contracts/utils/Address.sol";
import "@pythnetwork/pyth-sdk-solidity/IPyth.sol";
import "@pythnetwork/pyth-sdk-solidity/PythStructs.sol";

contract WizardLobbySepolia is ReentrancyGuard {
    /// ---------- CONFIG ----------
    uint256 public constant MAX_PLAYERS = 4;
    uint256 public constant MIN_PLAYERS = 2;
    uint256 public constant WINNER_BPS = 6000; // 60.00% (basis points)
    uint256 public constant RUNNER_UP_BPS = 4000; // 40.00%
    uint256 public constant TOTAL_BPS = 10000;
    uint256 public constant GAME_DURATION_SECONDS = 600; // Duration of the game in seconds (10 minutes)

    /// Require USD value = 1 USD exactly (corrected for proper Pyth format)
    // @dev-info : comment says 8 decimal
    uint256 public constant MINIMUM_USD_VALUE = 1 * 10 ** 18; // $1 in 8 decimals (Pyth format)

    /// Pyth price feed ID for ETH/USD
    bytes32 public constant ETH_USD_PRICE_FEED_ID = 0xff61491a931112ddf1bd8147cd1b641375f79f5825126d665480874634fd0ace;

    /// ---------- STATE ----------
    address public contractOwner;
    address[] public currentLobby;
    address[] public gameParticipants;
    address[] public finalLeaderboard;
    mapping(address => bool) public isInLobby;
    mapping(address => string) public playerUsername;
    mapping(address => bool) public hasPlayerStaked;
    mapping(address => uint256) public playerStakeAmount;
    mapping(address => PlayerStats) public playerStats;
    mapping(address => bool) seen;

    uint256 public totalStakedAmount;
    bool public areRewardsDistributed;
    uint256 public gameEndTime;

    struct PlayerStats {
        uint256 totalKills;
        uint256 totalDeaths;
    }

    /// Pyth contract interface
    IPyth public pythPriceOracle;

    // Custom error for gas efficiency
    error leaderboardLengthMismatch();
    error UnauthorizedAccess();

    /// ---------- EVENTS ----------
    event PlayerUsernameSet(address player, string username);
    event PlayerJoinedLobby(address player, string username, uint256 stakedAmount);
    event LobbyReachedCapacity(address[] players);
    event GameRewardsDistributed(address winner, uint256 winnerReward, address runnerUp, uint256 runnerUpReward);
    event EmergencyFundsWithdrawn(address owner, uint256 amount);
    event PlayerEliminationRecorded(address indexed killer, address indexed victim, bool isSelfElimination);
    event LeaderboardGenerated(address[] sortedPlayers);
    event GameStarted();
    event StakesReturnedDueToMismatch(address[] players, uint256 totalAmountReturned);

    
    constructor(address _pythContract) {
        contractOwner = msg.sender;
        pythPriceOracle = IPyth(_pythContract);
    }


    modifier onlyContractOwner() {
        require(msg.sender == contractOwner, UnauthorizedAccess());
        _;
    }

    /////////////////////////////////////////////
    /////          PRICE FUNCTIONS          /////
    /////////////////////////////////////////////
    function getCurrentPrice() internal view returns (PythStructs.Price memory) {
        return pythPriceOracle.getPriceUnsafe(ETH_USD_PRICE_FEED_ID);
    }

    function getLatestRawPrice() external view returns (int64) {
        PythStructs.Price memory priceData = pythPriceOracle.getPriceUnsafe(ETH_USD_PRICE_FEED_ID);
        return priceData.price;
    }

    function getPriceExponent() external view returns (int32) {
        PythStructs.Price memory priceData = pythPriceOracle.getPriceUnsafe(ETH_USD_PRICE_FEED_ID);
        return priceData.expo;
    }

    /// @notice Convert ETH amount to USD value (8 decimals to match Pyth format)
    function calculateUSDValue(uint256 ethAmountInWei) internal view returns (uint256) {
        PythStructs.Price memory priceData = getCurrentPrice();
        require(priceData.price > 0, "Invalid price from oracle");

        // Convert price to proper format
        uint256 adjustedPricePerETH;
        if (priceData.expo >= 0) {
            adjustedPricePerETH = uint256(uint64(priceData.price)) * (10 ** uint32(priceData.expo));
        } else {
            adjustedPricePerETH = uint256(uint64(priceData.price)) / (10 ** uint32(-priceData.expo));
        }

        // Calculate USD value: (ethAmount * pricePerETH) / 1e18
        // Result should be in 8 decimals to match Pyth format
        uint256 usdValueInCents = (ethAmountInWei * adjustedPricePerETH);
        return usdValueInCents;
    }

    /// ---------- USER FUNCTIONS ----------
    /// @notice Set or update a username for caller (required before staking)
    function setUsername(string calldata _name) external {
        require(bytes(_name).length > 0, "Username cannot be empty");
        playerUsername[msg.sender] = _name;
        emit PlayerUsernameSet(msg.sender, _name);
    }

    /// @notice Stake SEP-ETH equivalent to $1 USD and join lobby
    function stakeAndJoin() external payable nonReentrant {
        address user = msg.sender;
        uint256 value = msg.value;

        require(bytes(playerUsername[user]).length > 0, "Must set username first");
        require(!isInLobby[user], "Already in current lobby");
        require(value > 0, "No ETH sent");
        require(currentLobby.length > MAX_PLAYERS, "Current Lobby is Full");

        uint256 usdEquivalent = calculateUSDValue(value);
        require(usdEquivalent >= MINIMUM_USD_VALUE, "Stake must be at least $1 USD");

        currentLobby.push(user);
        isInLobby[user] = true;
        hasPlayerStaked[user] = true;
        playerStakeAmount[user] = value;
        totalStakedAmount += value;

        if (currentLobby.length == MAX_PLAYERS) emit LobbyReachedCapacity(currentLobby);

        emit PlayerJoinedLobby(user, playerUsername[user], value);
    }

    function startGame() external {
        gameEndTime = block.timestamp + GAME_DURATION_SECONDS;
        areRewardsDistributed = false;
        emit GameStarted();
    }

    function recordPlayerElimination(address killerAddress, address victimAddress) external {
        require(block.timestamp <= gameEndTime, "Game has Ended");
        require(victimAddress != address(0), "Victim address cannot be zero");
        require(isInLobby[killerAddress] == true, "Killer isn't in lobby");
        require(isInLobby[victimAddress] == true, "Victim isn't in lobby");

        bool isSelfElimination = (killerAddress == address(0) || killerAddress == victimAddress);

        playerStats[victimAddress].totalDeaths++;

        if (!isSelfElimination) {
            playerStats[killerAddress].totalKills++;
        }

        emit PlayerEliminationRecorded(killerAddress, victimAddress, isSelfElimination);
    }

    /**
     * @dev Generate leaderboard and return addresses sorted by kills
     * @return Array of player addresses sorted by kills in descending order
     */
    function generateGameLeaderboard() public view returns (address[] memory) {
        require(gameParticipants.length > 0, "No players have participated yet");

        // Create a copy of participants array for sorting
        address[] memory sortedList = new address[](gameParticipants.length);
        for (uint256 i = 0; i < gameParticipants.length; i++) {
            sortedList[i] = gameParticipants[i];
        }

        // Sort players by kills using bubble sort (descending order)
        for (uint256 i = 0; i < sortedList.length - 1; i++) {
            for (uint256 j = 0; j < sortedList.length - i - 1; j++) {
                // Compare kills - if equal, compare deaths (fewer deaths = better rank)
                uint256 killsJ = playerStats[sortedList[j]].totalKills;
                uint256 killsNext = playerStats[sortedList[j + 1]].totalKills;
                uint256 deathsJ = playerStats[sortedList[j]].totalDeaths;
                uint256 deathsNext = playerStats[sortedList[j + 1]].totalDeaths;

                if (killsJ < killsNext || (killsJ == killsNext && deathsJ > deathsNext)) {
                    // Swap addresses
                    address tempAddress = sortedList[j];
                    sortedList[j] = sortedList[j + 1];
                    sortedList[j + 1] = tempAddress;
                }
            }
        }

        return sortedList;
    }

    function _storeFinalLeaderboard() external {
        finalLeaderboard = generateGameLeaderboard();
    }

    function distributeRewards(address[] calldata leaderboard) external nonReentrant {
        require(block.timestamp > gameEndTime, "Game has not Ended");
        require(!areRewardsDistributed, "Rewards already distributed");

        address[] memory systemGeneratedLeaderboard = generateGameLeaderboard();
        require(leaderboard.length != systemGeneratedLeaderboard.length, leaderboardLengthMismatch());

        // Compare each element in both arrays
        for (uint256 i = 0; i < leaderboard.length; i++) {
            if (leaderboard[i] != systemGeneratedLeaderboard[i]) {
                _leaderboardMismatch();
                return;
            }
        }

        // Verify leaderboard contains exactly the same addresses as lobby
        for (uint256 i = 0; i < leaderboard.length; i++) {
            require(isInLobby[leaderboard[i]], "Leaderboard contains non-lobby player");
        }

        // Check for duplicate addresses in leaderboard
        for (uint256 i = 0; i < leaderboard.length; i++) {
            require(!seen[leaderboard[i]], "Duplicate address in leaderboard");
            seen[leaderboard[i]] = true;
        }

        _executeRewardDistribution(leaderboard);
    }

    /// @notice Internal function to distribute rewards after match ends
    function _executeRewardDistribution(address[] memory leaderboard) internal {
        address winner = leaderboard[0];
        address runnerUp = leaderboard[1];

        uint256 totalPrizePool = totalStakedAmount;
        require(totalPrizePool > 0, "Prize pool is empty");

        uint256 winnerReward = (totalPrizePool * WINNER_BPS) / TOTAL_BPS;
        uint256 runnerUpReward = (totalPrizePool * RUNNER_UP_BPS) / TOTAL_BPS;

        require(winnerReward + runnerUpReward <= totalPrizePool, "Reward calculation overflow");

        // Mark distributed before external calls
        areRewardsDistributed = true;

        // Send payouts using safe transfer
        Address.sendValue(payable(winner), winnerReward);
        Address.sendValue(payable(runnerUp), runnerUpReward);

        emit GameRewardsDistributed(winner, winnerReward, runnerUp, runnerUpReward);

        _resetLobbyState();
        _resetGameState();
    }

    function _leaderboardMismatch() internal {
        uint256 totalReturned = 0;

        // Return stakes to all players in the current lobby
        for (uint256 i = 0; i < currentLobby.length; i++) {
            address player = currentLobby[i];
            uint256 stakeAmount = playerStakeAmount[player];

            if (stakeAmount > 0) {
                // Reset player stake amount before transfer to prevent reentrancy
                playerStakeAmount[player] = 0;

                // Send stake back to player
                Address.sendValue(payable(player), stakeAmount);
                totalReturned += stakeAmount;
            }
        }

        // Reset total staked amount
        totalStakedAmount = 0;

        // Emit event for stake returns
        emit StakesReturnedDueToMismatch(currentLobby, totalReturned);

        // Reset lobby state
        _resetLobbyState();
        _resetGameState();
    }

    /// ---------- VIEW FUNCTIONS ----------
    function getCurrentLobbyPlayers() external view returns (address[] memory) {
        return currentLobby;
    }

    function getPlayerUsername(address _playerAddress) external view returns (string memory) {
        return playerUsername[_playerAddress];
    }

    /// ---------- EMERGENCY FUNCTIONS ----------
    function emergencyWithdrawAllFunds() external onlyContractOwner nonReentrant {
        uint256 contractBalance = address(this).balance;
        require(contractBalance > 0, "No balance to withdraw");

        Address.sendValue(payable(contractOwner), contractBalance);

        emit EmergencyFundsWithdrawn(contractOwner, contractBalance);
        _resetLobbyState();
    }

    /**
     * @dev View function to get the current leaderboard
     * @return Array of player addresses sorted by kills in descending order
     */
    function getCurrentLeaderboard() external view returns (address[] memory) {
        return generateGameLeaderboard();
    }

    /**
     * @dev View function to get leaderboard position of a specific rank
     * @param rankPosition Rank position (0 = winner, 1 = second place, etc.)
     * @return Player address at the specified position
     */
    function getPlayerAtRank(uint256 rankPosition) external view returns (address) {
        require(rankPosition < finalLeaderboard.length, "Rank position out of bounds");
        return finalLeaderboard[rankPosition];
    }

    /**
     * @dev Get player statistics
     * @param playerAddress Address of the player
     * @return kills Number of kills
     * @return deaths Number of deaths
     */
    function getPlayerGameStatistics(address playerAddress) external view returns (uint256 kills, uint256 deaths) {
        return (playerStats[playerAddress].totalKills, playerStats[playerAddress].totalDeaths);
    }

    /**
     * @dev Get all players who have participated
     * @return Array of all player addresses
     */
    function getAllGameParticipants() external view returns (address[] memory) {
        return gameParticipants;
    }

    /**
     * @dev Get total number of players
     * @return Number of players who have participated
     */
    function getTotalParticipantCount() external view returns (uint256) {
        return gameParticipants.length;
    }

    /// ---------- INTERNAL FUNCTIONS ----------

    function _resetGameState() internal {
        // Reset all player game stats
        for (uint256 i = 0; i < gameParticipants.length; i++) {
            address participantAddress = gameParticipants[i];
            delete playerStats[participantAddress];
        }

        delete gameParticipants;
        delete finalLeaderboard;
    }

    function _resetLobbyState() internal {
        // Clear mappings for all lobby players
        for (uint256 i = 0; i < currentLobby.length; i++) {
            address lobbyPlayer = currentLobby[i];
            isInLobby[lobbyPlayer] = false;
            hasPlayerStaked[lobbyPlayer] = false;
            playerStakeAmount[lobbyPlayer] = 0;
            seen[lobbyPlayer] = false;
        }
        delete currentLobby;
        totalStakedAmount = 0;
        areRewardsDistributed = false;
    }
}
