using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

/// <summary>
/// Level 1 "load &amp; haul" objective.
///
/// The player must excavate soil and dump it inside a marked spoil zone until a
/// target volume has been delivered. Every soil parcel the terrain accepts is
/// reported by <see cref="DigSystem"/> through <see cref="DigSystem.SoilPlaced"/>;
/// parcels that land inside the zone add to the delivered total.
///
/// Setup:
///  1. This component is already attached to the Level 1 "MenuController" object.
///  2. Tune the target volume and the spoil-zone centre/size in the Inspector.
///  3. A marker is drawn around the zone at runtime so the player can see it.
///  4. Reaching the target shows a popup offering "Keep Hauling" or "Finish".
/// </summary>
public class DigTracker : MonoBehaviour
{
    public static DigTracker I;

    [Header("Goal")]
    [Tooltip("How much soil (m³) must be dumped inside the spoil zone to finish the level.")]
    public float targetVolume = 0.5f;

    [Header("Spoil zone")]
    [Tooltip("World-space centre of the square drop zone on the ground.")]
    public Vector3 zoneCenter = new Vector3(0f, 0f, 4f);

    [Tooltip("Size of the square drop zone along X and Z (metres).")]
    public Vector2 zoneSize = new Vector2(3f, 3f);

    [Tooltip("Draw a bright outline + corner posts so the player can find the zone.")]
    public bool showZoneMarker = true;

    [Tooltip("Colour of the zone marker.")]
    public Color zoneColor = new Color(0.2f, 1f, 0.35f, 0.9f);

    [Header("HUD")]
    [Tooltip("Optional existing world-space canvas for the progress HUD. Generated if empty.")]
    public Canvas hudCanvas;

    [Tooltip("Optional label showing progress. Generated if empty.")]
    public TextMeshProUGUI percentLabel;

    [Tooltip("Optional fill-bar foreground. Generated if empty.")]
    public RectTransform fillBar;

    [Tooltip("Where to place the auto-generated HUD in world space.")]
    public Vector3 hudWorldPosition = new Vector3(0f, 1.6f, 8.8f);

    [Tooltip("How often (seconds) to refresh the HUD and check the goal.")]
    [Min(0.05f)]
    public float tickInterval = 0.1f;

    [Header("Completion Popup")]
    [Tooltip("Show a popup when the target volume is reached.")]
    public bool useCompletionPopup = true;

    [Tooltip("Popup heading text.")]
    public string popupTitle = "Load Delivered!";

    [Tooltip("Popup body text. Leave empty to auto-fill with the delivered amount.")]
    [TextArea]
    public string popupMessage = "";

    [Tooltip("Label on the button that dismisses the popup and keeps playing.")]
    public string continueButtonLabel = "Keep Hauling";

    [Tooltip("Label on the button that loads the next level.")]
    public string nextButtonLabel = "Finish Level";

    [Tooltip("Scene loaded when the player chooses 'Finish Level'.")]
    public string popupNextSceneName = "Level 2";

    // ── runtime state ─────────────────────────────────────────────────────
    float filled;
    bool goalReached;
    float tickClock;
    float fillBarMaxWidth = 400f;

    LineRenderer zoneLine;
    readonly Vector3[] perimPts = new Vector3[4 * 8];

    // Runtime-built popup (created lazily on goal).
    GameObject popupRoot;
    TextMeshProUGUI popupTitleText;
    TextMeshProUGUI popupMessageText;

    // ── Unity lifecycle ───────────────────────────────────────────────────
    void Awake()
    {
        I = this;
        filled = 0f;
        goalReached = false;
    }

    void OnEnable() { DigSystem.SoilPlaced += OnSoilPlaced; }

    void OnDisable() { DigSystem.SoilPlaced -= OnSoilPlaced; }

    void OnDestroy() { if (I == this) I = null; }

    void Start()
    {
        if (showZoneMarker) BuildZoneMarker();
        BuildHUDIfNeeded();
        RefreshHUD();
    }

    void Update()
    {
        tickClock += Time.deltaTime;
        if (tickClock < tickInterval) return;
        tickClock = 0f;

        if (filled > targetVolume) filled = targetVolume;

        if (showZoneMarker) UpdateZoneMarker();
        RefreshHUD();

        if (!goalReached && targetVolume > 0f && filled >= targetVolume)
            ReachGoal();
    }

    // ── goal logic ────────────────────────────────────────────────────────
    void OnSoilPlaced(Vector3 point, float volume)
    {
        if (goalReached || volume <= 0f) return;
        if (!InsideZone(point)) return;
        filled += volume;
    }

    bool InsideZone(Vector3 p)
    {
        float dx = p.x - zoneCenter.x;
        float dz = p.z - zoneCenter.z;
        return Mathf.Abs(dx) <= zoneSize.x * 0.5f && Mathf.Abs(dz) <= zoneSize.y * 0.5f;
    }

