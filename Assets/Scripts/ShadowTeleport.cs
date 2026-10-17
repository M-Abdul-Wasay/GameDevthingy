using System.Collections;
using UnityEngine;

// Add this to the Hero. Press the teleport key: the hero sinks into the floor
// as a shadow, the shadow slides to a new spot within the radius, and the hero rises there.
//
// SPECIAL EFFECTS (all made from code, no assets needed, each can be switched off in the Inspector):
//  - purple shadow smoke + sparks where you vanish
//  - a smoky trail that follows the shadow while it travels
//  - the screen darkens at the edges while you are "in the shadows"
//  - on arrival: smoke burst, rising embers, an expanding shockwave ring, a light flash, a screen flash
//  - camera shake
//  - ARRIVAL BLAST: enemies close to where you appear take damage
[DefaultExecutionOrder(1000)]
public class ShadowTeleport : MonoBehaviour
{
    [Header("Input")]
    public KeyCode teleportKey = KeyCode.Q;

    [Header("Teleport")]
    public float teleportRadius = 8f;        // max distance of the teleport
    public bool randomWithinRadius = false;  // false = go where you steer (WASD), true = random spot
    public float cooldown = 2f;

    [Header("Timing")]
    public float sinkDepth = 2f;    // how far below the floor he goes
    public float sinkTime = 0.35f;
    public float travelTime = 0.4f;
    public float riseTime = 0.35f;

    [Header("Shadow Visual")]
    public float shadowSize = 1.5f;

    [Header("Special effects")]
    public bool enableEffects = true;
    public Color shadowColor = new Color(0.22f, 0.05f, 0.40f, 1f);   // dark purple smoke
    public Color accentColor = new Color(0.65f, 0.25f, 1f, 1f);      // bright sparks / ring / flash
    public float shockwaveRadius = 5f;
    public float shockwaveTime = 0.55f;
    public bool screenVignette = true;
    public float cameraShake = 0.25f;                                // 0 = off
    public AudioClip sinkSound;                                      // optional
    public AudioClip riseSound;                                      // optional

    [Header("Arrival blast (damages enemies)")]
    public bool arrivalBlast = true;
    public float blastRadius = 4f;
    public float blastDamage = 20f;

    public bool IsTeleporting { get; private set; }

    private float nextTime;
    private ShadowHeroController hero;
    private PlayerAnimationController pAnim;
    private CameraFollow camFollow;
    private Renderer[] renderers;

    // screen / camera effect state
    float vignette, vignetteTarget, flash;
    float shakeTimer, shakeLength, shakeStrength;
    Transform shakeCam;
    Vector3 lastShake;

    static Texture2D softTex, vignetteTex;

    void Awake()
    {
        hero = GetComponent<ShadowHeroController>();
        pAnim = GetComponent<PlayerAnimationController>();
        if (Camera.main != null)
            camFollow = Camera.main.GetComponent<CameraFollow>();
    }

    void Update()
    {
        // take last frame's shake off the camera BEFORE the camera script moves it
        if (shakeCam != null && lastShake != Vector3.zero)
        {
            shakeCam.position -= lastShake;
            lastShake = Vector3.zero;
        }

        vignette = Mathf.MoveTowards(vignette, vignetteTarget, Time.deltaTime * 3f);
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 2.5f);

