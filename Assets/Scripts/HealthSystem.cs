using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
// No UI package needed any more: the health bar below draws itself.

public class HealthSystem : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;

    [Header("Extra health bar in the corner (OFF: your bar above the hero's head is used instead)")]
    public bool showHealthBar = false;

    [Header("Your existing bar above the hero's head")]
    [Tooltip("Copies this health into the hero's own health variable (health / currentHealth / hp) so the bar above his head moves")]
    public bool syncOtherHealthVariables = true;
    [Tooltip("Finds the GREEN bar above the hero's head and makes it shrink (and turn yellow, then red) with his health")]
    public bool driveOverheadBar = true;
    [Tooltip("Tick this to REMOVE the green bar above his head instead")]
    public bool hideOverheadBar = false;
    public Vector2 barPosition = new Vector2(24f, 24f);
    public Vector2 barSize = new Vector2(340f, 28f);
    public string barLabel = "HERO";

    [Header("Respawn")]
    public float respawnTime = 3f; // seconds to travel back in time
    [Tooltip("ON = you come back exactly where you died. OFF = you come back at the checkpoint / start position")]
    public bool respawnWhereDied = true;
    [Tooltip("If you die falling this far below solid ground, you come back on the last safe ground instead")]
    public float fallDeathDepth = 8f;
    [Tooltip("Seconds you can't be hurt after coming back")]
    public float respawnInvulnerability = 2f;

    [Header("Death / respawn effects")]
    public bool effects = true;
    public Color timeColor = new Color(0.35f, 0.7f, 1f, 1f);
    [Tooltip("Animator trigger played when you die (skipped if your Animator doesn't have it)")]
    public string dieTrigger = "Die";
    [Tooltip("While dead, also switch off the Hero... scripts (attacks, swords) so you can't fight as a ghost")]
    public bool pauseOtherHeroScripts = true;

    private float respawnTimer;
    private bool isRespawning;

    private Vector3 deathPosition;
    private Quaternion deathRotation;
    private Vector3 lastSafePosition;
    private Vector3 checkpointPosition;
    private bool hasCheckpoint;
    private float nextSafeCheck;
    private float invulnerableUntil;

    // visuals
    private float displayHealth;
    private float trailHealth;
    private float hitFlash;
    private float respawnFlash;
    private float lastHitTime = -10f;
    private GUIStyle bigStyle, smallStyle, barStyle;

    private Animator anim;
    private readonly HashSet<string> animParams = new HashSet<string>();
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly Dictionary<Behaviour, bool> pausedScripts = new Dictionary<Behaviour, bool>();

    private static Material softMat;
    private static Texture2D softTex;

    // keeps the hero's own health variable in sync (this is what moves the bar above his head)
    private class SyncTarget
    {
        public object target;
        public MemberInfo member;
        public bool isMax;
    }
    private readonly List<SyncTarget> syncTargets = new List<SyncTarget>();

    // the green bar above the hero's head
    private Renderer barRenderer;
    private Transform barTf;
    private float barFullScaleX;
    private float barBaseLocalX;
    private bool barShiftLocal;
    private int barColorProp = -1;
    private readonly List<Renderer> barGroup = new List<Renderer>();
    private MaterialPropertyBlock barBlock;
    private int barSearchTries;
    private float nextBarSearch;
    private float nextSyncSearch;
    private bool syncLogged;
    private static readonly string[] CurNames =
        { "currentHealth", "health", "hp", "currentHP", "currentHp", "curHealth", "CurrentHealth", "Health", "HP", "playerHealth" };
    private static readonly string[] MaxNames =
        { "maxHealth", "maxHP", "maxHp", "MaxHealth", "MaxHP", "startingHealth", "startHealth" };

    public bool IsDead { get { return isRespawning; } }

    // ------------------------------------------------------------------ setup

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
            foreach (var p in anim.parameters) animParams.Add(p.name);
    }

    void Start()
    {
        currentHealth = maxHealth;
        displayHealth = maxHealth;
        trailHealth = maxHealth;
        lastSafePosition = transform.position;

        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            renderers.Add(r);
        }
        UpdateHealthUI();
    }

    // ------------------------------------------------------------------ damage

    public void TakeDamage(int damage)
    {
        if (isRespawning) return;
        if (Time.time < invulnerableUntil) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        hitFlash = 1f;
        lastHitTime = Time.time;
        Debug.Log("Player took " + damage + " damage! HP " + currentHealth + "/" + maxHealth);
        UpdateHealthUI();

        if (currentHealth <= 0 && !isRespawning)
        {
            Die();
        }
    }

    // same thing for scripts that send decimals, e.g. TakeDamage(10f, false)
    public void TakeDamage(float damage)
    {
        TakeDamage(Mathf.CeilToInt(damage));
    }

    public void TakeDamage(float damage, bool unused)
    {
        TakeDamage(Mathf.CeilToInt(damage));
    }

    public void Heal(int amount)
    {
        if (isRespawning) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UpdateHealthUI();
    }

    /// <summary>Call this from a checkpoint to make it the respawn point (when respawnWhereDied is OFF).</summary>
    public void SetCheckpoint(Vector3 position)
    {
        checkpointPosition = position;
        hasCheckpoint = true;
    }

    // ------------------------------------------------------------------ death

    void Die()
    {
        Debug.Log("Player died. Initiating time travel respawn...");
        isRespawning = true;
        respawnTimer = respawnTime;

        // remember WHERE we died
        deathPosition = transform.position;
        deathRotation = transform.rotation;

        PauseHeroScripts(true);

        if (anim != null && animParams.Contains(dieTrigger)) anim.SetTrigger(dieTrigger);

        if (effects)
        {
            Vector3 chest = deathPosition + Vector3.up * 1.1f;
            SpawnBurst(chest, timeColor, 60, 0.2f, 5f, 1f, -0.1f);   // the hero dissolves into light
            StartCoroutine(FlashLight(chest, timeColor, 5f, 1f));
        }
    }

    void Update()
    {
        UpdateVisualNumbers();

        if ((driveOverheadBar || hideOverheadBar) && barTf == null && barSearchTries < 6 && Time.time >= nextBarSearch)
        {
            nextBarSearch = Time.time + 1f;
            barSearchTries++;
            FindOverheadBar();
            if (barTf == null && barSearchTries >= 6) LogBarInventory();
        }

        if (syncOtherHealthVariables && syncTargets.Count == 0 && Time.time >= nextSyncSearch)
        {
            nextSyncSearch = Time.time + 2f;
            FindSyncTargets();
        }

        if (isRespawning)
        {
            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f)
            {
                Respawn();
            }
            return;
        }

        RememberSafeGround();
    }

    void LateUpdate()
    {
        DriveOverheadBar();

        if (!syncOtherHealthVariables || syncTargets.Count == 0) return;

        // never write 0 into the hero's own variable: that could start a second death inside his own script
        float cur = Mathf.Max(1f, currentHealth);
        foreach (var t in syncTargets)
            WriteNumber(t, t.isMax ? maxHealth : cur);
    }

    // ------------------------------------------------------------------ the green bar above the hero's head

    void FindOverheadBar()
    {
        Renderer best = null;
        float bestScore = -1f;

        foreach (var r in FindObjectsOfType<Renderer>())
        {
            if (r is SkinnedMeshRenderer || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!r.enabled || r.sharedMaterial == null) continue;
            if (r.GetComponentInParent<ArcherAI>() != null) continue;

            Color c;
            var sr = r as SpriteRenderer;
            Material m = r.sharedMaterial;
            if (sr != null) c = sr.color;
            else if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor");
            else if (m.HasProperty("_Color")) c = m.GetColor("_Color");
            else continue;

            bool green = c.g > 0.45f && c.g > c.r + 0.2f && c.g > c.b + 0.1f;
            if (!green) continue;

            // a bar is wide and thin
            Vector3 size = r.bounds.size;
            if (Mathf.Max(size.x, size.z) < size.y * 3f) continue;

            bool underHero = r.transform.IsChildOf(transform);
            Vector3 d = r.bounds.center - transform.position;
            bool nearHead = new Vector2(d.x, d.z).magnitude < 2.5f && d.y > 0.3f && d.y < 6f;
            if (!underHero && !nearHead) continue;

            float score = (underHero ? 2f : 1f) + Mathf.Max(size.x, size.z) * 0.01f;
            if (score > bestScore) { bestScore = score; best = r; }
        }

        if (best == null) return;

        barRenderer = best;
        barTf = best.transform;
        barFullScaleX = barTf.localScale.x;
        barBaseLocalX = barTf.localPosition.x;
        // only slide the bar sideways when it sits upright inside its parent (so left edge stays put)
        barShiftLocal = barTf.parent != null && Quaternion.Angle(barTf.localRotation, Quaternion.identity) < 1f;
        barColorProp = best.sharedMaterial.HasProperty("_BaseColor") ? Shader.PropertyToID("_BaseColor") : Shader.PropertyToID("_Color");
        barBlock = new MaterialPropertyBlock();

        // the bar's background / siblings (flat renderers under the same parent)
        barGroup.Clear();
        if (barTf.parent != null && barTf.parent != transform)
        {
            foreach (var rr in barTf.parent.GetComponentsInChildren<Renderer>())
                if (!(rr is SkinnedMeshRenderer) && !(rr is ParticleSystemRenderer)) barGroup.Add(rr);
        }
        else barGroup.Add(best);

        string path = barTf.name;
        for (Transform t = barTf.parent; t != null; t = t.parent) path = t.name + "/" + path;
        Debug.Log("HealthSystem: found the green bar above the hero: " + path +
                  (hideOverheadBar ? " (hiding it)" : " (it now follows his health)"), this);
    }

    void DriveOverheadBar()
    {
        if (barTf == null) return;

        if (hideOverheadBar)
        {
            foreach (var r in barGroup) if (r != null) r.enabled = false;
            return;
        }
        if (!driveOverheadBar) return;

        // smooth: drops at once when hurt, refills when you respawn
        float pct = maxHealth > 0 ? Mathf.Clamp01(displayHealth / maxHealth) : 0f;

        Vector3 s = barTf.localScale;
        s.x = barFullScaleX * pct;
        barTf.localScale = s;

        if (barShiftLocal)
        {
            Vector3 lp = barTf.localPosition;
            lp.x = barBaseLocalX - barFullScaleX * (1f - pct) * 0.5f;   // keep the left edge fixed
            barTf.localPosition = lp;
        }

        // green -> yellow -> red
        Color col = pct > 0.5f
            ? Color.Lerp(new Color(1f, 0.85f, 0.15f), new Color(0.18f, 0.8f, 0.44f), (pct - 0.5f) * 2f)
            : Color.Lerp(new Color(0.9f, 0.15f, 0.15f), new Color(1f, 0.85f, 0.15f), pct * 2f);

        var sr = barRenderer as SpriteRenderer;
        if (sr != null) sr.color = col;
        else if (barRenderer != null)
        {
            barRenderer.GetPropertyBlock(barBlock);
            barBlock.SetColor(barColorProp, col);
            barRenderer.SetPropertyBlock(barBlock);
        }
    }

    // if the bar isn't an object, it is drawn by a script with OnGUI: list the suspects
    void LogBarInventory()
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        string onGui = "";
        int n = 0;
        foreach (var mb in FindObjectsOfType<MonoBehaviour>())
        {
            if (mb == null || mb == this) continue;
            var t = mb.GetType();
            if (t.Namespace != null && t.Namespace.StartsWith("Unity")) continue;
            if (t.GetMethod("OnGUI", F) == null) continue;
            onGui += t.Name + " (on " + mb.name + ")   ";
            if (++n >= 15) break;
        }

        string kids = "";
        foreach (var tr in GetComponentsInChildren<Transform>(true))
        {
            string nm = tr.name.ToLower();
            if (nm.Contains("bar") || nm.Contains("health") || nm.Contains("hp") || nm.Contains("hud")) kids += tr.name + "   ";
        }

        Debug.LogWarning("HealthSystem: I couldn't find the green bar as an object, so a script draws it with OnGUI. " +
                         "Scripts with OnGUI: " + (onGui == "" ? "none" : onGui) +
                         " | Objects on the hero with bar/health in the name: " + (kids == "" ? "none" : kids) +
                         " -- send me this line and the script that draws the bar.", this);
    }

    void FindSyncTargets()
    {
        var comps = new List<MonoBehaviour>();
        comps.AddRange(GetComponentsInParent<MonoBehaviour>(true));
        foreach (var c in GetComponentsInChildren<MonoBehaviour>(true))
            if (!comps.Contains(c)) comps.Add(c);

        foreach (var c in comps)
        {
            if (c == null || c == this) continue;
            AddSyncMember(c, CurNames, false);
            AddSyncMember(c, MaxNames, true);
        }

        if (syncTargets.Count > 0 && !syncLogged)
        {
            syncLogged = true;
            string list = "";
            foreach (var t in syncTargets) list += t.target.GetType().Name + "." + t.member.Name + "  ";
            Debug.Log("HealthSystem: keeping these in sync (moves the bar above your head): " + list, this);
        }
        else if (syncTargets.Count == 0 && !syncLogged)
        {
            syncLogged = true;   // say it only once
            string found = "";
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var c in comps)
            {
                if (c == null || c == this) continue;
                foreach (var f in c.GetType().GetFields(F))
                    if (IsNumber(f.FieldType) && f.Name.ToLower().Contains("health")) found += c.GetType().Name + "." + f.Name + "  ";
            }
            Debug.LogWarning("HealthSystem: couldn't find a health variable on the hero to sync. " +
                             "Numbers with 'health' in the name: " + (found == "" ? "none" : found) +
                             " -- tell me the script that draws the bar above the hero's head.", this);
        }
    }

    void AddSyncMember(MonoBehaviour c, string[] names, bool isMax)
    {
        const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var type = c.GetType();
        foreach (var n in names)
        {
            var f = type.GetField(n, F);
            if (f != null && IsNumber(f.FieldType) && !f.IsInitOnly && !f.IsLiteral)
            {
                syncTargets.Add(new SyncTarget { target = c, member = f, isMax = isMax });
                return;
            }
            var p = type.GetProperty(n, F);
            if (p != null && p.CanWrite && p.CanRead && IsNumber(p.PropertyType) && p.GetIndexParameters().Length == 0)
            {
                syncTargets.Add(new SyncTarget { target = c, member = p, isMax = isMax });
                return;
            }
        }
    }

    static bool IsNumber(System.Type t)
    {
        return t == typeof(float) || t == typeof(int) || t == typeof(double);
    }

    static void WriteNumber(SyncTarget t, float value)
    {
        var uo = t.target as Object;
        if (t.target is Object && uo == null) return;   // that script was destroyed

        var f = t.member as FieldInfo;
        var p = t.member as PropertyInfo;
        System.Type type = f != null ? f.FieldType : (p != null ? p.PropertyType : null);
        if (type == null) return;

        object v;
        if (type == typeof(int)) v = Mathf.RoundToInt(value);
        else if (type == typeof(double)) v = (double)value;
        else v = value;

        if (f != null) f.SetValue(t.target, v);
        else p.SetValue(t.target, v, null);
    }

    // remembers the last place you stood on solid ground (used if you die falling into a pit)
    void RememberSafeGround()
    {
        if (Time.time < nextSafeCheck) return;
        nextSafeCheck = Time.time + 0.5f;

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore))
            lastSafePosition = transform.position;
    }

    // ------------------------------------------------------------------ respawn

    void Respawn()
    {
        Vector3 target;
        if (respawnWhereDied)
        {
            target = deathPosition;
            // fell into a pit? come back on the last safe ground instead
            if (deathPosition.y < lastSafePosition.y - fallDeathDepth) target = lastSafePosition;
        }
        else
        {
            target = hasCheckpoint ? checkpointPosition : Vector3.zero;
        }

        Vector3 pos = target + Vector3.up * 0.1f;
        MoveTo(pos, respawnWhereDied ? deathRotation : transform.rotation);

        currentHealth = maxHealth;
        displayHealth = 0f;     // the bar refills in front of you
        trailHealth = 0f;
        isRespawning = false;
        invulnerableUntil = Time.time + respawnInvulnerability;

        ResetAnimator();        // gets him out of the lying-down pose
        PauseHeroScripts(false);
        UpdateHealthUI();

        if (effects)
        {
            respawnFlash = 1f;
            SpawnBurst(pos + Vector3.up * 1f, timeColor, 70, 0.22f, 6f, 1f, -0.15f);
            SpawnBurst(pos + Vector3.up * 0.1f, Color.white, 30, 0.16f, 4f, 0.7f, 0.1f);
            StartCoroutine(FlashLight(pos + Vector3.up * 1.2f, timeColor, 6f, 0.9f));
            StartCoroutine(LightPillar(pos));
            StartCoroutine(InvulnerabilityFlicker());
        }
        Debug.Log("Player respawned via time travel!");
    }

    // a CharacterController (or Rigidbody) would undo a plain transform.position change
    void MoveTo(Vector3 pos, Quaternion rot)
    {
        CharacterController cc = GetComponent<CharacterController>();
        bool ccWasOn = cc != null && cc.enabled;
        if (ccWasOn) cc.enabled = false;

        transform.position = pos;
        transform.rotation = rot;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (ccWasOn) cc.enabled = true;
        Physics.SyncTransforms();
    }

    void ResetAnimator()
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;
        anim.Rebind();      // back to the default (Idle) state, triggers cleared
        anim.Update(0f);
    }

    void PauseHeroScripts(bool pause)
    {
        if (pause)
        {
            pausedScripts.Clear();
            foreach (var b in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (b == null || b == this) continue;
                string n = b.GetType().Name;
                bool isHeroScript = n == "ShadowHeroController" || (pauseOtherHeroScripts && n.StartsWith("Hero"));
                if (!isHeroScript) continue;
                pausedScripts[b] = b.enabled;
                b.enabled = false;
            }
        }
        else
        {
            foreach (var kv in pausedScripts)
                if (kv.Key != null) kv.Key.enabled = kv.Value;
            pausedScripts.Clear();
        }
    }

    // ------------------------------------------------------------------ respawn effects

    IEnumerator InvulnerabilityFlicker()
    {
        float end = invulnerableUntil;
        while (Time.time < end)
        {
            SetRenderers(((int)(Time.time * 14f) % 2) == 0);
            yield return null;
        }
        SetRenderers(true);
    }

    void SetRenderers(bool on)
    {
        foreach (var r in renderers)
            if (r != null) r.enabled = on;
    }

    IEnumerator LightPillar(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.name = "TimeTravelPillar";
        go.transform.position = pos + Vector3.up * 1.5f;

        var mat = new Material(Shader.Find("Sprites/Default"));
        var rend = go.GetComponent<Renderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        float t = 0f, d = 0.9f;
        while (t < d)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / d);
            go.transform.localScale = new Vector3(Mathf.Lerp(1.6f, 0.2f, k), Mathf.Lerp(1.2f, 4f, k), Mathf.Lerp(1.6f, 0.2f, k));
            mat.color = new Color(timeColor.r, timeColor.g, timeColor.b, Mathf.Lerp(0.55f, 0f, k));
            yield return null;
        }
        Destroy(go);
    }

    IEnumerator FlashLight(Vector3 pos, Color c, float peak, float duration)
    {
        var go = new GameObject("TimeTravelLight");
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.range = 10f;
        l.shadows = LightShadows.None;
        l.intensity = peak;
        Destroy(go, duration + 0.2f);

        float t = 0f;
        while (t < duration && l != null)
        {
            t += Time.deltaTime;
            l.intensity = Mathf.Lerp(peak, 0f, t / duration);
            yield return null;
        }
    }

    static void SpawnBurst(Vector3 pos, Color color, int count, float size, float speed, float lifetime, float gravity)
    {
        var go = new GameObject("TimeTravelBurst");
        go.transform.position = pos;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(count, 8);

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.35f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SoftMaterial();

        ps.Play();
        Destroy(go, lifetime + 1f);
    }

    static Material SoftMaterial()
    {
        if (softMat != null) return softMat;
        if (softTex == null)
        {
            const int s = 64;
            softTex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            softTex.wrapMode = TextureWrapMode.Clamp;
            float half = s * 0.5f;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = x - half, dy = y - half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / half;
                    float a = Mathf.Clamp01(1f - d);
                    softTex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            softTex.Apply();
        }
        softMat = new Material(Shader.Find("Sprites/Default"));
        softMat.mainTexture = softTex;
        return softMat;
    }

    // ------------------------------------------------------------------ health bar

    void UpdateVisualNumbers()
    {
        float dt = Time.unscaledDeltaTime;

        // the bar drops instantly when hurt and fills up smoothly when healed
        if (currentHealth < displayHealth) displayHealth = currentHealth;
        else displayHealth = Mathf.MoveTowards(displayHealth, currentHealth, maxHealth * 0.8f * dt);

        // the pale "chip" trail drains slowly after a hit
        if (trailHealth < displayHealth) trailHealth = displayHealth;
        else if (Time.time - lastHitTime > 0.4f)
            trailHealth = Mathf.MoveTowards(trailHealth, displayHealth, maxHealth * 0.35f * dt);

        hitFlash = Mathf.MoveTowards(hitFlash, 0f, 2.5f * dt);
        respawnFlash = Mathf.MoveTowards(respawnFlash, 0f, 1.2f * dt);
    }

    void UpdateHealthUI()
    {
        // numbers are handled in UpdateVisualNumbers(); the bar is drawn in OnGUI()
    }

    static void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    static void Shadowed(GUIStyle style, Rect r, string text, Color c)
    {
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, style);
        style.normal.textColor = c;
        GUI.Label(r, text, style);
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;

        if (bigStyle == null)
        {
            bigStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            smallStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            barStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }

        Rect full = new Rect(0f, 0f, Screen.width, Screen.height);

        // red flash when you get hit
        if (hitFlash > 0.01f) Fill(full, new Color(1f, 0.05f, 0.05f, hitFlash * 0.28f));

        // the health bar
        if (showHealthBar && maxHealth > 0)
        {
            float pct = Mathf.Clamp01(displayHealth / maxHealth);
            float trail = Mathf.Clamp01(trailHealth / maxHealth);
            float x = barPosition.x, y = barPosition.y, w = barSize.x, h = barSize.y;

            Fill(new Rect(x - 3f, y - 3f, w + 6f, h + 6f), new Color(0f, 0f, 0f, 0.9f));
            Fill(new Rect(x, y, w, h), new Color(0.12f, 0.05f, 0.05f, 0.95f));
            Fill(new Rect(x, y, w * trail, h), new Color(1f, 0.9f, 0.6f, 0.9f));

            Color fill = Color.Lerp(new Color(0.85f, 0.1f, 0.15f), new Color(0.2f, 0.85f, 0.35f), pct);
            if (pct < 0.3f && !isRespawning)
            {
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 8f);
                fill = new Color(fill.r * pulse, fill.g * pulse, fill.b * pulse, 1f);
            }
            if (hitFlash > 0f) fill = Color.Lerp(fill, Color.white, hitFlash * 0.6f);

            Fill(new Rect(x, y, w * pct, h), fill);
            Fill(new Rect(x, y, w * pct, h * 0.45f), new Color(1f, 1f, 1f, 0.18f));

            barStyle.fontSize = Mathf.RoundToInt(h * 0.6f);
            string label = (string.IsNullOrEmpty(barLabel) ? "" : barLabel + "   ") + currentHealth + " / " + maxHealth;
            Shadowed(barStyle, new Rect(x, y, w, h), label, Color.white);
        }

        // the "time travel" screen while you are dead
        if (isRespawning)
        {
            float p = 1f - Mathf.Clamp01(respawnTimer / Mathf.Max(0.1f, respawnTime));
            float a = Mathf.Clamp01(p * 3f);

            Fill(full, new Color(0.02f, 0.05f, 0.12f, 0.72f * a));

            // rewind scan-lines racing across the screen
            int lines = 14;
            for (int i = 0; i < lines; i++)
            {
                float ly = (Time.time * 500f + i * (Screen.height / (float)lines)) % Screen.height;
                Fill(new Rect(0f, ly, Screen.width, 2f), new Color(timeColor.r, timeColor.g, timeColor.b, 0.28f * a));
            }

            bigStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.075f);
            smallStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.032f);
            Color tc = new Color(timeColor.r, timeColor.g, timeColor.b, a);

            Shadowed(bigStyle, new Rect(0f, Screen.height * 0.36f, Screen.width, Screen.height * 0.12f), "TIME TRAVEL", tc);
            Shadowed(smallStyle, new Rect(0f, Screen.height * 0.48f, Screen.width, Screen.height * 0.06f),
                     "Rewinding...  " + Mathf.CeilToInt(Mathf.Max(0f, respawnTimer)), new Color(1f, 1f, 1f, a));

            float bw = Screen.width * 0.28f, bh = 8f;
            float bx = (Screen.width - bw) * 0.5f, by = Screen.height * 0.56f;
            Fill(new Rect(bx - 2f, by - 2f, bw + 4f, bh + 4f), new Color(0f, 0f, 0f, 0.7f * a));
            Fill(new Rect(bx, by, bw * p, bh), tc);
        }

        // bright flash when you appear again
        if (respawnFlash > 0.01f)
            Fill(full, new Color(0.6f + 0.4f * timeColor.r, 0.8f + 0.2f * timeColor.g, 1f, respawnFlash * 0.8f));
    }
}
