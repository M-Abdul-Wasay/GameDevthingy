using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// WAKE-UP GATE for the boss.
/// Your boss already has BossChase (movement), BossSkills (skills) and EnemyHealth (health).
/// This script no longer moves or attacks. It only keeps BossChase + BossSkills ASLEEP
/// until the player comes close (or hits him), then wakes them up.
/// Old public fields are kept so other scripts that mention BossAI still compile.
/// </summary>
public class BossAI : MonoBehaviour
{
    public enum BossState { Idle, Chasing, Attacking, UsingSkill, Dead }

    [Header("Boss Info (health below just mirrors EnemyHealth)")]
    public string bossName = "Bone Warlord";
    public float health = 500f;
    public float maxHealth = 500f;

    [Header("Waiting (wake-up)")]
    [Tooltip("He stands still until the player gets this close")]
    public float aggroRange = 8f;
    [Tooltip("Tick this to wake him only when you are in FRONT of him")]
    public bool requireInFront = false;
    [Range(10f, 360f)] public float viewAngle = 140f;

    [Header("Scripts kept asleep until he wakes up")]
    public Behaviour[] sleepUntilAwake;
    [Tooltip("Automatically puts BossChase and BossSkills to sleep")]
    public bool autoSleepBossChase = true;

    [Header("Current State")]
    public BossState currentState = BossState.Idle;

    private Transform player;
    private EnemyHealth eh;
    private readonly List<Behaviour> sleepers = new List<Behaviour>();
    private bool awake;
    private float nextFind;

    void Awake()
    {
        eh = GetComponent<EnemyHealth>();
        if (eh == null) eh = GetComponentInChildren<EnemyHealth>();

        if (sleepUntilAwake != null)
            foreach (var b in sleepUntilAwake)
                if (b != null && b != this && !sleepers.Contains(b)) sleepers.Add(b);

        if (autoSleepBossChase)
        {
            foreach (var b in GetComponents<Behaviour>())
            {
                if (b == null || b == this) continue;
                string n = b.GetType().Name;
                if ((n == "BossChase" || n == "BossSkills") && !sleepers.Contains(b)) sleepers.Add(b);
            }
        }

        foreach (var b in sleepers) b.enabled = false;
    }

    void Start()
    {
        currentState = BossState.Idle;
        string names = "";
        foreach (var b in sleepers) names += b.GetType().Name + " ";
        Debug.Log("BossAI: waiting for the player (Aggro Range " + aggroRange + "). Asleep: " + names, this);
        FindPlayer();
    }

    void FindPlayer()
    {
        var hero = FindObjectOfType<ShadowHeroController>();
        if (hero != null) { player = hero.transform; return; }

        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) player = go.transform;
    }

    void Update()
    {
        if (eh != null)
        {
            health = eh.CurrentHealth;
            maxHealth = eh.maxHealth;
            if (eh.IsDead) { currentState = BossState.Dead; return; }
        }

        if (awake) return;

        // hitting him from far away wakes him up too
        if (eh != null && eh.CurrentHealth < eh.maxHealth - 0.01f)
        {
            WakeUp("he took damage");
            return;
        }

        if (player == null)
        {
            if (Time.time >= nextFind) { nextFind = Time.time + 0.5f; FindPlayer(); }
            return;
        }

        Vector3 to = player.position - transform.position;
        to.y = 0f;
        float d = to.magnitude;

        if (d > aggroRange) return;
        if (requireInFront && d > 0.1f && Vector3.Angle(transform.forward, to) > viewAngle * 0.5f) return;

        WakeUp("player came within " + d.ToString("F1") + " units");
    }

    void WakeUp(string reason)
    {
        if (awake) return;
        awake = true;
        Debug.Log("BossAI: WOKE UP because " + reason, this);
        foreach (var b in sleepers) if (b != null) b.enabled = true;
        currentState = BossState.Chasing;
    }

    // kept so older scripts that call it still work: it just wakes him up
    public void TakeDamage(float amount)
    {
        WakeUp("he took damage");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, aggroRange);
    }
}
