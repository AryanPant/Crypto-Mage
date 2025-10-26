// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

/// @notice Wizard lobby contract for hackathon
/// - Each player must stake Sepolia-ETH worth >= $1 (via Pyth ETH/USD feed)
/// - Single lobby (2..4 players). Unity (off-chain) runs the game timer.
/// - Owner (game server) submits the final leaderboard and contract pays 60%/40% to top 2.

import "@openzeppelin/contracts/security/ReentrancyGuard.sol";
import "@pythnetwork/pyth-sdk-solidity/IPyth.sol";
import "@pythnetwork/pyth-sdk-solidity/PythStructs.sol";

contract WizardLobbySepolia is ReentrancyGuard {
    /// ---------- CONFIG ----------
    uint256 public constant MAX_PLAYERS = 4;
    uint256 public constant MIN_PLAYERS = 2;
    uint256 public constant WINNER_SHARE_BPS = 6000; // 60.00% (basis points)
    uint256 public constant SECOND_SHARE_BPS = 4000; // 40.00%
    uint256 public constant BPS_DENOM = 10000;

    /// Require USD value = 1 USD exactly (corrected for proper Pyth format)
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
    mapping(address => PlayerStats) public playerGameStats;
    mapping(address => bool) public isParticipantRegistered;
    uint256 public totalStakedAmount;
    bool public areRewardsDistributed;

    struct PlayerStats {
        uint256 totalKills;
        uint256 totalDeaths;
    }

    /// Pyth contract interface
    IPyth public pythPriceOracle;

    // Custom error for gas efficiency
    error LeaderboardMismatch();

    /// ---------- EVENTS ----------
    event PlayerUsernameSet(address indexed player, string username);
    event PlayerJoinedLobby(address indexed player, string username, uint256 stakedAmount);
    event LobbyReachedCapacity(address[] players);
    event GameRewardsDistributed(address indexed winner, uint256 winnerReward, address indexed runnerUp, uint256 runnerUpReward);
    event EmergencyFundsWithdrawn(address indexed owner, uint256 amount);
    event PlayerEliminationRecorded(address indexed killer, address indexed victim, bool isSelfElimination);
    event LeaderboardGenerated(address[] sortedPlayers);

    /// ---------- CONSTRUCTOR ----------
    constructor(address _pythContract) {
        contractOwner = msg.sender;
        pythPriceOracle = IPyth(_pythContract);
    }

    /// ---------- MODIFIERS ----------
    modifier onlyContractOwner() {
        require(msg.sender == contractOwner, "Unauthorized: not contract owner");
        _;
    }
/////////////////////////////////////////////////////////////////////////////////////////////////////
    /// ---------- PRICE FUNCTIONS ----------
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
        uint256 usdValueInCents = (ethAmountInWei * adjustedPricePerETH) ;
        return usdValueInCents;
    }
/////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// ---------- USER FUNCTIONS ----------
    /// @notice Set or update a username for caller (required before staking)
    function setUsername(string calldata _name) external {
        require(bytes(_name).length > 0, "Username cannot be empty");
        playerUsername[msg.sender] = _name;
        emit PlayerUsernameSet(msg.sender, _name);
    }
//////////////////////////////////////////////////////////////////////////////////////////
    /// @notice Stake SEP-ETH equivalent to $1 USD and join lobby
    function stakeAndJoin() external payable nonReentrant {
        require(bytes(playerUsername[msg.sender]).length > 0, "Must set username first");
        require(!isInLobby[msg.sender], "Already in current lobby");
        require(currentLobby.length < MAX_PLAYERS, "Lobby is full");
        require(msg.value > 0, "No ETH sent");

        uint256 usdEquivalent = calculateUSDValue(msg.value);
        require(usdEquivalent >= MINIMUM_USD_VALUE, "Stake must be at least $1 USD");
//
        // Register player in lobby
        currentLobby.push(msg.sender);
        isInLobby[msg.sender] = true;
        hasPlayerStaked[msg.sender] = true;
        totalStakedAmount += msg.value;

        emit PlayerJoinedLobby(msg.sender, playerUsername[msg.sender], msg.value);

        if (currentLobby.length == MAX_PLAYERS) {
            emit LobbyReachedCapacity(currentLobby);
        }
    }
