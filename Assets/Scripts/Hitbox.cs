using UnityEngine;

public class Hitbox : MonoBehaviour
{
    [Header("Hitbox Settings")]
    public float damage = 10f;
    public bool isFireDamage = false;
    public string attackerTag = "Player"; // Who owns this hitbox?

    // This function is called when the hitbox touches something
    void OnTriggerEnter(Collider other)
    {
        // 1. Check if we are hitting the Boss
        BossAI boss = other.GetComponent<BossAI>();
        if (boss != null && attackerTag == "Player")
        {
            boss.TakeDamage(damage);
            Debug.Log($"Boss hit by {gameObject.name}! Damage: {damage}");
        }

        // 2. Check if we are hitting the Player
        ShadowHeroController player = other.GetComponent<ShadowHeroController>();
        if (player != null && attackerTag == "Boss")
        {
            player.TakeDamage(damage, isFireDamage);
            Debug.Log($"Player hit by {gameObject.name}! Damage: {damage}");
        }
    }
}
