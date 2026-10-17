using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public int health = 20;
    public int damageToPlayer = 10;
    public GameObject lifeDropPrefab; // Prefab that gives life when collected

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (comparisonTag(collision, "PlayerAttack"))
        {
            TakeDamage(10); // damage from player attack
        }
    }

    // Helper to avoid typo issues
    bool comparisonTag(Collider2D col, string tag)
    {
        return col.CompareTag(tag);
    }

    public void TakeDamage(int damage)
    {
        health -= damage;
        if (health <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        // Drop life item
        if (lifeDropPrefab != null)
        {
            Instantiate(lifeDropPrefab, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}