    void ReachGoal()
    {
        goalReached = true;
        Debug.Log($"[DigTracker] Goal reached: {filled:0.00}/{targetVolume:0.00} m³ delivered.");
        if (useCompletionPopup) ShowCompletionPopup();
        else SceneManager.LoadScene(popupNextSceneName);
    }

    // ── HUD ───────────────────────────────────────────────────────────────
    void RefreshHUD()
    {
        float ratio = targetVolume > 0f ? Mathf.Clamp01(filled / targetVolume) : 0f;
        int pct = Mathf.RoundToInt(ratio * 100f);

        if (percentLabel)
            percentLabel.text = $"Delivered\n{filled:0.00} / {targetVolume:0.00} m³ ({pct}%)";

        if (fillBar)
        {
            var sz = fillBar.sizeDelta;
            sz.x = fillBarMaxWidth * ratio;
            fillBar.sizeDelta = sz;
        }
    }

    void BuildHUDIfNeeded()
    {
        if (hudCanvas && percentLabel && fillBar) return;

        if (!hudCanvas)
        {
            var canvasGO = new GameObject("DigHUD");
            hudCanvas = canvasGO.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();

            var rt = hudCanvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(500f, 150f);
            rt.position = hudWorldPosition;
            rt.rotation = Quaternion.identity;
            rt.localScale = Vector3.one * 0.01f;
        }

        var canvas = hudCanvas.GetComponent<RectTransform>();

        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvas, false);
        var bgImg = bgGO.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.72f);
        var bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        var trackGO = new GameObject("BarTrack");
        trackGO.transform.SetParent(canvas, false);
        var trackImg = trackGO.AddComponent<UnityEngine.UI.Image>();
        trackImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        var trackRt = trackGO.GetComponent<RectTransform>();
        trackRt.anchorMin = new Vector2(0.05f, 0.1f);
        trackRt.anchorMax = new Vector2(0.95f, 0.45f);
        trackRt.offsetMin = trackRt.offsetMax = Vector2.zero;
        fillBarMaxWidth = 400f;

        if (!fillBar)
        {
            var fillGO = new GameObject("BarFill");
            fillGO.transform.SetParent(trackGO.transform, false);
            var fillImg = fillGO.AddComponent<UnityEngine.UI.Image>();
            fillImg.color = new Color(0.95f, 0.55f, 0.1f, 1f);
            fillBar = fillGO.GetComponent<RectTransform>();
            fillBar.anchorMin = Vector2.zero;
            fillBar.anchorMax = new Vector2(0f, 1f);
            fillBar.pivot = new Vector2(0f, 0.5f);
            fillBar.offsetMin = fillBar.offsetMax = Vector2.zero;
            fillBar.sizeDelta = new Vector2(0f, 0f);
            fillBarMaxWidth = trackRt.rect.width > 0 ? trackRt.rect.width : 400f;
        }

        if (!percentLabel)
        {
            var textGO = new GameObject("PercentLabel");
            textGO.transform.SetParent(canvas, false);
            percentLabel = textGO.AddComponent<TextMeshProUGUI>();
            percentLabel.text = "Delivered\n0.00 / 0.00 m³ (0%)";
            percentLabel.fontSize = 34;
            percentLabel.alignment = TextAlignmentOptions.Center;
            percentLabel.color = Color.white;
            var textRt = textGO.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.05f, 0.45f);
            textRt.anchorMax = new Vector2(0.95f, 0.95f);
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
        }
    }

    // ── zone marker ───────────────────────────────────────────────────────
    void BuildZoneMarker()
    {
        var go = new GameObject("SpoilZoneMarker");
        zoneLine = go.AddComponent<LineRenderer>();
        zoneLine.useWorldSpace = true;
        zoneLine.loop = true;
        zoneLine.widthMultiplier = 0.05f;
        zoneLine.positionCount = perimPts.Length;
        zoneLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        zoneLine.receiveShadows = false;

        var shader = Shader.Find("Sprites/Default");
        if (!shader) shader = Shader.Find("Unlit/Color");
        zoneLine.material = new Material(shader);
        zoneLine.startColor = zoneColor;
        zoneLine.endColor = zoneColor;

        UpdateZoneMarker();
    }

    void UpdateZoneMarker()
    {
        if (!zoneLine) return;

        float y = zoneCenter.y;
        if (DigSystem.I) y = DigSystem.I.SurfaceY(zoneCenter);

        float hx = zoneSize.x * 0.5f;
        float hz = zoneSize.y * 0.5f;
        // Four corners + 8 samples per edge so the outline hugs the terrain.
        int n = perimPts.Length;
        int perEdge = n / 4;
        for (int i = 0; i < n; i++)
        {
            int edge = i / perEdge;
            float t = (i % perEdge) / (float)perEdge;
            Vector3 a, b;
            switch (edge)
            {
                case 0: a = new Vector3(-hx, 0, -hz); b = new Vector3(hx, 0, -hz); break;
                case 1: a = new Vector3(hx, 0, -hz); b = new Vector3(hx, 0, hz); break;
                case 2: a = new Vector3(hx, 0, hz); b = new Vector3(-hx, 0, hz); break;
                default: a = new Vector3(-hx, 0, hz); b = new Vector3(-hx, 0, -hz); break;
            }
            Vector3 p = Vector3.Lerp(a, b, t) + new Vector3(zoneCenter.x, 0, zoneCenter.z);
            p.y = (DigSystem.I ? DigSystem.I.SurfaceY(p) : y) + 0.06f;
            perimPts[i] = p;
        }
        zoneLine.SetPositions(perimPts);
    }

    // ── completion popup (dedicated XR canvas, placed in front of the player) ─
    void ShowCompletionPopup()
    {
        if (popupRoot) { popupRoot.SetActive(true); return; }

        string msg = string.IsNullOrWhiteSpace(popupMessage)
            ? $"You delivered {filled:0.00} m³ of soil to the spoil zone."
            : popupMessage;

        BuildPopup();
        if (popupTitleText) popupTitleText.text = popupTitle;
        if (popupMessageText) popupMessageText.text = msg;
        if (popupRoot) popupRoot.SetActive(true);
        Debug.Log("[DigTracker] Completion popup shown.");
    }

    void BuildPopup()
    {
        var go = new GameObject("DigPopupCanvas", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>();
        // Guarantees the popup buttons receive VR controller input.
        go.AddComponent<TrackedDeviceGraphicRaycaster>();
        go.transform.localScale = Vector3.one * 0.001f;

        // Place it in front of the player so it is always visible.
        var cam = Camera.main;
        if (cam)
        {
            go.transform.position = cam.transform.position + cam.transform.forward * 2f;
            go.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position);
        }
        else
        {
            go.transform.position = hudWorldPosition;
        }

        var canvasRect = go.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(640f, 400f);

        popupRoot = new GameObject("Root", typeof(RectTransform));
        var root = popupRoot.GetComponent<RectTransform>();
        root.SetParent(go.transform, false);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;

        var dim = popupRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.78f);
        dim.raycastTarget = true;

        popupTitleText = MakeLabel(popupRoot.transform, "Title", popupTitle,
            new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.94f), 52, TextAlignmentOptions.Center, true);

        popupMessageText = MakeLabel(popupRoot.transform, "Message", popupMessage,
            new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.58f), 30, TextAlignmentOptions.Center, false);

        MakeButton(popupRoot.transform, "Continue", continueButtonLabel,
            new Vector2(0.06f, 0.10f), new Vector2(0.47f, 0.32f), 32, HidePopup);

        MakeButton(popupRoot.transform, "Next", nextButtonLabel,
            new Vector2(0.53f, 0.10f), new Vector2(0.94f, 0.32f), 32, GoToNextLevel);

        popupRoot.SetActive(false);
    }

    TextMeshProUGUI MakeLabel(Transform parent, string name, string content,
        Vector2 aMin, Vector2 aMax, int size, TextAlignmentOptions align, bool bold)
    {
        var lgo = new GameObject(name, typeof(RectTransform));
        var rt = lgo.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var t = lgo.AddComponent<TextMeshProUGUI>();
        t.text = content;
        t.fontSize = size;
        // Shrink-to-fit so longer labels (e.g. "Finish Level") stay inside the button.
        t.enableAutoSizing = true;
        t.fontSizeMin = 8f;
        t.fontSizeMax = Mathf.Max(8, size);
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    void MakeButton(Transform parent, string name, string label,
        Vector2 aMin, Vector2 aMax, int size, UnityEngine.Events.UnityAction onClick)
    {
        var bgo = new GameObject(name, typeof(RectTransform));
        var rt = bgo.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var img = bgo.AddComponent<Image>();
        img.color = new Color(0.16f, 0.38f, 0.72f, 0.96f);

        var btn = bgo.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var colors = btn.colors;
        colors.highlightedColor = new Color(0.25f, 0.52f, 0.90f, 1f);
        colors.pressedColor = new Color(0.12f, 0.28f, 0.55f, 1f);
        btn.colors = colors;

        var txt = MakeLabel(bgo.transform, "Label", label, Vector2.zero, Vector2.one, size, TextAlignmentOptions.Center, true);
        txt.raycastTarget = false;
    }

    void HidePopup()
    {
        if (popupRoot) popupRoot.SetActive(false);
        Debug.Log("[DigTracker] Keep hauling chosen.");
    }

    void GoToNextLevel()
    {
        if (popupRoot) popupRoot.SetActive(false);
        Debug.Log($"[DigTracker] Finish chosen - loading '{popupNextSceneName}'.");
        if (Application.CanStreamedLevelBeLoaded(popupNextSceneName))
        {
            goalReached = true;
            SceneManager.LoadScene(popupNextSceneName);
        }
        else
        {
            Debug.LogError($"[DigTracker] Scene '{popupNextSceneName}' is not in the build settings.");
        }
    }
}