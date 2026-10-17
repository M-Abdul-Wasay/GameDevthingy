using UnityEngine;

// Add this to the boss. After he wakes up he WALKS for a moment, then speeds up and RUNS at the hero.
// He stops when close, and holds still while casting a skill, screaming, or when dead.
public class BossChase : MonoBehaviour
{
    [Header("Target (auto-finds the hero if empty)")]
    public Transform target;

    [Header("Speed")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 6f;       // full run speed
    public float runDistance = 10f;   // only used if 'Slow When Close' is ticked

    [Header("Walk first, then run")]
    [Tooltip("Seconds he walks (slow, menacing) before he starts running")]
    public float walkFirstSeconds = 1.5f;
    [Tooltip("Seconds it takes to speed up from walk to full run")]
    public float runRampTime = 0.6f;
    [Tooltip("If ticked he walks again when closer than Run Distance")]
    public bool slowWhenClose = false;

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
    private ProceduralMotion motion;
    private float chaseStart = -1f;

    void Start()
    {
        skills = GetComponent<BossSkills>();

        motion = GetComponent<ProceduralMotion>();
        if (motion == null) motion = GetComponentInChildren<ProceduralMotion>();

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
        if (motion != null && motion.IsScreaming) return;   // stand still while screaming

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

        // 0 = walking, 1 = full run. The walk lasts walkFirstSeconds, then he ramps up to a run.
        if (chaseStart < 0f) chaseStart = Time.time;
        float run01 = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01((Time.time - chaseStart - walkFirstSeconds) / Mathf.Max(0.05f, runRampTime)));
        if (slowWhenClose && dist < runDistance) run01 = 0f;

        float speed = Mathf.Lerp(walkSpeed, runSpeed, run01);
        if (motion != null) motion.runAmount = run01;   // body bobs / leans harder when running

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
