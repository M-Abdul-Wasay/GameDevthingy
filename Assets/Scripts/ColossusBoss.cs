using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

// Boss 3 (Voidforge Colossus). Put this in Assets/Scripts and add it to the Boss 3 object (the one with the Animator).
// Do NOT also add ProceduralMotion to it, this script does its own body motion.
//
// What it does:
//  - walks / runs at the hero with SMOOTH acceleration, turning and body sway (no snapping)
//  - every few seconds picks an attack:
//      JUMP SLAM  : crouches, leaps over to where the hero stands (red circle warns you), lands, shakes the
//                   screen and ERUPTS LAVA: a shockwave, flying lava chunks and lava geysers around the landing spot
//      LAVA BALLS : rears back and fires a fan of glowing lava balls that explode on impact
//  - the hero is NOT hit while teleporting (ShadowTeleport), so teleporting is a real dodge
//  - needs a health script on the hero with a method called TakeDamage(float) or TakeDamage(int)
[DefaultExecutionOrder(1000)]
public class ColossusBoss : MonoBehaviour
{
    [Header("Target (auto-found if empty)")]
    public Transform target;
    public float aggroRange = 70f;

    [Header("Health (only used if the boss has no EnemyHealth yet)")]
    public string bossName = "Voidforge Colossus";
    public float maxHealth = 600f;

    [Header("Movement (smooth)")]
    public float moveSpeed = 4f;
    public float speedSmoothTime = 0.45f;   // higher = slower, softer speed changes
    public float turnSpeed = 4f;            // higher = turns faster (still smooth)
    public float stopDistance = 3.5f;
    public float groundOffset = 0f;         // only used when there is no NavMeshAgent
    public float stepFrequency = 1.6f;      // how fast the body sways while walking

    [Header("Attack timing")]
    public float firstAttackDelay = 2f;
    public float attackCooldown = 3.5f;     // pause between attacks (it keeps chasing meanwhile)
    [Range(0f, 1f)] public float jumpChance = 0.5f;

    [Header("Jump slam")]
    public float jumpMinDistance = 6f;
    public float jumpMaxDistance = 25f;
    public float jumpHeight = 6f;
    public float crouchTime = 0.6f;
    public float airTime = 1.0f;
    public float recoverTime = 0.8f;
    public float slamRadius = 7f;
    public float slamDamage = 30f;

    [Header("Lava eruption after landing")]
    public int geyserCount = 10;
    public float eruptionRadius = 11f;
    public float geyserRadius = 1.8f;
    public float geyserDamage = 20f;
    public float geyserTelegraph = 0.7f;

    [Header("Lava balls")]
    public int ballCount = 5;
    public float ballSpeed = 16f;
    public float ballSpread = 12f;          // degrees, the balls fan out
    public float ballDamage = 15f;
    public float ballInterval = 0.25f;
    public float ballWindup = 0.8f;
    public float ballHitRadius = 1.1f;
    public float ballSplashRadius = 2.5f;
    public Transform muzzle;                // optional: drag a hand/chest bone here
    public float muzzleHeight = 3f;         // used when no muzzle is set

    [Header("Look")]
    public Color lavaColor = new Color(1f, 0.35f, 0.05f, 1f);
    public Color lavaDark = new Color(0.30f, 0.05f, 0f, 1f);
    public float shakeOnSlam = 0.5f;
    public float poseSmooth = 12f;          // higher = body offsets react faster

    NavMeshAgent agent;
    Animator anim;
    EnemyHealth health;
    bool hasSpeed, hasRunSpeed;

    bool busy, airborne;
    float curSpeed, speedVel, nextPath, walkPhase, nextFind;

    // body pose layer (offsets applied to the children, like ProceduralMotion)
    Transform[] parts;
    Vector3[] prePos;
    Quaternion[] preRot;
    bool applied;
    bool poseOverride;
    Vector3 ovPos;
    float ovPitch, ovYaw, ovRoll;
    Vector3 curPos;
    float curPitch, curYaw, curRoll;

    // camera shake
    float shakeTimer, shakeLength, shakeStrength;
    Transform shakeCam;
    Vector3 lastShake;

    static Texture2D softTex;

