using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// DUAL SWORD COMBO for the main character.
// Put this in Assets/Scripts and add it to the HERO (same object as ShadowHeroController).
// Press G (or RIGHT mouse button) = full 3-strike combo:
//   1. RIGHT sword slashes across the front
//   2. LEFT sword slashes back the other way
//   3. BOTH swords rise up, then cross down in an X (finisher, double damage)
//
// It builds two swords for you (so you can test right away) with glowing slash trails.
// If you already have sword objects in your model, drag them into Custom Right / Left Sword.
// You do NOT need new animation clips: the swords are swung from code.
// If you added HeroTwoHandAttack earlier, REMOVE it (it also uses G / right mouse).
[DefaultExecutionOrder(2000)]
[DisallowMultipleComponent]
public class HeroDualSwords : MonoBehaviour
{
    [Header("Input")]
    public KeyCode key = KeyCode.G;
    public int mouseButton = 1;                 // 1 = right mouse, -1 = off

    [Header("Damage")]
    public float damage = 15f;                  // hit 1 and hit 2
    public float finisherDamage = 35f;          // the X slash
    public float range = 3f;
    [Range(30f, 360f)] public float angle = 180f;
    public float minionDamage = 999f;

    [Header("Speed")]
    public float windupTime = 0.12f;
    public float strikeTime = 0.10f;
    public float holdTime = 0.05f;
    public float finisherWindupTime = 0.22f;
    public float cooldown = 0.25f;
    public float lungeDistance = 0.35f;         // small step forward on every strike
    public float wallRadius = 0.3f;
    public float wallHeight = 0.9f;

    [Header("Swords")]
    public bool showSwordsAlways = true;        // false = swords only appear while attacking
    public bool useHandBones = true;            // swords come out of the real hands (Humanoid models)
    public Vector3 shoulderOffset = new Vector3(0.45f, 1.0f, 0.2f); // used if no hand bones
    public float bladeLength = 1.1f;
    public Transform customRightSword;          // optional: your own sword objects
    public Transform customLeftSword;
    public Vector3 customSwordEuler = Vector3.zero; // rotate your sword so its blade points forward (+Z)

    [Header("Look")]
    public Color bladeColor = new Color(0.82f, 0.9f, 1f);
    public Color trailColor = new Color(0.45f, 0.85f, 1f);
    public float shakeAmount = 0.15f;

    public bool IsAttacking { get; private set; }

    // swing poses, relative to the hero (pitch down +, yaw right +)
    static readonly Quaternion RestR = Quaternion.Euler(55f, 12f, 0f);
    static readonly Quaternion RestL = Quaternion.Euler(55f, -12f, 0f);
    static readonly Quaternion Wind1R = Quaternion.Euler(-15f, 85f, -35f);   // right sword pulled out to the right
    static readonly Quaternion Strike1R = Quaternion.Euler(10f, -60f, 35f);  // ...slashes across to the left
    static readonly Quaternion Wind2L = Quaternion.Euler(-15f, -85f, 35f);
    static readonly Quaternion Strike2L = Quaternion.Euler(10f, 60f, -35f);
    static readonly Quaternion Wind3R = Quaternion.Euler(-80f, 25f, 0f);     // both swords raised
    static readonly Quaternion Wind3L = Quaternion.Euler(-80f, -25f, 0f);
    static readonly Quaternion Strike3R = Quaternion.Euler(35f, -25f, 0f);   // cross into an X
    static readonly Quaternion Strike3L = Quaternion.Euler(35f, 25f, 0f);

    Animator anim;
    ShadowHeroController hero;
    Transform handR, handL;
    Transform pivotR, pivotL;
    TrailRenderer trailR, trailL;
    Quaternion curR = RestR, curL = RestL;
    float nextReady;
    float shakeUntil, shakeLen = 0.2f;

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        hero = GetComponent<ShadowHeroController>();

        if (useHandBones && anim != null && anim.isHuman)
        {
            handR = anim.GetBoneTransform(HumanBodyBones.RightHand);
            handL = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        }

        pivotR = BuildPivot("RightSwordPivot", customRightSword, out trailR);
        pivotL = BuildPivot("LeftSwordPivot", customLeftSword, out trailL);
        pivotR.gameObject.SetActive(showSwordsAlways);
        pivotL.gameObject.SetActive(showSwordsAlways);

