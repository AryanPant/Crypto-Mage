// SPDX-License-Identifier: MIT
pragma solidity ^0.8.27;

/// @notice Wizard lobby: players stake HBAR (native Hedera currency) to join a 2–4 player game.
/// After the game ends, the owner submits the final leaderboard; rewards pay 60%/40% to top two.
///
/// @dev HBAR is the native currency on Hedera, so it doesn't have a token address.
/// This contract uses native HBAR transfers via payable functions.

import "@openzeppelin/contracts/utils/ReentrancyGuard.sol";
import "@openzeppelin/contracts/utils/Address.sol";

contract WizardLobbySepolia is ReentrancyGuard {
    using Address for address payable;

    /// ---------- CONFIG ----------
    uint256 public constant MAX_PLAYERS = 4;
    uint256 public constant MIN_PLAYERS = 2;
    uint256 public constant WINNER_BPS = 6000; // 60.00%
    uint256 public constant RUNNER_UP_BPS = 4000; // 40.00%
    uint256 public constant TOTAL_BPS = 10000;
    uint256 public constant GAME_DURATION_SECONDS = 300; // Duration of the game in seconds (5 minutes)
    // Note: 1 HBAR = 100,000,000 tinybars (8 decimals)

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

    // Minimum stake in tinybars (1 HBAR = 100,000,000 tinybars, since 1 HBAR = 100,000,000 tinybars)
    uint256 public constant MINIMUM_STAKE_AMOUNT = 1_00_000_000; // 1 HBAR in tinybars

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

    constructor() {
        contractOwner = msg.sender;
    }

    modifier onlyContractOwner() {
        if (msg.sender != contractOwner) revert UnauthorizedAccess();
        _;
    }

    /////////////////////////////////////////////
    /////         STAKE REQUIREMENT         /////
    /////////////////////////////////////////////

    /// ---------- USER FUNCTIONS ----------
    /// @notice Set or update a username for caller (required before staking)
    function setUsername(string calldata _name) external {
        require(bytes(_name).length > 0, "Username cannot be empty");
        playerUsername[msg.sender] = _name;
        emit PlayerUsernameSet(msg.sender, _name);
    }

    /// @notice Stake HBAR and join the lobby
    /// @dev Users send HBAR via msg.value when calling this payable function
    function stakeAndJoin() external payable nonReentrant {
        address user = msg.sender;
        uint256 value = msg.value; // Amount of HBAR sent in tinybars

        require(bytes(playerUsername[user]).length > 0, "Must set username first");
        require(!isInLobby[user], "Already in current lobby");
        require(value > 0, "No HBAR sent");
        require(currentLobby.length < MAX_PLAYERS, "Current Lobby is Full");
        require(value >= MINIMUM_STAKE_AMOUNT, "Stake must be at least 1 HBAR");

        currentLobby.push(user);
        // Record participant for leaderboard generation (avoid duplicates within the same game)
        bool alreadyAdded = false;
        for (uint256 i = 0; i < gameParticipants.length; i++) {
            if (gameParticipants[i] == user) {
                alreadyAdded = true;
                break;
            }
        }
        if (!alreadyAdded) {
            gameParticipants.push(user);
        }
        isInLobby[user] = true;
        hasPlayerStaked[user] = true;
        playerStakeAmount[user] = value;
        totalStakedAmount += value;

        if (currentLobby.length == MAX_PLAYERS) emit LobbyReachedCapacity(currentLobby);

        emit PlayerJoinedLobby(user, playerUsername[user], value);
    }

    function startGame() external {
        require(currentLobby.length >= MIN_PLAYERS, "Not enough players staked");
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

    // finalLeaderboard is set
    function _storeFinalLeaderboard() external {
        finalLeaderboard = generateGameLeaderboard();
    }

    function distributeRewards(address[] calldata leaderboard) external nonReentrant {
        require(block.timestamp > gameEndTime, "Game has not Ended");
        require(!areRewardsDistributed, "Rewards already distributed");

        address[] memory systemGeneratedLeaderboard = generateGameLeaderboard();
        if (leaderboard.length != systemGeneratedLeaderboard.length) revert leaderboardLengthMismatch();

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

        // Send HBAR payouts using Address.sendValue (recommended method)
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

                // Send HBAR stake back to player using Address.sendValue
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
    /// @notice Emergency withdraw all HBAR from the contract
    /// @dev Withdraws all HBAR to contract owner using Address.sendValue
    function emergencyWithdrawAllFunds() external onlyContractOwner nonReentrant {
        uint256 contractBalance = address(this).balance;
        require(contractBalance > 0, "No balance to withdraw");

        Address.sendValue(payable(contractOwner), contractBalance);

        emit EmergencyFundsWithdrawn(contractOwner, contractBalance);
        _resetLobbyState();
    }

    // Returns the current leaderboard sorted by kills (desc) and deaths (asc on ties).
    function getCurrentLeaderboard() external view returns (address[] memory) {
        return generateGameLeaderboard();
    }

    // Returns the player address at the given rank position in the final leaderboard.
    function getPlayerAtRank(uint256 rankPosition) external view returns (address) {
        require(rankPosition < finalLeaderboard.length, "Rank position out of bounds");
        return finalLeaderboard[rankPosition];
    }

    // Returns the kills and deaths for the specified player address.
    function getPlayerGameStatistics(address playerAddress) external view returns (uint256 kills, uint256 deaths) {
        return (playerStats[playerAddress].totalKills, playerStats[playerAddress].totalDeaths);
    }

    // Returns an array of all player addresses that have participated in the game.
    function getAllGameParticipants() external view returns (address[] memory) {
        return gameParticipants;
    }

    // Returns the total number of players who have participated in the game.
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
