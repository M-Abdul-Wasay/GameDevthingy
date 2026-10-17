using UnityEngine;

// A blue flame that flies in a straight line and hurts the hero on contact.
// BossSkills creates and fires these.
public class FlameProjectile : MonoBehaviour
{
    public float hitRadius = 0.9f;
    public float lifetime = 5f;

    private Vector3 dir;
    private float speed;
    private float damage;
    private Transform target;

    public void Init(Vector3 direction, float flySpeed, float flameDamage, Transform hero)
    {
        dir = direction.normalized;
        speed = flySpeed;
        damage = flameDamage;
        target = hero;
    }

    void Update()
    {
        transform.position += dir * speed * Time.deltaTime;

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (target == null) return;

        Vector3 center = target.position + Vector3.up * 1f;
        if ((center - transform.position).sqrMagnitude <= hitRadius * hitRadius)
        {
            var hero = target.GetComponent<ShadowHeroController>();
            if (hero != null) hero.TakeDamage(damage, true); // true = fire damage
            Destroy(gameObject);
        }
    }

    // Default look: glowing blue ball with a trail (used when you have no flame prefab)
    public static GameObject CreateDefaultFlame(Vector3 position)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "BlueFlame";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.5f;

        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Standard");
        Color blue = new Color(0.25f, 0.7f, 1f, 1f);

        var rend = go.GetComponent<Renderer>();
        rend.material = new Material(sh) { color = blue };
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.4f;
        trail.startWidth = 0.4f;
        trail.endWidth = 0f;
        trail.material = new Material(sh);
        trail.startColor = blue;
        trail.endColor = new Color(blue.r, blue.g, blue.b, 0f);

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = blue;
        light.range = 5f;
        light.intensity = 2f;

        return go;
    }
}
