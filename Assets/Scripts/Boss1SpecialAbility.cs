using UnityEngine;

public class Boss1SpecialAbility : MonoBehaviour
{
    [Header("Skill 1: Flame Burst")]
    public float fireBlastRadius = 6f;
    public float fireDamage = 25f;
    public Color fireColor = Color.red;

    [Header("Skill 2: Shadow Void")]
    public float voidRadius = 8f;
    public float voidDamage = 40f;
    public Color voidColor = Color.black;

    // This is called by BossAI.cs when the 10s timer hits
    public void UseSkill1()
    {
        Debug.Log("BONE WARLORD releases FLAME BURST!");
        PerformAreaAttack(fireBlastRadius, fireDamage, true);
        SpawnVisualEffect(fireColor);
    }

    // This is called by BossAI.cs when the 15s timer hits
    public void UseSkill2()
    {
        Debug.Log("BONE WARLORD opens a SHADOW VOID!");
        PerformAreaAttack(voidRadius, voidDamage, false);
        SpawnVisualEffect(voidColor);
    }

    private void PerformAreaAttack(float radius, float damage, bool isFire)
    {
        // Find the player using the tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        
        if (playerObj != null)
        {
            float distance = Vector3.Distance(transform.position, playerObj.transform.position);
            
            if (distance <= radius)
            {
                ShadowHeroController player = playerObj.GetComponent<ShadowHeroController>();
                if (player != null)
                {
                    // Pass the fire flag so the player takes double damage if in Shadow Form
                    player.TakeDamage(damage, isFire);
                    Debug.Log($"Player hit by skill! Damage: {damage} | Fire: {isFire}");
                }
            }
        }
        else
        {
            Debug.LogWarning("Boss tried to use skill, but no Player was found with the 'Player' tag!");
        }
    }

    private void SpawnVisualEffect(Color effectColor)
    {
        // This is a placeholder for a particle system. 
        // If you have a particle system, you can instantiate it here.
        Debug.Log("Spawning visual effect of color: " + effectColor);
    }
}
