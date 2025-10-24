using UnityEngine;
using TMPro;

public class addressStorage : MonoBehaviour
{
    public static addressStorage Instance;

    [SerializeField] private TextMeshProUGUI addressText;

    private string address;
    private string username;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (addressText != null)
        {
            address = addressText.text;
        }
    }

    public void Storeaddress(string add)
    {
        address = add;
    }

    public void Storeusername(string name)
    {
        username = name;
    }

    public string Getaddress()
    {
        return address;
    }

    public string Getusername()
    {
        return username;
    }
}
