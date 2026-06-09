// ============================================================
// File:        Window_QuestPointer.cs
// Author:      Bryan Wilcutt
// Date:        06/07/2026
// Description: Thin tap receiver attached to the full-screen Canvas
//              Image. Tap routing is now owned entirely by
//              TagSelectionManager — this script is kept as a
//              placeholder in case direct IPointerClickHandler
//              wiring is needed in the future.
//
//              Tag selection and TagBar opening are both handled
//              by TagSelectionManager.Update(). This script no
//              longer needs to do anything on tap.
// ============================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

public class Window_QuestPointer : MonoBehaviour, IPointerClickHandler
{
    // --------------------------------------------------------
    // Inspector fields (kept for backward compat / future use)
    // --------------------------------------------------------

    [Header("References (optional — auto-found at runtime)")]
    [Tooltip("TagBarController — auto-found if left empty.")]
    public TagBarController tagBarController;

    [Tooltip("ARRaycastManager — auto-found if left empty.")]
    public ARRaycastManager arRaycastManager;

    // --------------------------------------------------------
    // Unity lifecycle
    // --------------------------------------------------------

    /// <summary>
    /// Function:   Awake
    /// Inputs:     none
    /// Outputs:    none
    /// Description: Auto-finds references if not assigned in Inspector.
    /// </summary>
    void Awake()
    {
        if (arRaycastManager == null)
            arRaycastManager = FindAnyObjectByType<ARRaycastManager>();

        if (tagBarController == null)
            tagBarController = FindAnyObjectByType<TagBarController>();

        Debug.Log($"Window_QuestPointer: Awake — tagBarController={(tagBarController != null ? tagBarController.name : "null")}");
    }

    // --------------------------------------------------------
    // Tap handler
    // --------------------------------------------------------

    /// <summary>
    /// Function:   OnPointerClick
    /// Inputs:     eventData — Unity pointer event with screen position
    /// Outputs:    none
    /// Description: Tap routing is now fully handled by TagSelectionManager.
    ///              This handler is intentionally a no-op — it exists so the
    ///              Canvas Image continues to receive pointer events, which
    ///              keeps the EventSystem active for UI button clicks.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // Intentionally empty — TagSelectionManager.Update() handles all tap routing.
        // Do NOT add TagBar open logic here; it will double-fire with TagSelectionManager.
    }
}
