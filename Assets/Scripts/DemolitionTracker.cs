using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

/// <summary>
/// Singleton manager for the Level 0 demolition goal.
///
/// Setup:
///  1. Add this component to any persistent GameObject in the Level 0 scene
///     (e.g. a "LevelManager" empty GameObject).
///  2. Add DemolitionBlock to every house cube (or use the
///     DemolitionHouseTag helper to do it automatically at runtime).
///  3. Optionally assign the HUD references below; if left empty the
///     tracker will create a simple World-Space canvas automatically.
///
/// Progress is measured as the fraction of registered blocks that have
/// either tilted past their tilt threshold OR moved past their displace
/// threshold.  The level is "complete" once completionPercent is reached.
/// </summary>
public class DemolitionTracker : MonoBehaviour
{
    // ── singleton ──────────────────────────────────────────────────────────
    static DemolitionTracker instance;

    // ── inspector ──────────────────────────────────────────────────────────
    [Header("Completion")]
    [Tooltip("Fraction of blocks that must be demolished to win (0–1).")]
    [Range(0f, 1f)]
    public float completionPercent = 0.5f;

    [Tooltip("If true, load the next scene when completionPercent is reached.")]
    public bool loadNextSceneOnComplete = false;

    [Tooltip("Scene name to load when complete (only used if loadNextSceneOnComplete = true).")]
    public string nextSceneName = "Menu";

    [Header("HUD – World Space Canvas")]
    [Tooltip("Leave null to auto-generate. Assign a Canvas with Render Mode = World Space.")]
    public Canvas hudCanvas;

    [Tooltip("TextMeshPro label that shows e.g. '37%'. Auto-created if canvas is null.")]
    public TextMeshProUGUI percentLabel;

    [Tooltip("RectTransform that acts as the fill bar foreground. Auto-created if canvas is null.")]
    public RectTransform fillBar;

    [Tooltip("Where to place the auto-generated HUD in world space.")]
    public Vector3 hudWorldPosition = new Vector3(0f, 4f, 10f);

    [Tooltip("How often (seconds) to re-evaluate all blocks.")]
    [Min(0.05f)]
    public float tickInterval = 0.1f;

    [Header("Tag-based Auto-registration")]
    [Tooltip("If set, every GameObject with this tag that also has a Rigidbody " +
             "gets a DemolitionBlock added automatically at runtime.")]
    public string houseTag = "HouseBlock";

    [Header("Completion Popup")]
    [Tooltip("Show a popup once the demolition goal is reached, offering " +
             "'Keep Demolishing' or 'Next Level'.")]
    public bool useCompletionPopup = true;

    [Tooltip("Popup heading text.")]
    public string popupTitle = "Target Reached!";

    [Tooltip("Popup body text. Leave empty to auto-fill with the achieved percentage.")]
    [TextArea]
    public string popupMessage = "";

    [Tooltip("Label on the button that dismisses the popup so the player can keep going.")]
    public string continueButtonLabel = "Keep Demolishing";

    [Tooltip("Label on the button that loads the next level.")]
    public string nextButtonLabel = "Next Level";

    [Tooltip("Scene loaded when the player picks 'Next Level' in the popup.")]
    public string popupNextSceneName = "Level 1";

    // ── runtime state ──────────────────────────────────────────────────────
    static readonly List<DemolitionBlock> blocks = new List<DemolitionBlock>();
    static int demolishedCount;
    bool levelComplete;
    bool goalReached;
    bool popupShown;
    float tickClock;

    // Runtime-built completion popup (lazily created on first goal).
    GameObject popupRoot;
    TextMeshProUGUI popupTitleText;
    TextMeshProUGUI popupMessageText;

    // Full-width of the fill bar (set during HUD creation / Awake)
    float fillBarMaxWidth = 400f;

    // A scene-assigned fill bar (e.g. a centred Image) is driven with Image.fillAmount
    // so it grows left-to-right; an auto-generated bar keeps using its width.
    bool fillBarUsesFillAmount;

