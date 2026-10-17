using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_PIPELINE_URP
using UnityEngine.Rendering.Universal;
#endif

/// <summary>
/// Full-moon night with fog + ground mist. Works on Built-in and URP.
/// Drop on any empty GameObject and press Play. Everything is tweakable in the Inspector.
/// </summary>
public class MoonlitMistSetup : MonoBehaviour
{
    [Header("Light (auto-finds a Directional Light if empty)")]
    public Light moonLight;
    public Color moonLightColor = new Color(0.75f, 0.82f, 1f);
    public float moonIntensity = 1.2f;
    public Vector3 moonLightRotation = new Vector3(35f, -30f, 0f);

    [Header("Brightness")]
    public Color ambientColor = new Color(0.25f, 0.30f, 0.45f);
    public Color fogColor = new Color(0.28f, 0.36f, 0.50f);
    [Range(0f, 0.1f)] public float fogDensity = 0.015f;
    [Range(0.2f, 1f)] public float skyBrightness = 0.7f;

    [Header("Moon")]
    public bool createMoon = true;
    public float moonDistance = 300f;
    public float moonSize = 90f;

    [Header("Mist")]
    public bool createMist = true;
    public float mistHeight = 0.5f;
    public Vector3 mistArea = new Vector3(60f, 1f, 60f);
    [Range(0f, 0.5f)] public float mistOpacity = 0.12f;

    Transform moonTransform;
    Transform mistTransform;
    Camera cam;

    void Start()
    {
        cam = Camera.main;
        if (moonLight == null) moonLight = FindDirectionalLight();

        SetupEnvironment();
        SetupLight();
        SetupCamera();
        if (createMoon) CreateMoon();
        if (createMist) CreateMist();
#if UNITY_PIPELINE_URP
        SetupURPPostProcessing();
#endif
    }

    void LateUpdate()
    {
        if (cam == null) return;

        // Keep the moon fixed in the sky (it never gets closer as you walk)
        if (moonTransform != null && moonLight != null)
        {
            moonTransform.position = cam.transform.position - moonLight.transform.forward * moonDistance;
            moonTransform.rotation = cam.transform.rotation;
        }

        // Mist follows the camera on the ground plane
        if (mistTransform != null)
        {
            Vector3 p = cam.transform.position;
            mistTransform.position = new Vector3(p.x, mistHeight, p.z);
        }
    }

    // ---------------------------------------------------------------- setup

    void SetupEnvironment()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
    }

    void SetupLight()
    {
        if (moonLight == null) return;
        moonLight.type = LightType.Directional;
        moonLight.color = moonLightColor;
        moonLight.intensity = moonIntensity;
        moonLight.shadows = LightShadows.Soft;
        moonLight.transform.rotation = Quaternion.Euler(moonLightRotation);
    }

    void SetupCamera()
    {
        if (cam == null) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = fogColor * skyBrightness;
        cam.allowHDR = true;
        if (cam.farClipPlane < moonDistance * 1.3f)
            cam.farClipPlane = moonDistance * 1.3f;
    }

    // ---------------------------------------------------------------- moon

    void CreateMoon()
    {
        var go = new GameObject("Full Moon");
        var sr = go.AddComponent<SpriteRenderer>();
        Texture2D tex = MakeMoonTexture(256);
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                  new Vector2(0.5f, 0.5f), tex.width);
        // Sprites/Default ignores fog, so the moon stays visible through the mist
        sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = -100;
        go.transform.localScale = Vector3.one * moonSize;
        moonTransform = go.transform;
    }

    static Texture2D MakeMoonTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float half = size * 0.5f;
        Color moon = new Color(1f, 0.98f, 0.90f);
        Color halo = new Color(0.70f, 0.80f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - half, dy = y - half;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / half;

                float disc = Mathf.SmoothStep(1f, 0f, (d - 0.30f) / 0.02f);
                float haloA = Mathf.Clamp01(1f - d);
                haloA = haloA * haloA * haloA * 0.6f;

                float crater = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
                Color discCol = moon * (1f - 0.12f * crater);

                Color c = Color.Lerp(halo, discCol, disc);
                c.a = Mathf.Max(disc, haloA);
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------- mist

    void CreateMist()
    {
        var go = new GameObject("Ground Mist");
        mistTransform = go.transform;
        mistTransform.position = new Vector3(0f, mistHeight, 0f);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 12f;
        main.startSpeed = 0.15f;
        main.startSize = new ParticleSystem.MinMaxCurve(8f, 14f);
        main.startColor = new Color(0.80f, 0.88f, 1f, mistOpacity);
        main.maxParticles = 150;

        var emission = ps.emission;
        emission.rateOverTime = 10f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = mistArea;

        // Fade in and out so particles never pop
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f),
                    new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = MakeSoftTexture(128) };
        rend.sortingOrder = -50;

        ps.Play();
    }

    static Texture2D MakeSoftTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - half, dy = y - half;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / half;
                float a = Mathf.Clamp01(1f - d);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------- helpers

    static Light FindDirectionalLight()
    {
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) return l;

        var go = new GameObject("Moon Light");
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        return light;
    }

#if UNITY_PIPELINE_URP
    void SetupURPPostProcessing()
    {
        if (cam != null)
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        var go = new GameObject("Night Volume");
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.value = 0.9f;
        bloom.intensity.value = 1.2f;
        bloom.scatter.value = 0.7f;

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.value = TonemappingMode.ACES;

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.value = 0.5f;
        color.saturation.value = -10f;
        color.colorFilter.value = new Color(0.85f, 0.92f, 1f);

        var vig = profile.Add<Vignette>(true);
        vig.intensity.value = 0.25f;
        vig.smoothness.value = 0.4f;

        vol.sharedProfile = profile;
    }
#endif
}