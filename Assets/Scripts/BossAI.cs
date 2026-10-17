using UnityEngine;
using System.Collections;

public class BossAI : MonoBehaviour
{
    public enum BossState { Idle, Chasing, Attacking, UsingSkill, Dead }
    
    [Header("Boss Info")]
    public string bossName = "Bone Warlord";
    public float health = 500f;
    public float maxHealth = 500f;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float attackRange = 2.5f;
    private Transform playerTransform;

    [Header("Skill Timers")]
    public float skill1Interval = 10f;
    public float skill2Interval = 15f;
    private float skill1Timer;
    private float skill2Timer;

    [Header("Current State")]
    public BossState currentState = BossState.Idle;

    private BossAnimationController animCtrl;
    private Boss1SpecialAbility specialAbilities;

    void Awake()
    {
        animCtrl = GetComponent<BossAnimationController>();
        specialAbilities = GetComponent<Boss1SpecialAbility>();
    }

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
        
        currentState = BossState.Chasing;
    }

    void Update()
    {
        if (currentState == BossState.Dead) return;

        UpdateTimers();
        HandleStateLogic();
    }

    void UpdateTimers()
    {
        skill1Timer += Time.deltaTime;
        skill2Timer += Time.deltaTime;

        // Check if it's time for a skill
        if (skill1Timer >= skill1Interval) {
            StartCoroutine(ExecuteSkill(1));
        } else if (skill2Timer >= skill2Interval) {
            StartCoroutine(ExecuteSkill(2));
        }
    }

    void HandleStateLogic()
    {
        if (playerTransform == null) return;

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        switch (currentState)
        {
            case BossState.Chasing:
                if (distance > attackRange) {
                    MoveTowardsPlayer();
                } else {
                    currentState = BossState.Attacking;
                }
                break;

            case BossState.Attacking:
                PerformBasicAttack();
                if (distance > attackRange + 1f) {
                    currentState = BossState.Chasing;
                }
                break;

            case BossState.UsingSkill:
                // Logic is handled by the Coroutine
                break;
        }
    }

    void MoveTowardsPlayer()
    {
        Vector3 targetPos = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);
        transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
        
        Vector3 dir = (targetPos - transform.position).normalized;
        if (dir != Vector3.zero) {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
        }

        if (animCtrl != null) animCtrl.SetWalking(true);
    }

    void PerformBasicAttack()
    {
        if (animCtrl != null) animCtrl.TriggerAttack();
        
        // Small delay to match animation
        Invoke("ApplyAttackDamage", 0.5f);
        
        // Go back to chasing after attack
        Invoke("ReturnToChase", 1.5f);
    }

    void ApplyAttackDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange);
        foreach (var hit in hits) {
            ShadowHeroController player = hit.GetComponent<ShadowHeroController>();
            if (player != null) player.TakeDamage(10f, false);
        }
    }

    void ReturnToChase() {
        if (currentState == BossState.Attacking) currentState = BossState.Chasing;
    }

    IEnumerator ExecuteSkill(int skillNum)
    {
        currentState = BossState.UsingSkill;
        if (animCtrl != null) animCtrl.TriggerAttack(); // Use attack anim for skills

        if (skillNum == 1) {
            specialAbilities?.UseSkill1();
            skill1Timer = 0;
        } else {
            specialAbilities?.UseSkill2();
            skill2Timer = 0;
        }

        yield return new WaitForSeconds(2f); // Skill duration
        currentState = BossState.Chasing;
    }

    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0) Die();
    }

    void Die()
    {
        currentState = BossState.Dead;
        if (animCtrl != null) animCtrl.TriggerDeath();
        GameManager.Instance?.DefeatBoss();
        Debug.Log(bossName + " has been slain!");
    }
}
