using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Put this in Assets/Scripts and add it to the HERO object (the one with the Animator or its parent).
// Left mouse button (or F) = attack. Damage lands at the strike moments of the Attack animation state.
// It also makes anything named "skeleton..." killable automatically.
public class HeroAttack : MonoBehaviour
{
    [Header("Input")]
    public int mouseButton = 0;
    public KeyCode alternateKey = KeyCode.F;

    [Header("Attack")]
    public string attackTrigger = "Attack";
    public string attackStateName = "Attack";
    public float damage = 15f;                    // damage PER hit
    public float attackRange = 2.5f;
    [Range(10f, 360f)] public float attackAngle = 140f;
    public float cooldown = 0.9f;

    [Tooltip("How far through the Attack animation (0-1) each hit lands. Two entries = double combo.")]
    public float[] hitPoints = { 0.2f, 0.7f };
    public float fallbackHitDelay = 0.4f;         // used if the Attack state can't be found

    [Header("Skeletons (minions)")]
    public string minionNamePrefix = "skeleton";
    public float minionHealth = 40f;
    public float minionRadius = 0.8f;
    public float minionRemoveDelay = 3f;

    Animator anim;
    float nextAttackTime;

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        if (anim == null)
            Debug.LogError("HeroAttack: no Animator found on the hero or its children.");
    }

    void Update()
    {
        bool pressed = Input.GetMouseButtonDown(mouseButton) || Input.GetKeyDown(alternateKey);
        if (pressed && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + cooldown;
            StartCoroutine(AttackRoutine());
        }
    }

    AnimatorStateInfo GetState()
    {
        return anim.IsInTransition(0) ? anim.GetNextAnimatorStateInfo(0) : anim.GetCurrentAnimatorStateInfo(0);
    }

    bool InAttackState()
    {
        return anim != null && anim.runtimeAnimatorController != null && GetState().IsName(attackStateName);
    }

    IEnumerator AttackRoutine()
    {
        if (anim != null) anim.SetTrigger(attackTrigger);

        float timeout = 0.5f;
        while (timeout > 0f && !InAttackState())
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (!InAttackState())
        {
            yield return new WaitForSeconds(fallbackHitDelay);
            DealDamage();
            yield break;
        }

        foreach (float point in hitPoints)
        {
            while (InAttackState() && GetState().normalizedTime < point) yield return null;
            if (!InAttackState()) yield break;
            DealDamage();
        }
    }

    void EnsureMinionsKillable()
    {
        string prefix = minionNamePrefix.ToLower();
        foreach (var t in FindObjectsOfType<Transform>())
        {
            if (!t.name.ToLower().StartsWith(prefix)) continue;
            if (t.GetComponentInParent<EnemyHealth>() != null) continue;
            if (t.parent != null && t.parent.name.ToLower().StartsWith(prefix)) continue;

            var h = t.gameObject.AddComponent<EnemyHealth>();
            h.Setup(minionHealth, false, minionRadius, minionRemoveDelay, true);
        }
    }

    public void DealDamage()
    {
        EnsureMinionsKillable();

        foreach (var e in new List<EnemyHealth>(EnemyHealth.All))
        {
            if (e == null || e.IsDead) continue;

            Vector3 toEnemy = e.transform.position - transform.position;
            toEnemy.y = 0f;
            float dist = toEnemy.magnitude;

            if (dist > attackRange + e.bodyRadius) continue;

            if (dist > 0.01f)
            {
                float angle = Vector3.Angle(transform.forward, toEnemy);
                if (angle > attackAngle * 0.5f) continue;
            }

            e.TakeDamage(damage);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
