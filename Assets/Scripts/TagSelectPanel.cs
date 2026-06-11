using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays a scrollable two-column list of PostTags owned by the current user.
/// Columns: Type (M/S/T) | Detail (filename / first 20 chars / sticker name)
/// Shows up to 20 rows before scrolling.
///
/// SCENE SETUP:
///   TagSelectPanel (this script, INACTIVE by default)
///     └── Window  (e.g. 700 x 860 px)
///           ├── TitleText
///           ├── HeaderRow        (non-interactive row with "Type" | "Detail" labels)
///           ├── ScrollView
///           │     └── Viewport
///           │           └── Content   ← assign to contentParent
///           └── CloseButton
/// </summary>
public class TagSelectPanel : MonoBehaviour
{
    [Header("References")]
    public Transform  contentParent;
    public GameObject entryPrefab;          // TagEntryPrefab — see below
    public Button     closeButton;

    // Row height in pixels — must match your entryPrefab height
    private const float ROW_HEIGHT = 56f;

    private List<PostTag> entries = new List<PostTag>();

    // -------------------------------------------------------------------------
    void Start()
    {
        gameObject.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
        else
            Debug.LogWarning("TagSelectPanel: CloseButton is NOT assigned!");

        if (contentParent == null) Debug.LogWarning("TagSelectPanel: contentParent NOT assigned!");
        if (entryPrefab   == null) Debug.LogWarning("TagSelectPanel: entryPrefab NOT assigned!");
    }

    // -------------------------------------------------------------------------
    public void Show()
    {
        Debug.Log("TagSelectPanel: Show() called.");
        BuildList();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // -------------------------------------------------------------------------
    void BuildList()
    {
        if (contentParent == null || entryPrefab == null) return;

        // Clear previous rows
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);
        entries.Clear();

        // Collect tags
        PostTag[] allTags = FindObjectsOfType<PostTag>();
        Debug.Log($"TagSelectPanel: Found {allTags.Length} PostTag(s).");

        foreach (PostTag tag in allTags)
        {
            bool isOwner = string.IsNullOrEmpty(UserInfo.userId) ||
                           tag.ownerId == UserInfo.userId;
            if (!isOwner) continue;
            entries.Add(tag);
        }

        if (entries.Count == 0)
        {
            // Show a single disabled "no tags" row
            SpawnRow(null);
            return;
        }

        foreach (PostTag tag in entries)
            SpawnRow(tag);
    }

    // -------------------------------------------------------------------------
    void SpawnRow(PostTag tag)
    {
        GameObject go = Instantiate(entryPrefab, contentParent);

        // Find the two TMP labels by name (see prefab setup notes below)
        TextMeshProUGUI typeLabel   = FindLabel(go, "TypeLabel");
        TextMeshProUGUI detailLabel = FindLabel(go, "DetailLabel");
        Button btn = go.GetComponent<Button>();

        if (tag == null)
        {
            // "No tags" placeholder
            if (typeLabel   != null) typeLabel.text   = "—";
            if (detailLabel != null) detailLabel.text = "No tags found.";
            if (btn != null) btn.interactable = false;
            return;
        }

        // --- Type column ---
        string typeCode = TypeCode(tag.mediaType);
        if (typeLabel != null) typeLabel.text = typeCode;

        // --- Detail column ---
        string detail = BuildDetail(tag);
        if (detailLabel != null) detailLabel.text = detail;

        // --- Click handler ---
        PostTag captured = tag;
        if (btn != null)
            btn.onClick.AddListener(() => OnRowClicked(captured));
    }

    // -------------------------------------------------------------------------
    static string TypeCode(string mediaType)
    {
        switch ((mediaType ?? "").ToLower())
        {
            case "image":
            case "video":   return "M";
            case "sticker": return "S";
            case "text":    return "T";
            default:        return "?";
        }
    }

    // -------------------------------------------------------------------------
    static string BuildDetail(PostTag tag)
    {
        switch ((tag.mediaType ?? "").ToLower())
        {
            case "image":
            case "video":
            {
                string filename = string.IsNullOrEmpty(tag.mediaUrl)
                    ? "(no file)"
                    : System.IO.Path.GetFileName(tag.mediaUrl);
                return Truncate(filename, 20);
            }

            case "text":
                return Truncate(string.IsNullOrEmpty(tag.message) ? "(empty)" : tag.message, 20);

            case "sticker":
                // Use a descriptive name if available, otherwise "Sticker #N"
                return "Sticker #" + tag.stickerIndex;

            default:
                return string.IsNullOrEmpty(tag.mediaType) ? "(unknown)" : tag.mediaType;
        }
    }

    static string Truncate(string s, int max)
        => s.Length <= max ? s : s.Substring(0, max) + "…";

    // -------------------------------------------------------------------------
    static TextMeshProUGUI FindLabel(GameObject root, string childName)
    {
        Transform t = root.transform.Find(childName);
        if (t != null) return t.GetComponent<TextMeshProUGUI>();
        // Fallback: search all children
        foreach (TextMeshProUGUI lbl in root.GetComponentsInChildren<TextMeshProUGUI>())
            if (lbl.gameObject.name == childName) return lbl;
        return null;
    }

    // -------------------------------------------------------------------------
    void OnRowClicked(PostTag tag)
    {
        Debug.Log($"TagSelectPanel: Selected tag postId={tag.postId} type={tag.mediaType}");
        Hide();
    }
}
