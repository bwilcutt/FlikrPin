// =============================================================================
// File:        SidebarController.cs
// Author:      Bryan Wilcutt
// Date:        06/08/2026
// Description: Controls the SidebarPanel slide animation. The panel slides in
//              from the left on launch and can be hidden by swiping left, then
//              revealed again by swiping right from the left edge of the screen.
//
//              When the sidebar is open, PlaceObjectOnPlane is disabled so the
//              user cannot accidentally place tags while interacting with the
//              sidebar. When the sidebar is closed, PlaceObjectOnPlane is
//              re-enabled.
//
//              Do NOT merge with SidebarManager.cs — that script handles icon
//              sizing only. This script handles show/hide animation.
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;

public class SidebarController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector fields
    // -------------------------------------------------------------------------

    [Header("Panel Reference")]
    [Tooltip("The SidebarPanel RectTransform to slide in and out.")]
    public RectTransform sidebarPanel;

    [Header("Placement Reference")]
    [Tooltip("PlaceObjectOnPlane component — disabled when sidebar is open.")]
    public PlaceObjectOnPlane placeObjectOnPlane;

    [Header("Slide Settings")]
    [Tooltip("How far off-screen to the left the panel slides when hidden. " +
             "Should match or exceed the panel width in pixels.")]
    public float hiddenX = -200f;

    [Tooltip("X position when the panel is fully visible (usually 0).")]
    public float visibleX = 0f;

    [Tooltip("Duration of the slide animation in seconds.")]
    public float slideDuration = 0.25f;

    [Header("Swipe Settings")]
    [Tooltip("Minimum horizontal swipe distance in pixels to trigger open/close.")]
    public float swipeThreshold = 80f;

    [Tooltip("Maximum vertical drift allowed before a swipe is ignored (keeps " +
             "it from triggering on vertical scrolls).")]
    public float verticalTolerance = 120f;

    [Tooltip("How far from the left edge (in pixels) a swipe must start to " +
             "count as an open gesture.")]
    public float edgeZone = 60f;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private bool    isOpen          = true;    // Sidebar starts open on launch
    private bool    isAnimating     = false;   // True while slide is running
    private float   animTimer       = 0f;      // Elapsed time in current animation
    private float   animStartX      = 0f;      // anchoredPosition.x at animation start
    private float   animTargetX     = 0f;      // anchoredPosition.x at animation end

    private bool    trackingSwipe   = false;   // True while a swipe is in progress
    private Vector2 swipeStartPos;             // Screen position where swipe began

    // -------------------------------------------------------------------------
    // Function:    Start
    // Inputs:      None
    // Outputs:     None
    // Description: Auto-finds PlaceObjectOnPlane if not assigned. Sets the
    //              sidebar to the open position on launch and updates placement.
    // -------------------------------------------------------------------------
    void Start()
    {
        if (placeObjectOnPlane == null)
            placeObjectOnPlane = FindAnyObjectByType<PlaceObjectOnPlane>();

        if (placeObjectOnPlane == null)
            Debug.LogWarning("SidebarController: PlaceObjectOnPlane not found — " +
                             "tag placement blocking will not work.");

        if (sidebarPanel == null)
        {
            Debug.LogError("SidebarController: sidebarPanel is not assigned!");
            return;
        }

        // Snap to open position immediately on launch
        SetPanelX(visibleX);
        isOpen = true;
        UpdatePlacement();

        Debug.Log("SidebarController: Start — sidebar open.");
    }

    // -------------------------------------------------------------------------
    // Function:    Update
    // Inputs:      None
    // Outputs:     None
    // Description: Each frame, advances any running slide animation and checks
    //              for swipe gestures using Touchscreen.current.
    // -------------------------------------------------------------------------
    void Update()
    {
        // Advance slide animation
        if (isAnimating)
        {
            animTimer += Time.deltaTime;
            float t = Mathf.Clamp01(animTimer / slideDuration);
            // Smooth step easing
            t = t * t * (3f - 2f * t);
            SetPanelX(Mathf.Lerp(animStartX, animTargetX, t));

            if (animTimer >= slideDuration)
            {
                SetPanelX(animTargetX);
                isAnimating = false;
                Debug.Log($"SidebarController: Animation complete — isOpen={isOpen}");
            }
            return;
        }

        // Detect swipe gestures via Touchscreen
        DetectSwipe();
    }

    // -------------------------------------------------------------------------
    // Function:    DetectSwipe
    // Inputs:      None
    // Outputs:     None
    // Description: Tracks touch start and end positions. On touch end, evaluates
    //              whether the gesture qualifies as an open or close swipe and
    //              triggers the appropriate animation.
    // -------------------------------------------------------------------------
    void DetectSwipe()
    {
        var ts = Touchscreen.current;
        if (ts == null) return;

        // Use the primary touch only
        var touch = ts.primaryTouch;
        var phase = touch.phase.ReadValue();

        if (phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            swipeStartPos  = touch.position.ReadValue();
            trackingSwipe  = true;
        }
        else if (phase == UnityEngine.InputSystem.TouchPhase.Ended && trackingSwipe)
        {
            trackingSwipe = false;
            Vector2 swipeEnd   = touch.position.ReadValue();
            Vector2 delta      = swipeEnd - swipeStartPos;
            float   absX       = Mathf.Abs(delta.x);
            float   absY       = Mathf.Abs(delta.y);

            // Must be primarily horizontal and exceed threshold
            if (absX < swipeThreshold || absY > verticalTolerance) return;

            if (delta.x > 0 && !isOpen)
            {
                // Swipe right — only open if swipe started near left edge
                if (swipeStartPos.x <= edgeZone)
                {
                    Debug.Log("SidebarController: Swipe right — opening sidebar.");
                    Open();
                }
            }
            else if (delta.x < 0 && isOpen)
            {
                // Swipe left — close sidebar
                Debug.Log("SidebarController: Swipe left — closing sidebar.");
                Close();
            }
        }
        else if (phase == UnityEngine.InputSystem.TouchPhase.None)
        {
            trackingSwipe = false;
        }
    }

    // -------------------------------------------------------------------------
    // Function:    Open
    // Inputs:      None
    // Outputs:     None
    // Description: Slides the sidebar panel into view and disables tag placement.
    // -------------------------------------------------------------------------
    public void Open()
    {
        if (isOpen || isAnimating) return;
        isOpen = true;
        StartSlide(visibleX);
        UpdatePlacement();
    }

    // -------------------------------------------------------------------------
    // Function:    Close
    // Inputs:      None
    // Outputs:     None
    // Description: Slides the sidebar panel off-screen and enables tag placement.
    // -------------------------------------------------------------------------
    public void Close()
    {
        if (!isOpen || isAnimating) return;
        isOpen = false;
        StartSlide(hiddenX);
        UpdatePlacement();
    }

    // -------------------------------------------------------------------------
    // Function:    Toggle
    // Inputs:      None
    // Outputs:     None
    // Description: Toggles the sidebar between open and closed states.
    //              Safe to call from a Button onClick event.
    // -------------------------------------------------------------------------
    public void Toggle()
    {
        if (isOpen) Close();
        else        Open();
    }

    // -------------------------------------------------------------------------
    // Function:    IsOpen
    // Inputs:      None
    // Outputs:     bool — true if sidebar is currently open or animating open
    // Description: Returns the current open state of the sidebar.
    // -------------------------------------------------------------------------
    public bool IsOpen => isOpen;

    // ── Private helpers ───────────────────────────────────────────────────────

    // -------------------------------------------------------------------------
    // Function:    StartSlide
    // Inputs:      targetX — destination anchoredPosition.x
    // Outputs:     None
    // Description: Begins a slide animation from the current panel position
    //              to the given target X.
    // -------------------------------------------------------------------------
    void StartSlide(float targetX)
    {
        animStartX  = sidebarPanel.anchoredPosition.x;
        animTargetX = targetX;
        animTimer   = 0f;
        isAnimating = true;
    }

    // -------------------------------------------------------------------------
    // Function:    SetPanelX
    // Inputs:      x — the anchoredPosition.x to apply to the sidebar panel
    // Outputs:     None
    // Description: Directly sets the sidebar panel's horizontal position.
    // -------------------------------------------------------------------------
    void SetPanelX(float x)
    {
        if (sidebarPanel == null) return;
        Vector2 pos = sidebarPanel.anchoredPosition;
        pos.x = x;
        sidebarPanel.anchoredPosition = pos;
    }

    // -------------------------------------------------------------------------
    // Function:    UpdatePlacement
    // Inputs:      None
    // Outputs:     None
    // Description: Enables PlaceObjectOnPlane when sidebar is closed, disables
    //              it when sidebar is open to prevent accidental tag placement.
    // -------------------------------------------------------------------------
    void UpdatePlacement()
    {
        if (placeObjectOnPlane == null) return;
        placeObjectOnPlane.enabled = !isOpen;
        Debug.Log($"SidebarController: PlaceObjectOnPlane.enabled={!isOpen}");
    }
}
