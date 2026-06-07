// =============================================================================
// File:        CompassBar.cs
// Author:      Bryan Wilcutt
// Date Started: 06/06/2026
// Description: Draws a scrolling compass bar UI at the bottom of the screen.
//              Ticks and cardinal/intercardinal labels scroll as the device
//              heading changes. Heading is sourced from CompassManager, which
//              reads the native Android CompassPlugin (accelerometer +
//              magnetometer + gyroscope fusion). Falls back to ARCamera yaw
//              in the Unity Editor for test-spinning.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CompassBar : MonoBehaviour
{
    [Header("Dimensions")]
    public float barHeight      = 60f;   // Height of the compass bar in pixels
    public float degreesVisible = 90f;   // How many degrees of arc are shown across the full bar width

    [Header("Appearance")]
    public Color backgroundColor      = new Color(0f, 0f, 0f, 0.55f);
    public Color tickColor             = new Color(1f, 1f, 1f, 0.8f);
    public Color cardinalColor         = Color.white;
    public Color intercardinalColor    = new Color(1f, 1f, 0.5f, 1f);
    public Color centerMarkerColor     = new Color(1f, 0.4f, 0.1f, 1f);
    public float majorTickHeight       = 20f;
    public float minorTickHeight       = 10f;
    public float labelFontSize         = 20f;
    public float intercardinalFontSize = 14f;

    [Header("Smoothing")]
    [Range(1f, 30f)]
    public float smoothSpeed = 5f;   // Higher = snappier response, lower = more lag

    [Header("Editor Test")]
    public bool editorTestSpin = false;   // Spins the bar automatically in the editor for visual testing

    // ── private state ─────────────────────────────────────────────────────
    private RectTransform     barRect;        // RectTransform of this compass bar GameObject
    private TextMeshProUGUI[] labelObjects;   // N, NE, E, SE, S, SW, W, NW label instances
    private RectTransform[]   tickMarks;      // All 72 tick mark instances (one per 5°)
    private float             currentHeading = 0f;   // Smoothed heading currently displayed
    private bool              built          = false; // True once UI elements are created

    // 72 ticks covers the full 360° at 5° intervals
    private const int TICK_COUNT = 72;

    // Label definitions: degree position and display string
    private static readonly (float deg, string label)[] Labels =
    {
        (0f,   "N"),  (45f,  "NE"), (90f,  "E"),  (135f, "SE"),
        (180f, "S"),  (225f, "SW"), (270f, "W"),   (315f, "NW"),
    };

    // -------------------------------------------------------------------------
    // Function:    Start
    // Inputs:      None
    // Outputs:     None
    // Description: Builds all compass bar UI elements on scene start.
    // -------------------------------------------------------------------------
    void Start()
    {
        // Build the bar background and center marker
        BuildBar();

        // Create the N/NE/E/etc. label objects
        CreateLabels();

        // Pre-create all 72 tick mark objects
        CreateTicks();

        // Mark as ready so Update() will begin rendering
        built = true;
    }

    // -------------------------------------------------------------------------
    // Function:    Update
    // Inputs:      None
    // Outputs:     None
    // Description: Reads heading from CompassManager (device) or ARCamera yaw
    //              (editor), smooths it with LerpAngle, then redraws ticks
    //              and labels each frame.
    // -------------------------------------------------------------------------
    void Update()
    {
        // Don't render until the bar is fully built
        if (!built) return;

        float targetHeading = currentHeading;

#if UNITY_EDITOR
        if (editorTestSpin)
        {
            // Spin the bar at 45°/sec for visual testing in the editor
            targetHeading = (currentHeading + 45f * Time.deltaTime) % 360f;
        }
        else
        {
            // In editor without spin, use ARCamera yaw as a stand-in for compass heading
            Camera arCamera = Camera.main;
            if (arCamera != null)
                targetHeading = arCamera.transform.eulerAngles.y;
        }
#else
        // On device, read the fused heading from the native compass singleton
        if (CompassManager.Instance != null)
            targetHeading = CompassManager.Instance.Heading;
#endif

        // Smooth the heading change using LerpAngle to handle 0/360 wrap correctly
        currentHeading = Mathf.LerpAngle(currentHeading, targetHeading, smoothSpeed * Time.deltaTime);

        // Keep heading in [0, 360) range after lerp
        if (currentHeading < 0f) currentHeading += 360f;

        // Reposition labels and ticks to reflect the updated heading
        UpdateLabels();
        DrawTicks();
    }

    // -------------------------------------------------------------------------
    // Function:    BuildBar
    // Inputs:      None
    // Outputs:     None
    // Description: Creates and positions the bar background Image and the
    //              center marker line that indicates the current heading.
    // -------------------------------------------------------------------------
    void BuildBar()
    {
        // Get or add a RectTransform on this GameObject
        barRect = GetComponent<RectTransform>();
        if (barRect == null) barRect = gameObject.AddComponent<RectTransform>();

        // Anchor the bar to the bottom edge of the screen at full width
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot     = new Vector2(0.5f, 0f);
        barRect.offsetMin = new Vector2(0f, 0f);
        barRect.offsetMax = new Vector2(0f, barHeight);

        // Semi-transparent black background spanning the full bar rect
        var bgGO   = new GameObject("BG", typeof(Image));
        bgGO.transform.SetParent(transform, false);
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        bgGO.GetComponent<Image>().color = backgroundColor;

        // Narrow vertical line at the center of the bar indicating current heading
        var cmGO   = new GameObject("CenterLine", typeof(Image));
        cmGO.transform.SetParent(transform, false);
        var cmRect = cmGO.GetComponent<RectTransform>();
        cmRect.anchorMin  = new Vector2(0.5f, 0f);
        cmRect.anchorMax  = new Vector2(0.5f, 1f);
        cmRect.pivot      = new Vector2(0.5f, 0.5f);
        cmRect.offsetMin  = new Vector2(-1.5f, 0f);   // 3px wide total
        cmRect.offsetMax  = new Vector2( 1.5f, 0f);
        cmGO.GetComponent<Image>().color = centerMarkerColor;
    }

    // -------------------------------------------------------------------------
    // Function:    CreateLabels
    // Inputs:      None
    // Outputs:     None
    // Description: Instantiates a TextMeshProUGUI GameObject for each entry in
    //              the Labels array. Cardinals (N/E/S/W) are larger and bold;
    //              intercardinals (NE/SE/SW/NW) are smaller and tinted yellow.
    // -------------------------------------------------------------------------
    void CreateLabels()
    {
        labelObjects = new TextMeshProUGUI[Labels.Length];

        for (int i = 0; i < Labels.Length; i++)
        {
            // Cardinals are every 90°; intercardinals are every 45° (non-90°)
            bool isCardinal = (Labels[i].deg % 90 == 0);

            var go = new GameObject("Lbl_" + Labels[i].label,
                                    typeof(RectTransform),
                                    typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);

            // Anchor label to bar height, centered horizontally (position set in UpdateLabels)
            var rt       = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(50f, 0f);

            // Style differs between cardinal and intercardinal labels
            var tmp       = go.GetComponent<TextMeshProUGUI>();
            tmp.text      = Labels[i].label;
            tmp.fontSize  = isCardinal ? labelFontSize : intercardinalFontSize;
            tmp.color     = isCardinal ? cardinalColor : intercardinalColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = isCardinal ? FontStyles.Bold : FontStyles.Normal;

            labelObjects[i] = tmp;
        }
    }

    // -------------------------------------------------------------------------
    // Function:    UpdateLabels
    // Inputs:      None
    // Outputs:     None
    // Description: Each frame, repositions each label horizontally based on its
    //              angular offset from the current heading. Labels too far off
    //              center are hidden to avoid cluttering the bar edges.
    // -------------------------------------------------------------------------
    void UpdateLabels()
    {
        if (labelObjects == null) return;

        float barWidth = barRect.rect.width;
        if (barWidth <= 0f) return;   // Bar not laid out yet

        // Pixels per degree based on visible arc width
        float pxPerDeg = barWidth / degreesVisible;

        for (int i = 0; i < Labels.Length; i++)
        {
            // Signed angular distance from current heading to this label's degree position
            // Negative = label is to the left, positive = label is to the right
            float delta = Mathf.DeltaAngle(currentHeading, Labels[i].deg);

            // Hide labels that are outside the visible portion of the bar
            bool visible = Mathf.Abs(delta) < degreesVisible * 0.6f;
            labelObjects[i].gameObject.SetActive(visible);
            if (!visible) continue;

            // Position the label horizontally relative to bar center
            labelObjects[i].GetComponent<RectTransform>().anchoredPosition =
                new Vector2(delta * pxPerDeg, 0f);
        }
    }

    // -------------------------------------------------------------------------
    // Function:    CreateTicks
    // Inputs:      None
    // Outputs:     None
    // Description: Pre-creates 72 Image GameObjects (one per 5° around the full
    //              360°) to use as tick marks. Actual position and size are set
    //              each frame in DrawTicks().
    // -------------------------------------------------------------------------
    void CreateTicks()
    {
        tickMarks = new RectTransform[TICK_COUNT];

        for (int i = 0; i < TICK_COUNT; i++)
        {
            // Name each tick by its degree value for easy identification in hierarchy
            var go = new GameObject("Tick_" + (i * 5), typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            go.GetComponent<Image>().color = tickColor;
            tickMarks[i] = go.GetComponent<RectTransform>();
        }
    }

    // -------------------------------------------------------------------------
    // Function:    DrawTicks
    // Inputs:      None
    // Outputs:     None
    // Description: Each frame, repositions and resizes each tick mark based on
    //              its angular offset from the current heading. Tick height varies
    //              by interval: major every 45°, medium every 15°, minor every 5°.
    // -------------------------------------------------------------------------
    void DrawTicks()
    {
        if (tickMarks == null) return;

        float barWidth = barRect.rect.width;
        if (barWidth <= 0f) return;   // Bar not laid out yet

        float pxPerDeg = barWidth / degreesVisible;

        for (int i = 0; i < TICK_COUNT; i++)
        {
            // Degree position this tick represents
            float deg = i * 5f;

            // Signed angular offset from current heading (negative = left, positive = right)
            float delta = Mathf.DeltaAngle(currentHeading, deg);

            // Hide ticks outside the visible arc
            bool visible = Mathf.Abs(delta) < degreesVisible * 0.6f;
            tickMarks[i].gameObject.SetActive(visible);
            if (!visible) continue;

            // Determine tick height by interval:
            // i % 9 == 0 → every 45° = major tick
            // i % 3 == 0 → every 15° = medium tick
            // otherwise  → every  5° = minor tick
            bool  isMajor = (i % 9 == 0);
            bool  isMid   = (i % 3 == 0);
            float h       = isMajor ? majorTickHeight
                          : isMid   ? majorTickHeight * 0.6f
                          :           minorTickHeight;

            // Anchor tick to bottom of bar, position horizontally by delta
            tickMarks[i].anchorMin        = new Vector2(0.5f, 0f);
            tickMarks[i].anchorMax        = new Vector2(0.5f, 0f);
            tickMarks[i].pivot            = new Vector2(0.5f, 0f);
            tickMarks[i].sizeDelta        = new Vector2(2f, h);   // 2px wide
            tickMarks[i].anchoredPosition = new Vector2(delta * pxPerDeg, 0f);
        }
    }
}
