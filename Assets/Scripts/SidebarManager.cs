using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages sidebar icon ordering and sizing.
/// - Gear is always first
/// - Shopping is always second
/// - Dynamic icons in the middle
/// - Trashcan is always last
/// Attach to SidebarPanel alongside SidebarController.
/// </summary>
public class SidebarManager : MonoBehaviour
{
    [Header("Fixed Icons (assign in Inspector)")]
    public RectTransform gearIcon;
    public RectTransform shoppingIcon;
    public RectTransform trashcanIcon;

    [Header("Icon Settings")]
    public float paddingTop    = 24f;
    public float paddingBottom = 24f;
    public float spacing       = 12f;
    public float minIconSize   = 48f;
    public float maxIconSize   = 142f;

    private List<RectTransform> dynamicIcons = new List<RectTransform>();
    private RectTransform panelRect;
    private bool isRecalculating = false;

    void Awake()
    {
        panelRect = GetComponent<RectTransform>();
    }

    void Start()
    {
        EnforceIconOrder();
        RecalculateLayout();
    }

    void OnRectTransformDimensionsChange()
    {
        if (isRecalculating) return;
        RecalculateLayout();
    }

    public void AddIcon(RectTransform icon)
    {
        if (!dynamicIcons.Contains(icon))
        {
            dynamicIcons.Add(icon);
            icon.SetParent(transform, false);
        }
        EnforceIconOrder();
        RecalculateLayout();
    }

    public void RemoveIcon(RectTransform icon)
    {
        if (dynamicIcons.Contains(icon))
        {
            dynamicIcons.Remove(icon);
            icon.SetParent(null, false);
        }
        RecalculateLayout();
    }

    void EnforceIconOrder()
    {
        int index = 0;
        if (gearIcon     != null) gearIcon.SetSiblingIndex(index++);
        if (shoppingIcon != null) shoppingIcon.SetSiblingIndex(index++);
        foreach (var icon in dynamicIcons)
            if (icon != null) icon.SetSiblingIndex(index++);
        if (trashcanIcon != null) trashcanIcon.SetSiblingIndex(index); // always last
    }

    void RecalculateLayout()
    {
        if (panelRect == null) return;

        isRecalculating = true;
        Canvas.ForceUpdateCanvases();

        float panelHeight = panelRect.rect.height;
        if (panelHeight <= 0f)
        {
            isRecalculating = false;
            return;
        }

        // Count only non-null fixed icons + dynamic icons
        int fixedCount = 0;
        if (gearIcon     != null) fixedCount++;
        if (shoppingIcon != null) fixedCount++;
        if (trashcanIcon != null) fixedCount++;

        int totalIcons = fixedCount + dynamicIcons.Count;
        int gaps       = totalIcons - 1;

        float available = panelHeight
                        - paddingTop
                        - paddingBottom
                        - (spacing * gaps);

        float iconSize = Mathf.Clamp(available / totalIcons, minIconSize, maxIconSize);

        ApplySize(gearIcon,     iconSize);
        ApplySize(shoppingIcon, iconSize);
        ApplySize(trashcanIcon, iconSize);

        foreach (var icon in dynamicIcons)
            ApplySize(icon, iconSize);

        isRecalculating = false;
    }

    void ApplySize(RectTransform rt, float size)
    {
        if (rt == null) return;
        LayoutElement le = rt.GetComponent<LayoutElement>();
        if (le != null)
        {
            le.preferredWidth  = size;
            le.preferredHeight = size;
            le.minWidth        = size;
            le.minHeight       = size;
        }
    }
}
