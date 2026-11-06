using System.Collections.Generic;
using UnityEngine;
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

    // ── runtime state ──────────────────────────────────────────────────────
    static readonly List<DemolitionBlock> blocks = new List<DemolitionBlock>();
    static int demolishedCount;
    bool levelComplete;
    float tickClock;

    // Full-width of the fill bar (set during HUD creation / Awake)
    float fillBarMaxWidth = 400f;

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

        BuildHUDIfNeeded();
        RefreshHUD();
    }

    void Update()
    {
        tickClock += Time.deltaTime;
        if (tickClock < tickInterval) return;
        tickClock = 0f;

        if (!levelComplete)
        {
            // Poll each block (cheaper than per-block Update)
            foreach (var b in blocks)
                if (b && !b.IsDemolished) b.Tick(tickInterval);

            if (loadNextSceneOnComplete && !levelComplete)
                CheckCompletion();
        }

        RefreshHUD();
    }

    // ── internal helpers ───────────────────────────────────────────────────
    void CheckCompletion()
    {
        if (blocks.Count == 0) return;
        float ratio = (float)demolishedCount / blocks.Count;
        if (ratio >= completionPercent)
        {
            levelComplete = true;
            Debug.Log($"[DemolitionTracker] Level complete! {demolishedCount}/{blocks.Count} blocks demolished.");
            if (loadNextSceneOnComplete)
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
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

        // Update fill bar width
        if (fillBar)
        {
            var sz = fillBar.sizeDelta;
            sz.x = fillBarMaxWidth * ratio;
            fillBar.sizeDelta = sz;
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
}
