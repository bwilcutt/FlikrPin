using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays stickers from persistentDataPath/Stickers/.
/// Horizontal scrolling tab bar at top for category selection.
/// Vertical scrolling grid below for stickers.
/// Root PNGs = "Default" category. Subdirectories = additional categories.
/// </summary>
public class StickerPickerWindow : MonoBehaviour
{
    [Header("UI References")]
    public ScrollRect    stickerScrollRect;  // Vertical scroll for sticker grid
    public RectTransform contentRect;        // Content inside stickerScrollRect
    public ScrollRect    tabScrollRect;      // Horizontal scroll for tab bar
    public RectTransform tabContainer;       // Content inside tabScrollRect
    public GameObject    cellPrefab;         // RawImage + Button cell
    public GameObject    tabPrefab;          // Button + Text tab
    public Button        closeButton;
    public TMPro.TextMeshProUGUI categoryLabel;
    public GameObject    sidebarPanel;       // Hide when picker opens, restore on close

    [Header("Grid Settings")]
    public float cellSize = 150f;
    public float spacing  =  12f;

    [Header("Tab Settings")]
    public float tabWidth   = 180f;
    public float tabHeight  =  60f;
    public Color tabNormal    = new Color(0.85f, 0.85f, 0.85f, 1f);
    public Color tabSelected  = new Color(0.3f,  0.6f,  1.0f,  1f);

    // Callback
    public System.Action<Texture2D> OnStickerSelected;

    // ── private state ─────────────────────────────────────────────────────
    private string           stickerDir;
    private bool             isLoading       = false;
    private int              currentCat      = 0;
    private List<string>     catNames        = new List<string>();
    private List<string>     catPaths        = new List<string>();
    private List<Button>     tabButtons      = new List<Button>();
    private bool             sidebarWasActive = false;

    // ── Unity lifecycle ───────────────────────────────────────────────────

    void Awake()
    {
        stickerDir = Path.Combine(Application.persistentDataPath, "Stickers");
        gameObject.SetActive(false);
    }

