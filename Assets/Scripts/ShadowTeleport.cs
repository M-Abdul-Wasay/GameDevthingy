using System.Collections;
using UnityEngine;

// Add this to the Hero. Press the teleport key: the hero sinks into the floor
// as a shadow, the shadow slides to a new spot within the radius, and the hero rises there.
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

    public bool IsTeleporting { get; private set; }

    private float nextTime;
    private ShadowHeroController hero;
    private PlayerAnimationController pAnim;
    private CameraFollow camFollow;
    private Renderer[] renderers;

    void Awake()
    {
        hero = GetComponent<ShadowHeroController>();
        pAnim = GetComponent<PlayerAnimationController>();
        if (Camera.main != null)
            camFollow = Camera.main.GetComponent<CameraFollow>();
    }

    void Update()
    {
        if (!IsTeleporting && Time.time >= nextTime && Input.GetKeyDown(teleportKey))
            StartCoroutine(DoTeleport());
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

        // 2. Shadow slides to the destination
        Vector3 shadowFrom = new Vector3(startPos.x, startGround + 0.03f, startPos.z);
        Vector3 shadowTo = new Vector3(dest.x, destGround + 0.03f, dest.z);
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(travelTime, 0.01f);
            shadow.position = Vector3.Lerp(shadowFrom, shadowTo, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        // 3. Place the hero under the floor at the destination, then rise
        transform.position = new Vector3(dest.x, destGround - sinkDepth, dest.z);
        SetRenderers(true);

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
