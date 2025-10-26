import express from 'express';
import { createServer } from 'http';
import Web3 from 'web3';
import dotenv from 'dotenv';
import cors from 'cors';

dotenv.config();

const app = express();
const server = createServer(app);

// Middleware
app.use(cors());
app.use(express.json());

// Web3 setup - Base Sepolia
const web3 = new Web3(process.env.RPC_URL);

// Contract addresses
const CHEST_ADDRESS = process.env.CHEST_CONTRACT_ADDRESS;
const LOBBY_ADDRESS = process.env.LOBBY_CONTRACT_ADDRESS;
const RANDOM_CONTRACT_ADDRESS = process.env.RANDOM_CONTRACT_ADDRESS;

// Chest Contract ABI
const CHEST_ABI = [
  {
    "inputs": [{"internalType": "uint64", "name": "sequenceNumber", "type": "uint64"}],
    "name": "activateFallback",
    "outputs": [],
    "stateMutability": "nonpayable",
    "type": "function"
  },
{
  "inputs": [],
  "name": "requestChestOpening",
  "outputs": [{"internalType": "uint64", "name": "sequenceNumber", "type": "uint64"}],
  "stateMutability": "payable",
  "type": "function"
},
{
  "inputs": [],
  "name": "openChestWithFallback",
  "outputs": [{"internalType": "uint64", "name": "sequenceNumber", "type": "uint64"}],
  "stateMutability": "payable",
  "type": "function"
},
{
  "inputs": [{"internalType": "address", "name": "user", "type": "address"}],
  "name": "getUserCoins",
  "outputs": [{"internalType": "uint256", "name": "", "type": "uint256"}],
  "stateMutability": "view",
  "type": "function"
},
{
  "inputs": [{"internalType": "uint64", "name": "sequenceNumber", "type": "uint64"}],
  "name": "getRequestStatus",
  "outputs": [
    {"internalType": "address", "name": "requester", "type": "address"},
    {"internalType": "bool", "name": "fulfilled", "type": "bool"},
    {"internalType": "uint256", "name": "coinsReceived", "type": "uint256"},
    {"internalType": "uint256", "name": "timestamp", "type": "uint256"},
    {"internalType": "string", "name": "status", "type": "string"},
    {"internalType": "string", "name": "randomnessSource", "type": "string"},
    {"internalType": "bool", "name": "canFallback", "type": "bool"}
  ],
  "stateMutability": "view",
  "type": "function"
},
{
  "inputs": [{"internalType": "address", "name": "user", "type": "address"}],
  "name": "getUserChestRequests",
  "outputs": [
    {"internalType": "uint64[]", "name": "sequenceNumbers", "type": "uint64[]"},
    {"internalType": "bool[]", "name": "fulfilled", "type": "bool[]"},
    {"internalType": "uint256[]", "name": "coinsWon", "type": "uint256[]"},
    {"internalType": "uint256[]", "name": "timestamps", "type": "uint256[]"},
    {"internalType": "string[]", "name": "status", "type": "string[]"},
    {"internalType": "string[]", "name": "randomnessSource", "type": "string[]"}
  ],
  "stateMutability": "view",
  "type": "function"
},
{
  "inputs": [],
  "name": "getEntropyFee",
  "outputs": [{"internalType": "uint256", "name": "", "type": "uint256"}],
  "stateMutability": "view",
  "type": "function"
},
{
  "inputs": [{"internalType": "uint64", "name": "sequenceNumber", "type": "uint64"}],
  "name": "canActivateFallback",
  "outputs": [{"internalType": "bool", "name": "", "type": "bool"}],
  "stateMutability": "view",
  "type": "function"
}
];

