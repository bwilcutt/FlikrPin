using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CompassBar : MonoBehaviour
{
    [Header("Dimensions")]
    public float barHeight      = 60f;
    public float degreesVisible = 90f;

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
    public float smoothSpeed = 5f;

    [Header("Editor Test")]
    public bool editorTestSpin = false;

    // ── private ───────────────────────────────────────────────────────────
    private RectTransform     barRect;
    private TextMeshProUGUI[] labelObjects;
    private RectTransform[]   tickMarks;
    private float             currentHeading = 0f;
    private bool              built          = false;

    private const int TICK_COUNT = 72; // every 5°

    private static readonly (float deg, string label)[] Labels =
    {
        (0f,   "N"),  (45f,  "NE"), (90f,  "E"),  (135f, "SE"),
        (180f, "S"),  (225f, "SW"), (270f, "W"),   (315f, "NW"),
    };

    // ── lifecycle ──────────────────────────────────────────────────────────
    void Start()
    {
        Debug.Log("CompassBar: Start()");

        BuildBar();
        CreateLabels();
        CreateTicks();
        built = true;

        Debug.Log("CompassBar: ready.");
    }

    void Update()
    {
        if (!built) return;

        float targetHeading = currentHeading;

#if UNITY_EDITOR
        if (editorTestSpin)
            targetHeading = (currentHeading + 45f * Time.deltaTime) % 360f;
#else
        // trueHeading uses sensor fusion; falls back to magneticHeading if GPS unavailable
        Camera arCamera = Camera.main;
        if (arCamera != null)
            targetHeading = arCamera.transform.eulerAngles.y;
#endif

        currentHeading = Mathf.LerpAngle(currentHeading, targetHeading, smoothSpeed * Time.deltaTime);
        if (currentHeading < 0f) currentHeading += 360f;

        UpdateLabels();
        DrawTicks();
    }

    // ── build UI ───────────────────────────────────────────────────────────
    void BuildBar()
    {
        Debug.Log("CompassBar: BuildBar()");

        barRect = GetComponent<RectTransform>();
        if (barRect == null) barRect = gameObject.AddComponent<RectTransform>();

        // Pin to bottom of screen, full width
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot     = new Vector2(0.5f, 0f);
        barRect.offsetMin = new Vector2(0f, 0f);
        barRect.offsetMax = new Vector2(0f, barHeight);

        // Background
        var bgGO   = new GameObject("BG", typeof(Image));
        bgGO.transform.SetParent(transform, false);
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        bgGO.GetComponent<Image>().color = backgroundColor;

        // Center marker
        var cmGO   = new GameObject("CenterLine", typeof(Image));
        cmGO.transform.SetParent(transform, false);
        var cmRect = cmGO.GetComponent<RectTransform>();
        cmRect.anchorMin  = new Vector2(0.5f, 0f);
        cmRect.anchorMax  = new Vector2(0.5f, 1f);
        cmRect.pivot      = new Vector2(0.5f, 0.5f);
        cmRect.offsetMin  = new Vector2(-1.5f, 0f);
        cmRect.offsetMax  = new Vector2( 1.5f, 0f);
        cmGO.GetComponent<Image>().color = centerMarkerColor;

        Debug.Log("CompassBar: BuildBar() complete.");
    }

    // ── labels ─────────────────────────────────────────────────────────────
    void CreateLabels()
    {
        labelObjects = new TextMeshProUGUI[Labels.Length];
        for (int i = 0; i < Labels.Length; i++)
        {
            bool isCardinal = (Labels[i].deg % 90 == 0);
            var  go         = new GameObject("Lbl_" + Labels[i].label,
                                             typeof(RectTransform),
                                             typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);

            var rt       = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(50f, 0f);

            var tmp       = go.GetComponent<TextMeshProUGUI>();
            tmp.text      = Labels[i].label;
            tmp.fontSize  = isCardinal ? labelFontSize : intercardinalFontSize;
            tmp.color     = isCardinal ? cardinalColor : intercardinalColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = isCardinal ? FontStyles.Bold : FontStyles.Normal;

            labelObjects[i] = tmp;
        }
        Debug.Log("CompassBar: CreateLabels() complete.");
    }

    void UpdateLabels()
    {
        if (labelObjects == null) return;
        float barWidth = barRect.rect.width;
        if (barWidth <= 0f) return;
        float pxPerDeg = barWidth / degreesVisible;

        for (int i = 0; i < Labels.Length; i++)
        {
            float delta   = Mathf.DeltaAngle(currentHeading, Labels[i].deg);
            bool  visible = Mathf.Abs(delta) < degreesVisible * 0.6f;
            labelObjects[i].gameObject.SetActive(visible);
            if (!visible) continue;
            labelObjects[i].GetComponent<RectTransform>().anchoredPosition =
                new Vector2(delta * pxPerDeg, 0f);
        }
    }

    // ── ticks ──────────────────────────────────────────────────────────────
    void CreateTicks()
    {
        tickMarks = new RectTransform[TICK_COUNT];
        for (int i = 0; i < TICK_COUNT; i++)
        {
            var go = new GameObject("Tick_" + (i * 5), typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            go.GetComponent<Image>().color = tickColor;
            tickMarks[i] = go.GetComponent<RectTransform>();
        }
    }

    void DrawTicks()
    {
        if (tickMarks == null) return;
        float barWidth = barRect.rect.width;
        if (barWidth <= 0f) return;
        float pxPerDeg = barWidth / degreesVisible;

        for (int i = 0; i < TICK_COUNT; i++)
        {
            float deg     = i * 5f;
            float delta   = Mathf.DeltaAngle(currentHeading, deg);
            bool  visible = Mathf.Abs(delta) < degreesVisible * 0.6f;
            tickMarks[i].gameObject.SetActive(visible);
            if (!visible) continue;

            bool  isMajor = (i % 9 == 0);
            bool  isMid   = (i % 3 == 0);
            float h       = isMajor ? majorTickHeight : (isMid ? majorTickHeight * 0.6f : minorTickHeight);

            tickMarks[i].anchorMin        = new Vector2(0.5f, 0f);
            tickMarks[i].anchorMax        = new Vector2(0.5f, 0f);
            tickMarks[i].pivot            = new Vector2(0.5f, 0f);
            tickMarks[i].sizeDelta        = new Vector2(2f, h);
            tickMarks[i].anchoredPosition = new Vector2(delta * pxPerDeg, 0f);
        }
    }
}