    // ------------------------------------------------------------------ setup

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.updateRotation = false;   // we turn the boss ourselves, smoothly
            agent.acceleration = 200f;      // we smooth the speed ourselves
            agent.stoppingDistance = 0f;
        }

        anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (var p in anim.parameters)
            {
                if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) hasSpeed = true;
                if (p.name == "RunSpeed" && p.type == AnimatorControllerParameterType.Float) hasRunSpeed = true;
            }
        }

        health = GetComponent<EnemyHealth>();
        if (health == null)
        {
            health = gameObject.AddComponent<EnemyHealth>();
            health.Setup(maxHealth, true, 2.5f, -1f, false);
            health.displayName = bossName;
        }

        int n = transform.childCount;
        parts = new Transform[n];
        prePos = new Vector3[n];
        preRot = new Quaternion[n];
        for (int i = 0; i < n; i++)
        {
            parts[i] = transform.GetChild(i);
            prePos[i] = parts[i].localPosition;
            preRot[i] = parts[i].localRotation;
        }
    }

    void Start()
    {
        StartCoroutine(Brain());
    }

    Transform FindTarget()
    {
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) return go.transform;

        var shc = FindObjectOfType<ShadowHeroController>();
        if (shc != null) return shc.transform;

        var ha = FindObjectOfType<HeroAttack>();
        if (ha != null) return ha.transform;

        go = GameObject.Find("Hero");
        return go != null ? go.transform : null;
    }

    // ------------------------------------------------------------------ per frame

    void Update()
    {
        // take last frame's pose + shake off BEFORE the Animator / camera scripts run
        if (applied)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].localPosition = prePos[i];
                parts[i].localRotation = preRot[i];
            }
        }
        if (shakeCam != null && lastShake != Vector3.zero)
        {
            shakeCam.position -= lastShake;
            lastShake = Vector3.zero;
        }

        if (health != null && health.IsDead) return;

        if (target == null && Time.time >= nextFind)
        {
            target = FindTarget();
            nextFind = Time.time + 0.5f;
        }

        Locomotion();
        DriveAnimator();
    }

    void Locomotion()
    {
        Vector3 to = Vector3.zero;
        float dist = 0f;
        if (target != null)
        {
            to = target.position - transform.position;
            to.y = 0f;
            dist = to.magnitude;
        }

        bool chase = !busy && target != null && dist > stopDistance && dist < aggroRange;
        float desired = chase ? moveSpeed : 0f;
        curSpeed = Mathf.SmoothDamp(curSpeed, desired, ref speedVel, Mathf.Max(0.05f, speedSmoothTime));
        if (curSpeed < 0.01f) curSpeed = 0f;

        bool agentOn = agent != null && agent.enabled && agent.isOnNavMesh;

        // smooth turning
        Vector3 face = to;
        if (agentOn && chase && agent.velocity.sqrMagnitude > 0.5f) face = agent.velocity;
        face.y = 0f;
        if (!airborne && face.sqrMagnitude > 0.01f)
        {
            Quaternion want = Quaternion.LookRotation(face.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }

        if (airborne) return;

        if (agentOn)
        {
            agent.speed = Mathf.Max(0.05f, curSpeed);
            agent.isStopped = curSpeed < 0.05f;
            if (chase && Time.time >= nextPath)
            {
                agent.SetDestination(target.position);
                nextPath = Time.time + 0.2f;
            }
        }
        else
        {
            transform.position += transform.forward * (curSpeed * Time.deltaTime);
            SnapToGround();
        }
    }

    void DriveAnimator()
    {
        if (anim == null) return;
        float s01 = Mathf.Clamp01(curSpeed / Mathf.Max(0.01f, moveSpeed));
        if (hasSpeed) anim.SetFloat("Speed", curSpeed);
        if (hasRunSpeed) anim.SetFloat("RunSpeed", Mathf.Lerp(0.5f, 1.3f, s01));
    }

    void SnapToGround()
    {
        float gy;
        if (RaycastGround(transform.position + Vector3.up * 3f, out gy))
        {
            Vector3 p = transform.position;
            p.y = Mathf.Lerp(p.y, gy + groundOffset, 1f - Mathf.Exp(-15f * Time.deltaTime));
            transform.position = p;
        }
    }

    bool RaycastGround(Vector3 origin, out float y)
    {
        y = 0f;
        bool found = false;
        float best = float.MaxValue;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 150f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(transform)) continue;
            if (h.distance < best)
            {
                best = h.distance;
                y = h.point.y;
                found = true;
            }
        }
        return found;
    }

    // runs AFTER the camera script (execution order 1000)
    void LateUpdate()
    {
        if (health != null && health.IsDead) return;

        // ---- body pose
        if (parts.Length > 0)
        {
            Vector3 tp;
            float pitch, yaw, roll;

            if (poseOverride)
            {
                tp = ovPos; pitch = ovPitch; yaw = ovYaw; roll = ovRoll;
            }
            else
            {
                float s01 = Mathf.Clamp01(curSpeed / Mathf.Max(0.01f, moveSpeed));
                walkPhase += curSpeed * Time.deltaTime * stepFrequency;
                float idle = Time.time * 1.4f;
                float bob = Mathf.Lerp((Mathf.Sin(idle) * 0.5f + 0.5f) * 0.03f, Mathf.Abs(Mathf.Sin(walkPhase)) * 0.09f, s01);
                tp = new Vector3(0f, bob, 0f);
                pitch = 7f * s01;
                yaw = 0f;
                roll = Mathf.Lerp(Mathf.Sin(idle * 0.5f) * 1.5f, Mathf.Sin(walkPhase) * 3f, s01);
            }

            float a = 1f - Mathf.Exp(-poseSmooth * Time.deltaTime);
            curPos = Vector3.Lerp(curPos, tp, a);
            curPitch = Mathf.Lerp(curPitch, pitch, a);
            curYaw = Mathf.Lerp(curYaw, yaw, a);
            curRoll = Mathf.Lerp(curRoll, roll, a);

            Quaternion q = Quaternion.Euler(curPitch, curYaw, curRoll);
            for (int i = 0; i < parts.Length; i++)
            {
                prePos[i] = parts[i].localPosition;
                preRot[i] = parts[i].localRotation;
                parts[i].localPosition = curPos + q * prePos[i];
                parts[i].localRotation = q * preRot[i];
            }
            applied = true;
        }

        // ---- camera shake
        if (shakeTimer > 0f && Camera.main != null)
        {
            shakeTimer -= Time.deltaTime;
            shakeCam = Camera.main.transform;
            float k = Mathf.Clamp01(shakeTimer / Mathf.Max(0.01f, shakeLength));
            lastShake = Random.insideUnitSphere * shakeStrength * k;
            shakeCam.position += lastShake;
        }
    }

    void Shake(float strength, float length)
    {
        if (strength <= 0f) return;
        shakeStrength = Mathf.Max(shakeStrength * Mathf.Clamp01(shakeTimer / Mathf.Max(0.01f, shakeLength)), strength);
        shakeLength = length;
        shakeTimer = length;
    }

    // ------------------------------------------------------------------ attack brain

    IEnumerator Brain()
    {
        yield return new WaitForSeconds(firstAttackDelay);

        while (true)
        {
            yield return new WaitForSeconds(attackCooldown);   // keeps chasing meanwhile
            if (health != null && health.IsDead) yield break;
            if (target == null) continue;

            Vector3 d = target.position - transform.position;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > aggroRange) continue;

            bool canJump = dist >= jumpMinDistance && dist <= jumpMaxDistance;

            busy = true;
            if (canJump && Random.value <= jumpChance) yield return JumpSlam();
            else yield return LavaBalls();
            busy = false;
        }
    }

    // ------------------------------------------------------------------ JUMP SLAM

    IEnumerator JumpSlam()
    {
        Vector3 start = transform.position;
        Vector3 land = target.position;
        land.y = start.y;
        float gy;
        if (RaycastGround(land + Vector3.up * 10f, out gy)) land.y = gy;

        // red warning circle where he will land
        GameObject marker = MakeDisc(land + Vector3.up * 0.06f, slamRadius * 2f, new Color(1f, 0.15f, 0.05f, 0.6f), 8f);

        // 1. crouch
        poseOverride = true;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, crouchTime);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            ovPos = new Vector3(0f, -0.45f * k, 0f);
            ovPitch = 22f * k;
            ovYaw = 0f;
            ovRoll = Mathf.Sin(Time.time * 40f) * 2f * k;
            yield return null;
        }

        // 2. leap (the agent is switched off while we fly)
        airborne = true;
        if (agent != null && agent.enabled) agent.enabled = false;

        Vector3 flat = land - start;
        flat.y = 0f;
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.1f, airTime);
            float u = Mathf.Clamp01(t);

            Vector3 p = Vector3.Lerp(start, land, u);
            p.y = Mathf.Lerp(start.y, land.y, u) + 4f * jumpHeight * u * (1f - u);
            transform.position = p;

            if (flat.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat.normalized), 1f - Mathf.Exp(-10f * Time.deltaTime));

            ovPos = new Vector3(0f, 0.2f * Mathf.Sin(u * Mathf.PI), 0f);
            ovPitch = Mathf.Lerp(-15f, 35f, u);
            ovRoll = 0f;
            yield return null;
        }
        transform.position = land;
        airborne = false;

        // 3. impact: shockwave, flying lava, geysers, damage
        if (marker != null) Destroy(marker);
        Impact(land);

        // 4. recover from the landing crouch
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, recoverTime);
            float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            ovPos = new Vector3(0f, -0.6f * k, 0f);
            ovPitch = 25f * k;
            yield return null;
        }
        poseOverride = false;

        ReenableAgent();
    }

    void ReenableAgent()
    {
        if (agent == null) return;
        NavMeshHit nh;
        if (NavMesh.SamplePosition(transform.position, out nh, 4f, NavMesh.AllAreas))
        {
            agent.enabled = true;
            agent.Warp(nh.position);
        }
        // no NavMesh nearby: the boss just keeps walking without the agent
    }

    void Impact(Vector3 p)
    {
        Shake(shakeOnSlam, 0.5f);
        StartCoroutine(Shockwave(p + Vector3.up * 0.05f, slamRadius * 1.5f, 0.6f, lavaColor));
        Burst(p, lavaColor, 90, 12f, 0.4f, 1.6f, 0.8f, 2.2f, true, true);      // lava chunks flying up
        Burst(p, lavaDark, 50, 5f, 1.8f, 1.5f, 1f, -0.1f, false, false);       // dark smoke
        StartCoroutine(LightFlash(p, lavaColor, 12f, 12f));

        if (target != null && FlatDistance(target.position, p) <= slamRadius)
            BossDamage.Hero(target, slamDamage);

        for (int i = 0; i < geyserCount; i++)
        {
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(slamRadius * 0.5f, eruptionRadius);
            Vector3 gp = p + new Vector3(r.x, 0f, r.y);
            float gy;
            gp.y = RaycastGround(gp + Vector3.up * 10f, out gy) ? gy : p.y;
            StartCoroutine(Geyser(gp, Random.Range(0.1f, 1.0f)));
        }
    }

    IEnumerator Geyser(Vector3 pos, float delay)
    {
        yield return new WaitForSeconds(delay);

        // warning glow, pulsing
        GameObject disc = MakeDisc(pos + Vector3.up * 0.05f, geyserRadius * 2f, lavaColor, 10f);
        float t = 0f;
        while (t < geyserTelegraph)
        {
            t += Time.deltaTime;
            SetDiscAlpha(disc, 0.35f + 0.35f * Mathf.Abs(Mathf.Sin(t * 14f)));
            yield return null;
        }

        // erupt
        Burst(pos, lavaColor, 70, 16f, 0.45f, 1.1f, 0.3f, 1.8f, true, true);
        Burst(pos, lavaDark, 25, 6f, 1.4f, 1.0f, 0.5f, -0.1f, false, true);
        StartCoroutine(LightFlash(pos, lavaColor, 6f, 6f));
        Shake(0.15f, 0.15f);

        if (target != null && FlatDistance(target.position, pos) <= geyserRadius)
            BossDamage.Hero(target, geyserDamage);

        StartCoroutine(FadeAndDestroy(disc, 3f));
    }

    // ------------------------------------------------------------------ LAVA BALLS

    IEnumerator LavaBalls()
    {
        poseOverride = true;

        // rear back, glowing
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, ballWindup);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            ovPos = new Vector3(0f, 0.3f * k, 0f);
            ovPitch = -20f * k;
            ovYaw = 0f;
            ovRoll = Mathf.Sin(Time.time * 35f) * 2f * k;
            yield return null;
        }

        for (int i = 0; i < ballCount; i++)
        {
            float yaw = ballCount > 1 ? Mathf.Lerp(-ballSpread, ballSpread, i / (float)(ballCount - 1)) : 0f;
            FireBall(yaw);

            // recoil, then ease back
            ovPitch = 14f;
            ovPos = new Vector3(0f, 0.1f, -0.15f);
            Shake(0.12f, 0.12f);

            float w = 0f;
            while (w < ballInterval)
            {
                w += Time.deltaTime;
                ovPitch = Mathf.Lerp(ovPitch, -12f, 6f * Time.deltaTime);
                yield return null;
            }
        }

        ovPos = Vector3.zero;
        ovPitch = 0f;
        ovRoll = 0f;
        yield return new WaitForSeconds(0.5f);
        poseOverride = false;
    }

    void FireBall(float yawOffset)
    {
        if (target == null) return;

        Vector3 from = muzzle != null ? muzzle.position
                                      : transform.position + Vector3.up * muzzleHeight + transform.forward * 1.2f;
        Vector3 aim = target.position + Vector3.up * 1f;
        Vector3 dir = (aim - from).normalized;
        dir = Quaternion.AngleAxis(yawOffset, Vector3.up) * dir;

        StartCoroutine(BallRoutine(from, dir * ballSpeed));
    }

    IEnumerator BallRoutine(Vector3 pos, Vector3 vel)
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "LavaBall";
        Destroy(ball.GetComponent<Collider>());
        ball.transform.position = pos;
        ball.transform.localScale = Vector3.one * 0.9f;
        Destroy(ball, 8f);   // safety, in case the boss dies mid-flight

        var r = ball.GetComponent<Renderer>();
        Shader sh = Shader.Find("Unlit/Color");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        r.material = new Material(sh);
        SetMatColor(r.material, lavaColor);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        var lg = new GameObject("BallLight");
        lg.transform.SetParent(ball.transform, false);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = lavaColor;
        l.range = 7f;
        l.intensity = 3f;
        l.shadows = LightShadows.None;

        GameObject trail = MakeTrail(ball.transform, lavaColor);

        Vector3 g = Physics.gravity * 0.35f;
        float life = 6f;
        bool exploded = false;

        while (life > 0f && ball != null)
        {
            life -= Time.deltaTime;
            Vector3 prev = ball.transform.position;
            vel += g * Time.deltaTime;
            Vector3 next = prev + vel * Time.deltaTime;

            RaycastHit hit;
            if (Physics.Linecast(prev, next, out hit, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
            {
                ExplodeBall(hit.point);
                exploded = true;
                break;
            }

            ball.transform.position = next;

            if (target != null && Vector3.Distance(next, target.position + Vector3.up) <= ballHitRadius)
            {
                ExplodeBall(next);
                exploded = true;
                break;
            }
            yield return null;
        }

        if (trail != null) EndTrail(trail);
        if (ball != null) Destroy(ball);
    }

    void ExplodeBall(Vector3 p)
    {
        Burst(p, lavaColor, 40, 7f, 0.3f, 0.9f, 0.3f, 2f, true, false);
        Burst(p, lavaDark, 15, 3f, 1.0f, 0.8f, 0.4f, -0.1f, false, false);
        StartCoroutine(LightFlash(p, lavaColor, 5f, 5f));
        Shake(0.1f, 0.15f);

        if (target != null && Vector3.Distance(p, target.position + Vector3.up) <= ballSplashRadius)
            BossDamage.Hero(target, ballDamage);

        // glowing scorch mark if it landed on the ground
        float gy;
        if (RaycastGround(p + Vector3.up * 0.5f, out gy) && p.y - gy < 1.5f)
        {
            GameObject disc = MakeDisc(new Vector3(p.x, gy + 0.05f, p.z), ballSplashRadius * 1.2f, lavaColor, 8f);
            StartCoroutine(FadeAndDestroy(disc, 3f));
        }
    }

    // ------------------------------------------------------------------ effects (all built from code)

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void Burst(Vector3 pos, Color color, int count, float speed, float size, float life, float radius, float gravity, bool additive, bool up)
    {
        GameObject go = new GameObject("LavaFX");
        go.SetActive(false);
        go.transform.position = pos;
        if (up) go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // cone points up

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.duration = 1f;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, new Color(1f, 0.85f, 0.3f), 0.7f));
        main.gravityModifier = gravity;
        main.maxParticles = Mathf.Max(count, 10);

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = up ? ParticleSystemShapeType.Cone : ParticleSystemShapeType.Sphere;
        shape.angle = 28f;
        shape.radius = radius;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeOut();

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

        go.GetComponent<ParticleSystemRenderer>().material = MakeMat(additive, true);

        go.SetActive(true);
        Destroy(go, life + 1.5f);
    }

    GameObject MakeTrail(Transform parent, Color c)
    {
        GameObject go = new GameObject("BallTrail");
        go.SetActive(false);
        go.transform.SetParent(parent, false);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.5f;
        main.startSpeed = 0.2f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startColor = new ParticleSystem.MinMaxGradient(c, Color.Lerp(c, new Color(1f, 0.85f, 0.3f), 0.7f));
        main.gravityModifier = -0.2f;

        var emission = ps.emission;
        emission.rateOverTime = 80f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeOut();

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));

        go.GetComponent<ParticleSystemRenderer>().material = MakeMat(true, true);

        go.SetActive(true);
        return go;
    }

    void EndTrail(GameObject trail)
    {
        var ps = trail.GetComponent<ParticleSystem>();
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        trail.transform.SetParent(null);
        Destroy(trail, 1.2f);
    }

    IEnumerator Shockwave(Vector3 center, float maxRadius, float duration, Color c)
    {
        const int points = 48;
        GameObject go = new GameObject("LavaShockwave");
        Destroy(go, duration + 2f);
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
            float r = Mathf.Lerp(0.3f, maxRadius, e);

            lr.startWidth = lr.endWidth = Mathf.Lerp(0.7f, 0.05f, t);
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

    IEnumerator LightFlash(Vector3 pos, Color c, float intensity, float range)
    {
        GameObject go = new GameObject("LavaLight");
        Destroy(go, 2f);
        go.transform.position = pos + Vector3.up;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.5f;
            if (l != null) l.intensity = Mathf.Lerp(intensity, 0f, t);
            yield return null;
        }
        Destroy(go);
    }

    // soft glowing circle lying on the ground
    GameObject MakeDisc(Vector3 pos, float diameter, Color c, float life)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "LavaDisc";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(diameter, diameter, 1f);
        Destroy(go, life);

        var r = go.GetComponent<Renderer>();
        r.material = MakeMat(false, true);
        SetMatColor(r.material, c);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    void SetDiscAlpha(GameObject disc, float a)
    {
        if (disc == null) return;
        var r = disc.GetComponent<Renderer>();
        if (r == null) return;
        Color c = r.material.color;
        c.a = a;
        r.material.color = c;
    }

    IEnumerator FadeAndDestroy(GameObject disc, float time)
    {
        float t = 0f;
        float start = 0.6f;
        while (t < 1f && disc != null)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, time);
            SetDiscAlpha(disc, Mathf.Lerp(start, 0f, t));
            yield return null;
        }
        if (disc != null) Destroy(disc);
    }

    static Gradient FadeOut()
    {
        var g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    static void SetMatColor(Material m, Color c)
    {
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        else if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c);
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, slamRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, eruptionRadius);
    }
}

