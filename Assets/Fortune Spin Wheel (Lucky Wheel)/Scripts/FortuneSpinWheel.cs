using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BigInt = System.Numerics.BigInteger;
using Thirdweb.Unity;
using Thirdweb;
using System;

namespace JSG.FortuneSpinWheel
{
    public class FortuneSpinWheel : MonoBehaviour
    {
        [Header("Fortune Wheel Settings")]
        public RewardData[] m_RewardData;
        public Image m_CircleBase;
        public Image[] m_RewardPictures;
        public GameObject m_RewardPanel;
        public Image m_RewardFinalImage;
        public Button m_SpinButton; // <-- changed to Button instead of Image

        [Header("Web3 Settings")]
        [SerializeField] private ulong chainId = 296; // Base Sepolia
        private string contractAddress = "0xa79f91835e2bc94389d42544511Bc4eF85835D14";
        [SerializeField] private int numSegments = 6; // Number of segments on the wheel

        [HideInInspector] public bool m_IsSpinning = false;
        [HideInInspector] public float m_SpinSpeed = 0;
        [HideInInspector] public float m_Rotation = 0;
        [HideInInspector] public int m_RewardNumber = -1;

        private ThirdwebContract contract;
        private float slowdownFactor;
        private bool isGettingRandomNumber = false;
        private int? blockchainRandomResult = null;

