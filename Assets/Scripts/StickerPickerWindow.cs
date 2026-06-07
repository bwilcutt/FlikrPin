// =============================================================================
// File:        StickerPickerWindow.cs
// Author:      Bryan Wilcutt
// Date Started: 06/06/2026
// Description: Displays a scrollable sticker picker UI for FlikrPin. A horizontal
//              tab bar at the top lets the user switch between sticker categories.
//              A vertical scrolling grid below shows the stickers in the selected
//              category. Root PNGs in persistentDataPath/Stickers/ appear under
//              a "Default" tab; subdirectories appear as additional tabs.
//
//              Caller assigns OnStickerSelected before calling Show(). The window
//              hides itself and invokes the callback when a sticker is tapped.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class StickerPickerWindow : MonoBehaviour
{
    [Header("UI References")]
    public ScrollRect    stickerScrollRect;   // Vertical scroll view for the sticker grid
    public RectTransform contentRect;         // Content container inside stickerScrollRect
    public ScrollRect    tabScrollRect;       // Horizontal scroll view for the tab bar
    public RectTransform tabContainer;        // Content container inside tabScrollRect
    public GameObject    cellPrefab;          // Prefab for a single sticker cell (RawImage + Button)
    public GameObject    tabPrefab;           // Prefab for a category tab (Button + Text)
    public Button        closeButton;
    public TMPro.TextMeshProUGUI categoryLabel;
    public GameObject    sidebarPanel;        // Hidden while picker is open, restored on close

    [Header("Grid Settings")]
    public float cellSize = 150f;   // Width and height of each sticker cell in pixels
    public float spacing  =  12f;   // Gap between cells in the grid

    [Header("Tab Settings")]
    public float tabWidth    = 180f;
    public float tabHeight   =  60f;
    public Color tabNormal   = new Color(0.85f, 0.85f, 0.85f, 1f);   // Unselected tab color
    public Color tabSelected = new Color(0.3f,  0.6f,  1.0f,  1f);   // Selected tab color

    // Callback invoked with the chosen Texture2D when the user taps a sticker
    public System.Action<Texture2D> OnStickerSelected;

    // ── Private state ─────────────────────────────────────────────────────
    private string        stickerDir;           // Full path to persistentDataPath/Stickers/
    private bool          isLoading       = false;
    private int           currentCat      = 0;  // Index of the currently displayed category
    private List<string>  catNames        = new List<string>();   // Display name per category
    private List<string>  catPaths        = new List<string>();   // Folder path per category
    private List<Button>  tabButtons      = new List<Button>();   // Tab button instances
    private bool          sidebarWasActive = false;   // Remembers sidebar state before open

    // -------------------------------------------------------------------------
    // Function:    Awake
    // Inputs:      None
    // Outputs:     None
    // Description: Resolves the sticker directory path and hides the window.
    //              Window starts hidden and is shown explicitly via Show().
    // -------------------------------------------------------------------------
    void Awake()
    {
        // Build the path to the sticker directory in persistent storage
        stickerDir = Path.Combine(Application.persistentDataPath, "Stickers");
        gameObject.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Function:    Start
    // Inputs:      None
    // Outputs:     None
    // Description: Ensures window is hidden and wires the close button listener.
    // -------------------------------------------------------------------------
    void Start()
    {
        gameObject.SetActive(false);

        // Wire the close button to Hide() so it works from the Inspector reference
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    // -------------------------------------------------------------------------
    // Function:    Preload
    // Inputs:      None
    // Outputs:     None
    // Description: Builds tabs and loads the first category in the background
    //              while the window is invisible. Call this at app startup so
    //              the first Show() is fast.
    // -------------------------------------------------------------------------
    public void Preload()
    {
        // Only preload if we haven't already started loading
        if (catNames.Count == 0 && !isLoading)
        {
            gameObject.SetActive(true);
            StartCoroutine(PreloadThenHide());
        }
    }

    // -------------------------------------------------------------------------
    // Function:    PreloadThenHide
    // Inputs:      None
    // Outputs:     None
    // Description: Waits for StickerCopier to finish, builds tabs, loads the
    //              first category, then hides the window so it's invisible.
    // -------------------------------------------------------------------------
    IEnumerator PreloadThenHide()
    {
        // Wait for sticker files to be copied from StreamingAssets
        while (!StickerCopier.CopyComplete)
            yield return null;

        yield return StartCoroutine(BuildTabs());
        yield return StartCoroutine(LoadCategory(0));

        // Hide after preload — the window will show instantly on first Show()
        gameObject.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Function:    Show
    // Inputs:      None
    // Outputs:     None
    // Description: Shows the sticker picker. Hides the sidebar first so it
    //              doesn't overlap. If categories haven't been built yet, kicks
    //              off the full init coroutine; otherwise just refreshes tab highlight.
    // -------------------------------------------------------------------------
    public void Show()
    {
        // Remember and hide the sidebar to avoid UI overlap
        if (sidebarPanel != null)
        {
            sidebarWasActive = sidebarPanel.activeSelf;
            sidebarPanel.SetActive(false);
        }

        gameObject.SetActive(true);

        if (catNames.Count == 0 && !isLoading)
        {
            // First open — build the full UI
            StartCoroutine(InitAndShow());
        }
        else
        {
            // Already built — just re-highlight the current tab
            UpdateTabHighlight(currentCat);
        }
    }

    // -------------------------------------------------------------------------
    // Function:    InitAndShow
    // Inputs:      None
    // Outputs:     None
    // Description: Waits for sticker copy to complete, then builds tabs and
    //              loads the first category for the initial open.
    // -------------------------------------------------------------------------
    IEnumerator InitAndShow()
    {
        // Wait for sticker files to be available before scanning directories
        while (!StickerCopier.CopyComplete)
            yield return null;

        yield return StartCoroutine(BuildTabs());
        yield return StartCoroutine(LoadCategory(0));
    }

    // -------------------------------------------------------------------------
    // Function:    Hide
    // Inputs:      None
    // Outputs:     None
    // Description: Hides the sticker picker and restores the sidebar to its
    //              previous visibility state.
    // -------------------------------------------------------------------------
    public void Hide()
    {
        // Restore the sidebar if it was showing when we opened
        if (sidebarPanel != null && sidebarWasActive)
            sidebarPanel.SetActive(true);

        gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Tab bar
    // ─────────────────────────────────────────────────────────────────────

    // -------------------------------------------------------------------------
    // Function:    BuildTabs
    // Inputs:      None
    // Outputs:     None
    // Description: Scans the sticker directory for PNGs (Default tab) and
    //              subdirectories (category tabs), then instantiates a tab
    //              button for each. Clears any existing tabs before rebuilding.
    // -------------------------------------------------------------------------
    IEnumerator BuildTabs()
    {
        isLoading = true;

        // Reset category data and tab button list
        catNames.Clear();
        catPaths.Clear();
        tabButtons.Clear();

        // Destroy any previously created tab buttons
        if (tabContainer != null)
            foreach (Transform child in tabContainer)
                Destroy(child.gameObject);

        // Ensure the sticker directory exists
        if (!Directory.Exists(stickerDir))
            Directory.CreateDirectory(stickerDir);

        // Root-level PNGs form the "Default" category
        if (Directory.GetFiles(stickerDir, "*.png").Length > 0)
        {
            catNames.Add("Default");
            catPaths.Add(stickerDir);
        }

        // Each subdirectory that contains PNGs becomes its own category tab
        foreach (string subdir in Directory.GetDirectories(stickerDir))
        {
            if (Directory.GetFiles(subdir, "*.png").Length > 0)
            {
                catNames.Add(Path.GetFileName(subdir));   // Use folder name as tab label
                catPaths.Add(subdir);
            }
        }

        // Nothing to show if no categories were found
        if (catNames.Count == 0)
        {
            isLoading = false;
            yield break;
        }

        // Build the tab button UI for each category
        if (tabContainer != null && tabPrefab != null)
        {
            // Size the tab container to fit all tabs side by side
            tabContainer.sizeDelta = new Vector2(tabWidth * catNames.Count, tabHeight);

            for (int i = 0; i < catNames.Count; i++)
            {
                int idx = i;   // Capture loop variable for the lambda closure

                GameObject tab = Instantiate(tabPrefab, tabContainer);

                // Position each tab sequentially from left to right
                RectTransform tr   = tab.GetComponent<RectTransform>();
                tr.sizeDelta        = new Vector2(tabWidth, tabHeight);
                tr.anchorMin        = new Vector2(0f, 0f);
                tr.anchorMax        = new Vector2(0f, 1f);
                tr.pivot            = new Vector2(0f, 0.5f);
                tr.anchoredPosition = new Vector2(tabWidth * i, 0f);

                // Set the tab label text to the category name
                TMPro.TextMeshProUGUI label = tab.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (label != null)
                    label.text = catNames[i];

                // Wire the tab button to load its category when clicked
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

    // -------------------------------------------------------------------------
    // Function:    OnTabSelected
    // Inputs:      idx — index of the tapped category tab
    // Outputs:     None
    // Description: Switches to the selected category and reloads the sticker
    //              grid. Does nothing if the selected tab is already active.
    // -------------------------------------------------------------------------
    void OnTabSelected(int idx)
    {
        // Ignore taps on the already-selected tab
        if (idx == currentCat) return;

        currentCat = idx;
        UpdateTabHighlight(idx);
        StartCoroutine(LoadCategory(idx));
    }

    // -------------------------------------------------------------------------
    // Function:    UpdateTabHighlight
    // Inputs:      idx — index of the tab to highlight as selected
    // Outputs:     None
    // Description: Colors the selected tab with tabSelected and all others
    //              with tabNormal using the tab button's Image component.
    // -------------------------------------------------------------------------
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

    // ─────────────────────────────────────────────────────────────────────
    // Sticker grid
    // ─────────────────────────────────────────────────────────────────────

    // -------------------------------------------------------------------------
    // Function:    LoadCategory
    // Inputs:      idx — index of the category to load from catPaths
    // Outputs:     None
    // Description: Clears the current sticker grid, sets up the GridLayoutGroup
    //              and ContentSizeFitter, then reads all PNG files from the
    //              selected category folder and builds a cell for each one.
    //              Yields every 6 cells to avoid frame stalls on large sets.
    // -------------------------------------------------------------------------
    IEnumerator LoadCategory(int idx)
    {
        if (idx >= catPaths.Count) yield break;

        isLoading = true;
        UpdateTabHighlight(idx);

        // Remove all existing sticker cells from the grid
        if (contentRect != null)
            foreach (Transform child in contentRect)
                Destroy(child.gameObject);

        // Configure or add a GridLayoutGroup to arrange cells automatically
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

        // ContentSizeFitter makes the content rect grow vertically as cells are added
        ContentSizeFitter fitter = contentRect.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Anchor content to the top-left of the scroll view so it grows downward
        contentRect.anchorMin        = new Vector2(0f, 1f);
        contentRect.anchorMax        = new Vector2(1f, 1f);
        contentRect.pivot            = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;

        // Load every PNG in the category folder and build a sticker cell for each
        string[] files = Directory.GetFiles(catPaths[idx], "*.png");
        int count      = 0;

        foreach (string filePath in files)
        {
            // Read raw PNG bytes and decode to Texture2D
            byte[]    bytes = File.ReadAllBytes(filePath);
            Texture2D tex   = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes);
            tex.name = Path.GetFileNameWithoutExtension(filePath);

            BuildCell(tex);

            count++;

            // Yield every 6 cells to keep the frame rate smooth during loading
            if (count % 6 == 0)
                yield return null;
        }

        yield return null;

        // Force layout rebuild so the grid sizes correctly after all cells are added
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        // Scroll back to the top when switching categories
        if (stickerScrollRect != null)
            stickerScrollRect.verticalNormalizedPosition = 1f;

        isLoading = false;
    }

    // -------------------------------------------------------------------------
    // Function:    ComputeColumns
    // Inputs:      None
    // Outputs:     int — number of columns for the sticker grid
    // Description: Returns the fixed column count for the sticker grid.
    //              Currently hardcoded to 5. Can be made dynamic based on
    //              contentRect width and cellSize if needed.
    // -------------------------------------------------------------------------
    int ComputeColumns()
    {
        return 5;
    }

    // -------------------------------------------------------------------------
    // Function:    BuildCell
    // Inputs:      tex — Texture2D of the sticker to display
    // Outputs:     None
    // Description: Instantiates a cell prefab, assigns the sticker texture to
    //              its RawImage, and wires its Button to invoke OnStickerSelected
    //              and close the picker when tapped.
    // -------------------------------------------------------------------------
    void BuildCell(Texture2D tex)
    {
        GameObject cell = Instantiate(cellPrefab, contentRect);

        // Assign the sticker texture to the cell's RawImage
        RawImage img = cell.GetComponent<RawImage>();
        if (img == null) img = cell.GetComponentInChildren<RawImage>();
        if (img != null)
        {
            img.texture = tex;
            img.color   = Color.white;
        }

        // Configure the button: transparent normally, tinted on hover/press
        Button btn = cell.GetComponent<Button>();
        if (btn == null) btn = cell.GetComponentInChildren<Button>();
        if (btn != null)
        {
            ColorBlock cb       = btn.colors;
            cb.normalColor      = new Color(1f,   1f,   1f,   0f);    // Fully transparent
            cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 0.5f);
            cb.pressedColor     = new Color(0.7f,  0.7f,  0.7f,  0.7f);
            btn.colors          = cb;

            // Capture tex in a local for the lambda — avoids closure over the loop variable
            Texture2D captured = tex;
            btn.onClick.AddListener(() =>
            {
                // Notify the caller with the selected texture, then close the picker
                OnStickerSelected?.Invoke(captured);
                Hide();
            });
        }
    }
}