// Lobby Contract ABI
const LOBBY_ABI = [
{
  "inputs": [{"internalType": "address", "name": "killerAddress", "type": "address"}, {"internalType": "address", "name": "victimAddress", "type": "address"}],
  "name": "recordPlayerElimination",
  "outputs": [],
  "stateMutability": "nonpayable",
  "type": "function"
},
{
  "inputs": [],
  "name": "generateGameLeaderboard",
  "outputs": [{"internalType": "address[]", "name": "", "type": "address[]"}],
  "stateMutability": "nonpayable",
  "type": "function"
},
{
  "inputs": [{"internalType": "address[]", "name": "leaderboard", "type": "address[]"}],
  "name": "distributeRewards",
  "outputs": [],
  "stateMutability": "nonpayable",
  "type": "function"
},
{
  "inputs": [],
  "name": "getLobbyPlayers",
  "outputs": [{"internalType": "address[]", "name": "", "type": "address[]"}],
  "stateMutability": "view",
  "type": "function"
}
];

// Random Contract ABI
const RANDOM_ABI = [
  {
    "inputs": [],
    "name": "random",
    "outputs": [{"internalType": "uint8", "name": "", "type": "uint8"}],
    "stateMutability": "nonpayable",
    "type": "function"
  },
{
  "anonymous": false,
  "inputs": [
    {"indexed": true, "internalType": "address", "name": "caller", "type": "address"},
    {"indexed": false, "internalType": "uint8", "name": "number", "type": "uint8"}
  ],
  "name": "RandomGenerated",
  "type": "event"
}
];

// Create contract instances
const chestContract = new web3.eth.Contract(CHEST_ABI, CHEST_ADDRESS);
const lobbyContract = new web3.eth.Contract(LOBBY_ABI, LOBBY_ADDRESS);
const randomContract = new web3.eth.Contract(RANDOM_ABI, RANDOM_CONTRACT_ADDRESS);

// Helper function to convert BigInt values to strings
const convertBigIntToString = (obj) => {
  if (typeof obj === 'bigint') {
    return obj.toString();
  } else if (Array.isArray(obj)) {
    return obj.map(convertBigIntToString);
  } else if (obj && typeof obj === 'object') {
    const result = {};
    for (const [key, value] of Object.entries(obj)) {
      result[key] = convertBigIntToString(value);
    }
    return result;
  }
  return obj;
};

// ===== REST API ENDPOINTS =====

// Health check
app.get('/api/health', (req, res) => {
  res.json({
    success: true,
    message: 'Server is running',
    timestamp: new Date().toISOString(),
           contracts: {
             chest: CHEST_ADDRESS,
             lobby: LOBBY_ADDRESS,
             random: RANDOM_CONTRACT_ADDRESS
           }
  });
});

// RANDOM NUMBER GENERATION
app.post('/api/generate-random-number', async (req, res) => {
  try {
    console.log("🎲 Generating random number...");

    const adminPrivateKey = process.env.PRIVATE_KEY;
    if (!adminPrivateKey) throw new Error("PRIVATE_KEY not set in environment");

    const account = web3.eth.accounts.privateKeyToAccount(adminPrivateKey);
    web3.eth.accounts.wallet.add(account);

    const tx = randomContract.methods.random();
    const gas = await tx.estimateGas({ from: account.address });
    const txData = tx.encodeABI();

    const txObject = {
      from: account.address,
      to: RANDOM_CONTRACT_ADDRESS,
      data: txData,
      gas
    };

    const signedTx = await web3.eth.accounts.signTransaction(txObject, adminPrivateKey);
    const receipt = await web3.eth.sendSignedTransaction(signedTx.rawTransaction);

    console.log("✅ Random number generated:", receipt.transactionHash);

    const eventSignature = web3.eth.abi.encodeEventSignature({
      name: 'RandomGenerated',
      type: 'event',
      inputs: [
        { type: 'address', name: 'caller', indexed: true },
        { type: 'uint8', name: 'number', indexed: false }
      ]
    });

    let randomNumber = null;
    if (receipt.logs) {
      for (const log of receipt.logs) {
        if (log.topics[0] === eventSignature) {
          const decodedLog = web3.eth.abi.decodeLog(
            [
              { type: 'address', name: 'caller', indexed: true },
              { type: 'uint8', name: 'number', indexed: false }
            ],
            log.data,
            log.topics.slice(1)
          );
          randomNumber = parseInt(decodedLog.number);
          break;
        }
      }
    }

    res.json({
      success: true,
      txHash: receipt.transactionHash,
      randomNumber: randomNumber,
      caller: account.address,
      timestamp: new Date().toISOString()
    });

  } catch (error) {
    console.error("❌ Error generating random number:", error);
    res.status(500).json({
      success: false,
      error: error.message
    });
  }
});

