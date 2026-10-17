using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// When the boss drops to 50% health: it screams (rears back, shakes, slams forward),
/// a blue particle burst + shockwave ring explode out, the screen shakes, and the boss
/// keeps a blue aura of floating particles for the rest of the fight.
///
/// Add this to the SAME object as ProceduralMotion (Boss 1). It tries to find your health
/// variable by itself. If it can't, call OnHealthChanged(current, max) from your damage code.
/// Everything is generated in code: no prefabs or textures needed.
/// </summary>
[DefaultExecutionOrder(1000)]
public class BossPhaseScream : MonoBehaviour
{
    [Header("Trigger")]
    [Range(0.05f, 0.95f)] public float triggerAtHealthPercent = 0.5f;
    public bool autoDetectHealth = true;
    [Tooltip("Optional: exact name of your current-health variable (field or property)")]
    public string healthFieldName = "";
    [Tooltip("Optional: exact name of your max-health variable. Empty = use the highest health seen")]
    public string maxHealthFieldName = "";

    [Header("Scream")]
    public float screamDuration = 2.2f;
    public AudioClip screamClip;
    [Range(0f, 1f)] public float screamVolume = 1f;
    [Tooltip("Drag the boss AI / movement / attack scripts here so he stands still while screaming")]
    public Behaviour[] disableDuringScream;
    [Tooltip("Automatically freezes BossChase and BossSkills while he screams")]
    public bool autoPauseBossScripts = true;
    public UnityEvent onScreamStart;
    public UnityEvent onScreamEnd;

    [Header("Blue effects")]
    public Color blueColor = new Color(0.25f, 0.65f, 1f, 1f);
    public bool burst = true;
    public bool shockwaveRing = true;
    public bool auraAfterwards = true;
    public bool pulseLight = true;
    public float lightPeak = 5f;
    public float lightSettle = 1.5f;
    public bool tintBody = true;
    [Range(0f, 1f)] public float tintStrength = 0.45f;
    [Tooltip("Camera shake strength in world units (0 = off)")]
    public float cameraShake = 0.15f;

    // ------------------------------------------------------------------ internals

    static readonly string[] CurNames =
        { "currentHealth", "health", "hp", "currentHP", "currentHp", "curHealth", "CurrentHealth", "Health", "HP" };
    static readonly string[] MaxNames =
        { "maxHealth", "maxHP", "maxHp", "startingHealth", "startHealth", "initialHealth", "MaxHealth", "MaxHP" };

    class TintTarget
    {
        public Renderer r;
        public int prop;
        public Color orig;
        public MaterialPropertyBlock mpb = new MaterialPropertyBlock();
    }

    ProceduralMotion motion;
    EnemyHealth eh;
    readonly List<Behaviour> autoPaused = new List<Behaviour>();
    readonly Dictionary<Behaviour, bool> priorState = new Dictionary<Behaviour, bool>();
    Vector3 centerLocal;
    float height = 2f, radius = 0.6f;

    object curTarget, maxTarget;
    MemberInfo curMember, maxMember;
    float observedMax;

    bool triggered, phaseDead;
    ParticleSystem auraPS;
    Light glow;
    readonly List<TintTarget> tints = new List<TintTarget>();
    Material additiveMat, alphaMat;
    static Texture2D softTex;

    Camera cam;
    float shakeAmount;
    Vector3 lastBase, lastShaken;
    bool hasShaken;

    Vector3 Center { get { return transform.TransformPoint(centerLocal); } }

    // ------------------------------------------------------------------ setup

