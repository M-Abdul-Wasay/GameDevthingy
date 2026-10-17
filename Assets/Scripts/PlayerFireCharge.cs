using UnityEngine;

public class PlayerFireCharge : MonoBehaviour
{
    public float speed = 22f;
    public float damage = 3f;
    public float hitRadius = 1.3f;
    public float lifetime = 4f;

    private Vector3 moveDir;
    private bool initialized = false;

    public void Init(Vector3 direction, float projectileSpeed, float projectileDamage)
    {
        moveDir = direction.normalized;
        speed = projectileSpeed;
        damage = projectileDamage;
        if (moveDir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(moveDir);

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

        // Check for enemy collisions
        Collider[] hits = Physics.OverlapSphere(transform.position, hitRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player") || hit.transform.IsChildOf(transform)) continue;

            ArcherAI archer = hit.GetComponentInParent<ArcherAI>();
            if (archer != null)
            {
                archer.TakeDamage(damage);
                SpawnExplosion();
                Destroy(gameObject);
                return;
            }

            BossAI boss = hit.GetComponentInParent<BossAI>();
            if (boss != null)
            {
                boss.TakeDamage(damage);
                SpawnExplosion();
                Destroy(gameObject);
                return;
            }
        }
    }

    void SpawnExplosion()
    {
        GameObject burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        burst.name = "FireExplosion";
        Destroy(burst.GetComponent<Collider>());
        burst.transform.position = transform.position;
        burst.transform.localScale = Vector3.one * 1.6f;

        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Standard");

        var rend = burst.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(sh) { color = new Color(1f, 0.45f, 0.1f, 0.9f) };
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        Destroy(burst, 0.25f);
    }

    public static GameObject SpawnFireball(Vector3 origin, Vector3 direction, float speed = 22f, float damage = 3f)
    {
        GameObject fireObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fireObj.name = "PlayerFireCharge";
        Destroy(fireObj.GetComponent<Collider>());
        fireObj.transform.position = origin;
        fireObj.transform.localScale = Vector3.one * 0.65f;

        Shader sh = Shader.Find("Standard");
        Color fireColor = new Color(1f, 0.35f, 0.05f);

        var rend = fireObj.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(sh) { color = fireColor };
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", fireColor * 2.5f);
            rend.material = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Light
        Light light = fireObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.5f, 0.1f);
        light.range = 8f;
        light.intensity = 2f;

        // Trail
        TrailRenderer trail = fireObj.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.startWidth = 0.5f;
        trail.endWidth = 0.05f;
        Shader spriteSh = Shader.Find("Sprites/Default");
        trail.material = new Material(spriteSh != null ? spriteSh : sh) { color = new Color(1f, 0.55f, 0.1f) };

        var charge = fireObj.AddComponent<PlayerFireCharge>();
        charge.Init(direction, speed, damage);

        return fireObj;
    }
}