        if (!IsTeleporting && Time.time >= nextTime && Input.GetKeyDown(teleportKey))
            StartCoroutine(DoTeleport());
    }

    // runs AFTER the camera script (execution order 1000), so the shake sits on top of its position
    void LateUpdate()
    {
        if (shakeTimer > 0f && Camera.main != null)
        {
            shakeTimer -= Time.deltaTime;
            shakeCam = Camera.main.transform;
            float k = Mathf.Clamp01(shakeTimer / Mathf.Max(0.01f, shakeLength));
            lastShake = Random.insideUnitSphere * shakeStrength * k;
            shakeCam.position += lastShake;
        }
    }

    IEnumerator DoTeleport()
    {
        IsTeleporting = true;
        nextTime = Time.time + cooldown;

        if (hero != null) hero.enabled = false; // stop walking/jumping while teleporting
        if (pAnim != null) pAnim.SetWalking(false);

        renderers = GetComponentsInChildren<Renderer>();

        Vector3 startPos = transform.position;
        float startGround = GroundY(startPos, startPos.y);

        Vector3 dest = PickDestination(startPos);
        float destGround = GroundY(dest, startGround);

        Transform shadow = CreateShadow();
        shadow.position = new Vector3(startPos.x, startGround + 0.03f, startPos.z);

        if (enableEffects)
        {
            FxDeparture(new Vector3(startPos.x, startGround, startPos.z));
            vignetteTarget = screenVignette ? 1f : 0f;
        }

        // 1. Sink into the floor, shadow grows
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(sinkTime, 0.01f);
            float e = Mathf.SmoothStep(0f, 1f, t);
            Vector3 p = transform.position;
            p.y = Mathf.Lerp(startPos.y, startGround - sinkDepth, e);
            transform.position = p;
            SetShadowScale(shadow, e);
            yield return null;
        }

        SetRenderers(false);

        // 2. Shadow slides to the destination (with a smoky trail)
        GameObject trail = enableEffects ? MakeTrail(shadow) : null;

        Vector3 shadowFrom = new Vector3(startPos.x, startGround + 0.03f, startPos.z);
        Vector3 shadowTo = new Vector3(dest.x, destGround + 0.03f, dest.z);
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(travelTime, 0.01f);
            shadow.position = Vector3.Lerp(shadowFrom, shadowTo, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        if (trail != null) EndTrail(trail);

        // 3. Place the hero under the floor at the destination, then rise
        transform.position = new Vector3(dest.x, destGround - sinkDepth, dest.z);
        SetRenderers(true);

        if (enableEffects)
        {
            FxArrival(new Vector3(dest.x, destGround, dest.z));
            vignetteTarget = 0f;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(riseTime, 0.01f);
            float e = Mathf.SmoothStep(0f, 1f, t);
            Vector3 p = transform.position;
            p.y = Mathf.Lerp(destGround - sinkDepth, destGround, e);
            transform.position = p;
            SetShadowScale(shadow, 1f - e);
            yield return null;
        }

        transform.position = new Vector3(dest.x, destGround, dest.z);
        Destroy(shadow.gameObject);

        if (hero != null) hero.enabled = true;
        IsTeleporting = false;
    }

    // ---------------------------------------------------------------- effects

    void FxDeparture(Vector3 groundPos)
    {
        Burst(groundPos, shadowColor, 40, 2.5f, 1.2f, 1.0f, 0.5f, -0.15f, false);
        Burst(groundPos + Vector3.up * 0.2f, accentColor, 25, 4f, 0.25f, 0.6f, 0.4f, 0f, true);
        Shake(cameraShake * 0.4f, 0.15f);
        if (sinkSound != null) AudioSource.PlayClipAtPoint(sinkSound, groundPos);
    }

    void FxArrival(Vector3 groundPos)
    {
        Burst(groundPos, shadowColor, 70, 4f, 1.6f, 1.2f, 0.6f, -0.1f, false);
        Burst(groundPos + Vector3.up * 0.2f, accentColor, 50, 7f, 0.3f, 0.8f, 0.4f, 0f, true);
        Burst(groundPos, accentColor, 30, 2f, 0.2f, 1.2f, 0.7f, -0.5f, true);   // rising embers

        StartCoroutine(Shockwave(groundPos + Vector3.up * 0.05f, shockwaveRadius, shockwaveTime, accentColor));
        StartCoroutine(LightFlash(groundPos, accentColor));

        flash = 1f;
        Shake(cameraShake, 0.35f);
        if (riseSound != null) AudioSource.PlayClipAtPoint(riseSound, groundPos);

        if (arrivalBlast) Blast(groundPos);
    }

    void Blast(Vector3 center)
    {
        foreach (var e in new System.Collections.Generic.List<EnemyHealth>(EnemyHealth.All))
        {
            if (e == null || e.IsDead) continue;
            Vector3 d = e.transform.position - center;
            d.y = 0f;
            if (d.magnitude <= blastRadius + e.bodyRadius)
                e.TakeDamage(blastDamage);
        }
    }

    void Shake(float strength, float length)
    {
        if (strength <= 0f) return;
        shakeStrength = strength;
        shakeLength = length;
        shakeTimer = length;
    }

    // one-shot particle burst built from code
    void Burst(Vector3 pos, Color color, int count, float speed, float size, float life, float radius, float gravity, bool additive)
    {
        GameObject go = new GameObject("TeleportFX");
        go.SetActive(false);                       // configure first, play after
        go.transform.position = pos;

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.duration = 1f;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.maxParticles = Mathf.Max(count, 10);

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeOut();

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.material = MakeMat(additive, true);

        go.SetActive(true);
        Destroy(go, life + 1.5f);
    }

    // smoky trail that follows the travelling shadow
    GameObject MakeTrail(Transform shadow)
    {
        GameObject go = new GameObject("TeleportTrail");
        go.SetActive(false);
        go.transform.SetParent(shadow, false);
        go.transform.localPosition = Vector3.zero;

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.7f;
        main.startSpeed = 0.3f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.startColor = shadowColor;
        main.gravityModifier = -0.1f;

        var emission = ps.emission;
        emission.rateOverTime = 60f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeOut();

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

        go.GetComponent<ParticleSystemRenderer>().material = MakeMat(false, true);

        go.SetActive(true);
        return go;
    }

    void EndTrail(GameObject trail)
    {
        var ps = trail.GetComponent<ParticleSystem>();
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        trail.transform.SetParent(null);
        Destroy(trail, 1.5f);
    }

    IEnumerator Shockwave(Vector3 center, float maxRadius, float duration, Color c)
    {
        const int points = 48;
        GameObject go = new GameObject("TeleportShockwave");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = points;
        lr.material = MakeMat(true, false);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, duration);
            float e = 1f - (1f - t) * (1f - t);
            float r = Mathf.Lerp(0.2f, maxRadius, e);

            lr.startWidth = lr.endWidth = Mathf.Lerp(0.5f, 0.05f, t);
            lr.startColor = lr.endColor = new Color(c.r, c.g, c.b, 1f - t);

            for (int i = 0; i < points; i++)
            {
                float ang = i / (float)points * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
            }
            yield return null;
        }
        Destroy(go);
    }

    IEnumerator LightFlash(Vector3 pos, Color c)
    {
        GameObject go = new GameObject("TeleportLight");
        go.transform.position = pos + Vector3.up;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.range = 9f;
        l.intensity = 6f;
        l.shadows = LightShadows.None;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.5f;
            l.intensity = Mathf.Lerp(6f, 0f, t);
            yield return null;
        }
        Destroy(go);
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;

        if (vignette > 0.01f)
        {
            GUI.color = new Color(accentColor.r * 0.25f, accentColor.g * 0.08f, accentColor.b * 0.45f, vignette * 0.95f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), VignetteTexture(), ScaleMode.StretchToFill);
        }

        if (flash > 0.01f)
        {
            GUI.color = new Color(accentColor.r, accentColor.g, accentColor.b, flash * 0.45f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        }

        GUI.color = Color.white;
    }

    // ---------------------------------------------------------------- helpers for the effects

    static Gradient FadeOut()
    {
        var g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    static Material MakeMat(bool additive, bool soft)
    {
        Shader sh = additive ? Shader.Find("Legacy Shaders/Particles/Additive") : null;
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        if (soft) m.mainTexture = SoftTexture();
        return m;
    }

    static Texture2D SoftTexture()
    {
        if (softTex != null) return softTex;
        int n = 64;
        softTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        softTex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                softTex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        softTex.Apply();
        return softTex;
    }

    static Texture2D VignetteTexture()
    {
        if (vignetteTex != null) return vignetteTex;
        int n = 128;
        vignetteTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        vignetteTex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.35f) / 0.75f));
                vignetteTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        vignetteTex.Apply();
        return vignetteTex;
    }

    // ---------------------------------------------------------------- original teleport logic (unchanged)

    Vector3 PickDestination(Vector3 from)
    {
        Vector3 dir;
        float dist = teleportRadius;

        if (randomWithinRadius)
        {
            Vector2 r = Random.insideUnitCircle.normalized;
            if (r == Vector2.zero) r = Vector2.up;
            dir = new Vector3(r.x, 0f, r.y);
            dist = Random.Range(teleportRadius * 0.4f, teleportRadius);
        }
        else
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 input = new Vector3(h, 0f, v);

            if (input.sqrMagnitude > 0.01f)
            {
                float yaw = camFollow != null ? camFollow.Yaw : 0f;
                dir = Quaternion.Euler(0f, yaw, 0f) * input.normalized;
            }
            else
            {
                dir = transform.forward;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                dir.Normalize();
            }
        }

        // Don't teleport through walls: stop short of any obstacle in the way
        RaycastHit hit;
        Vector3 origin = from + Vector3.up * 1f;
        if (RaycastIgnoreSelf(origin, dir, dist, out hit))
            dist = Mathf.Max(0f, hit.distance - 0.6f);

        return from + dir * dist;
    }

    float GroundY(Vector3 pos, float fallback)
    {
        RaycastHit hit;
        Vector3 origin = new Vector3(pos.x, pos.y + 3f, pos.z);
        if (RaycastIgnoreSelf(origin, Vector3.down, 200f, out hit))
            return hit.point.y;
        return fallback;
    }

    bool RaycastIgnoreSelf(Vector3 origin, Vector3 dir, float dist, out RaycastHit best)
    {
        best = default(RaycastHit);
        bool found = false;
        float bestDist = float.MaxValue;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(transform)) continue;
            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                best = h;
                found = true;
            }
        }
        return found;
    }

    void SetRenderers(bool on)
    {
        foreach (var r in renderers)
            if (r != null) r.enabled = on;
    }

    Transform CreateShadow()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "TeleportShadow";
        Destroy(go.GetComponent<Collider>());

        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Standard");
        var r = go.GetComponent<Renderer>();
        r.material = new Material(sh) { color = new Color(0f, 0f, 0f, 0.75f) };
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        go.transform.localScale = Vector3.zero;
        return go.transform;
    }

    void SetShadowScale(Transform shadow, float amount)
    {
        float s = shadowSize * Mathf.Clamp01(amount);
        shadow.localScale = new Vector3(s, 0.01f, s);
    }
}
