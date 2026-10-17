using UnityEngine;

// Put this in Assets/Scripts (NOT Editor) and add it to the SAME object as the Animator
// (Boss 1 / Hero). It moves the model from code, so it works even when the animation clips are flat.
// It reads your existing Animator: the "Speed" float or "isWalking" bool, and the current state name.
//
// NEW: PlayScream(duration) makes the body rear back, shake while roaring, then slam forward.
// BossPhaseScream.cs calls it automatically when the boss reaches 50% health.
[DisallowMultipleComponent]
public class ProceduralMotion : MonoBehaviour
{
    [Header("Animator state names")]
    public string[] lungeStates = { "Attack", "Skill3" };   // wind-up + strike
    public string[] raiseStates = { "Skill1" };             // body rises and shakes (summon)
    public string dieState = "Die";                         // body falls over

    [Header("Idle")]
    public float idleBobHeight = 0.04f;
    public float idleBobSpeed = 1.6f;
    public float idleSwayDegrees = 2f;

    [Header("Walk / run")]
    public float moveThreshold = 0.1f;
    public float walkBobHeight = 0.10f;
    public float walkBobSpeed = 8f;
    public float walkLeanDegrees = 8f;
    public float walkSwayDegrees = 5f;

    [Header("Run (BossAI sets runAmount from 0 to 1)")]
    public float runBobSpeedMultiplier = 1.7f;   // steps come faster
    public float runBobHeightMultiplier = 1.5f;  // bounces higher
    public float runLeanDegrees = 22f;           // leans forward more (NEGATIVE if he leans backwards)
    public float runSwayDegrees = 8f;
    [HideInInspector] public float runAmount;    // 0 = walk, 1 = full run

    [Header("Attack: wind-up, strike, recover")]
    public int comboHits = 1;                 // HERO: set to 2 (matches the double combo). BOSS: 1
    [Range(0.05f, 0.9f)] public float windupEnd = 0.30f;   // part of each hit spent winding up
    [Range(0.1f, 0.95f)] public float strikeEnd = 0.50f;  // when the strike lands
    public float windupBack = 0.35f;          // pulls back this far first
    public float windupLeanBack = 18f;
    public float windupYaw = 25f;             // twists one way...
    public float lungeDistance = 0.8f;        // ...then drives forward (NEGATIVE if it goes backwards)
    public float lungeLeanDegrees = 28f;      // (NEGATIVE too if it goes backwards)
    public float strikeYaw = -35f;            // ...and whips the other way
    public float attackSmoothing = 30f;       // higher = snappier

    [Header("Summon / skill 1")]
    public float raiseHeight = 0.6f;
    public float raiseLeanBackDegrees = 15f;

    [Header("Scream (phase change)")]
    public float screamRearDegrees = 30f;     // leans back to roar (NEGATIVE if it leans forward instead)
    public float screamSlamDegrees = 18f;     // slams forward at the end (NEGATIVE if it goes backwards)
    public float screamRiseHeight = 0.35f;    // body lifts up while roaring
    public float screamShakePosition = 0.04f; // how hard the body trembles
    public float screamShakeDegrees = 3f;

    [Header("Death")]
    public float deathFallDegrees = 85f;      // NEGATIVE to fall the other way
    public float deathSink = 0.25f;

    [Header("Smoothing (idle / walk)")]
    public float smoothing = 10f;

    Animator anim;
    Transform[] parts;
    Vector3[] prePos;
    Quaternion[] preRot;
    bool applied;
    bool hasSpeed, hasIsWalking;

    Vector3 curPos;
    float curPitch, curYaw, curRoll;

    float walkPhase;
    float screamStart = -1f;
    float screamDuration = 2f;

    public bool IsScreaming { get { return screamStart >= 0f; } }

    /// <summary>Start the roar pose. Call this when the boss changes phase.</summary>
    public void PlayScream(float duration)
    {
        screamStart = Time.time;
        screamDuration = Mathf.Max(0.3f, duration);
    }

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();

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

        if (anim == null || anim.runtimeAnimatorController == null)
        {
            Debug.LogError("ProceduralMotion: no Animator with a controller found on " + name);
            return;
        }

