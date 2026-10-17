using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Put in Assets/Scripts.
// BOSS: add to Boss 1 (Is Boss ticked). First REMOVE the old BossHealth component and delete BossHealth.cs.
// SKELETONS: HeroAttack adds this automatically to anything named "skeleton...", so they are killable with no setup.
public class EnemyHealth : MonoBehaviour
{
    public static readonly List<EnemyHealth> All = new List<EnemyHealth>();

    [Header("Health")]
    public float maxHealth = 200f;
    public bool isBoss = true;
    public string displayName = "";          // e.g. FROSTFLAME LICH (empty = object name)
    public float bodyRadius = 1.5f;          // added to the hero's attack range
    public string dieTrigger = "Die";
    public float destroyDelay = -1f;         // seconds after death before the body is removed (-1 = keep it)

    [Header("Health bar (floats above the head)")]
    public bool showBar = true;
    public bool onlyShowWhenDamaged = false;
    public float heightOffset = 0.5f;        // gap above the top of the model
    public float barHeightOverride = 0f;     // set > 0 to place the bar at a fixed height
    public float barWidth = 170f;
    public float maxBarDistance = 45f;
    public Color lowColor = new Color(0.65f, 0.04f, 0.10f);
    public Color highColor = new Color(1f, 0.38f, 0.12f);

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    Animator anim;
    bool hasDieTrigger, damaged, heightReady;
    float trail = 1f, hitFlash, lastHitTime, barHeight;
    GUIStyle nameStyle, numStyle;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Awake()
    {
        CurrentHealth = maxHealth;
        anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (var p in anim.parameters)
                if (p.name == dieTrigger && p.type == AnimatorControllerParameterType.Trigger) hasDieTrigger = true;
        }
    }

    // Used by HeroAttack when it adds this to skeletons at runtime
    public void Setup(float hp, bool boss, float radius, float removeAfter, bool hideUntilHit)
    {
        maxHealth = hp;
        CurrentHealth = hp;
        isBoss = boss;
        bodyRadius = radius;
        destroyDelay = removeAfter;
        onlyShowWhenDamaged = hideUntilHit;
        trail = 1f;
    }

    void Update()
    {
        if (hitFlash > 0f) hitFlash -= Time.deltaTime;

        float frac = maxHealth > 0f ? CurrentHealth / maxHealth : 0f;
        if (trail > frac)
        {
            if (Time.time - lastHitTime > 0.45f)
                trail = Mathf.MoveTowards(trail, frac, 0.5f * Time.deltaTime);
        }
        else trail = frac;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        damaged = true;
        hitFlash = 0.15f;
        lastHitTime = Time.time;

        if (CurrentHealth <= 0f) Die();
    }

    void Die()
    {
        IsDead = true;

        // Stop EVERYTHING that could make it follow or cast: scripts, coroutines, invokes, agent, physics
        foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == this || mb is ProceduralMotion) continue;
            mb.StopAllCoroutines();
            mb.CancelInvoke();
            mb.enabled = false;
        }

        foreach (var agent in GetComponentsInChildren<NavMeshAgent>(true))
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            agent.enabled = false;
        }

        foreach (var cc in GetComponentsInChildren<CharacterController>(true)) cc.enabled = false;

        foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (var p in anim.parameters)
            {
                if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) anim.SetFloat("Speed", 0f);
                if (p.name == "isWalking" && p.type == AnimatorControllerParameterType.Bool) anim.SetBool("isWalking", false);
            }
            if (hasDieTrigger) anim.SetTrigger(dieTrigger);
        }

        if (!hasDieTrigger) StartCoroutine(FallOver());
        if (destroyDelay > 0f) StartCoroutine(RemoveLater());

        // Boss dies -> its summoned skeletons die too
        if (isBoss)
        {
            foreach (var e in new List<EnemyHealth>(All))
                if (e != null && e != this && !e.isBoss) e.TakeDamage(99999f);
        }

        Debug.Log(name + " defeated!");
    }

    IEnumerator FallOver()
    {
        Quaternion start = transform.rotation;
        Quaternion end = start * Quaternion.Euler(-85f, 0f, 0f);
        Vector3 startPos = transform.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.6f;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.rotation = Quaternion.Slerp(start, end, k);
            transform.position = startPos + Vector3.down * 0.15f * k;
            yield return null;
        }
    }

    IEnumerator RemoveLater()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }

    void EnsureHeight()
    {
        if (heightReady) return;
        heightReady = true;

        if (barHeightOverride > 0f) { barHeight = barHeightOverride; return; }

        float best = 0f;
        bool any = false;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            float y = r.bounds.max.y - transform.position.y;
            if (!any || y > best) { best = y; any = true; }
        }
        float top = any ? Mathf.Clamp(best, 0.5f, 20f) : 1.8f;
        barHeight = top + heightOffset;
    }

    static void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void Label(GUIStyle style, Rect r, string text, Color c)
    {
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), text, style);
        style.normal.textColor = c;
        GUI.Label(r, text, style);
    }

    void OnGUI()
    {
        if (!showBar || IsDead) return;
        if (onlyShowWhenDamaged && !damaged) return;
        if (Event.current.type != EventType.Repaint) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        EnsureHeight();

        Vector3 world = transform.position + Vector3.up * barHeight;
        float dist = Vector3.Distance(cam.transform.position, world);
        if (dist > maxBarDistance) return;

        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z <= 0f) return;

        float scale = Mathf.Clamp(14f / Mathf.Max(dist, 0.1f), 0.55f, 1.25f);
        if (!isBoss) scale *= 0.6f;

        float w = barWidth * scale;
        float h = (isBoss ? 16f : 9f) * scale;
        float x = sp.x - w * 0.5f;
        float y = Screen.height - sp.y - h;

        float frac = maxHealth > 0f ? Mathf.Clamp01(CurrentHealth / maxHealth) : 0f;

        // border + background
        Fill(new Rect(x - 2f, y - 2f, w + 4f, h + 4f), new Color(0f, 0f, 0f, 0.9f));
        Fill(new Rect(x, y, w, h), new Color(0.12f, 0.05f, 0.05f, 0.95f));

        // delayed "chip" damage trail
        Fill(new Rect(x, y, w * Mathf.Clamp01(trail), h), new Color(1f, 0.85f, 0.3f, 0.9f));

        // main fill (flashes white when hit)
        Color fill = Color.Lerp(lowColor, highColor, frac);
        if (hitFlash > 0f) fill = Color.Lerp(fill, Color.white, Mathf.Clamp01(hitFlash / 0.15f));
        Fill(new Rect(x, y, w * frac, h), fill);

        // gloss highlight on the top half
        Fill(new Rect(x, y, w * frac, h * 0.45f), new Color(1f, 1f, 1f, 0.18f));

        // quarter ticks on the boss bar
        if (isBoss)
        {
            for (int i = 1; i < 4; i++)
                Fill(new Rect(x + w * i / 4f - 0.5f, y, 1f, h), new Color(0f, 0f, 0f, 0.5f));
        }

        if (isBoss)
        {
            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label);
                nameStyle.alignment = TextAnchor.MiddleCenter;
                nameStyle.fontStyle = FontStyle.Bold;
                numStyle = new GUIStyle(GUI.skin.label);
                numStyle.alignment = TextAnchor.MiddleCenter;
                numStyle.fontStyle = FontStyle.Bold;
            }

            string nm = string.IsNullOrEmpty(displayName) ? name.Replace("(Clone)", "").Trim() : displayName;
            nameStyle.fontSize = Mathf.RoundToInt(15f * scale);
            Label(nameStyle, new Rect(x - 60f, y - 24f * scale, w + 120f, 22f * scale), nm.ToUpper(), Color.white);

            numStyle.fontSize = Mathf.RoundToInt(11f * scale);
            Label(numStyle, new Rect(x, y, w, h), Mathf.CeilToInt(CurrentHealth) + " / " + Mathf.CeilToInt(maxHealth), Color.white);
        }
    }
}
