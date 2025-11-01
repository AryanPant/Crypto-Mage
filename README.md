# CryptoMage 🎮⚔️

**Track:** Gaming & Entertainment | **Team:** CryptoMage

[![Hedera](https://img.shields.io/badge/Hedera-Testnet-00D4AA?style=flat-square&logo=hedera&logoColor=white)](https://hedera.com)
[![Solidity](https://img.shields.io/badge/Solidity-0.8.x-363636?style=flat-square&logo=solidity&logoColor=white)](https://soliditylang.org)
[![License](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)

## 🎯 Project Overview

CryptoMage is an **immersive, 3D multiplayer play-to-earn fantasy combat game** that solves a fundamental challenge in Web3 gaming: players invest time and money but don't truly own their digital assets or earn real value. 

Unlike typical 2D Web3 hackathon games, **CryptoMage delivers console-level gameplay on the blockchain**, setting a new standard for Web3 gaming innovation. Players stake 1 HBAR to enter competitive battles, with prizes awarded to top performers on the leaderboard—creating a **fair, skill-driven blockchain gaming economy**.

---

### 📸 Screenshots
<img width="1920" height="1080" alt="Main frame" src="https://github.com/user-attachments/assets/5243d1db-aa7f-481a-8219-2c3e255a83de" />

<img width="716" height="560" alt="Logo" src="https://github.com/user-attachments/assets/4b678e54-ecd1-49e6-9d4d-ba687dd5fe84" />

<!-- <img width="746" height="416" alt="image" src="https://github.com/user-attachments/assets/b7b4a24a-a1e1-4091-b216-a9fd0f829ab1" /> -->
<!-- <img width="700" height="392" alt="image" src="https://github.com/user-attachments/assets/ff16abf4-d621-4f16-bf13-f9a916cdf412" /> -->
<img width="1600" height="664" alt="Gameplay" src="https://github.com/user-attachments/assets/fb5b5842-5ba8-4da5-afeb-8827287812ac" />






---


## 🔗 Important Links

- **Live Demo:** [Itch,io Link](https://aryanpant.itch.io/cryptomage)
- **Pitch Deck:** [View Presentation](https://docs.google.com/presentation/d/14Zg2Psdtes7M-6Rf5wHC5pUxSX1hwOoV4UgSAVky6mM/edit?slide=id.g39f35629b3d_0_192#slide=id.g39f35629b3d_0_192)
- **Demo Video:** [Watch Demo](https://drive.google.com/drive/folders/1cfJJoUkknruDW0u9gzQdZko72EzyoEpX?usp=sharing)
- **Smart Contracts:** 0x91ADeF47103B72f9C771f14eDf5f4BDB88da0b2d ,0x61146B3Dd96e03B8fF0F7fcd2A53701d362C9Bd6, 0x0462FA393F0dbEc480bb8997F93102558D09b714, 0xc46D8faE24B69496070777052b20b5cEF30FbF64 
- **Certificate** [Certificate](https://drive.google.com/file/d/169A4W3eGc0blmy4m0IaSsjTx-bAZ9UwS/view?usp=sharing)
- **Contracts Folder** [Click to open folder](https://github.com/AryanPant/Crypto-Mage/tree/main/contracts)

---

## 🌟 Why CryptoMage?

### The Problem
Traditional Web3 games fail to deliver:
- **True Digital Ownership:** Players don't control their in-game assets
- **Real Value Creation:** Time and money invested yield no tangible returns
- **Immersive Experiences:** Most blockchain games are limited to simple 2D mechanics
- **Fair Competition:** Lack of transparent, skill-based reward systems

### Our Solution
CryptoMage leverages **Hedera's high-performance, low-cost blockchain** to enable:
- ✅ **True Asset Ownership:** HTS-powered NFTs that players fully control
- ✅ **Play-to-Earn Economy:** Skill-based rewards with transparent prize distribution
- ✅ **Console-Quality 3D Gameplay:** Built with Unity for immersive experiences
- ✅ **Fair & Transparent:** On-chain randomness and verifiable game mechanics

---

## ⚡ Hedera Integration Summary

### 1. **Hedera Token Service (HTS)** - NFT Minting & Transfers
**Why HTS:** We chose HTS for its **predictable $0.0001 fee guarantees**, which ensures operational cost stability essential for sustainable game economies in emerging markets like Africa. Unlike Ethereum's volatile gas fees, HTS enables **scalable NFT distribution** without unpredictable costs.

**Transaction Types:**
- `TokenCreateTransaction` - Creating the CryptoMage NFT collection
- `TokenMintTransaction` - Minting unique weapon/armor NFTs with metadata
- `TokenAssociateTransaction` - Associating tokens with player accounts
- `TransferTransaction` - Distributing NFT rewards to winners

**Implementation:** 
- Uses the HTS precompile address for direct on-chain NFT operations
- Replaces custom ERC-721 contracts with Hedera's native tokenization
- Handles token association, minting with on-chain metadata, and instant transfers
- Each spin wheel win triggers an NFT mint with unique attributes stored on-chain

**Economic Justification:** HTS's fixed fees enable **micro-transaction gaming economies** where players can earn $0.50-$2 rewards without fees consuming profits—critical for player adoption in cost-sensitive markets.

---

### 2. **Hedera Consensus Service (HCS)** - Game State & Leaderboards
**Why HCS:** We chose HCS for **immutable logging of critical gameplay events** with predictable $0.0001 fee guarantees, ensuring operational cost stability for real-time multiplayer systems in bandwidth-constrained regions.

**Transaction Types:**
- `TopicCreateTransaction` - Creating game event topics
- `TopicMessageSubmitTransaction` - Logging match results, leaderboard updates, stake entries

**Implementation:**
- Records all competitive match outcomes to Topics for transparent verification
- Maintains tamper-proof leaderboard state with mirror node queries
- Enables dispute resolution through immutable audit trails

**Economic Justification:** HCS's low-cost consensus ensures **sustainable real-time gaming** where thousands of state updates don't erode tournament prize pools—essential for competitive multiplayer viability.

---

### 3. **Hedera PRNG (HIP-351)** - On-Chain Randomness
**Why PRNG:** We chose Hedera's native PRNG for **verifiably fair randomness** without expensive VRF oracles, maintaining low operational costs while ensuring players trust the integrity of loot mechanics.

**Transaction Types:**
- `ContractExecuteTransaction` - Calling the 0x169 precompile for random numbers

**Implementation:**
- Spin wheel prize selection using cryptographically secure on-chain randomness
- Treasure chest loot drops with variable coin rewards (0-50 coins)
- All randomness generated transparently on-chain for player verification

**Economic Justification:** Native PRNG eliminates **$0.50-$2 VRF oracle fees per roll**, making frequent loot mechanics economically viable for high-engagement gameplay.

---

### 4. **Hedera Smart Contract Service (HSCS)** - Game Logic
**Why HSCS:** We chose HSCS for its **EVM compatibility** with Hedera's high throughput (10,000 TPS) and sub-3-second finality, enabling **real-time game mechanics** impossible on congested L1s.

**Transaction Types:**
- `ContractCreateTransaction` - Deploying CryptoMage game contracts
- `ContractExecuteTransaction` - Staking HBAR, claiming rewards, NFT distributions

**Implementation:**
- Staking contract holds 1 HBAR entry fees and distributes prizes to top 2 players
- Integrates HTS precompiles for NFT operations within smart contract logic
- Executes PRNG calls for provably fair randomness

**Economic Justification:** HSCS enables **complex game loops** (stake → play → randomize → reward) in single sub-$0.01 transactions, where competitors like Polygon require $0.10-$0.50 for equivalent operations.

---

## 🏗️ Architecture Diagram
```
┌─────────────────────────────────────────────────────────────┐
│                    CRYPTOMAGE ARCHITECTURE                    │
└─────────────────────────────────────────────────────────────┘

┌──────────────────┐         ┌──────────────────┐
│   Unity Frontend │◄────────┤  MetaMask/Blade  │
│   (WebGL Build)  │  Sign   │     Wallet       │
└────────┬─────────┘  Txns   └──────────────────┘
         │
         │ JSON-RPC
         ▼
┌─────────────────────────────────────────────────────┐
│              HEDERA TESTNET LAYER                    │
├──────────────┬───────────────┬────────────┬─────────┤
│   HTS NFTs   │  PRNG  │ HCS Topics │ HSCS    │
│       │               │            │ Staking │
└──────┬───────┴───────┬───────┴──────┬─────┴────┬────┘
       │               │              │          │
       │ Mint/Transfer │ Randomness   │ Events   │ Execute
       ▼               ▼              ▼          ▼
┌─────────────────────────────────────────────────────┐
│         SMART CONTRACTS (Solidity 0.8.x)            │
│  ┌────────────┐  ┌──────────┐  ┌──────────────┐   │
│  │  Staking   │  │   Loot   │  │  Tournament  │   │
│  │  Contract  │  │  System  │  │  Rewards     │   │
│  └────────────┘  └──────────┘  └──────────────┘   │
└─────────────────────────────────────────────────────┘
       │                    │                  │
       └────────────────────┴──────────────────┘
                            │
                            ▼
                 ┌─────────────────────┐
                 │  Hedera Mirror Node │
                 │  (Leaderboard Query)│
                 └─────────────────────┘

DATA FLOW:
1. Player stakes 1 HBAR → HSCS Staking Contract
2. Game match results → HCS Topic for verification
3. Winner triggers spin wheel → PRNG generates random NFT
4. NFT minted via HTS → Transferred to player wallet
5. Leaderboard updated → Mirror Node query for rankings
```

---
## 🔑 Deployed Hedera IDs (Testnet)

### Smart Contracts
- **Chest Hedera Contract:** `0x61146B3Dd96e03B8fF0F7fcd2A53701d362C9Bd6 ` - [View on HashScan](https://hashscan.io/testnet/contract/0x61146B3Dd96e03B8fF0F7fcd2A53701d362C9Bd6 )
- **Staking Contract:** `0x91ADeF47103B72f9C771f14eDf5f4BDB88da0b2d ` - [View on HashScan](https://hashscan.io/testnet/contract/0x91ADeF47103B72f9C771f14eDf5f4BDB88da0b2d )
- **Shop Hedera Contract:** `0x0462FA393F0dbEc480bb8997F93102558D09b714 ` - [View on HashScan](https://hashscan.io/testnet/contract/0x0462FA393F0dbEc480bb8997F93102558D09b714 )
- **Shop Hedera Contract:** `0xa79f91835e2bc94389d42544511Bc4eF85835D14 ` - [View on HashScan](https://hashscan.io/testnet/contract/0xa79f91835e2bc94389d42544511Bc4eF85835D14)
- **Wheel Hedera Contract:** `0xc46D8faE24B69496070777052b20b5cEF30FbF64` - [View on HashScan](https://hashscan.io/testnet/contract/0xc46D8faE24B69496070777052b20b5cEF30FbF64)

## 🚀 Deployment & Setup Instructions

### Prerequisites
- **Node.js** v18+ and npm
- **Unity** 2021.3 LTS or higher
- **MetaMask** or **Blade Wallet** (Hedera Testnet configured)
- **Hedera Testnet Account** ([Get one here](https://portal.hedera.com))

### Environment Configuration

Create a `.env.example` file showing the **structure**

---

### Installation Steps

#### Clone the Repository
```bash
git clone https://github.com/your-username/cryptomage.git
cd cryptomage
```


####  Deploy Smart Contracts
```bash
cd backend/contracts
npx hardhat compile
npx hardhat run scripts/deploy.js --network hedera-testnet
```


####  Launch the Unity Frontend

**Option A: Local Development**
```bash
# Open Unity project
# File → Open Project → Select cryptomage/unity-client

# Configure Hedera settings in Unity
# Edit → Project Settings → CryptoMage Settings
# Enter your contract addresses and network config

# Run in Unity Editor (Play button)
# Or build WebGL: File → Build Settings → WebGL → Build
```

**Option B: Pre-built WebGL Version**
```bash
cd unity-client/Build
npx serve -s .

# Open browser at http://localhost:5000
```

---

### Running Environment

After successful setup, you should have:

- **Unity Game Client:** Running on `http://localhost:5000` (WebGL) or in Unity Editor
- **MetaMask:** Connected to Hedera Testnet
- **Smart Contracts:** Deployed and verified on [HashScan](https://hashscan.io/testnet)


---




  

## 👥 Judge Credentials & Testing

To facilitate testing, test credentials are provided in the **DoraHacks submission notes**.


**How to Access:**
1. Import the test account into MetaMask using the private key
2. Connect to Hedera Testnet (`https://testnet.hashio.io/api`)
3. Navigate to the game at WebGL Build link
4. Stake 1 HBAR to join a match
5. Play the game and test NFT rewards from the spin wheel

---



## 🎮 How It Works (User Flow)

1. **Connect Wallet** → Player connects MetaMask/Blade to Hedera Testnet
2. **Stake 1 HBAR** → Smart contract locks the entry fee
3. **Battle in 3D Arena** → Engage in real-time multiplayer combat
4. **Leaderboard Ranking** → Results logged to HCS, rankings updated via mirror node
5. **Prize Distribution** → Top 2 players automatically receive HBAR rewards
6. **Spin Wheel Rewards** → Winners use PRNG to mint random NFTs (weapons/armor)
7. **Open Treasure Chests** → Use PRNG for variable coin loot (0-50 coins)


## 🤝 Contributing

We welcome contributions! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'feat: add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

**Code Standards:**
- Follow existing code style (ESLint/Prettier)
- Add tests for new features
- Update documentation
- Ensure all tests pass before submitting PR

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- **Hedera Hashgraph** for the developer tools and testnet resources
- **Unity Technologies** for the game engine
- **DoraHacks** for hosting the hackathon
- **Our amazing community testers**

---


## 🎯 Built for DoraHacks x Hedera Hackathon

**Team CryptoMage** - Bridging console-quality gaming with blockchain innovation

*Setting a new standard for Web3 gaming experiences* 🚀⚔️


### 🎥 Demo Video

[https://drive.google.com/drive/folders/1cfJJoUkknruDW0u9gzQdZko72EzyoEpX?usp=sharing]

---