        foreach (var p in anim.parameters)
        {
            if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) hasSpeed = true;
            if (p.name == "isWalking" && p.type == AnimatorControllerParameterType.Bool) hasIsWalking = true;
        }
    }

    // Undo last frame's offset BEFORE the Animator runs, so the Animator never fights us.
    void Update()
    {
        if (!applied) return;
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i].localPosition = prePos[i];
            parts[i].localRotation = preRot[i];
        }
    }

    // LateUpdate runs after the Animator, so our offset is applied on top of whatever it wrote.
    void LateUpdate()
    {
        if (anim == null || anim.runtimeAnimatorController == null || parts.Length == 0) return;

        AnimatorStateInfo s = anim.IsInTransition(0)
            ? anim.GetNextAnimatorStateInfo(0)
            : anim.GetCurrentAnimatorStateInfo(0);

        Vector3 targetPos = Vector3.zero;
        float pitch = 0f, yaw = 0f, roll = 0f;
        float sm = smoothing;
        float n = Mathf.Clamp01(s.normalizedTime);

        if (s.IsName(dieState))
        {
            screamStart = -1f;   // dying cancels the roar
            float k = Mathf.SmoothStep(0f, 1f, n);
            pitch = -deathFallDegrees * k;
            targetPos = new Vector3(0f, -deathSink * k, 0f);
            sm = attackSmoothing;
        }
        else if (IsScreaming)
        {
            float t = (Time.time - screamStart) / screamDuration;
            const float rearEnd = 0.20f, holdEnd = 0.80f, slamEnd = 0.88f;
            float up, lean;

            if (t >= 1f)
            {
                screamStart = -1f;
                up = 0f; lean = 0f;
            }
            else if (t < rearEnd)
            {
                // rear back and lift the chest
                float x = t / rearEnd;
                float k = 1f - (1f - x) * (1f - x);
                lean = -screamRearDegrees * k;
                up = screamRiseHeight * k;
            }
            else if (t < holdEnd)
            {
                // hold the roar: tremble, heave the chest
                float sh = Time.time * 45f;
                lean = -screamRearDegrees + Mathf.Sin(Time.time * 12f) * 2f;
                up = screamRiseHeight + Mathf.Sin(sh * 1.7f) * screamShakePosition;
                targetPos.x = Mathf.Sin(sh * 1.3f) * screamShakePosition;
                roll = Mathf.Sin(sh) * screamShakeDegrees;
                yaw = Mathf.Sin(sh * 0.7f) * screamShakeDegrees * 0.5f;
            }
            else if (t < slamEnd)
            {
                // slam forward
                float k = (t - holdEnd) / (slamEnd - holdEnd);
                k = k * k;
                lean = Mathf.Lerp(-screamRearDegrees, screamSlamDegrees, k);
                up = Mathf.Lerp(screamRiseHeight, 0f, k);
            }
            else
            {
                // recover
                float k = Mathf.SmoothStep(0f, 1f, (t - slamEnd) / (1f - slamEnd));
                lean = Mathf.Lerp(screamSlamDegrees, 0f, k);
                up = 0f;
            }

            targetPos.y = up;
            pitch = lean;
            sm = attackSmoothing;
        }
        else if (IsAny(s, raiseStates))
        {
            float k = Mathf.Sin(n * Mathf.PI);
            targetPos = new Vector3(0f, raiseHeight * k, 0f);
            pitch = -raiseLeanBackDegrees * k;
            roll = Mathf.Sin(Time.time * 30f) * 3f * k;
            sm = attackSmoothing;
        }
        else if (IsAny(s, lungeStates))
        {
            float c = (n * Mathf.Max(1, comboHits)) % 1f;   // progress through the current hit
            float wEnd = windupEnd;
            float sEnd = Mathf.Max(strikeEnd, wEnd + 0.05f);
            float z, p, yw;

            if (c < wEnd)
            {
                // wind-up: pull back and twist (ease out)
                float x = c / wEnd;
                float k = 1f - (1f - x) * (1f - x);
                z = -windupBack * k;
                p = -windupLeanBack * k;
                yw = windupYaw * k;
            }
            else if (c < sEnd)
            {
                // strike: accelerate hard forward
                float k = (c - wEnd) / (sEnd - wEnd);
                k = k * k;
                z = Mathf.Lerp(-windupBack, lungeDistance, k);
                p = Mathf.Lerp(-windupLeanBack, lungeLeanDegrees, k);
                yw = Mathf.Lerp(windupYaw, strikeYaw, k);
            }
            else
            {
                // recover
                float k = Mathf.SmoothStep(0f, 1f, (c - sEnd) / (1f - sEnd));
                z = Mathf.Lerp(lungeDistance, 0f, k);
                p = Mathf.Lerp(lungeLeanDegrees, 0f, k);
                yw = Mathf.Lerp(strikeYaw, 0f, k);
            }

            targetPos = new Vector3(0f, 0f, z);
            pitch = p;
            yaw = yw;
            sm = attackSmoothing;
        }
        else if (IsMoving())
        {
            float r = Mathf.Clamp01(runAmount);
            // keep the step phase continuous so the bob doesn't jump when speeding up
            walkPhase += Time.deltaTime * walkBobSpeed * Mathf.Lerp(1f, runBobSpeedMultiplier, r);
            float bobH = walkBobHeight * Mathf.Lerp(1f, runBobHeightMultiplier, r);
            targetPos = new Vector3(0f, Mathf.Abs(Mathf.Sin(walkPhase)) * bobH, 0f);
            pitch = Mathf.Lerp(walkLeanDegrees, runLeanDegrees, r);
            roll = Mathf.Sin(walkPhase) * Mathf.Lerp(walkSwayDegrees, runSwayDegrees, r);
        }
        else
        {
            float phase = Time.time * idleBobSpeed;
            targetPos = new Vector3(0f, (Mathf.Sin(phase) * 0.5f + 0.5f) * idleBobHeight, 0f);
            roll = Mathf.Sin(phase * 0.5f) * idleSwayDegrees;
        }

        float a = 1f - Mathf.Exp(-sm * Time.deltaTime);
        curPos = Vector3.Lerp(curPos, targetPos, a);
        curPitch = Mathf.Lerp(curPitch, pitch, a);
        curYaw = Mathf.Lerp(curYaw, yaw, a);
        curRoll = Mathf.Lerp(curRoll, roll, a);

        Quaternion q = Quaternion.Euler(curPitch, curYaw, curRoll);
        for (int i = 0; i < parts.Length; i++)
        {
            // what the Animator left us (or the untouched pose) is the base
            prePos[i] = parts[i].localPosition;
            preRot[i] = parts[i].localRotation;

            parts[i].localPosition = curPos + q * prePos[i];
            parts[i].localRotation = q * preRot[i];
        }
        applied = true;
    }

    bool IsMoving()
    {
        if (hasSpeed) return anim.GetFloat("Speed") > moveThreshold;
        if (hasIsWalking) return anim.GetBool("isWalking");
        return false;
    }

    static bool IsAny(AnimatorStateInfo s, string[] names)
    {
        foreach (var n in names)
            if (!string.IsNullOrEmpty(n) && s.IsName(n)) return true;
        return false;
    }
}
