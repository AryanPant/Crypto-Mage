using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Numerics;
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
        public Image m_SpinButton;

        [Header("Web3 Settings")]
        [SerializeField] private ulong chainId = 84532; // Base Sepolia (change as needed)
        private string contractAddress = "0xE90524d7dD89aDaBc146e5f4E0498a18984829fD";

        [HideInInspector] public bool m_IsSpinning = false;
        [HideInInspector] public float m_SpinSpeed = 0;
        [HideInInspector] public float m_Rotation = 0;
        [HideInInspector] public int m_RewardNumber = -1;

        private ThirdwebContract contract;
        private float slowdownFactor;
        private bool isGettingRandomNumber = false;

        private async void Start()
        {
            // Contract ABI for the random function
            string randomAbi = @"[
                {
                    ""inputs"": [],
                    ""name"": ""random"",
                    ""outputs"": [
                        {
                            ""internalType"": ""uint8"",
                            ""name"": """",
                            ""type"": ""uint8""
                        }
                    ],
                    ""stateMutability"": ""nonpayable"",
                    ""type"": ""function""
                }
            ]";

            try
            {
                // Initialize contract using ThirdwebManager
                contract = await ThirdwebManager.Instance.GetContract(contractAddress, chainId, randomAbi);
                Debug.Log("Contract initialized successfully!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize contract: {e.Message}");
            }

            // Initialize wheel
            m_Rotation = 0;
            m_IsSpinning = false;
            m_RewardNumber = -1;

            for (int i = 0; i < m_RewardData.Length; i++)
            {
                m_RewardPictures[i].sprite = m_RewardData[i].m_Icon;
            }
        }

        void Update()
        {
            if (m_IsSpinning)
            {
                m_RewardPanel.gameObject.SetActive(false);

                // Natural slowdown
                m_SpinSpeed -= slowdownFactor * Time.deltaTime;
                m_Rotation += 100 * Time.deltaTime * m_SpinSpeed;
                m_CircleBase.transform.localRotation = UnityEngine.Quaternion.Euler(0, 0, m_Rotation);

                for (int i = 0; i < m_RewardPictures.Length; i++)
                {
                    m_RewardPictures[i].transform.rotation = UnityEngine.Quaternion.identity;
                }

                if (m_SpinSpeed <= 0)
                {
                    m_SpinSpeed = 0;
                    m_IsSpinning = false;
                    
                    // Calculate final reward based on rotation
                    m_RewardNumber = (int)((m_Rotation % 360) / (360 / m_RewardData.Length));
                    
                    StartCoroutine(ShowRewardMenu(1));
                    HandleReward();
                }
            }
            else
            {
                if (m_RewardNumber != -1)
                {
                    m_RewardPictures[m_RewardNumber].transform.localScale =
                        (1 + 0.2f * Mathf.Sin(10 * Time.time)) * UnityEngine.Vector3.one;
                }
            }
        }

        public void HandleReward()
        {
            if (m_RewardNumber >= 0 && m_RewardNumber < m_RewardData.Length)
            {
                RewardData reward = m_RewardData[m_RewardNumber];
                Debug.Log($"Reward won: {reward.m_Type}");
                
                switch (reward.m_Type)
                {
                    case "coin":
                        // Add coin count to your inventory
                        Debug.Log("Player won coins!");
                        break;
                    case "gem":
                        // Add gem count to your inventory
                        Debug.Log("Player won gems!");
                        break;
                    case "nothing":
                        Debug.Log("No reward this time!");
                        break;
                }
            }
        }

        IEnumerator ShowRewardMenu(int seconds)
        {
            RewardData reward = m_RewardData[m_RewardNumber];
            yield return new WaitForSeconds(seconds);

            if (reward.m_Type != "nothing")
            {
                m_RewardPanel.gameObject.SetActive(true);
                m_RewardFinalImage.sprite = reward.m_Icon;
                yield return new WaitForSeconds(2);
            }

            yield return new WaitForSeconds(.1f);
            Reset();
        }

        public async void StartSpin()
        {
            if (!m_IsSpinning && !isGettingRandomNumber)
            {
                isGettingRandomNumber = true;
                m_SpinButton.gameObject.SetActive(false);
                
                try
                {
                    Debug.Log("Getting random number from blockchain...");
                    
                    var wallet = ThirdwebManager.Instance.GetActiveWallet();
                    
                    // Call the random function
                    var randomTx = await contract.Prepare(
                        wallet: wallet,
                        method: "random",
                        weiValue: BigInteger.Zero
                    );
                    var txHash = await ThirdwebTransaction.Send(randomTx);
                    
                    Debug.Log($"Random function called! TX: {txHash}");
                    
                    // Use transaction hash as seed for randomness
                    var hashSeed = txHash.GetHashCode();
                    var randomValue = Math.Abs(hashSeed) % 256; // Simulate uint8 (0-255)
                    
                    // Map random value to spin speed (8-18 range)
                    m_SpinSpeed = 8f + (randomValue / 255f) * 10f;
                    
                    // Set slowdown factor (consistent for fair gameplay)
                    slowdownFactor = 0.5f;
                    
                    Debug.Log($"Blockchain random: {randomValue}, Spin speed: {m_SpinSpeed}");
                    
                    // Start spinning
                    m_IsSpinning = true;
                    isGettingRandomNumber = false;
                    m_RewardNumber = -1;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to get blockchain random: {e.Message}");
                    // Fallback to Unity random
                    m_SpinSpeed = UnityEngine.Random.Range(8f, 18f);
                    slowdownFactor = 0.5f;
                    
                    m_IsSpinning = true;
                    isGettingRandomNumber = false;
                    m_RewardNumber = -1;
                }
            }
        }

        public void Reset()
        {
            m_Rotation = 0;
            m_CircleBase.transform.localRotation = UnityEngine.Quaternion.identity;
            m_IsSpinning = false;
            isGettingRandomNumber = false;
            m_RewardNumber = -1;
            m_SpinButton.gameObject.SetActive(true);
            m_RewardPanel.gameObject.SetActive(false);
        }

        // Helper method to check if we can interact with blockchain
        public bool IsBlockchainReady()
        {
            return contract != null && ThirdwebManager.Instance.GetActiveWallet() != null;
        }
    }
}