// CHEST CONTRACT ENDPOINTS

// Get user coins
app.get('/api/get-user-coins/:userAddress', async (req, res) => {
  try {
    const { userAddress } = req.params;
    const coins = await chestContract.methods.getUserCoins(userAddress).call();

    res.json({
      success: true,
      coins: coins.toString(),
             userAddress: userAddress
    });

  } catch (error) {
    console.error('Get user coins error:', error);
    res.status(500).json({
      success: false,
      error: error.message
    });
  }
});

// Get request status
app.get('/api/get-request-status/:sequenceNumber', async (req, res) => {
  try {
    const { sequenceNumber } = req.params;
    const result = await chestContract.methods.getRequestStatus(sequenceNumber).call();

    res.json({
      success: true,
      sequenceNumber: sequenceNumber,
      requester: result[0],
      fulfilled: result[1],
      coinsReceived: result[2].toString(),
             timestamp: result[3].toString(),
             status: result[4],
             randomnessSource: result[5],
             canFallback: result[6]
    });

  } catch (error) {
    console.error('Get request status error:', error);
    res.status(500).json({
      success: false,
      error: error.message
    });
  }
});

// Get user chest requests
app.get('/api/get-user-chest-requests/:userAddress', async (req, res) => {
  try {
    const { userAddress } = req.params;
    const result = await chestContract.methods.getUserChestRequests(userAddress).call();

    res.json({
      success: true,
      userAddress: userAddress,
      sequenceNumbers: convertBigIntToString(result[0]),
             fulfilled: result[1],
             coinsWon: convertBigIntToString(result[2]),
             timestamps: convertBigIntToString(result[3]),
             status: result[4],
             randomnessSource: result[5]
    });

  } catch (error) {
    console.error('Get user chest requests error:', error);
    res.status(500).json({
      success: false,
      error: error.message
    });
  }
});

// LOBBY CONTRACT ENDPOINTS

// Get lobby players
app.get('/api/get-current-lobby-players', async (req, res) => {
  try {
    const players = await lobbyContract.methods.getLobbyPlayers().call();
    console.log('Lobby players:', players);

    res.json({
      success: true,
      players: players,
      playerCount: players.length
    });

  } catch (error) {
    console.error('Get lobby players error:', error);
    res.status(500).json({
      success: false,
      error: error.message
    });
  }
});

// Send kill data
app.post('/api/send-kill-data', async (req, res) => {
  try {
    const { killerAddress, victimAddress } = req.body;

    if (!killerAddress || !victimAddress) {
      return res.status(400).json({
        success: false,
        error: "killerAddress and victimAddress are required"
      });
    }

    console.log(`Recording elimination: killer=${killerAddress}, victim=${victimAddress}`);

    const adminPrivateKey = process.env.PRIVATE_KEY;
    if (!adminPrivateKey) throw new Error("PRIVATE_KEY not set in environment");

    const account = web3.eth.accounts.privateKeyToAccount(adminPrivateKey);
    web3.eth.accounts.wallet.add(account);

    const tx = lobbyContract.methods.recordPlayerElimination(killerAddress, victimAddress);
    const gas = await tx.estimateGas({ from: account.address });
    const txData = tx.encodeABI();

    const txObject = {
      from: account.address,
      to: LOBBY_ADDRESS,
      data: txData,
      gas
    };

    const signedTx = await web3.eth.accounts.signTransaction(txObject, adminPrivateKey);
    const receipt = await web3.eth.sendSignedTransaction(signedTx.rawTransaction);

    console.log("✅ Kill recorded:", receipt.transactionHash);

    res.json({
      success: true,
      txHash: receipt.transactionHash,
      killerAddress,
      victimAddress
    });

  } catch (error) {
    console.error("❌ Error recording kill:", error);
    res.status(500).json({
      success: false,
      error: error.message
    });
  }
});

