// ============================================================
// File:        PlaceObjectOnPlane.cs
// Author:      Bryan Wilcutt
// Date:        06/08/2026
// Description: Detects screen taps on the AR view and opens the
//              TagBar at the tapped AR plane position. Uses
//              Touchscreen.current and legacy Input.touches as
//              fallback to avoid conflicts with the Input System
//              UI Input Module consuming Enhanced Touch events.
//              Tag placement is blocked when the sidebar is open.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(requiredComponent: typeof(ARRaycastManager),
    requiredComponent2: typeof(ARPlaneManager))]
public class PlaceObjectOnPlane : MonoBehaviour
{
    [Header("Tag Bar Controller")]
    public TagBarController postTypeWindow;

    private ARRaycastManager   aRRaycastManager;
    private ARPlaneManager     aRPlaneManager;
    private List<ARRaycastHit> hits            = new List<ARRaycastHit>();
    private SidebarController  sidebarController;

    // -------------------------------------------------------------------------
    // Function:    Awake
    // Inputs:      None
    // Outputs:     None
    // Description: Caches AR component references and finds SidebarController.
    // -------------------------------------------------------------------------
    private void Awake()
    {
        aRRaycastManager  = GetComponent<ARRaycastManager>();
        aRPlaneManager    = GetComponent<ARPlaneManager>();
        sidebarController = FindAnyObjectByType<SidebarController>();

        if (sidebarController == null)
            Debug.LogWarning("PlaceObjectOnPlane: SidebarController not found — sidebar check disabled.");
    }

    // -------------------------------------------------------------------------
    // Function:    Update
    // Inputs:      None
    // Outputs:     None
    // Description: Polls for touch input each frame using Touchscreen.current
    //              and legacy Input.touches as fallback. Blocked when sidebar
    //              is open or TagBar is already visible. When a tap is detected
    //              on empty AR space (not over UI), opens the TagBar at the
    //              AR plane hit position.
    // -------------------------------------------------------------------------
    private void Update()
    {
        // Don't open if sidebar is open
        if (sidebarController != null && sidebarController.IsOpen) return;

        // Don't open if TagBar is already visible
        if (postTypeWindow != null && postTypeWindow.IsVisible) return;

        Vector2? tapPosition = null;

        // New Input System — Touchscreen
        var touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            foreach (var touch in touchscreen.touches)
            {
                if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    tapPosition = touch.position.ReadValue();
                    Debug.Log("PlaceObjectOnPlane: Touchscreen tap at " + tapPosition);
                    break;
                }
            }
        }

        // Legacy input fallback
        if (tapPosition == null && Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == UnityEngine.TouchPhase.Began)
            {
                tapPosition = t.position;
                Debug.Log("PlaceObjectOnPlane: Legacy touch at " + tapPosition);
            }
        }

        // Editor — mouse click
        var mouse = Mouse.current;
        if (tapPosition == null && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            tapPosition = mouse.position.ReadValue();
            Debug.Log("PlaceObjectOnPlane: Mouse click at " + tapPosition);
        }

        if (tapPosition == null) return;

        // Ignore taps over UI elements
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("PlaceObjectOnPlane: Tap blocked by UI.");
            return;
        }

        Debug.Log("PlaceObjectOnPlane: Processing tap at " + tapPosition);

        // AR raycast at tap point
        Vector3 dropPosition;
        if (aRRaycastManager.Raycast(tapPosition.Value, hits, TrackableType.PlaneWithinPolygon))
        {
            dropPosition = hits[0].pose.position;
            Debug.Log("PlaceObjectOnPlane: AR plane hit at " + dropPosition);
        }
        else
        {
            dropPosition = Camera.main.transform.position + Camera.main.transform.forward * 2f;
            Debug.Log("PlaceObjectOnPlane: No AR plane — using camera forward fallback.");
        }

        if (postTypeWindow != null)
            postTypeWindow.Show(tapPosition.Value);
        else
            Debug.LogWarning("PlaceObjectOnPlane: postTypeWindow is not assigned!");
    }
}
