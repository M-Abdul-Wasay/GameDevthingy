using System.Collections;
using UnityEngine;

// TWO-HANDED SLAM
// Put this in Assets/Scripts and add it to the HERO (same object as ShadowHeroController).
// Press G (or RIGHT mouse button):
//   1. both hands raise overhead and charge up with glowing energy
//   2. the hero throws his whole body forward and SLAMS both fists down
//   3. a shockwave hits everything in front of him (boss, skeletons, anything with EnemyHealth)
//
// The body pose comes from ProceduralMotion (it re-uses the roar pose: lean back, tremble, slam forward).
// If the hero has no ProceduralMotion it still works, just without the body pose.
// Optional: if you have a real two-hand animation, type its Animator trigger name in "Anim Trigger".
[DefaultExecutionOrder(2000)]
[DisallowMultipleComponent]
public class HeroTwoHandAttack : MonoBehaviour
{
    [Header("Input")]
    public KeyCode key = KeyCode.G;
    public int mouseButton = 1;              // 1 = right mouse, -1 = turn mouse off

    [Header("Timing")]
    public float duration = 1.1f;            // whole move, charge + slam + recover
    public float cooldown = 3f;
    public string animTrigger = "";          // optional Animator trigger for your own animation

    [Header("Damage")]
    public float damage = 45f;               // much stronger than the normal sword hit (15)
    public float range = 3.5f;
    [Range(30f, 360f)] public float angle = 200f;
    public float minionDamage = 999f;        // skeletons just die

    [Header("Slam dash")]
    public float dashDistance = 1.2f;        // he lunges forward while slamming
    public float wallRadius = 0.3f;
    public float wallHeight = 0.9f;

    [Header("Look")]
    public Color glowColor = new Color(0.45f, 0.85f, 1f);
    public float shakeAmount = 0.18f;
    public float shakeTime = 0.3f;

    public bool IsAttacking { get; private set; }

    Animator anim;
    ProceduralMotion motion;
    ShadowHeroController hero;
    Transform leftHand, rightHand;
    Transform orbL, orbR;
    float nextReady;
    float shakeUntil;

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        motion = GetComponentInChildren<ProceduralMotion>();
        if (motion == null) motion = GetComponentInParent<ProceduralMotion>();
        hero = GetComponent<ShadowHeroController>();