// Finds a health method on the hero without needing to know your hero's script name.
// It looks for TakeDamage(float) or TakeDamage(int) (also ApplyDamage / Damage / Hurt).
public static class BossDamage
{
    static bool warned;
    static readonly string[] names = { "TakeDamage", "ApplyDamage", "Damage", "Hurt" };

    public static void Hero(Transform hero, float amount)
    {
        if (hero == null) return;

        // teleporting through the shadows dodges the attack
        var tp = hero.GetComponentInParent<ShadowTeleport>();
        if (tp != null && tp.IsTeleporting) return;

        var list = new List<MonoBehaviour>();
        list.AddRange(hero.GetComponentsInParent<MonoBehaviour>());
        list.AddRange(hero.GetComponentsInChildren<MonoBehaviour>());

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var mb in list)
        {
            if (mb == null) continue;
            foreach (string n in names)
            {
                MethodInfo m = mb.GetType().GetMethod(n, flags, null, new System.Type[] { typeof(float) }, null);
                if (m != null) { m.Invoke(mb, new object[] { amount }); return; }

                m = mb.GetType().GetMethod(n, flags, null, new System.Type[] { typeof(int) }, null);
                if (m != null) { m.Invoke(mb, new object[] { Mathf.RoundToInt(amount) }); return; }
            }
        }

        if (!warned)
        {
            warned = true;
            Debug.LogWarning("ColossusBoss: no TakeDamage(float) or TakeDamage(int) found on the hero, so the boss can't hurt it. " +
                             "Add a health script with a public TakeDamage method to the hero.");
        }
    }
}
