using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Add this to the boss (the object with the Animator built by Tools > Build Boss Animator).
// Skill 1 = summon small skeletons around the boss.
// Skill 3 = throw blue flames at the hero.
public class BossSkills : MonoBehaviour
{
    [Header("Target (auto-finds the hero if empty)")]
    public Transform target;

    [Header("Animator trigger names (swap these two if the skills are the other way around)")]
    public string summonTrigger = "Skill1";
    public string flameTrigger = "Skill3";

    [Header("Skill 1 - Summon Skeletons")]
    public GameObject minionPrefab;        // your small skeleton prefab (optional)
    public int minionsPerCast = 4;
    public int maxMinions = 8;
    public float summonRadius = 6f;        // minions appear within this radius of the boss
    public float summonTriggerRange = 20f; // boss only summons when the hero is this close
    public float summonCooldown = 12f;
    public float summonDelay = 0.8f;       // seconds after the animation starts
    public float summonDuration = 2.5f;    // length of the skill animation

    [Header("Skill 3 - Blue Flame")]
    public GameObject flamePrefab;         // your flame effect prefab (optional)
    public float flameRange = 12f;
    public float flameCooldown = 6f;
    public float flameDelay = 0.6f;
    public float flameDuration = 2f;
    public int flameCount = 3;
    public float flameSpread = 15f;        // degrees between left and right flame
    public float flameSpeed = 12f;
    public float flameDamage = 15f;

    [Header("General")]
    public float startDelay = 2f;
    public float decisionInterval = 1f;
    public float turnSpeed = 8f;

    public bool IsCasting { get; private set; }
    public bool IsDead { get; private set; }

    private Animator anim;
    private Vector3 lastPos;
    private float nextDecision;
    private float summonReady;
    private float flameReady;
    private readonly List<GameObject> minions = new List<GameObject>();

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
        lastPos = transform.position;
    }

    void Start()
    {
        if (target == null)
        {
            var hero = FindObjectOfType<ShadowHeroController>();
            if (hero != null) target = hero.transform;
        }
        nextDecision = Time.time + startDelay;
    }

    void Update()
    {
        if (IsDead) return;

        UpdateLocomotion();

        if (IsCasting)
        {
            FaceTarget();
            return;
        }

        if (target == null || Time.time < nextDecision) return;
        nextDecision = Time.time + decisionInterval;
        ChooseSkill();
    }

    // Drives Idle / Walk / Run from how fast the boss really moves
    void UpdateLocomotion()
    {
        float speed = (transform.position - lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPos = transform.position;
        if (anim != null) anim.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
    }

    void ChooseSkill()
    {
        minions.RemoveAll(m => m == null);
        float dist = Vector3.Distance(transform.position, target.position);

        bool canFlame = Time.time >= flameReady && dist <= flameRange;
        bool canSummon = Time.time >= summonReady && minions.Count < maxMinions && dist <= summonTriggerRange;

        if (canFlame && canSummon)
        {
            if (Random.value < 0.5f) canSummon = false; else canFlame = false;
        }

        if (canSummon) StartCoroutine(CastSummon());
        else if (canFlame) StartCoroutine(CastFlame());
    }

    IEnumerator CastSummon()
    {
        IsCasting = true;
        summonReady = Time.time + summonCooldown;
        if (anim != null) anim.SetTrigger(summonTrigger);

        yield return new WaitForSeconds(summonDelay);
        if (IsDead) yield break;

        for (int i = 0; i < minionsPerCast && minions.Count < maxMinions; i++)
            SpawnMinion();

        yield return new WaitForSeconds(Mathf.Max(0f, summonDuration - summonDelay));
        IsCasting = false;
    }

    IEnumerator CastFlame()
    {
        IsCasting = true;
        flameReady = Time.time + flameCooldown;
        if (anim != null) anim.SetTrigger(flameTrigger);

        yield return new WaitForSeconds(flameDelay);
        if (IsDead) yield break;

        ThrowFlames();

        yield return new WaitForSeconds(Mathf.Max(0f, flameDuration - flameDelay));
        IsCasting = false;
    }

    void SpawnMinion()
    {
        Vector2 c = Random.insideUnitCircle.normalized * Random.Range(summonRadius * 0.4f, summonRadius);
        Vector3 p = transform.position + new Vector3(c.x, 0f, c.y);
        p.y = GroundY(p, transform.position.y);

        GameObject m;
        if (minionPrefab != null)
        {
            m = Instantiate(minionPrefab, p, Quaternion.identity);
        }
        else
        {
            // Placeholder so you can test before you have a prefab
            m = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            m.name = "MinionSkeleton";
            m.transform.localScale = Vector3.one * 0.6f;
            m.transform.position = p + Vector3.up * 0.6f;
        }

        if (m.GetComponent<MinionSkeleton>() == null) m.AddComponent<MinionSkeleton>();
        minions.Add(m);
    }

    void ThrowFlames()
    {
        if (target == null) return;

        Vector3 origin = transform.position + Vector3.up * 1.5f + transform.forward * 1f;
        Vector3 aim = (target.position + Vector3.up * 1f - origin).normalized;

        for (int i = 0; i < flameCount; i++)
        {
            float angle = flameCount <= 1 ? 0f : Mathf.Lerp(-flameSpread, flameSpread, i / (flameCount - 1f));
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * aim;
            SpawnFlame(origin, dir);
        }
    }

    void SpawnFlame(Vector3 origin, Vector3 dir)
    {
        GameObject f;
        if (flamePrefab != null)
        {
            f = Instantiate(flamePrefab, origin, Quaternion.LookRotation(dir));
        }
        else
        {
            f = FlameProjectile.CreateDefaultFlame(origin);
        }

        var proj = f.GetComponent<FlameProjectile>();
        if (proj == null) proj = f.AddComponent<FlameProjectile>();
        proj.Init(dir, flameSpeed, flameDamage, target);
    }

    void FaceTarget()
    {
        if (target == null) return;
        Vector3 to = target.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), turnSpeed * Time.deltaTime);
    }

    float GroundY(Vector3 pos, float fallback)
    {
        Vector3 origin = new Vector3(pos.x, pos.y + 5f, pos.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestDist = float.MaxValue;
        float y = fallback;
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<MinionSkeleton>() != null) continue;
            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                y = h.point.y;
                found = true;
            }
        }
        return found ? y : fallback;
    }

    // Call this from BossAI when the boss health reaches 0
    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        StopAllCoroutines();
        if (anim != null) anim.SetTrigger("Die");

        foreach (var m in minions)
            if (m != null) Destroy(m);
        minions.Clear();
    }
}