    // ── static API (called by DemolitionBlock) ─────────────────────────────
    public static void Register(DemolitionBlock block)
    {
        if (!blocks.Contains(block)) blocks.Add(block);
    }

    public static void Unregister(DemolitionBlock block)
    {
        blocks.Remove(block);
    }

    public static void OnBlockDemolished()
    {
        demolishedCount++;
        if (instance) instance.RefreshHUD();
    }

    // ── Unity lifecycle ────────────────────────────────────────────────────
    void Awake()
    {
        instance = this;
        demolishedCount = 0;
        blocks.Clear();
    }

    void Start()
    {
        // Auto-add DemolitionBlock to tagged objects
        if (!string.IsNullOrEmpty(houseTag))
        {
            var tagged = GameObject.FindGameObjectsWithTag(houseTag);
            foreach (var go in tagged)
            {
                if (go.GetComponent<Rigidbody>() && !go.GetComponent<DemolitionBlock>())
                    go.AddComponent<DemolitionBlock>();
            }
        }

        // Remember whether the fill bar was assigned in the scene (vs auto-generated)
        // so we can drive it with fillAmount instead of resizing from the centre.
        bool hadAssignedFillBar = fillBar != null;

        BuildHUDIfNeeded();

        if (hadAssignedFillBar) ConfigureAssignedFillBar();
        RefreshHUD();
    }

    // Turn a scene-assigned fill Image into a left-to-right filled bar.
    void ConfigureAssignedFillBar()
    {
        if (!fillBar) return;
        var img = fillBar.GetComponent<UnityEngine.UI.Image>();
        if (!img) return;

        img.type = UnityEngine.UI.Image.Type.Filled;
        img.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        img.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
        img.fillAmount = 0f;
        fillBarUsesFillAmount = true;
    }

    void Update()
    {
        tickClock += Time.deltaTime;
        if (tickClock < tickInterval) return;
        tickClock = 0f;

        // Keep polling blocks even after the goal is reached so the player can
        // choose "Keep Demolishing" and continue without the world freezing.
        foreach (var b in blocks)
            if (b && !b.IsDemolished) b.Tick(tickInterval);

        EvaluateGoal();
        RefreshHUD();
    }

    // ── internal helpers ───────────────────────────────────────────────────
    void EvaluateGoal()
    {
        if (goalReached || levelComplete || blocks.Count == 0) return;

        float ratio = (float)demolishedCount / blocks.Count;
        if (ratio < completionPercent) return;

        goalReached = true;
        Debug.Log($"[DemolitionTracker] Goal reached! {demolishedCount}/{blocks.Count} blocks demolished ({ratio * 100f:0}%).");

        if (useCompletionPopup)
        {
            ShowCompletionPopup();
        }
        else if (loadNextSceneOnComplete)
        {
            levelComplete = true;
            SceneManager.LoadScene(nextSceneName);
        }
    }

    void RefreshHUD()
    {
        if (blocks.Count == 0) return;

        float ratio = Mathf.Clamp01((float)demolishedCount / blocks.Count);
        int pct = Mathf.RoundToInt(ratio * 100f);

        // Update percentage text
        if (percentLabel)
            percentLabel.text = $"Demolition\n{pct}%";

        // Update the fill bar so it grows left-to-right.
        if (fillBar)
        {
            if (fillBarUsesFillAmount)
            {
                var fillImg = fillBar.GetComponent<UnityEngine.UI.Image>();
                if (fillImg) fillImg.fillAmount = ratio;
            }
            else
            {
                var sz = fillBar.sizeDelta;
                sz.x = fillBarMaxWidth * ratio;
                fillBar.sizeDelta = sz;
            }
        }

        // Check completion threshold visually (colour the bar)
        UpdateBarColour(ratio);
    }