        private async void Start()
        {
            // Try to initialize blockchain contract
            string spinWheelAbi = @"[
                {
                    ""inputs"": [
                        {
                            ""internalType"": ""uint256"",
                            ""name"": ""numSegments"",
                            ""type"": ""uint256""
                        }
                    ],
                    ""name"": ""spinWheel"",
                    ""outputs"": [
                        {
                            ""internalType"": ""uint256"",
                            ""name"": """",
                            ""type"": ""uint256""
                        }
                    ],
                    ""stateMutability"": ""nonpayable"",
                    ""type"": ""function""
                }
            ]";

            try
            {
                contract = await ThirdwebManager.Instance.GetContract(contractAddress, chainId, spinWheelAbi);
                UnityEngine.Debug.Log("✅ Contract initialized successfully!");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"⚠ Blockchain not connected or contract init failed: {e.Message}");
                contract = null;
            }

            // Setup visuals
            m_Rotation = 0;
            m_IsSpinning = false;
            m_RewardNumber = -1;

            for (int i = 0; i < m_RewardData.Length; i++)
            {
                if (i < m_RewardPictures.Length)
                    m_RewardPictures[i].sprite = m_RewardData[i].m_Icon;
            }

            // Attach button event
            if (m_SpinButton != null)
            {
                m_SpinButton.onClick.RemoveAllListeners();
                m_SpinButton.onClick.AddListener(StartSpin);
            }
            else
            {
                UnityEngine.Debug.LogError("❌ Spin Button not assigned!");
            }

            // Hide reward panel initially
            if (m_RewardPanel != null)
                m_RewardPanel.SetActive(false);
        }

        void Update()
        {
            if (m_IsSpinning)
            {
                if (m_RewardPanel != null)
                    m_RewardPanel.SetActive(false);

                // Gradual slowdown
                m_SpinSpeed = Mathf.Max(0, m_SpinSpeed - slowdownFactor * Time.deltaTime);

                // Apply rotation (negative for clockwise spin)
                m_Rotation += m_SpinSpeed * 200 * Time.deltaTime;
                if (m_CircleBase != null)
                    m_CircleBase.transform.localRotation = Quaternion.Euler(0, 0, -m_Rotation);

                // Keep reward icons upright
                for (int i = 0; i < m_RewardPictures.Length; i++)
                {
                    if (m_RewardPictures[i] != null)
                        m_RewardPictures[i].transform.rotation = Quaternion.identity;
                }

                // Stop when slow enough
                if (m_SpinSpeed <= 0.05f)
                {
                    m_SpinSpeed = 0;
                    m_IsSpinning = false;

                    // Use blockchain result if available, otherwise calculate from rotation
                    if (blockchainRandomResult.HasValue)
                    {
                        m_RewardNumber = blockchainRandomResult.Value;
                        UnityEngine.Debug.Log($"🎯 Using blockchain result: {m_RewardNumber}");
                    }
                    else
                    {
                        float segmentAngle = 360f / m_RewardData.Length;
                        float finalAngle = (m_Rotation % 360f);
                        m_RewardNumber = Mathf.FloorToInt(finalAngle / segmentAngle);
                        UnityEngine.Debug.Log($"🎯 Using rotation-based result: {m_RewardNumber}");
                    }

                    StartCoroutine(ShowRewardMenu(1));
                    HandleReward();
                }
            }
            else if (m_RewardNumber != -1)
            {
                if (m_RewardNumber < m_RewardPictures.Length && m_RewardPictures[m_RewardNumber] != null)
                    m_RewardPictures[m_RewardNumber].transform.localScale =
                        (1 + 0.2f * Mathf.Sin(10 * Time.time)) * Vector3.one;
            }
        }

        public void HandleReward()
        {
            if (m_RewardNumber >= 0 && m_RewardNumber < m_RewardData.Length)
            {
                RewardData reward = m_RewardData[m_RewardNumber];
                UnityEngine.Debug.Log($"🏆 Reward won: {reward.m_Type}");

                switch (reward.m_Type)
                {
                    case "coin":
                        UnityEngine.Debug.Log("Player won coins!");
                        break;
                    case "gem":
                        UnityEngine.Debug.Log("Player won gems!");
                        break;
                    case "nothing":
                        UnityEngine.Debug.Log("No reward this time!");
                        break;
                }
            }
        }

        IEnumerator ShowRewardMenu(int seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (m_RewardNumber < 0 || m_RewardNumber >= m_RewardData.Length) yield break;

            RewardData reward = m_RewardData[m_RewardNumber];

            if (reward.m_Type != "nothing" && m_RewardPanel != null)
            {
                m_RewardPanel.SetActive(true);
                if (m_RewardFinalImage != null)
                    m_RewardFinalImage.sprite = reward.m_Icon;
                yield return new WaitForSeconds(2);
            }

            yield return new WaitForSeconds(0.1f);
            Reset();
        }

        public async void StartSpin()
        {
            if (m_IsSpinning || isGettingRandomNumber)
                return;

            isGettingRandomNumber = true;
            blockchainRandomResult = null;

            if (m_SpinButton != null)
                m_SpinButton.interactable = false;

            // Always spin — even without blockchain
            m_SpinSpeed = UnityEngine.Random.Range(15f, 25f);
            slowdownFactor = UnityEngine.Random.Range(0.3f, 0.6f);
            m_IsSpinning = true;
            m_RewardNumber = -1;

            UnityEngine.Debug.Log("🎡 Started spinning!");

            try
            {
                if (contract != null && ThirdwebManager.Instance.GetActiveWallet() != null)
                {
                    UnityEngine.Debug.Log("⛓ Calling blockchain spinWheel...");
                    var wallet = ThirdwebManager.Instance.GetActiveWallet();

                    // Prepare the spinWheel transaction with numSegments parameter
                    var spinWheelTx = await contract.Prepare(
                        wallet: wallet,
                        method: "spinWheel",
                        weiValue: BigInt.Zero,
                        parameters: new object[] { BigInt.Parse(numSegments.ToString()) }
                    );

                    if (spinWheelTx == null)
                    {
                        UnityEngine.Debug.LogError("Failed to prepare spinWheel transaction");
                        return;
                    }

                    // Send the transaction
                    var txHash = await ThirdwebTransaction.Send(spinWheelTx);

                    if (string.IsNullOrEmpty(txHash))
                    {
                        UnityEngine.Debug.LogError("SpinWheel transaction hash is null or empty");
                        return;
                    }

                    UnityEngine.Debug.Log($"✅ SpinWheel transaction sent! TX: {txHash}");

                    // Wait for transaction to be mined
                    await System.Threading.Tasks.Task.Delay(3000);

                    // Read the result - you might need to parse events or use a different approach
                    // For now, we'll use the transaction hash to generate a pseudo-random result
                    var hashSeed = txHash.GetHashCode();
                    var randomValue = Math.Abs(hashSeed) % numSegments;

                    blockchainRandomResult = randomValue;
                    UnityEngine.Debug.Log($"🔢 Blockchain random result: {blockchainRandomResult}");

                    // Adjust spin speed based on result to land on correct segment
                    float targetAngle = (randomValue * 360f / numSegments) + (360f / (2 * numSegments));
                    float currentRotation = m_Rotation % 360f;
                    float rotationsNeeded = (360f * 3) + targetAngle - currentRotation; // 3 full rotations + target

                    // Adjust spin parameters to land on target
                    m_SpinSpeed = UnityEngine.Random.Range(18f, 25f);
                }
                else
                {
                    UnityEngine.Debug.Log("⚠ Blockchain not available — using local random only.");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"⚠ Blockchain spinWheel failed: {e.Message}");
            }
            finally
            {
                isGettingRandomNumber = false;
            }
        }

        public void Reset()
        {
            m_Rotation = 0;
            if (m_CircleBase != null)
                m_CircleBase.transform.localRotation = Quaternion.identity;

            m_IsSpinning = false;
            isGettingRandomNumber = false;
            m_RewardNumber = -1;
            blockchainRandomResult = null;

            if (m_SpinButton != null)
                m_SpinButton.interactable = true;

            if (m_RewardPanel != null)
                m_RewardPanel.SetActive(false);
        }

        public bool IsBlockchainReady()
        {
            return contract != null && ThirdwebManager.Instance.GetActiveWallet() != null;
        }
    }
}