        // Real hand bones if the model is Humanoid, otherwise we use points beside the body
        if (anim != null && anim.isHuman)
        {
            leftHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        }
    }

    void Update()
    {
        if (IsAttacking || Time.time < nextReady) return;
        if (hero != null && hero.isDead) return;

        bool pressed = Input.GetKeyDown(key) || (mouseButton >= 0 && Input.GetMouseButtonDown(mouseButton));
        if (pressed) StartCoroutine(Slam());
    }

    IEnumerator Slam()
    {
        IsAttacking = true;
        nextReady = Time.time + cooldown;

        if (motion != null) motion.PlayScream(duration);
        if (anim != null && !string.IsNullOrEmpty(animTrigger)) anim.SetTrigger(animTrigger);

        CreateOrbs();

        float impactT = duration * 0.86f;    // moment the fists land
        float dashStart = duration * 0.80f;  // body starts throwing itself forward
        float t0 = Time.time;

        while (Time.time - t0 < impactT)
        {
            if (hero != null && hero.isDead) { DestroyOrbs(); IsAttacking = false; yield break; }

            float t = Time.time - t0;
            UpdateOrbs(t / impactT);

            if (t >= dashStart)
            {
                float speed = dashDistance / Mathf.Max(0.05f, impactT - dashStart);
                Vector3 step = transform.forward * speed * Time.deltaTime;
                transform.position += WallCheck.Allowed(transform, step, wallRadius, wallHeight);
            }
            yield return null;
        }

        DestroyOrbs();
        Impact();

        yield return new WaitForSeconds(Mathf.Max(0f, duration - impactT));
        IsAttacking = false;
    }

    // ---------------------------------------------------------------- damage

    void Impact()
    {
        Vector3 point = transform.position + transform.forward * 1.4f;
        shakeUntil = Time.time + shakeTime;

        StartCoroutine(Shockwave(point));

        // everything with EnemyHealth (boss, skeletons the hero already made killable)
        foreach (var e in new System.Collections.Generic.List<EnemyHealth>(EnemyHealth.All))
        {
            if (e == null || e.IsDead) continue;
            if (InCone(e.transform.position, range + e.bodyRadius)) e.TakeDamage(damage);
        }

        // summoned skeletons that don't have EnemyHealth yet
        foreach (var m in FindObjectsOfType<MinionSkeleton>())
        {
            if (m == null) continue;
            if (m.GetComponentInParent<EnemyHealth>() != null) continue;
            if (InCone(m.transform.position, range + 0.8f)) m.TakeDamage(minionDamage);
        }
    }

    bool InCone(Vector3 pos, float maxDist)
    {
        Vector3 to = pos - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist > maxDist) return false;
        if (dist > 0.01f && Vector3.Angle(transform.forward, to) > angle * 0.5f) return false;
        return true;
    }

    // ---------------------------------------------------------------- visuals

    static Material GlowMat(Color c)
    {
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Standard");
        return new Material(sh) { color = c };
    }

    static Transform MakePrimitive(PrimitiveType type, string n, Color c)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = n;
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        r.material = GlowMat(c);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    void CreateOrbs()
    {
        orbL = MakePrimitive(PrimitiveType.Sphere, "HandOrbL", glowColor);
        orbR = MakePrimitive(PrimitiveType.Sphere, "HandOrbR", glowColor);
        UpdateOrbs(0f);
    }

    void DestroyOrbs()
    {
        if (orbL != null) Destroy(orbL.gameObject);
        if (orbR != null) Destroy(orbR.gameObject);
    }

    // p: 0 = start of the charge, 1 = fists land
    void UpdateOrbs(float p)
    {
        if (orbL == null || orbR == null) return;

        float charge = Mathf.Clamp01(p / 0.93f);
        float slam = Mathf.Clamp01((p - 0.93f) / 0.07f);
        float size = Mathf.Lerp(0.12f, 0.42f, charge) * (1f + 0.15f * Mathf.Sin(Time.time * 30f));

        SetOrb(orbL, leftHand, -1f, charge, slam, size);
        SetOrb(orbR, rightHand, 1f, charge, slam, size);
    }

    void SetOrb(Transform orb, Transform hand, float side, float charge, float slam, float size)
    {
        Vector3 pos;
        if (hand != null)
        {
            pos = hand.position;
        }
        else
        {
            float k = 1f - (1f - charge) * (1f - charge);
            Vector3 overhead = transform.position
                             + Vector3.up * (1.3f + 1.1f * k)
                             + transform.right * side * Mathf.Lerp(0.55f, 0.22f, k)
                             + transform.forward * 0.1f;
            Vector3 front = transform.position + transform.forward * 1.6f + Vector3.up * 0.4f;
            pos = Vector3.Lerp(overhead, front, slam * slam);
        }
        orb.position = pos;
        orb.localScale = Vector3.one * size;
    }

    IEnumerator Shockwave(Vector3 center)
    {
        float groundY = transform.position.y + 0.05f;
        Vector3 c = new Vector3(center.x, groundY, center.z);

        Transform ring = MakePrimitive(PrimitiveType.Cylinder, "SlamRing", glowColor);
        Transform flash = MakePrimitive(PrimitiveType.Sphere, "SlamFlash", Color.white);
        flash.position = c + Vector3.up * 0.3f;

        var ringRend = ring.GetComponent<Renderer>();
        var flashRend = flash.GetComponent<Renderer>();

        GameObject lightGo = new GameObject("SlamLight");
        lightGo.transform.position = c + Vector3.up * 1f;
        var lt = lightGo.AddComponent<Light>();
        lt.type = LightType.Point;
        lt.color = glowColor;
        lt.range = range * 3f;

        float t = 0f, len = 0.4f;
        while (t < 1f)
        {
            t += Time.deltaTime / len;
            float k = Mathf.Clamp01(t);
            float eased = 1f - (1f - k) * (1f - k);

            float d = Mathf.Lerp(0.5f, range * 1.8f, eased);
            ring.position = c;
            ring.localScale = new Vector3(d, 0.03f, d);
            Color rc = glowColor; rc.a = 1f - k;
            ringRend.material.color = rc;

            float fs = Mathf.Lerp(0.8f, 2.2f, eased);
            flash.localScale = Vector3.one * fs;
            Color fc = Color.white; fc.a = 0.9f * (1f - k);
            flashRend.material.color = fc;

            lt.intensity = 6f * (1f - k);
            yield return null;
        }

        Destroy(ring.gameObject);
        Destroy(flash.gameObject);
        Destroy(lightGo);
    }

    // runs after CameraFollow (execution order 2000), so the shake sits on top of the camera position
    void LateUpdate()
    {
        if (Time.time >= shakeUntil) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        float fade = (shakeUntil - Time.time) / Mathf.Max(0.01f, shakeTime);
        cam.transform.position += Random.insideUnitSphere * shakeAmount * fade;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.85f, 1f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