        Debug.Log("HeroDualSwords is ACTIVE on '" + name + "'. Press " + key + (mouseButton >= 0 ? " or mouse button " + mouseButton : "") +
                  ". Hand bones: " + (handR != null ? "found" : "none (using Shoulder Offset)"), this);
    }

    void OnDestroy()
    {
        if (pivotR != null) Destroy(pivotR.gameObject);
        if (pivotL != null) Destroy(pivotL.gameObject);
    }

    void Update()
    {
        if (IsAttacking || Time.time < nextReady) return;
        if (hero != null && hero.isDead) return;

        bool pressed = Input.GetKeyDown(key) || (mouseButton >= 0 && Input.GetMouseButtonDown(mouseButton));
        if (pressed) StartCoroutine(Combo());
    }

    // ---------------------------------------------------------------- the combo

    IEnumerator Combo()
    {
        IsAttacking = true;
        Debug.Log("HeroDualSwords: combo started", this);
        pivotR.gameObject.SetActive(true);
        pivotL.gameObject.SetActive(true);

        // 1) right sword
        yield return StartCoroutine(Tween(Wind1R, null, windupTime, 0.5f));
        if (Dead()) { Finish(); yield break; }
        yield return StartCoroutine(Strike(Strike1R, null, true, false, damage, range, angle, false));

        // 2) left sword
        yield return StartCoroutine(Tween(null, Wind2L, windupTime, 0.5f));
        if (Dead()) { Finish(); yield break; }
        yield return StartCoroutine(Strike(null, Strike2L, false, true, damage, range, angle, false));

        // 3) both swords, X slash
        yield return StartCoroutine(Tween(Wind3R, Wind3L, finisherWindupTime, 0.5f));
        if (Dead()) { Finish(); yield break; }
        yield return StartCoroutine(Strike(Strike3R, Strike3L, true, true, finisherDamage, range * 1.15f, angle * 1.2f, true));

        // back to the sides
        yield return StartCoroutine(Tween(RestR, RestL, 0.25f, 0.5f));
        Finish();
    }

    void Finish()
    {
        SetTrails(false, false);
        if (!showSwordsAlways)
        {
            pivotR.gameObject.SetActive(false);
            pivotL.gameObject.SetActive(false);
        }
        curR = RestR;
        curL = RestL;
        nextReady = Time.time + cooldown;
        IsAttacking = false;
    }

    bool Dead() { return hero != null && hero.isDead; }

    // Smoothly rotate the swords to a pose. null = leave that sword where it is.
    IEnumerator Tween(Quaternion? toR, Quaternion? toL, float time, float power)
    {
        Quaternion fR = curR, fL = curL;
        Quaternion tR = toR.HasValue ? toR.Value : curR;
        Quaternion tL = toL.HasValue ? toL.Value : curL;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, time);
            float k = Mathf.Pow(Mathf.Clamp01(t), power);
            curR = Quaternion.Slerp(fR, tR, k);
            curL = Quaternion.Slerp(fL, tL, k);
            yield return null;
        }
        curR = tR;
        curL = tL;
    }

    IEnumerator Strike(Quaternion? toR, Quaternion? toL, bool trR, bool trL, float dmg, float rng, float ang, bool big)
    {
        SetTrails(trR, trL);
        StartCoroutine(Lunge(strikeTime));

        yield return StartCoroutine(Tween(toR, toL, strikeTime, 2.2f));

        DealDamage(dmg, rng, ang, big);
        if (big) { shakeLen = 0.25f; shakeUntil = Time.time + shakeLen; }

        yield return new WaitForSeconds(holdTime);
        SetTrails(false, false);
    }

    IEnumerator Lunge(float time)
    {
        float speed = lungeDistance / Mathf.Max(0.02f, time);
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            Vector3 step = transform.forward * speed * Time.deltaTime;
            transform.position += WallCheck.Allowed(transform, step, wallRadius, wallHeight);
            yield return null;
        }
    }

    // ---------------------------------------------------------------- damage

    void DealDamage(float dmg, float rng, float ang, bool big)
    {
        foreach (var e in new List<EnemyHealth>(EnemyHealth.All))
        {
            if (e == null || e.IsDead) continue;
            if (!InCone(e.transform.position, rng + e.bodyRadius, ang)) continue;
            e.TakeDamage(dmg);
            Spark(e.transform.position + Vector3.up * 1.2f, big);
        }

        foreach (var m in FindObjectsOfType<MinionSkeleton>())
        {
            if (m == null) continue;
            if (m.GetComponentInParent<EnemyHealth>() != null) continue;
            if (!InCone(m.transform.position, rng + 0.8f, ang)) continue;
            m.TakeDamage(minionDamage);
            Spark(m.transform.position + Vector3.up * 0.8f, big);
        }
    }

    bool InCone(Vector3 pos, float maxDist, float ang)
    {
        Vector3 to = pos - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist > maxDist) return false;
        if (dist > 0.01f && Vector3.Angle(transform.forward, to) > ang * 0.5f) return false;
        return true;
    }

    // ---------------------------------------------------------------- visuals

    static Material Flat(Color c)
    {
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Standard");
        return new Material(sh) { color = c };
    }

    Transform BuildPivot(string pivotName, Transform custom, out TrailRenderer trail)
    {
        Transform pivot = new GameObject(pivotName).transform;

        if (custom != null)
        {
            custom.SetParent(pivot, false);
            custom.localPosition = Vector3.zero;
            custom.localRotation = Quaternion.Euler(customSwordEuler);
        }
        else
        {
            Part(pivot, "Blade", new Vector3(0f, 0f, 0.15f + bladeLength * 0.5f), new Vector3(0.07f, 0.02f, bladeLength), bladeColor);
            Part(pivot, "Guard", new Vector3(0f, 0f, 0.15f), new Vector3(0.32f, 0.05f, 0.06f), new Color(0.9f, 0.7f, 0.2f));
            Part(pivot, "Handle", Vector3.zero, new Vector3(0.05f, 0.05f, 0.3f), new Color(0.3f, 0.18f, 0.1f));
        }

        // slash trail at the tip
        GameObject tip = new GameObject("Tip");
        tip.transform.SetParent(pivot, false);
        tip.transform.localPosition = new Vector3(0f, 0f, 0.15f + bladeLength);
        trail = tip.AddComponent<TrailRenderer>();
        trail.time = 0.18f;
        trail.startWidth = 0.28f;
        trail.endWidth = 0f;
        trail.material = Flat(Color.white);
        trail.startColor = trailColor;
        Color end = trailColor; end.a = 0f;
        trail.endColor = end;
        trail.emitting = false;
        return pivot;
    }

    static void Part(Transform parent, string n, Vector3 localPos, Vector3 scale, Color c)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = n;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.material = Flat(c);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void SetTrails(bool r, bool l)
    {
        if (trailR != null) trailR.emitting = r;
        if (trailL != null) trailL.emitting = l;
    }

    void Spark(Vector3 pos, bool big)
    {
        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = "SwordSpark";
        Destroy(s.GetComponent<Collider>());
        s.transform.position = pos;
        s.transform.localScale = Vector3.one * (big ? 0.8f : 0.4f);
        var r = s.GetComponent<Renderer>();
        r.material = Flat(new Color(trailColor.r, trailColor.g, trailColor.b, 0.9f));
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(s, 0.12f);
    }

    // Runs after the body and the camera (execution order 2000)
    void LateUpdate()
    {
        PlaceSword(pivotR, handR, 1f, curR);
        PlaceSword(pivotL, handL, -1f, curL);

        if (Time.time < shakeUntil)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                float fade = (shakeUntil - Time.time) / Mathf.Max(0.01f, shakeLen);
                cam.transform.position += Random.insideUnitSphere * shakeAmount * fade;
            }
        }
    }

    void PlaceSword(Transform pivot, Transform hand, float side, Quaternion local)
    {
        if (pivot == null) return;

        Vector3 pos;
        if (hand != null)
            pos = hand.position;
        else
            pos = transform.position + transform.rotation * new Vector3(shoulderOffset.x * side, shoulderOffset.y, shoulderOffset.z);

        pivot.position = pos;
        pivot.rotation = transform.rotation * local;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.85f, 1f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
