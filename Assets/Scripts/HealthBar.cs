using UnityEngine;
// using UnityEngine.UI; // Commented out to prevent compile errors until UI package is installed

public class HealthBar : MonoBehaviour
{
    // public Slider slider; // Commented out

    public void SetMaxHealth(int health)
    {
        // slider.maxValue = health;
        // slider.value = health;
    }

    public void SetHealth(int health)
    {
        // slider.value = health;
    }
}