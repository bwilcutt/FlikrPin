using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages sidebar icon ordering and sizing.
/// - Gear is always first (also anchored to top via Inspector)
/// - Shopping is always second
/// - Dynamic icons in the middle
/// - Compass is always last (also anchored to bottom via Inspector)
/// Attach to SidebarPanel alongside SidebarController.
/// </summary>
public class SidebarManager : MonoBehaviour
{
    [Header("Fixed Icons (assign in Inspector)")]
    public RectTransform gearIcon;
    public RectTransform shoppingIcon;
    public RectTransform compassIcon;

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
        if (compassIcon  != null) compassIcon.SetSiblingIndex(index);
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

        int totalIcons = 2 + dynamicIcons.Count + 1;
        int gaps       = totalIcons - 1;

        float available = panelHeight
                        - paddingTop
                        - paddingBottom
                        - (spacing * gaps);

        float iconSize = Mathf.Clamp(available / totalIcons, minIconSize, maxIconSize);

        // Apply size to gear and shopping only
        // Compass and Gear positions are handled by their anchor settings in Inspector
        ApplySize(gearIcon,     iconSize);
        ApplySize(shoppingIcon, iconSize);
        ApplySize(compassIcon,  iconSize);

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