    void Start()
    {
        motion = GetComponent<ProceduralMotion>();
        if (motion == null) motion = GetComponentInChildren<ProceduralMotion>();
        if (motion == null) motion = GetComponentInParent<ProceduralMotion>();

        CollectRenderers();

        eh = GetComponent<EnemyHealth>();
        if (eh == null) eh = GetComponentInChildren<EnemyHealth>();
        if (eh == null) eh = GetComponentInParent<EnemyHealth>();

        if (autoPauseBossScripts)
        {
            foreach (var b in GetComponents<Behaviour>())
            {
                if (b == null || b == this) continue;
                string n = b.GetType().Name;
                if (n == "BossChase" || n == "BossSkills") autoPaused.Add(b);
            }
        }

        if (eh != null)
        {
            Debug.Log("BossPhaseScream: reading health from EnemyHealth (scream at " +
                      Mathf.RoundToInt(triggerAtHealthPercent * 100f) + "%)", this);
        }
        else if (autoDetectHealth || !string.IsNullOrEmpty(healthFieldName))
        {
            bool okCur = FindMember(CurNames, healthFieldName, out curTarget, out curMember);
            FindMember(MaxNames, maxHealthFieldName, out maxTarget, out maxMember);

            if (okCur)
            {
                Debug.Log("BossPhaseScream: reading health from " + curMember.DeclaringType.Name + "." + curMember.Name);
                float c, m;
                TryReadHealth(out c, out m);   // primes observedMax
            }
            else
            {
                Debug.LogWarning("BossPhaseScream: couldn't find a health variable. " +
                                 "Either type its exact name in 'Health Field Name', or call " +
                                 "OnHealthChanged(current, max) from your damage code.");
            }
        }
    }

    void CollectRenderers()
    {
        bool has = false;
        Bounds b = new Bounds(transform.position, new Vector3(1f, 2f, 1f));
        int baseId = Shader.PropertyToID("_BaseColor");
        int colId = Shader.PropertyToID("_Color");

        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);