///////////////////////////////////////////////////////////////////////////////////////////////
    function recordPlayerElimination(address killerAddress, address victimAddress) external {
        require(victimAddress != address(0), "Victim address cannot be zero");
        
        bool isSelfElimination = (killerAddress == address(0) || killerAddress == victimAddress);
        
        // Add victim to participants array if not already registered
        if (!isParticipantRegistered[victimAddress]) {
            gameParticipants.push(victimAddress);
            isParticipantRegistered[victimAddress] = true;
        }
        
        // Update victim's death count
        playerGameStats[victimAddress].totalDeaths++;
        
        // If not self-elimination, update killer's stats
        if (!isSelfElimination) {
            // Add killer to participants array if not already registered
            if (!isParticipantRegistered[killerAddress]) {
                gameParticipants.push(killerAddress);
                isParticipantRegistered[killerAddress] = true;
            }
            
            // Update killer's kill count
            playerGameStats[killerAddress].totalKills++;
        }
        
        emit PlayerEliminationRecorded(killerAddress, victimAddress, isSelfElimination);
    }
/////////////////////////////////////////////////////////////////////////////////////////////
    /**
     * @dev Generate leaderboard and return addresses sorted by kills
     * @return Array of player addresses sorted by kills in descending order
     */
    function generateGameLeaderboard() public returns (address[] memory) {
        require(gameParticipants.length > 0, "No players have participated yet");
        
        // Create a copy of participants array for sorting
        address[] memory sortedPlayerAddresses = new address[](gameParticipants.length);
        for (uint i = 0; i < gameParticipants.length; i++) {
            sortedPlayerAddresses[i] = gameParticipants[i];
        }
        
        // Sort players by kills using bubble sort (descending order)
        for (uint i = 0; i < sortedPlayerAddresses.length - 1; i++) {
            for (uint j = 0; j < sortedPlayerAddresses.length - i - 1; j++) {
                // Compare kills - if equal, compare deaths (fewer deaths = better rank)
                if (playerGameStats[sortedPlayerAddresses[j]].totalKills < playerGameStats[sortedPlayerAddresses[j + 1]].totalKills ||
                    (playerGameStats[sortedPlayerAddresses[j]].totalKills == playerGameStats[sortedPlayerAddresses[j + 1]].totalKills && 
                     playerGameStats[sortedPlayerAddresses[j]].totalDeaths > playerGameStats[sortedPlayerAddresses[j + 1]].totalDeaths)) {
                    
                    // Swap addresses
                    address tempAddress = sortedPlayerAddresses[j];
                    sortedPlayerAddresses[j] = sortedPlayerAddresses[j + 1];
                    sortedPlayerAddresses[j + 1] = tempAddress;
                }
            }
        }

        // Store the leaderboard for viewing
        delete finalLeaderboard;
        for (uint i = 0; i < sortedPlayerAddresses.length; i++) {
            finalLeaderboard.push(sortedPlayerAddresses[i]);
        }
        
        emit LeaderboardGenerated(sortedPlayerAddresses);
        return sortedPlayerAddresses;
    }
