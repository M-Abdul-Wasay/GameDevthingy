using UnityEngine;

public abstract class BossBase : MonoBehaviour
{
    public int maxHealth;
    public int currentHealth;
    public HealthBar healthBar; // UI component for boss health

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        if (healthBar != null)
        {
            healthBar.SetMaxHealth(maxHealth);
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        if (healthBar != null)
        {
            healthBar.SetHealth(currentHealth);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        Debug.Log($"{name} has been defeated!");
        // Drop clue, play death animation, etc.
        OnBossDefeated();
        Destroy(gameObject);
    }

    protected virtual void OnBossDefeated()
    {
        // To be overridden: give clue, spawn next stage, etc.
    }
}