            var mat = r.sharedMaterial;
            if (mat == null) continue;
            int id = mat.HasProperty(baseId) ? baseId : (mat.HasProperty(colId) ? colId : -1);
            if (id < 0) continue;
            tints.Add(new TintTarget { r = r, prop = id, orig = mat.GetColor(id) });
        }

        centerLocal = transform.InverseTransformPoint(b.center);
        height = Mathf.Max(b.size.y, 0.5f);
        radius = Mathf.Max(Mathf.Max(b.extents.x, b.extents.z), 0.2f);
    }

    // ------------------------------------------------------------------ health

    void Update()
    {
        if (phaseDead) return;

        float cur, max;
        if (!TryReadHealth(out cur, out max)) return;

        if (!triggered) OnHealthChanged(cur, max);
        else if (cur <= 0f) { phaseDead = true; StopPhaseTwo(); }
    }

    /// <summary>Call this yourself if auto-detect can't find your health variable.</summary>
    public void OnHealthChanged(float current, float max)
    {
        if (triggered || max <= 0f || current <= 0f) return;
        if (current / max <= triggerAtHealthPercent) TriggerScream();
    }

    [ContextMenu("Test Scream")]
    public void TriggerScream()
    {
        if (triggered) return;
        triggered = true;
        StartCoroutine(ScreamRoutine());
    }

    /// <summary>Lets the boss scream again (for respawns / restarting the fight).</summary>
    public void ResetPhase()
    {
        StopAllCoroutines();
        triggered = false; phaseDead = false; observedMax = 0f;
        shakeAmount = 0f;
        if (auraPS != null) Destroy(auraPS.gameObject);
        if (glow != null) Destroy(glow.gameObject);
        ApplyTint(0f);
        SetBehaviours(true);
    }

    bool TryReadHealth(out float cur, out float max)
    {
        cur = 0f; max = 0f;

        if (eh != null)
        {
            cur = eh.CurrentHealth;
            max = eh.maxHealth;
            return true;
        }

        if (!ReadMember(curTarget, curMember, out cur)) return false;

        if (!ReadMember(maxTarget, maxMember, out max))
        {
            observedMax = Mathf.Max(observedMax, cur);
            max = observedMax;
        }
        return true;
    }

    static bool ReadMember(object target, MemberInfo m, out float v)
    {
        v = 0f;
        if (target == null || m == null) return false;
        var uo = target as UnityEngine.Object;
        if (target is UnityEngine.Object && uo == null) return false;   // destroyed Unity object

        object o = null;
        var f = m as FieldInfo;
        var p = m as PropertyInfo;
        if (f != null) o = f.GetValue(target);
        else if (p != null && p.CanRead && p.GetIndexParameters().Length == 0) o = p.GetValue(target, null);

        if (o is float || o is int || o is double || o is long || o is short)
        {
            v = System.Convert.ToSingle(o);
            return true;
        }
        return false;
    }

    bool FindMember(string[] defaults, string explicitName, out object target, out MemberInfo member)
    {
        target = null; member = null;

        var comps = new List<MonoBehaviour>();
        comps.AddRange(GetComponentsInParent<MonoBehaviour>(true));
        comps.AddRange(GetComponentsInChildren<MonoBehaviour>(true));

        string[] names = string.IsNullOrEmpty(explicitName) ? defaults : new[] { explicitName };
        const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        foreach (var n in names)
        {
            foreach (var c in comps)
            {
                if (c == null || c == this || c is ProceduralMotion) continue;
                var t = c.GetType();

                var f = t.GetField(n, F);
                if (f != null && IsNumber(f.FieldType)) { target = c; member = f; return true; }

                var p = t.GetProperty(n, F);
                if (p != null && p.CanRead && IsNumber(p.PropertyType)) { target = c; member = p; return true; }
            }
        }
        return false;
    }

    static bool IsNumber(System.Type t)
    {
        return t == typeof(float) || t == typeof(int) || t == typeof(double) || t == typeof(long) || t == typeof(short);
    }

    // ------------------------------------------------------------------ the scream

    IEnumerator ScreamRoutine()
    {
        SetBehaviours(false);
        onScreamStart.Invoke();
        if (motion != null) motion.PlayScream(screamDuration);
        if (screamClip != null) AudioSource.PlayClipAtPoint(screamClip, Center, screamVolume);
        if (pulseLight) CreateGlow();

        float t0 = Time.time;
        bool fired = false;

        while (true)
        {
            float t = (Time.time - t0) / screamDuration;
            if (t >= 1f) break;

            // mouth opens at 20%: explode the particles
            if (!fired && t >= 0.2f) { fired = true; FireBurst(); }

            // loudest between 20% and 80% of the scream
            float env = Mathf.Clamp01(Mathf.InverseLerp(0.15f, 0.35f, t)) *
                        (1f - Mathf.InverseLerp(0.8f, 1f, t));

            shakeAmount = cameraShake * env;
            if (glow != null)
                glow.intensity = lightPeak * Mathf.Clamp01(env * 1.3f) * (0.85f + 0.15f * Mathf.Sin(Time.time * 40f));
            if (tintBody)
                ApplyTint(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.6f, t)));

            yield return null;
        }

        shakeAmount = 0f;
        if (tintBody) ApplyTint(1f);
        SetBehaviours(true);
        onScreamEnd.Invoke();

        // settle the light into a steady blue glow (or fade it out)
        if (glow != null)
        {
            float from = glow.intensity;
            float to = auraAfterwards ? lightSettle : 0f;
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.6f)
            {
                if (glow == null) yield break;
                glow.intensity = Mathf.Lerp(from, to, k);
                yield return null;
            }
            if (glow != null)
            {
                glow.intensity = to;
                if (to <= 0f) glow.enabled = false;
            }
        }
    }

    void SetBehaviours(bool on)
    {
        if (on && eh != null && eh.IsDead) return;   // never bring a dead boss back to life

        if (disableDuringScream != null)
            foreach (var b in disableDuringScream) ToggleBehaviour(b, on);
        foreach (var b in autoPaused) ToggleBehaviour(b, on);
    }

    void ToggleBehaviour(Behaviour b, bool on)
    {
        if (b == null) return;
        if (!on)
        {
            priorState[b] = b.enabled;   // remember if it was awake or still asleep
            b.enabled = false;
        }
        else
        {
            bool was;
            b.enabled = priorState.TryGetValue(b, out was) ? was : true;
        }
    }

    // EnemyHealth switches off every script when the boss dies: stop the blue aura with it
    void OnDisable()
    {
        if (triggered && eh != null && eh.IsDead) StopPhaseTwo();
    }

    // ------------------------------------------------------------------ particles

    void FireBurst()
    {
        Vector3 c = Center;

        if (burst)
        {
            var ps = NewSystem("Blue Burst", c, Quaternion.identity, true);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(height * 1.2f, height * 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(height * 0.05f, height * 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(blueColor, new Color(0.75f, 0.92f, 1f, 1f));
            main.gravityModifier = -0.15f;
            main.maxParticles = 300;

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 200) });

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = radius * 0.5f;

            FadeOut(ps);
            ps.Play();
            Destroy(ps.gameObject, 5f);
        }

        if (shockwaveRing)
        {
            Vector3 feet = c - Vector3.up * (height * 0.5f) + Vector3.up * 0.1f;
            var ps = NewSystem("Blue Shockwave", feet, Quaternion.Euler(90f, 0f, 0f), true);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = 0.7f;
            main.startSpeed = height * 3.5f;
            main.startSize = height * 0.09f;
            main.startColor = new ParticleSystem.MinMaxGradient(blueColor, new Color(0.8f, 0.95f, 1f, 1f));
            main.maxParticles = 200;

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 120) });

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.2f;
            sh.radiusThickness = 0f;   // emit from the edge only, so it forms a ring

            FadeOut(ps);
            ps.Play();
            Destroy(ps.gameObject, 3f);
        }

        if (auraAfterwards && auraPS == null) CreateAura();
    }

    void CreateAura()
    {
        auraPS = NewSystem("Blue Aura", Center, Quaternion.identity, true);
        var main = auraPS.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSpeed = 0.1f;
        main.startSize = new ParticleSystem.MinMaxCurve(height * 0.03f, height * 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(blueColor, new Color(0.75f, 0.92f, 1f, 1f));
        main.gravityModifier = -0.08f;   // motes drift upward
        main.maxParticles = 200;

        var em = auraPS.emission;
        em.rateOverTime = 25f;

        var sh = auraPS.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(radius * 1.6f, height * 0.9f, radius * 1.6f);

        // fade in and out so motes never pop
        var col = auraPS.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var size = auraPS.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        auraPS.Play();
    }

    void CreateGlow()
    {
        if (glow != null) return;
        var go = new GameObject("Phase2 Glow");
        go.transform.SetParent(transform, true);
        go.transform.position = Center;
        glow = go.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = blueColor;
        glow.range = height * 3.5f;
        glow.shadows = LightShadows.None;
        glow.intensity = 0f;
    }

    void StopPhaseTwo()
    {
        if (auraPS != null)
        {
            var em = auraPS.emission;
            em.enabled = false;
        }
        if (glow != null) glow.enabled = false;
    }

    static void FadeOut(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
    }

    ParticleSystem NewSystem(string n, Vector3 worldPos, Quaternion worldRot, bool additive)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, true);
        go.transform.position = worldPos;
        go.transform.rotation = worldRot;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.sharedMaterial = GetMaterial(additive);
        return ps;
    }

    Material GetMaterial(bool additive)
    {
        if (additive && additiveMat != null) return additiveMat;
        if (!additive && alphaMat != null) return alphaMat;

        Shader sh = additive ? Shader.Find("Legacy Shaders/Particles/Additive") : null;
        if (sh == null) sh = Shader.Find("Sprites/Default");   // always available, works in Built-in and URP

        var m = new Material(sh);
        m.mainTexture = SoftTexture();
        if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));

        if (additive) additiveMat = m; else alphaMat = m;
        return m;
    }

    static Texture2D SoftTexture()
    {
        if (softTex != null) return softTex;
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
                a *= a;
                softTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        softTex.Apply();
        return softTex;
    }

    // ------------------------------------------------------------------ tint

    void ApplyTint(float blend)
    {
        foreach (var t in tints)
        {
            if (t.r == null) continue;
            t.r.GetPropertyBlock(t.mpb);
            t.mpb.SetColor(t.prop, Color.Lerp(t.orig, blueColor, tintStrength * blend));
            t.r.SetPropertyBlock(t.mpb);
        }
    }

    // ------------------------------------------------------------------ camera shake

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        Transform t = cam.transform;

        if (shakeAmount > 0.0005f)
        {
            // if a follow-camera moved us since last frame, treat its position as the new base
            Vector3 b = (hasShaken && t.position == lastShaken) ? lastBase : t.position;
            Vector3 off = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * shakeAmount;
            Vector3 shaken = b + t.TransformDirection(off);
            t.position = shaken;
            lastBase = b;
            lastShaken = shaken;
            hasShaken = true;
        }
        else if (hasShaken)
        {
            t.position = (t.position == lastShaken) ? lastBase : t.position;
            hasShaken = false;
        }
    }
}
