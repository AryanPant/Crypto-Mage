using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;
using Thirdweb.Unity;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public class WebGLWalletConnector : MonoBehaviour
{
    [Header("Wallet UI")]
    [SerializeField] private Button metamaskButton;
    [SerializeField] private Button walletConnectButton;
    [SerializeField] private TMP_Text statusText;

    [Header("ENS / Guest UI")]
    [SerializeField] private Button getUsernameButton;   // assign in Inspector
    [SerializeField] private Button playGuestButton;     // assign in Inspector

    [Header("Config")]
    [SerializeField] private ulong chainId = 421614;

    [Header("App refs")]
    public addressStorage Address;
    public SimpleLobbyManager lobbyManager;
    string address = null;

    private async void Start()
    {
        metamaskButton.onClick.AddListener(ConnectMetamask);
        walletConnectButton.onClick.AddListener(ConnectWalletConnect);

        getUsernameButton.gameObject.SetActive(false);
        playGuestButton.gameObject.SetActive(false);

        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            metamaskButton.gameObject.SetActive(true);
            walletConnectButton.gameObject.SetActive(true);
        }
        else
        {
            metamaskButton.gameObject.SetActive(false);
            walletConnectButton.gameObject.SetActive(true);
        }
        address = null;
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private async void ConnectMetamask()
    {
        statusText.text = "Connecting to MetaMask extension...";
        try
        {
            var options = new WalletOptions(
                provider: WalletProvider.MetaMaskWallet,
                chainId: chainId
            );

            var wallet = await ThirdwebManager.Instance.ConnectWallet(options);
            address = await wallet.GetAddress();
            statusText.text = $"Connected via MetaMask: {address}";
            Address.Storeaddress(address);

            // Resolve ENS (via public API)
            StartCoroutine(ResolveEnsCoroutine(address));
        }
        catch (System.Exception e)
        {
            statusText.text = $"MetaMask Error: {e.Message}";
        }
    }

    private async void ConnectWalletConnect()
    {
        statusText.text = "Connecting via WalletConnect...";
        try
        {
            var options = new WalletOptions(
                provider: WalletProvider.WalletConnectWallet,
                chainId: chainId
            );

            var wallet = await ThirdwebManager.Instance.ConnectWallet(options);
            address = await wallet.GetAddress();
            statusText.text = $"Connected via WalletConnect: {address}";
            Address.Storeaddress(address);

            // Resolve ENS (via public API)
            StartCoroutine(ResolveEnsCoroutine(address));
        }
        catch (System.Exception e)
        {
            statusText.text = $"WalletConnect Error: {e.Message}";
        }
    }

    private IEnumerator ResolveEnsCoroutine(string address)
    {
        // Use ENSIdeas public API to reverse-lookup an address to its primary ENS name.
        // Example: https://api.ensideas.com/ens/resolve/0xabc...
        string url = $"https://api.ensideas.com/ens/resolve/{address.ToLower()}";

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            www.timeout = 10;
            yield return www.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool requestOk = www.result == UnityWebRequest.Result.Success;
#else
            bool requestOk = !www.isNetworkError && !www.isHttpError;
#endif

            if (requestOk)
            {
                // ENS API returns JSON like: { "address":"0x...", "name":"frolic.eth", "displayName":"frolic.eth", "avatar":"..." }
                var json = www.downloadHandler.text;
                EnsResolveResponse resp = null;
                try
                {
                    resp = JsonUtility.FromJson<EnsResolveResponse>(json);
                }
                catch { resp = null; }

                if (resp != null && !string.IsNullOrEmpty(resp.name))
                {
                    // Case 1: ENS exists -> use it
                    lobbyManager.setusername(resp.name);
                    statusText.text = $"Welcome, {resp.name}";
                    Address.Storeusername(resp.name);
                    Address.Storeaddress(resp.address);
                    getUsernameButton.gameObject.SetActive(false);
                    playGuestButton.gameObject.SetActive(false);
                    yield break;
                }
            }

            // If we reach here -> no ENS or API error -> show Get Username or Play as Guest
            ShowNoEnsOptions();
        }
    }

    private void ShowNoEnsOptions()
    {
        statusText.text = "No ENS name found. Choose an option:";
        getUsernameButton.gameObject.SetActive(true);
        playGuestButton.gameObject.SetActive(true);

        getUsernameButton.onClick.RemoveAllListeners();
        playGuestButton.onClick.RemoveAllListeners();

        getUsernameButton.onClick.AddListener(() =>
        {
            statusText.text = "Redirecting to ENS registration...";
            // Redirect user to official ENS app to register their name (they'll need ETH to register)
            Application.OpenURL("https://app.ens.domains/");
            getUsernameButton.gameObject.SetActive(false);
            playGuestButton.gameObject.SetActive(false);
            StartCoroutine(ResolveEnsCoroutine(address));


        });

        playGuestButton.onClick.AddListener(() =>
        {
            string guestName = AuthenticationService.Instance.PlayerName;
            lobbyManager.setusername(guestName);
            Address.Storeusername(guestName);
            statusText.text = $"Welcome, {guestName}";
            getUsernameButton.gameObject.SetActive(false);
            playGuestButton.gameObject.SetActive(false);
            SceneManager.LoadScene("StartScene");
            Debug.Log("Next scene");
        });
    }

    [System.Serializable]
    private class EnsResolveResponse
    {
        public string address;
        public string name;
        public string displayName;
        public string avatar;
    }
}