using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private Transform target;   // the head of the player
    [SerializeField] private Vector3 offset = new Vector3(0, 2.2f, 0);

    private Camera mainCam;

    private void Start()
    {
        mainCam = Camera.main;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // follow player’s head
        transform.position = target.position + offset;

        // face the camera
        if (mainCam != null)
            transform.LookAt(transform.position + mainCam.transform.forward);
    }

    public void SetMaxHealth(int maxHealth)
    {
        slider.maxValue = maxHealth;
        slider.value = maxHealth;
    }

    public void SetHealth(int health)
    {
        slider.value = health;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
