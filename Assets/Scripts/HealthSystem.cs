using UnityEngine;
// using UnityEngine.UI; // Commented out to prevent compile errors until UI package is installed

public class HealthSystem : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;
    // public Slider healthSlider; // Assign in inspector

    public float respawnTime = 3f; // seconds to travel back in time
    private float respawnTimer;
    private bool isRespawning;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthUI();
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        UpdateHealthUI();

        if (currentHealth <= 0 && !isRespawning)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log("Player died. Initiating time travel respawn...");
        isRespawning = true;
        respawnTimer = respawnTime;
        
        ShadowHeroController pc = GetComponent<ShadowHeroController>();
        if (pc != null) pc.enabled = false;
    }

    void Update()
    {
        if (isRespawning)
        {
            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f)
            {
                Respawn();
            }
        }
    }

    void Respawn()
    {
        transform.position = Vector3.zero; // placeholder
        currentHealth = maxHealth;
        UpdateHealthUI();
        isRespawning = false;

        ShadowHeroController pc = GetComponent<ShadowHeroController>();
        if (pc != null) pc.enabled = true;
        Debug.Log("Player respawned via time travel!");
    }

    void UpdateHealthUI()
    {
        /*
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
        */
    }
}