    void Start()
    {
        gameObject.SetActive(false);
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    // ── Public API ────────────────────────────────────────────────────────

    public void Preload()
    {
        if (catNames.Count == 0 && !isLoading)
        {
            gameObject.SetActive(true);
            StartCoroutine(PreloadThenHide());
        }
    }

    IEnumerator PreloadThenHide()
    {
        while (!StickerCopier.CopyComplete)
            yield return null;
        yield return StartCoroutine(BuildTabs());
        yield return StartCoroutine(LoadCategory(0));
        gameObject.SetActive(false);
    }

    public void Show()
    {
        // Remember and hide sidebar
        if (sidebarPanel != null)
        {
            sidebarWasActive = sidebarPanel.activeSelf;
            sidebarPanel.SetActive(false);
        }

        gameObject.SetActive(true);
        if (catNames.Count == 0 && !isLoading)
            StartCoroutine(InitAndShow());
        else
            UpdateTabHighlight(currentCat);
    }

    IEnumerator InitAndShow()
    {
        while (!StickerCopier.CopyComplete)
            yield return null;
        yield return StartCoroutine(BuildTabs());
        yield return StartCoroutine(LoadCategory(0));
    }

    public void Hide()
    {
        // Restore sidebar if it was showing before
        if (sidebarPanel != null && sidebarWasActive)
            sidebarPanel.SetActive(true);

        gameObject.SetActive(false);
    }

    // ── Tab bar ───────────────────────────────────────────────────────────

    IEnumerator BuildTabs()
    {
        isLoading = true;

        catNames.Clear();
        catPaths.Clear();
        tabButtons.Clear();

        // Clear existing tabs
        if (tabContainer != null)
            foreach (Transform child in tabContainer)
                Destroy(child.gameObject);

        // Ensure sticker dir exists
        if (!Directory.Exists(stickerDir))
            Directory.CreateDirectory(stickerDir);

        // Root PNGs = Default
        if (Directory.GetFiles(stickerDir, "*.png").Length > 0)
        {
            catNames.Add("Default");
            catPaths.Add(stickerDir);
        }

        // Subdirectories = categories
        foreach (string subdir in Directory.GetDirectories(stickerDir))
        {
            if (Directory.GetFiles(subdir, "*.png").Length > 0)
            {
                catNames.Add(Path.GetFileName(subdir));
                catPaths.Add(subdir);
            }
        }

        if (catNames.Count == 0)
        {
            isLoading = false;
            yield break;
        }

        // Build tab buttons
        if (tabContainer != null && tabPrefab != null)
        {
            // Size tab container
            tabContainer.sizeDelta = new Vector2(tabWidth * catNames.Count, tabHeight);

            for (int i = 0; i < catNames.Count; i++)
            {
                int idx = i;
                GameObject tab = Instantiate(tabPrefab, tabContainer);
                RectTransform tr = tab.GetComponent<RectTransform>();
                tr.sizeDelta        = new Vector2(tabWidth, tabHeight);
                tr.anchorMin        = new Vector2(0f, 0f);
                tr.anchorMax        = new Vector2(0f, 1f);
                tr.pivot            = new Vector2(0f, 0.5f);
                tr.anchoredPosition = new Vector2(tabWidth * i, 0f);

                // Set tab label
                TMPro.TextMeshProUGUI label = tab.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (label != null)
                    label.text = catNames[i];

                // Wire button
                Button btn = tab.GetComponent<Button>();
                if (btn != null)
                {
                    tabButtons.Add(btn);
                    btn.onClick.AddListener(() => OnTabSelected(idx));
                }
            }
        }

        isLoading = false;
        yield return null;
    }

    void OnTabSelected(int idx)
    {
        if (idx == currentCat) return;
        currentCat = idx;
        UpdateTabHighlight(idx);
        StartCoroutine(LoadCategory(idx));
    }

    void UpdateTabHighlight(int idx)
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            if (tabButtons[i] == null) continue;
            Image img = tabButtons[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == idx) ? tabSelected : tabNormal;
        }
    }

    // ── Sticker grid ──────────────────────────────────────────────────────

    IEnumerator LoadCategory(int idx)
    {
        if (idx >= catPaths.Count) yield break;

        isLoading = true;
        UpdateTabHighlight(idx);

        // Clear existing cells
        if (contentRect != null)
            foreach (Transform child in contentRect)
                Destroy(child.gameObject);

        // Set up GridLayoutGroup
        GridLayoutGroup grid = contentRect.GetComponent<GridLayoutGroup>();
        if (grid == null)
            grid = contentRect.gameObject.AddComponent<GridLayoutGroup>();

        grid.cellSize        = new Vector2(cellSize, cellSize);
        grid.spacing         = new Vector2(spacing, spacing);
        grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis       = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment  = TextAnchor.UpperLeft;
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = ComputeColumns();
        grid.padding         = new RectOffset(16, 16, 16, 16);

        // ContentSizeFitter
        ContentSizeFitter fitter = contentRect.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Anchor Content top-left
        contentRect.anchorMin        = new Vector2(0f, 1f);
        contentRect.anchorMax        = new Vector2(1f, 1f);
        contentRect.pivot            = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;

        // Load PNGs
        string[] files = Directory.GetFiles(catPaths[idx], "*.png");
        int count = 0;
        foreach (string filePath in files)
        {
            byte[]    bytes = File.ReadAllBytes(filePath);
            Texture2D tex   = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes);
            tex.name = Path.GetFileNameWithoutExtension(filePath);
            BuildCell(tex);

            count++;
            if (count % 6 == 0)
                yield return null;
        }

        yield return null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        if (stickerScrollRect != null)
            stickerScrollRect.verticalNormalizedPosition = 1f;

        isLoading = false;
        Debug.Log("StickerPickerWindow: Loaded " + files.Length + " stickers for category '" + catNames[idx] + "'.");
    }

    int ComputeColumns()
    {
        return 5;
    }

    void BuildCell(Texture2D tex)
    {
        GameObject cell = Instantiate(cellPrefab, contentRect);

        RawImage img = cell.GetComponent<RawImage>();
        if (img == null) img = cell.GetComponentInChildren<RawImage>();
        if (img != null)
        {
            img.texture = tex;
            img.color   = Color.white;
        }

        Button btn = cell.GetComponent<Button>();
        if (btn == null) btn = cell.GetComponentInChildren<Button>();
        if (btn != null)
        {
            ColorBlock cb       = btn.colors;
            cb.normalColor      = new Color(1f, 1f, 1f, 0f);
            cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 0.5f);
            cb.pressedColor     = new Color(0.7f,  0.7f,  0.7f,  0.7f);
            btn.colors          = cb;

            Texture2D captured = tex;
            btn.onClick.AddListener(() =>
            {
                OnStickerSelected?.Invoke(captured);
                Hide();
            });
        }
    }
}