//////////////////////////////////////////////////////////////////////////////////////////////////
   
    function distributeRewards(address[] calldata leaderboard) 
        external  
        nonReentrant 
    {
        // Get the system-generated leaderboard
        address[] memory systemGeneratedLeaderboard = generateGameLeaderboard();
        
        // Check if arrays have the same length
        if (leaderboard.length != systemGeneratedLeaderboard.length) {
            revert LeaderboardMismatch();
        }
        
        // Compare each element in both arrays
        for (uint256 i = 0; i < leaderboard.length; i++) {
            if (leaderboard[i] != systemGeneratedLeaderboard[i]) {
                revert LeaderboardMismatch();
            }
        }
        
        // If we reach here, arrays are identical - distribute rewards
        _executeRewardDistribution(leaderboard);
    }

    /// ---------- ADMIN FUNCTIONS ----------
    /// @notice Internal function to distribute rewards after match ends
    function _executeRewardDistribution(address[] memory confirmedLeaderboard) internal {
        require(currentLobby.length >= MIN_PLAYERS, "Not enough players in lobby");
        require(confirmedLeaderboard.length == currentLobby.length, "Leaderboard size doesn't match lobby");
        require(!areRewardsDistributed, "Rewards already distributed");

        // Verify leaderboard contains exactly the same addresses as lobby
        for (uint256 i = 0; i < confirmedLeaderboard.length; i++) {
            require(isInLobby[confirmedLeaderboard[i]], "Leaderboard contains non-lobby player");
        }

        // Check for duplicate addresses in leaderboard
        for (uint256 i = 0; i < confirmedLeaderboard.length; i++) {
            for (uint256 j = i + 1; j < confirmedLeaderboard.length; j++) {
                require(confirmedLeaderboard[i] != confirmedLeaderboard[j], "Duplicate address in leaderboard");
            }
        }

        address gameWinner = confirmedLeaderboard[0];
        address gameRunnerUp = confirmedLeaderboard[1];

        uint256 totalPrizePool = totalStakedAmount;
        require(totalPrizePool > 0, "Prize pool is empty");

        uint256 winnerRewardAmount = (totalPrizePool * WINNER_SHARE_BPS) / BPS_DENOM;
        uint256 runnerUpRewardAmount = (totalPrizePool * SECOND_SHARE_BPS) / BPS_DENOM;

        require(winnerRewardAmount + runnerUpRewardAmount <= totalPrizePool, "Reward calculation overflow");

        // Mark distributed before external calls
        areRewardsDistributed = true;

        // Send payouts
        (bool winnerTransferSuccess,) = payable(gameWinner).call{value: winnerRewardAmount}("");
        require(winnerTransferSuccess, "Transfer to winner failed");

        (bool runnerUpTransferSuccess,) = payable(gameRunnerUp).call{value: runnerUpRewardAmount}("");
        require(runnerUpTransferSuccess, "Transfer to runner-up failed");

        emit GameRewardsDistributed(gameWinner, winnerRewardAmount, gameRunnerUp, runnerUpRewardAmount);

        // Reset lobby for next game
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
        
        (bool withdrawSuccess,) = payable(contractOwner).call{value: contractBalance}("");
        require(withdrawSuccess, "Emergency withdrawal failed");
        
        emit EmergencyFundsWithdrawn(contractOwner, contractBalance);
        _resetLobbyState();
    }

    /**
     * @dev View function to get the current leaderboard
     * @return Array of player addresses sorted by kills in descending order
     */
    function getCurrentLeaderboard() external view returns (address[] memory) {
        return finalLeaderboard;
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
        return (playerGameStats[playerAddress].totalKills, playerGameStats[playerAddress].totalDeaths);
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
        // Reset all player stats
        for (uint i = 0; i < gameParticipants.length; i++) {
            address participantAddress = gameParticipants[i];
            playerGameStats[participantAddress].totalKills = 0;
            playerGameStats[participantAddress].totalDeaths = 0;
            isParticipantRegistered[participantAddress] = false;
        }
        
        // Clear the participants array
        delete gameParticipants;
        
        // Clear the leaderboard
        delete finalLeaderboard;
    }
    
    function _resetLobbyState() internal {
        // Clear mappings for all lobby players
        for (uint256 i = 0; i < currentLobby.length; i++) {
            address lobbyPlayer = currentLobby[i];
            isInLobby[lobbyPlayer] = false;
            hasPlayerStaked[lobbyPlayer] = false;
        }
        delete currentLobby;
        totalStakedAmount = 0;
        areRewardsDistributed = false;
    }
}
