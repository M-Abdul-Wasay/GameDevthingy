using UnityEngine;

public class ArrowProjectile : MonoBehaviour
{
    public float speed = 24f;
    public float damage = 2f;
    public float hitRadius = 0.9f;
    public float lifetime = 5f;
    public bool isBossArrow = false;

    private Vector3 moveDir;
    private Transform playerTarget;
    private bool initialized = false;

    public void Init(Vector3 targetPos, float projectileSpeed, float projectileDamage, bool isBoss)
    {
        speed = projectileSpeed;
        damage = projectileDamage;
        isBossArrow = isBoss;

        Vector3 targetCenter = targetPos + Vector3.up * 0.9f;
        moveDir = (targetCenter - transform.position).normalized;
        if (moveDir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(moveDir);

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTarget = p.transform;

        initialized = true;
    }

    void Update()
    {
        if (!initialized) return;

        transform.position += moveDir * speed * Time.deltaTime;
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (playerTarget != null)
        {
            Vector3 heroCenter = playerTarget.position + Vector3.up * 0.9f;
            if ((heroCenter - transform.position).sqrMagnitude <= hitRadius * hitRadius)
            {
                var hero = playerTarget.GetComponent<ShadowHeroController>();
                if (hero != null)
                {
                    hero.TakeDamage(damage, false);
                }
                SpawnImpactEffect();
                Destroy(gameObject);
            }
        }
    }

    void SpawnImpactEffect()
    {
        GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spark.name = "ArrowHitSpark";
        Destroy(spark.GetComponent<Collider>());
        spark.transform.position = transform.position;
        spark.transform.localScale = Vector3.one * (isBossArrow ? 0.6f : 0.35f);

        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Standard");
        var rend = spark.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(sh) { color = isBossArrow ? new Color(1f, 0.2f, 0.3f) : new Color(1f, 0.8f, 0.2f) };
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        Destroy(spark, 0.2f);
    }

    public static GameObject CreateDefaultArrow(Vector3 origin, Vector3 target, float speed, float damage, bool isBoss)
    {
        GameObject arrowObj = new GameObject(isBoss ? "BossArrow" : "ArcherArrow");
        arrowObj.transform.position = origin;

        // Visual shaft
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(shaft.GetComponent<Collider>());
        shaft.transform.SetParent(arrowObj.transform, false);
        shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shaft.transform.localScale = isBoss ? new Vector3(0.08f, 0.7f, 0.08f) : new Vector3(0.04f, 0.45f, 0.04f);

        // Visual tip
        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(tip.GetComponent<Collider>());
        tip.transform.SetParent(arrowObj.transform, false);
        tip.transform.localPosition = new Vector3(0f, 0f, isBoss ? 0.7f : 0.45f);
        tip.transform.localScale = isBoss ? new Vector3(0.2f, 0.2f, 0.35f) : new Vector3(0.12f, 0.12f, 0.22f);

        Shader sh = Shader.Find("Standard");
        Color shaftColor = new Color(0.35f, 0.22f, 0.15f);
        Color tipColor = isBoss ? new Color(0.95f, 0.15f, 0.25f) : new Color(0.9f, 0.75f, 0.2f);

        var shaftRend = shaft.GetComponent<Renderer>();
        if (shaftRend != null) shaftRend.material = new Material(sh) { color = shaftColor };

        var tipRend = tip.GetComponent<Renderer>();
        if (tipRend != null)
        {
            var mat = new Material(sh) { color = tipColor };
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", tipColor * 1.5f);
            tipRend.material = mat;
        }

        // Trail
        TrailRenderer trail = arrowObj.AddComponent<TrailRenderer>();
        trail.time = 0.25f;
        trail.startWidth = isBoss ? 0.2f : 0.1f;
        trail.endWidth = 0.01f;
        Shader spriteSh = Shader.Find("Sprites/Default");
        trail.material = new Material(spriteSh != null ? spriteSh : sh) { color = tipColor };

        var proj = arrowObj.AddComponent<ArrowProjectile>();
        proj.Init(target, speed, damage, isBoss);

        return arrowObj;
    }
}
