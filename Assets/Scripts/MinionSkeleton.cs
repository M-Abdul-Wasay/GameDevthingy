using UnityEngine;

// A small skeleton summoned by the boss. It rises out of the floor,
// stays on the ground, can't walk through walls, chases the hero and hits him up close.
public class MinionSkeleton : MonoBehaviour
{
    public float health = 30f;
    public float moveSpeed = 3f;
    public float attackRange = 1.3f;
    public float attackDamage = 5f;
    public float attackCooldown = 1.2f;

    [Header("Spawn")]
    public float riseDepth = 1.2f;
    public float riseSpeed = 2.5f;

    [Header("Walls / Ground")]
    public float wallRadius = 0.3f;
    public float wallHeight = 0.8f;
    public float groundFollowSpeed = 20f;

    private Transform target;
    private ShadowHeroController hero;
    private float nextAttack;
    private float footOffset;
    private bool rising = true;
    private float riseTargetY;

    void Start()
    {
        hero = FindObjectOfType<ShadowHeroController>();
        if (hero != null) target = hero.transform;

        // distance from the pivot down to the bottom of the collider
        var col = GetComponentInChildren<Collider>();
        footOffset = col != null ? Mathf.Max(0f, transform.position.y - col.bounds.min.y) : 0f;

        float ground;
        riseTargetY = TryGetGround(out ground) ? ground + footOffset : transform.position.y;

        Vector3 p = transform.position;
        p.y = riseTargetY - riseDepth;   // start under the floor
        transform.position = p;
    }

    void Update()
    {
        if (rising)
        {
            Vector3 p = transform.position;
            p.y = Mathf.MoveTowards(p.y, riseTargetY, riseSpeed * Time.deltaTime);
            transform.position = p;
            if (Mathf.Approximately(p.y, riseTargetY)) rising = false;
            return;
        }

        if (target != null)
        {
            Vector3 to = target.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            if (dist > attackRange)
            {
                Vector3 dir = to.normalized;
                Vector3 step = dir * moveSpeed * Time.deltaTime;
                transform.position += WallCheck.Allowed(transform, step, wallRadius, wallHeight);

                if (dir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
            }
            else if (Time.time >= nextAttack)
            {
                nextAttack = Time.time + attackCooldown;
                if (hero != null) hero.TakeDamage(attackDamage, false);
            }
        }

        // Always stay on the ground
        float g;
        if (TryGetGround(out g))
        {
            Vector3 p = transform.position;
            p.y = Mathf.MoveTowards(p.y, g + footOffset, groundFollowSpeed * Time.deltaTime);
            transform.position = p;
        }
    }

    bool TryGetGround(out float y)
    {
        y = 0f;
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);

        bool found = false;
        float best = float.MaxValue;
        foreach (var h in hits)
        {
            if (h.transform == transform || h.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<MinionSkeleton>() != null) continue;
            if (h.collider.GetComponentInParent<ShadowHeroController>() != null) continue;
            if (h.collider.GetComponentInParent<BossSkills>() != null) continue;
            if (h.distance < best)
            {
                best = h.distance;
                y = h.point.y;
                found = true;
            }
        }
        return found;
    }

    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0f) Destroy(gameObject);
    }
}
