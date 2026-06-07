// =============================================================================
// File:        SidebarManager.cs
// Author:      Bryan Wilcutt
// Date Started: 06/06/2026
// Description: Manages sidebar icon ordering and sizing. Fixed icons (Gear,
//              Shopping, Trashcan) are always first, middle, and last
//              respectively. Dynamic icons can be added or removed at runtime
//              and are placed between Shopping and Trashcan. Icon sizes are
//              recalculated whenever the panel rect changes to fill available
//              space within configurable min/max bounds.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SidebarManager : MonoBehaviour
{
    [Header("Fixed Icons (assign in Inspector)")]
    public RectTransform gearIcon;      // Always first in the sidebar
    public RectTransform shoppingIcon;  // Always second in the sidebar
    public RectTransform trashcanIcon;  // Always last in the sidebar

    [Header("Icon Settings")]
    public float paddingTop    = 24f;    // Space above the first icon
    public float paddingBottom = 24f;    // Space below the last icon
    public float spacing       = 12f;    // Gap between icons
    public float minIconSize   = 48f;    // Smallest an icon can be (px)
    public float maxIconSize   = 142f;   // Largest an icon can be (px)

    // ── Private state ─────────────────────────────────────────────────────
    private List<RectTransform> dynamicIcons   = new List<RectTransform>();
    private RectTransform       panelRect;
    private bool                isRecalculating = false;   // Guards against re-entrant layout calls

    // -------------------------------------------------------------------------
    // Function:    Awake
    // Inputs:      None
    // Outputs:     None
    // Description: Caches the panel's RectTransform for use in layout calculations.
    // -------------------------------------------------------------------------
    void Awake()
    {
        panelRect = GetComponent<RectTransform>();
    }

    // -------------------------------------------------------------------------
    // Function:    Start
    // Inputs:      None
    // Outputs:     None
    // Description: Enforces icon sibling order and runs an initial layout pass
    //              once the scene has started.
    // -------------------------------------------------------------------------
    void Start()
    {
        EnforceIconOrder();
        RecalculateLayout();
    }

    // -------------------------------------------------------------------------
    // Function:    OnRectTransformDimensionsChange
    // Inputs:      None
    // Outputs:     None
    // Description: Unity callback fired when this RectTransform is resized.
    //              Triggers a layout recalculation so icons always fill the
    //              panel correctly. Guard flag prevents infinite recursion.
    // -------------------------------------------------------------------------
    void OnRectTransformDimensionsChange()
    {
        // Prevent re-entry if RecalculateLayout itself triggers a resize event
        if (isRecalculating) return;
        RecalculateLayout();
    }

    // -------------------------------------------------------------------------
    // Function:    AddIcon
    // Inputs:      icon — RectTransform of the icon to add dynamically
    // Outputs:     None
    // Description: Adds a dynamic icon to the sidebar if not already present,
    //              re-parents it to this panel, then re-orders and re-layouts.
    // -------------------------------------------------------------------------
    public void AddIcon(RectTransform icon)
    {
        if (!dynamicIcons.Contains(icon))
        {
            dynamicIcons.Add(icon);
            icon.SetParent(transform, false);   // Reparent into sidebar panel
        }

        EnforceIconOrder();
        RecalculateLayout();
    }

    // -------------------------------------------------------------------------
    // Function:    RemoveIcon
    // Inputs:      icon — RectTransform of the icon to remove
    // Outputs:     None
    // Description: Removes a dynamic icon from the sidebar and detaches it from
    //              the panel, then recalculates layout for remaining icons.
    // -------------------------------------------------------------------------
    public void RemoveIcon(RectTransform icon)
    {
        if (dynamicIcons.Contains(icon))
        {
            dynamicIcons.Remove(icon);
            icon.SetParent(null, false);   // Detach from sidebar panel
        }

        RecalculateLayout();
    }

    // -------------------------------------------------------------------------
    // Function:    EnforceIconOrder
    // Inputs:      None
    // Outputs:     None
    // Description: Sets sibling indices to maintain the required display order:
    //              Gear → Shopping → [dynamic icons] → Trashcan.
    // -------------------------------------------------------------------------
    void EnforceIconOrder()
    {
        int index = 0;

        // Fixed positions: Gear first, Shopping second
        if (gearIcon     != null) gearIcon.SetSiblingIndex(index++);
        if (shoppingIcon != null) shoppingIcon.SetSiblingIndex(index++);

        // Dynamic icons fill the middle in the order they were added
        foreach (var icon in dynamicIcons)
            if (icon != null) icon.SetSiblingIndex(index++);

        // Trashcan always goes last
        if (trashcanIcon != null) trashcanIcon.SetSiblingIndex(index);
    }

    // -------------------------------------------------------------------------
    // Function:    RecalculateLayout
    // Inputs:      None
    // Outputs:     None
    // Description: Computes the optimal icon size to fill the panel height given
    //              the current padding, spacing, and icon count, then applies it
    //              to all icons via their LayoutElement components.
    // -------------------------------------------------------------------------
    void RecalculateLayout()
    {
        if (panelRect == null) return;

        isRecalculating = true;

        // Force Unity to update canvas layout so panelRect.rect.height is current
        Canvas.ForceUpdateCanvases();

        float panelHeight = panelRect.rect.height;
        if (panelHeight <= 0f)
        {
            // Panel not yet laid out — bail and wait for the next callback
            isRecalculating = false;
            return;
        }

        // Count how many icons are actually assigned
        int fixedCount = 0;
        if (gearIcon     != null) fixedCount++;
        if (shoppingIcon != null) fixedCount++;
        if (trashcanIcon != null) fixedCount++;

        int totalIcons = fixedCount + dynamicIcons.Count;
        int gaps       = totalIcons - 1;   // Number of spacing gaps between icons

        // Available height after removing padding and inter-icon spacing
        float available = panelHeight - paddingTop - paddingBottom - (spacing * gaps);

        // Divide evenly and clamp to min/max bounds
        float iconSize  = Mathf.Clamp(available / totalIcons, minIconSize, maxIconSize);

        // Apply the computed size to all icons
        ApplySize(gearIcon,     iconSize);
        ApplySize(shoppingIcon, iconSize);
        ApplySize(trashcanIcon, iconSize);
        foreach (var icon in dynamicIcons)
            ApplySize(icon, iconSize);

        isRecalculating = false;
    }

    // -------------------------------------------------------------------------
    // Function:    ApplySize
    // Inputs:      rt   — RectTransform of the icon to resize
    //              size — target width and height in pixels
    // Outputs:     None
    // Description: Sets the LayoutElement preferred and minimum dimensions on
    //              the given RectTransform so the layout system sizes it correctly.
    // -------------------------------------------------------------------------
    void ApplySize(RectTransform rt, float size)
    {
        if (rt == null) return;

        // LayoutElement drives the icon size inside the sidebar's vertical layout group
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
