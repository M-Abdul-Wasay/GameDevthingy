using UnityEngine;

// Add this to the boss. He walks toward the hero, runs when the hero is far,
// stops when close, and holds still while casting a skill or when dead.
public class BossChase : MonoBehaviour
{
    [Header("Target (auto-finds the hero if empty)")]
    public Transform target;

    [Header("Speed")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 5f;       // used when the hero is farther than Run Distance
    public float runDistance = 10f;

    [Header("Distances")]
    public float stopDistance = 2.5f; // how close he gets before stopping
    public float aggroRange = 40f;    // ignores the hero beyond this distance

    [Header("Other")]
    public float turnSpeed = 6f;
    public bool followGround = true;

    [Header("Walls")]
    public float wallRadius = 0.5f;
    public float wallHeight = 1.2f;

    private BossSkills skills;

    void Start()
    {
        skills = GetComponent<BossSkills>();

        if (target == null)
        {
            var hero = FindObjectOfType<ShadowHeroController>();
            if (hero != null) target = hero.transform;
        }
    }

    void Update()
    {
        if (target == null) return;
        if (skills != null && (skills.IsDead || skills.IsCasting)) return;

        Vector3 to = target.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;

        if (dist > aggroRange) return;

        // Always turn toward the hero
        if (dist > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(to);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        if (dist <= stopDistance) return;

        float speed = dist > runDistance ? runSpeed : walkSpeed;
        Vector3 step = (to / dist) * speed * Time.deltaTime;
        transform.position += WallCheck.Allowed(transform, step, wallRadius, wallHeight);

        if (followGround) SnapToGround();
    }

    void SnapToGround()
    {
        Vector3 pos = transform.position;
        Vector3 origin = pos + Vector3.up * 5f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestDist = float.MaxValue;
        float y = pos.y;
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<ShadowHeroController>() != null) continue;
            if (h.collider.GetComponentInParent<MinionSkeleton>() != null) continue;
            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                y = h.point.y;
                found = true;
            }
        }

        if (found)
        {
            pos.y = y;
            transform.position = pos;
        }
    }
}
