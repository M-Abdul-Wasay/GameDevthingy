using UnityEngine;

public class LifeDrop : MonoBehaviour
{
    public int healAmount = 20;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            var health = collision.GetComponent<HealthSystem>();
            if (health != null)
            {
                health.TakeDamage(-healAmount); // negative damage heals
                // Alternatively add a Heal method
            }
            Destroy(gameObject);
        }
    }
}