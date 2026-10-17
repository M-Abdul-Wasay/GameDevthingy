using UnityEngine;
using UnityEngine.AI;

// Put this in Assets/Scripts and add it to the BOSS object (Boss 1, the one with the Animator).
public class BossHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 200f;
    public float bossRadius = 1.5f;          // added to the hero's attack range so big bosses are easy to hit
    public string dieTrigger = "Die";        // must match the Animator trigger name

    [Header("Health bar")]
    public bool showHealthBar = true;

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    Animator anim;

    void Awake()
    {
        CurrentHealth = maxHealth;
        anim = GetComponentInChildren<Animator>();
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        Debug.Log("Boss took " + amount + " damage. HP: " + CurrentHealth + "/" + maxHealth);

        if (CurrentHealth <= 0f) Die();
    }

    void Die()
    {
        IsDead = true;

        if (anim != null) anim.SetTrigger(dieTrigger);

        // Stop the boss from moving or casting skills
        var skills = GetComponent<BossSkills>();
        if (skills != null) skills.enabled = false;

        var agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        Debug.Log("Boss defeated!");
    }

    void OnGUI()
    {
        if (!showHealthBar || IsDead) return;

        float w = 400f, h = 22f;
        float x = (Screen.width - w) * 0.5f, y = 20f;
        float fill = CurrentHealth / maxHealth;

        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(x - 2, y - 2, w + 4, h + 4), Texture2D.whiteTexture);
        GUI.color = Color.red;
        GUI.DrawTexture(new Rect(x, y, w * fill, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
