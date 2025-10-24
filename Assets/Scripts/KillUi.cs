using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class KillUI : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI killText;
    [SerializeField] private TextMeshProUGUI deathText;
    [SerializeField] private Slider healthSlider;   // new field for health bar

    private ScoreSystem scoreSystem;
    private Health health;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return; // Each player only updates their own HUD  

        // --- FIND UI ELEMENTS IN SCENE ---
        if (killText == null)
        {
            GameObject textObj = GameObject.Find("KillCounterText");
            if (textObj != null)
                killText = textObj.GetComponent<TextMeshProUGUI>();
            else
                Debug.LogError("KillCounterText not found!");
        }

        if (deathText == null)
        {
            GameObject textObj = GameObject.Find("DeathCounterText");
            if (textObj != null)
                deathText = textObj.GetComponent<TextMeshProUGUI>();
            else
                Debug.LogError("DeathCounterText not found!");
        }

        if (healthSlider == null)
        {
            GameObject sliderObj = GameObject.Find("HealthSlider");
            if (sliderObj != null)
                healthSlider = sliderObj.GetComponent<Slider>();
            else
                Debug.LogError("HealthSlider not found!");
        }

        // --- GET SYSTEM REFERENCES ---
        if (!TryGetComponent(out scoreSystem))
        {
            scoreSystem = Object.FindFirstObjectByType<ScoreSystem>();
            if (scoreSystem == null)
            {
                Debug.LogError("ScoreSystem not found!");
                return;
            }
        }

        if (!TryGetComponent(out health))
        {
            health = GetComponent<Health>();
            if (health == null)
            {
                Debug.LogError("Health component not found!");
                return;
            }
        }

        // --- SUBSCRIBE TO EVENTS ---
        scoreSystem.Kills.OnValueChanged += OnKillsChanged;
        scoreSystem.Deaths.OnValueChanged += OnDeathsChanged;

        health.CurrentHealth.OnValueChanged += OnHealthChanged;  // network var event

        // --- INITIALIZE UI ---
        UpdateKillUI(scoreSystem.Kills.Value);
        UpdateDeathUI(scoreSystem.Deaths.Value);
        UpdateHealthUI(health.CurrentHealth.Value);
    }

    private void OnKillsChanged(int oldValue, int newValue) => UpdateKillUI(newValue);
    private void OnDeathsChanged(int oldValue, int newValue) => UpdateDeathUI(newValue);

    private void OnHealthChanged(int oldValue, int newValue) => UpdateHealthUI(newValue);

    private void UpdateKillUI(int value)
    {
        if (killText != null)
            killText.text = $"Kills: {value}";
    }

    private void UpdateDeathUI(int value)
    {
        if (deathText != null)
            deathText.text = $"Deaths: {value}";
    }

    private void UpdateHealthUI(int value)
    {
        if (healthSlider != null)
            healthSlider.value = value;
    }

    public override void OnDestroy()
    {
        if (scoreSystem != null)
        {
            scoreSystem.Kills.OnValueChanged -= OnKillsChanged;
            scoreSystem.Deaths.OnValueChanged -= OnDeathsChanged;
        }

        if (health != null)
        {
            health.CurrentHealth.OnValueChanged -= OnHealthChanged;
        }
    }
}