// Generate game leaderboard
app.post('/api/generate-game-leaderboard', async (req, res) => {
  try {
    console.log("⚡ Generating game leaderboard...");

    const adminPrivateKey = process.env.PRIVATE_KEY;
    if (!adminPrivateKey) throw new Error("PRIVATE_KEY not set in environment");

    const account = web3.eth.accounts.privateKeyToAccount(adminPrivateKey);
    web3.eth.accounts.wallet.add(account);

    const tx = lobbyContract.methods.generateGameLeaderboard();
    const gas = await tx.estimateGas({ from: account.address });
    const txData = tx.encodeABI();

    const txObject = {
      from: account.address,
      to: LOBBY_ADDRESS,
      data: txData,
      gas
    };

    const signedTx = await web3.eth.accounts.signTransaction(txObject, adminPrivateKey);
    const receipt = await web3.eth.sendSignedTransaction(signedTx.rawTransaction);

    console.log("✅ Leaderboard generated:", receipt.transactionHash);

    // Fetch leaderboard after tx mined
    const leaderboard = await lobbyContract.methods.generateGameLeaderboard().call();

    res.json({
      success: true,
      txHash: receipt.transactionHash,
      leaderboard
    });

  } catch (error) {
    console.error("❌ Error generating leaderboard:", error);
    res.status(500).json({
      success: false,
      error: error.message
    });
  }
});

// Distribute rewards
app.post('/api/distribute-rewards', async (req, res) => {
  try {
    const { leaderboard } = req.body;

    if (!leaderboard || !Array.isArray(leaderboard) || leaderboard.length === 0) {
      return res.status(400).json({
        success: false,
        error: "Leaderboard array is required to distribute rewards"
      });
    }

    console.log("⚡ Distributing rewards for leaderboard:", leaderboard);

    const adminPrivateKey = process.env.PRIVATE_KEY;
    if (!adminPrivateKey) throw new Error("PRIVATE_KEY not set in environment");

    const account = web3.eth.accounts.privateKeyToAccount(adminPrivateKey);
    web3.eth.accounts.wallet.add(account);

    const tx = lobbyContract.methods.distributeRewards(leaderboard);
    const gas = await tx.estimateGas({ from: account.address });
    const txData = tx.encodeABI();

    const txObject = {
      from: account.address,
      to: LOBBY_ADDRESS,
      data: txData,
      gas
    };

    const signedTx = await web3.eth.accounts.signTransaction(txObject, adminPrivateKey);
    const receipt = await web3.eth.sendSignedTransaction(signedTx.rawTransaction);

    console.log("✅ Rewards distributed:", receipt.transactionHash);

    res.json({
      success: true,
      txHash: receipt.transactionHash,
      leaderboard
    });

  } catch (error) {
    console.error("❌ Error distributing rewards:", error);
    res.status(500).json({
      success: false,
      error: error.message
    });
  }
});

// Error handling middleware
app.use((err, req, res, next) => {
  console.error('Unhandled error:', err);
  res.status(500).json({
    success: false,
    error: err.message || 'Internal server error'
  });
});

const PORT = process.env.PORT || 3000;
server.listen(PORT, () => {
  console.log(`🚀 Smart Contract REST API Server running on port ${PORT}`);
  console.log(`📝 Chest Contract: ${CHEST_ADDRESS}`);
  console.log(`🎮 Lobby Contract: ${LOBBY_ADDRESS}`);
  console.log(`🎲 Random Contract: ${RANDOM_CONTRACT_ADDRESS}`);
  console.log(`\n📡 API Endpoints:`);
  console.log(`   GET  /api/health`);
  console.log(`   POST /api/generate-random-number`);
  console.log(`   GET  /api/get-user-coins/:userAddress`);
  console.log(`   GET  /api/get-request-status/:sequenceNumber`);
  console.log(`   GET  /api/get-user-chest-requests/:userAddress`);
  console.log(`   GET  /api/get-current-lobby-players`);
  console.log(`   POST /api/send-kill-data`);
  console.log(`   POST /api/generate-game-leaderboard`);
  console.log(`   POST /api/distribute-rewards`);
});
