using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// START MENU. Put this in Assets/Scripts, make a NEW scene called MainMenu, create an Empty GameObject in it
// and add this script. Everything else (buttons, fade, video, event system) is built from code.
//
// The video (main.mp4) already contains the "afterDeath" title, so this only adds PLAY and EXIT buttons.
public class MainMenu : MonoBehaviour
{
    [Header("What PLAY loads (exact scene name, must be in File > Build Settings)")]
    public string firstScene = "Level 1";

    [Header("Background video")]
    public VideoClip backgroundVideo;          // drag main.mp4 here
    [Range(0f, 1f)] public float videoVolume = 0.8f;
    public bool holdLastFrame = true;          // the video plays once and freezes on the title

    [Header("Buttons")]
    public float buttonDelay = 4.5f;           // seconds into the video before the buttons fade in
    public float fadeTime = 0.8f;
    public Font font;                          // optional: drag your own font here
    public Color normalColor = new Color(0.06f, 0.04f, 0.12f, 0.80f);
    public Color hoverColor = new Color(0.42f, 0.20f, 0.72f, 0.95f);
    public Color pressedColor = new Color(0.25f, 0.10f, 0.45f, 1f);
    public Color borderColor = new Color(0.45f, 0.90f, 1f, 0.65f);

    VideoPlayer vp;
    CanvasGroup buttonsGroup;
    CanvasGroup fadeGroup;
    Button playBtn, exitBtn;
    bool shown, loading;

    void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        SetupVideo();
        BuildUI();

        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    // ---------------------------------------------------------------- video

    void SetupVideo()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGo = new GameObject("MenuCamera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            cam = camGo.GetComponent<Camera>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;

        VideoClip clip = backgroundVideo != null ? backgroundVideo : Resources.Load<VideoClip>("main");
        if (clip == null)
        {
            Debug.LogWarning("MainMenu: no video set. Drag main.mp4 into the Background Video slot. Showing the buttons without it.");
            buttonDelay = 0.3f;
            return;
        }

        vp = gameObject.AddComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.clip = clip;
        vp.isLooping = false;
        vp.renderMode = VideoRenderMode.CameraFarPlane;
        vp.targetCamera = cam;
        vp.aspectRatio = VideoAspectRatio.FitOutside;      // fills the screen
        vp.audioOutputMode = VideoAudioOutputMode.Direct;
        vp.controlledAudioTrackCount = 1;
        vp.EnableAudioTrack(0, true);
        vp.SetDirectAudioVolume(0, videoVolume);
        vp.Play();
    }

    // ---------------------------------------------------------------- UI

    void BuildUI()
    {
        GameObject canvasGo = new GameObject("MenuCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // group holding both buttons (starts hidden)
        GameObject group = new GameObject("Buttons", typeof(RectTransform), typeof(CanvasGroup));
        group.transform.SetParent(canvasGo.transform, false);
        RectTransform grt = (RectTransform)group.transform;
        grt.anchorMin = Vector2.zero;
        grt.anchorMax = Vector2.one;
        grt.offsetMin = grt.offsetMax = Vector2.zero;
        buttonsGroup = group.GetComponent<CanvasGroup>();
        buttonsGroup.alpha = 0f;
        buttonsGroup.interactable = false;
        buttonsGroup.blocksRaycasts = false;

        playBtn = MakeButton(group.transform, "PLAY", new Vector2(0f, 250f), OnPlay);
        exitBtn = MakeButton(group.transform, "EXIT", new Vector2(0f, 140f), OnExit);

        // full-screen black used to fade out when you press PLAY
        GameObject fade = new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        fade.transform.SetParent(canvasGo.transform, false);
        RectTransform frt = (RectTransform)fade.transform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        fade.GetComponent<Image>().color = Color.black;
        fadeGroup = fade.GetComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        fadeGroup.interactable = false;
    }

    Button MakeButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);   // bottom centre of the screen
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(380f, 78f);
        rt.anchoredPosition = pos;

        Image img = go.GetComponent<Image>();
        img.color = Color.white;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.normalColor = normalColor;
        cb.highlightedColor = hoverColor;
        cb.selectedColor = hoverColor;
        cb.pressedColor = pressedColor;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.12f;
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        RectTransform trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        Text t = textGo.GetComponent<Text>();
        t.text = label;
        t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = 38;
        t.fontStyle = FontStyle.Bold;
        t.color = Color.white;
        t.raycastTarget = false;
        t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return btn;
    }

    // ---------------------------------------------------------------- behaviour

    void Update()
    {
        if (vp != null && holdLastFrame && vp.isPlaying && vp.length > 0.5 && vp.time >= vp.length - 0.12)
            vp.Pause();   // freeze on the final frame (the title) instead of going black

        if (!shown && !loading)
        {
            bool ready = vp == null
                ? Time.timeSinceLevelLoad >= buttonDelay
                : (vp.time >= buttonDelay || vp.isPaused);

            if (ready || Input.anyKeyDown)
                ShowButtons();
        }
    }

    void ShowButtons()
    {
        shown = true;
        buttonsGroup.interactable = true;
        buttonsGroup.blocksRaycasts = true;
        StartCoroutine(FadeGroup(buttonsGroup, 1f, fadeTime));
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(playBtn.gameObject);   // so Enter / arrow keys work too
    }

    void OnPlay()
    {
        if (loading) return;
        StartCoroutine(LoadGame());
    }

    void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator LoadGame()
    {
        if (!Application.CanStreamedLevelBeLoaded(firstScene))
        {
            Debug.LogError("MainMenu: scene '" + firstScene + "' can't be loaded. Check the name is exact and add the scene in File > Build Settings.");
            yield break;
        }

        loading = true;
        buttonsGroup.interactable = false;
        fadeGroup.blocksRaycasts = true;

        float startVol = videoVolume;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, fadeTime);
            fadeGroup.alpha = Mathf.Clamp01(t);
            if (vp != null) vp.SetDirectAudioVolume(0, startVol * (1f - Mathf.Clamp01(t)));
            yield return null;
        }

        SceneManager.LoadScene(firstScene);
    }

    IEnumerator FadeGroup(CanvasGroup g, float to, float time)
    {
        float from = g.alpha;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, time);
            g.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t));
            yield return null;
        }
        g.alpha = to;
    }
}