    void UpdateBarColour(float ratio)
    {
        if (!fillBar) return;
        var img = fillBar.GetComponent<UnityEngine.UI.Image>();
        if (!img) return;

        Color target;
        if (ratio >= completionPercent)
            target = new Color(0.2f, 0.9f, 0.2f); // green – done
        else if (ratio >= completionPercent * 0.5f)
            target = new Color(1f, 0.75f, 0f);     // orange – halfway
        else
            target = new Color(0.95f, 0.25f, 0.1f); // red – just started

        img.color = Color.Lerp(img.color, target, Time.deltaTime * 4f);
    }

    // ── Auto-HUD builder ───────────────────────────────────────────────────
    void BuildHUDIfNeeded()
    {
        if (hudCanvas && percentLabel && fillBar) return; // already assigned

        // ── Canvas ──
        if (!hudCanvas)
        {
            var canvasGO = new GameObject("DemolitionHUD");
            hudCanvas = canvasGO.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var rt = hudCanvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(500f, 150f);
            rt.position = hudWorldPosition;
            rt.rotation = Quaternion.identity;
            rt.localScale = Vector3.one * 0.01f; // 1 unit = 1 cm in world space
        }

        var canvas = hudCanvas.GetComponent<RectTransform>();

        // ── Background panel ──
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvas, false);
        var bgImg = bgGO.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.72f);
        var bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        // ── Progress bar track ──
        var trackGO = new GameObject("BarTrack");
        trackGO.transform.SetParent(canvas, false);
        var trackImg = trackGO.AddComponent<UnityEngine.UI.Image>();
        trackImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        var trackRt = trackGO.GetComponent<RectTransform>();
        trackRt.anchorMin = new Vector2(0.05f, 0.1f);
        trackRt.anchorMax = new Vector2(0.95f, 0.45f);
        trackRt.offsetMin = trackRt.offsetMax = Vector2.zero;
        fillBarMaxWidth = 400f; // approx pixel width of anchored bar

        // ── Progress bar fill ──
        if (!fillBar)
        {
            var fillGO = new GameObject("BarFill");
            fillGO.transform.SetParent(trackGO.transform, false);
            var fillImg = fillGO.AddComponent<UnityEngine.UI.Image>();
            fillImg.color = new Color(0.95f, 0.25f, 0.1f, 1f);
            fillBar = fillGO.GetComponent<RectTransform>();
            fillBar.anchorMin = Vector2.zero;
            fillBar.anchorMax = new Vector2(0f, 1f); // left-anchored
            fillBar.pivot = new Vector2(0f, 0.5f);
            fillBar.offsetMin = fillBar.offsetMax = Vector2.zero;
            fillBar.sizeDelta = new Vector2(0f, 0f); // starts empty
            fillBarMaxWidth = trackRt.rect.width > 0 ? trackRt.rect.width : 400f;
        }

        // ── Text label ──
        if (!percentLabel)
        {
            var textGO = new GameObject("PercentLabel");
            textGO.transform.SetParent(canvas, false);
            percentLabel = textGO.AddComponent<TextMeshProUGUI>();
            percentLabel.text = "Demolition\n0%";
            percentLabel.fontSize = 36;
            percentLabel.alignment = TextAlignmentOptions.Center;
            percentLabel.color = Color.white;
            var textRt = textGO.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.05f, 0.45f);
            textRt.anchorMax = new Vector2(0.95f, 0.95f);
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
        }

        Debug.Log("[DemolitionTracker] Auto-generated World-Space HUD.");
    }

    // ── Completion popup ───────────────────────────────────────────────────
    void ShowCompletionPopup()
    {
        if (popupShown) return;
        popupShown = true;

        BuildCompletionPopupIfNeeded();

        if (popupTitleText) popupTitleText.text = popupTitle;

        if (popupMessageText)
        {
            if (string.IsNullOrWhiteSpace(popupMessage))
            {
                int pct = Mathf.RoundToInt(completionPercent * 100f);
                popupMessageText.text = $"You demolished {pct}% of the structure.";
            }
            else
            {
                popupMessageText.text = popupMessage;
            }
        }

        if (popupRoot) popupRoot.SetActive(true);
        Debug.Log("[DemolitionTracker] Completion popup shown.");
    }

    void BuildCompletionPopupIfNeeded()
    {
        if (popupRoot) return;

        // Build a dedicated, camera-facing canvas so the popup is large and centred
        // in the player's view instead of being squeezed into the small progress HUD.
        var go = new GameObject("DemolitionPopupCanvas", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.AddComponent<UnityEngine.UI.CanvasScaler>();
        // Makes the popup buttons receive VR controller input.
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
        float H = canvasRect.rect.height > 1f ? canvasRect.rect.height : 400f;

        popupRoot = new GameObject("CompletionPopup", typeof(RectTransform));
        var root = popupRoot.GetComponent<RectTransform>();
        root.SetParent(go.transform, false);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;

        // Dim backdrop (also blocks clicks from passing through to the HUD).
        var dim = popupRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.78f);
        dim.raycastTarget = true;

        popupTitleText = MakeLabel(popupRoot.transform, "Title", popupTitle,
            new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.94f),
            Mathf.RoundToInt(H * 0.13f), TextAlignmentOptions.Center, FontStyles.Bold);

        popupMessageText = MakeLabel(popupRoot.transform, "Message", popupMessage,
            new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.58f),
            Mathf.RoundToInt(H * 0.095f), TextAlignmentOptions.Center, FontStyles.Normal);

        MakeButton(popupRoot.transform, "Continue", continueButtonLabel,
            new Vector2(0.06f, 0.10f), new Vector2(0.47f, 0.32f),
            Mathf.RoundToInt(H * 0.11f), ContinueDemolishing);

        MakeButton(popupRoot.transform, "NextLevel", nextButtonLabel,
            new Vector2(0.53f, 0.10f), new Vector2(0.94f, 0.32f),
            Mathf.RoundToInt(H * 0.11f), GoToNextLevel);

        popupRoot.SetActive(false);
        Debug.Log("[DemolitionTracker] Completion popup built.");
    }

    TextMeshProUGUI MakeLabel(Transform parent, string name, string content,
        Vector2 anchorMin, Vector2 anchorMax, int fontSize, TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = content;
        t.fontSize = Mathf.Max(6, fontSize);
        // Shrink-to-fit so longer labels (e.g. "Keep Demolishing") stay inside the button.
        t.enableAutoSizing = true;
        t.fontSizeMin = 8f;
        t.fontSizeMax = Mathf.Max(8, fontSize);
        t.fontStyle = style;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    void MakeButton(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, int fontSize, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.16f, 0.38f, 0.72f, 0.96f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var colors = btn.colors;
        colors.highlightedColor = new Color(0.25f, 0.52f, 0.90f, 1f);
        colors.pressedColor = new Color(0.12f, 0.28f, 0.55f, 1f);
        btn.colors = colors;

        var txt = MakeLabel(go.transform, "Label", label, Vector2.zero, Vector2.one,
            Mathf.Max(6, fontSize), TextAlignmentOptions.Center, FontStyles.Bold);
        txt.raycastTarget = false;
    }

    void ContinueDemolishing()
    {
        if (popupRoot) popupRoot.SetActive(false);
        Debug.Log("[DemolitionTracker] Continue chosen - keep demolishing.");
    }

    void GoToNextLevel()
    {
        if (popupRoot) popupRoot.SetActive(false);
        Debug.Log($"[DemolitionTracker] Next level chosen - loading '{popupNextSceneName}'.");
        if (Application.CanStreamedLevelBeLoaded(popupNextSceneName))
        {
            levelComplete = true;
            SceneManager.LoadScene(popupNextSceneName);
        }
        else
        {
            Debug.LogError($"[DemolitionTracker] Scene '{popupNextSceneName}' is not in the build settings.");
        }
    }
}
