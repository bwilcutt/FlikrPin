// =============================================================================
// File:        TagSelectionManager.cs
// Author:      Bryan Wilcutt
// Date:        2026-06-05
// Description: Singleton that manages tap-to-select on PostTag objects using
//              screen-space proximity. Each tap checks all live PostTags by
//              projecting their world position to screen space and measuring
//              distance to the tap point.
//
//              On selection:
//                - Applies alpha pulse to the selected tag
//                - Notifies TagEditDeleteController so sidebar buttons activate
//              On deselect (re-tap or tap empty space):
//                - Restores original material colors
//                - Clears TagEditDeleteController selection
//
//              On tap of empty AR space (no tag hit):
//                - Opens the TagBar via TagBarController
//
//              Attach to any persistent scene GameObject (e.g. PostCreator).
//              Wire TagEditDeleteController, BtnTrashcan, and TagBarController
//              in Inspector. No colliders required on tag prefabs.
// =============================================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TagSelectionManager : MonoBehaviour
{
    public static TagSelectionManager Instance { get; private set; }

    [Header("Sidebar Controller")]

    [Header("Trashcan Button")]
    [Tooltip("Wire the sidebar trashcan Button here.")]
    public Button trashcanButton;

    [Header("TagBar")]
    [Tooltip("TagBarController to open when a tap misses all existing tags.")]
    public TagBarController tagBarController;

    [Header("Selection Settings")]
    [Tooltip("How close (in pixels) a tap must be to a tag's screen center to select it.")]
    public float tapRadiusPx = 150f;

    [Header("Selection Pulse")]
    [Tooltip("Speed of the pulse.")]
    public float pulseSpeed = 3f;
    [Tooltip("Minimum alpha during pulse (0 = fully transparent).")]
    public float pulseMinAlpha = 0.2f;
    [Tooltip("Maximum alpha during pulse (1 = fully opaque).")]
    public float pulseMaxAlpha = 1.0f;

    private SidebarController sidebarController = null;
    private PostTag    selectedTag        = null;
    private Camera     arCamera           = null;
    private bool       suppressNextTap    = false;
    private float      lastTapTime        = -1f;
    private float      tapDebounceSeconds = 0.5f;
    private Coroutine  pulseCoroutine     = null;
    private Dictionary<Material, Color> savedColors = new Dictionary<Material, Color>();

    // Touch tracking — finger-down position recorded at Began,
    // evaluated at Ended to distinguish tap from swipe/scroll.
    private Vector2 touchDownPos             = Vector2.zero;
    private bool    trackingTouch            = false;

    // Maximum pixel drift allowed between finger-down and finger-up
    // for the gesture to be treated as a tap rather than a swipe.
    // SidebarController uses 80px as its swipe threshold, so 40px
    // gives a clean gap between scroll and tap intent.
    private const float TapStayedPutThresholdPx = 40f;

    // -------------------------------------------------------------------------
    // Function:    Awake
    // Inputs:      None
    // Outputs:     None
    // Description: Enforces singleton pattern. Auto-finds TagBarController
    //              if not assigned in Inspector.
    // -------------------------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (sidebarController == null)
            sidebarController = FindAnyObjectByType<SidebarController>();

        if (tagBarController == null)
            tagBarController = FindAnyObjectByType<TagBarController>();

        if (sidebarController == null)
            sidebarController = FindAnyObjectByType<SidebarController>();

        if (tagBarController == null)
            Debug.LogWarning("TagSelectionManager: TagBarController not found — tapping empty space will not open TagBar.");
        else
            Debug.Log("TagSelectionManager: tagBarController found: " + tagBarController.name);
    }

    // -------------------------------------------------------------------------
    // Function:    Start
    // Inputs:      None
    // Outputs:     None
    // Description: Caches the AR camera and wires the trashcan button.
    // -------------------------------------------------------------------------
    void Start()
    {
        arCamera = Camera.main;

        if (trashcanButton != null)
            trashcanButton.onClick.AddListener(TryDeleteSelected);
        else
            Debug.LogWarning("TagSelectionManager: trashcanButton is not assigned.");
    }

    // -------------------------------------------------------------------------
    // Function:    Update
    // Inputs:      None
    // Outputs:     None
    // Description: Polls touch/click each frame using Touchscreen and Mouse
    //              from the New Input System.
    //
    //              Touch devices use a two-phase approach to avoid mistaking
    //              sidebar swipe gestures for tag placement taps:
    //                - TouchPhase.Began  : records finger-down position only.
    //                - TouchPhase.Ended  : compares finger-up position to
    //                  finger-down. If the finger moved more than
    //                  TapStayedPutThresholdPx in any direction, the gesture
    //                  is treated as a swipe/scroll and ignored. Only when the
    //                  finger stayed put is the tap processed, using the
    //                  finger-up position for the AR drop raycast.
    //
    //              Mouse clicks fire immediately on press (editor use only).
    // -------------------------------------------------------------------------
    void Update()
    {
        Vector2? tapPosition = null;

        // ── Editor / standalone — mouse click ────────────────────────────────
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            tapPosition = mouse.position.ReadValue();
            Debug.Log("TagSelectionManager: mouse click at " + tapPosition);
        }

        // ── Device — touchscreen (New Input System) ──────────────────────────
        if (tapPosition == null)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    var phase = touch.phase.ReadValue();

                    if (phase == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        // Record finger-down position — no action yet.
                        touchDownPos   = touch.position.ReadValue();
                        trackingTouch  = true;
                        Debug.Log("TagSelectionManager: touch began at " + touchDownPos);
                        break;
                    }

                    if (phase == UnityEngine.InputSystem.TouchPhase.Ended && trackingTouch)
                    {
                        trackingTouch = false;
                        Vector2 upPos = touch.position.ReadValue();
                        float   drift = Vector2.Distance(touchDownPos, upPos);

                        if (drift > TapStayedPutThresholdPx)
                        {
                            // Finger moved too far — treat as swipe/scroll, ignore.
                            Debug.Log($"TagSelectionManager: touch ended — drift {drift:F1}px > threshold, ignoring as swipe.");
                            break;
                        }

                        // Finger stayed put — treat as tap at finger-up position.
                        tapPosition = upPos;
                        Debug.Log($"TagSelectionManager: touch ended — drift {drift:F1}px, treating as tap at {tapPosition}.");
                        break;
                    }

                    if (phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                    {
                        trackingTouch = false;
                        break;
                    }
                }
            }
        }

        // ── Legacy input fallback ────────────────────────────────────────────
        // Catches touches when Touchscreen.current is null. Applies the same
        // stayed-put check using legacy TouchPhase.
        if (tapPosition == null && Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);

            if (t.phase == UnityEngine.TouchPhase.Began)
            {
                touchDownPos  = t.position;
                trackingTouch = true;
                Debug.Log("TagSelectionManager: legacy touch began at " + touchDownPos);
            }
            else if (t.phase == UnityEngine.TouchPhase.Ended && trackingTouch)
            {
                trackingTouch = false;
                float drift = Vector2.Distance(touchDownPos, t.position);

                if (drift > TapStayedPutThresholdPx)
                {
                    Debug.Log($"TagSelectionManager: legacy touch ended — drift {drift:F1}px > threshold, ignoring as swipe.");
                }
                else
                {
                    tapPosition = t.position;
                    Debug.Log($"TagSelectionManager: legacy touch ended — drift {drift:F1}px, treating as tap at {tapPosition}.");
                }
            }
            else if (t.phase == UnityEngine.TouchPhase.Canceled)
            {
                trackingTouch = false;
            }
        }

        if (tapPosition == null) return;

        // ── Debounce ─────────────────────────────────────────────────────────
        if (Time.time - lastTapTime < tapDebounceSeconds) return;
        lastTapTime = Time.time;

        // ── Ignore taps that land on a UI element ────────────────────────────
        // Exception: the TagBar backdrop is a full-screen transparent Button
        // that sits over the entire screen. A tap on the backdrop should still
        // reach tag hit-detection — the backdrop is just the AR world with a
        // UI layer on top. We pass through if the ONLY UI hit is the backdrop.
        // Any other UI element (buttons, panels, sidebars) still blocks.
        //
        // NOTE: IsPointerOverGameObject() is NOT used — unreliable on the same
        // frame as TouchPhase.Ended with the New Input System. RaycastAll runs
        // unconditionally instead.
        if (EventSystem.current != null)
        {
            var ped = new PointerEventData(EventSystem.current);
            ped.position = tapPosition.Value;
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);
            bool blockedByRealUI = false;
            foreach (var r in results)
            {
                bool isBackdrop = tagBarController != null &&
                                  r.gameObject == tagBarController.backdropButton?.gameObject;
                if (!isBackdrop)
                {
                    blockedByRealUI = true;
                    break;
                }
            }
            if (blockedByRealUI)
            {
                tapPosition = null;
                return;
            }
        }

        // ── Suppress one tap cycle after trashcan fires ───────────────────────
        if (suppressNextTap)
        {
            suppressNextTap = false;
            Debug.Log("TagSelectionManager: Suppressing tap (trashcan just fired).");
            return;
        }

        Debug.Log($"TagSelectionManager: Tap confirmed at screen pos {tapPosition.Value}");
        FindAndSelectTagAtPoint(tapPosition.Value);
    }

    // -------------------------------------------------------------------------
    // Function:    FindAndSelectTagAtPoint
    // Inputs:      screenPos — the confirmed finger-up screen-space position
    // Outputs:     None
    // Description: For every live PostTag, checks screen-space proximity to
    //              the tap. Selects the nearest tag within tapRadiusPx, or
    //              opens the TagBar if no tag is hit. Passes screenPos into
    //              TagBarController.Show() so the AR drop raycast fires from
    //              the actual finger position rather than screen center.
    // -------------------------------------------------------------------------
    void FindAndSelectTagAtPoint(Vector2 screenPos)
    {
        // Don't open TagBar if sidebar is open
        if (sidebarController != null && sidebarController.IsOpen)
        {
            Debug.Log("TagSelectionManager: Sidebar open — ignoring tap.");
            return;
        }

        PostTag[] allTags = FindObjectsByType<PostTag>(FindObjectsSortMode.None);

        PostTag tapped         = null;
        float   nearestDist    = float.MaxValue;

        foreach (PostTag tag in allTags)
        {
            Vector3 viewportPos = arCamera.WorldToViewportPoint(tag.transform.position);
            if (viewportPos.z <= 0f) continue;

            Vector3 screenPosTag = arCamera.WorldToScreenPoint(tag.transform.position);
            float   dist         = Vector2.Distance(screenPos, new Vector2(screenPosTag.x, screenPosTag.y));

            Debug.Log($"TagSelectionManager: Tag '{tag.name}' dist {dist:F1}px");

            if (dist < nearestDist)
            {
                nearestDist = dist;
                tapped      = tag;
            }
        }

        if (tapped != null && nearestDist > tapRadiusPx)
            tapped = null;



        if (tapped != null)
        {
            if (tapped == selectedTag)
            {
                // Re-tap on the already-selected tag — deselect it
                ClearTint(selectedTag);
                selectedTag = null;
                NotifyController(null);
                Debug.Log("TagSelectionManager: Deselected (re-tap).");
            }
            else
            {
                // New tag tapped — select it
                if (selectedTag != null) ClearTint(selectedTag);
                selectedTag = tapped;
                ApplyTint(selectedTag);
                NotifyController(selectedTag);
                Debug.Log($"TagSelectionManager: Selected '{tapped.name}'.");
            }
        }
        else
        {
            // Tapped empty space — deselect any current tag
            if (selectedTag != null)
            {
                ClearTint(selectedTag);
                selectedTag = null;
                NotifyController(null);
            }
            Debug.Log("TagSelectionManager: Tap missed all tags — deselected.");
        }

        // Always open the TagBar on any tap (tag hit or miss).
        // If a tag was selected above, the TagBar delete button will act on it.
        if (tagBarController != null && !tagBarController.IsVisible)
        {
            Debug.Log("TagSelectionManager: Opening TagBar.");
            tagBarController.Show(screenPos);
        }
        else if (tagBarController == null)
        {
            Debug.LogWarning("TagSelectionManager: tagBarController is null — cannot open TagBar.");
        }
    }

    // -------------------------------------------------------------------------
    // Function:    IsTagAtScreenPoint
    // Inputs:      screenPos — screen-space position to check
    // Outputs:     bool — true if a PostTag is within tapRadiusPx of screenPos
    // Description: Non-destructive hit test used externally to check whether
    //              a tap lands on an existing tag without selecting it.
    // -------------------------------------------------------------------------
    public bool IsTagAtScreenPoint(Vector2 screenPos)
    {
        PostTag[] allTags = FindObjectsByType<PostTag>(FindObjectsSortMode.None);

        foreach (PostTag tag in allTags)
        {
            Vector3 viewportPos = arCamera.WorldToViewportPoint(tag.transform.position);
            if (viewportPos.z <= 0f) continue;

            Vector3 screenPosTag = arCamera.WorldToScreenPoint(tag.transform.position);
            float   dist         = Vector2.Distance(screenPos, new Vector2(screenPosTag.x, screenPosTag.y));

            if (dist <= tapRadiusPx)
                return true;
        }

        return false;
    }

    // -------------------------------------------------------------------------
    // Function:    ResetDebounce
    // Inputs:      None
    // Outputs:     None
    // Description: Resets the tap debounce timer. Call after programmatically
    //              placing a tag so the placement tap doesn't select the new tag.
    // -------------------------------------------------------------------------
    public void ResetDebounce()
    {
        lastTapTime = Time.time + tapDebounceSeconds;

        if (selectedTag != null)
        {
            ClearTint(selectedTag);
            selectedTag = null;
            NotifyController(null);
        }
    }

    // -------------------------------------------------------------------------
    // Function:    TryDeleteSelected
    // Inputs:      None
    // Outputs:     None
    // Description: Called by the trashcan button. Delegates to
    //              TagEditDeleteController for server DELETE. Falls back to
    //              local Destroy if controller is not wired.
    // -------------------------------------------------------------------------
    public void TryDeleteSelected()
    {
        if (selectedTag == null)
        {
            Debug.Log("TagSelectionManager: Trashcan tapped — no tag selected.");
            return;
        }

        suppressNextTap = true;
        if (selectedTag.postId == null)
        {
            // No server record yet — local destroy only
            Destroy(selectedTag.gameObject);
        }
        else
        {
            // TODO: call server DELETE endpoint here
            Debug.LogWarning("TagSelectionManager: Server delete not yet implemented for postId=" + selectedTag.postId);
            Destroy(selectedTag.gameObject);
        }

        selectedTag = null;
    }

    // -------------------------------------------------------------------------
    // Function:    OnTagDestroyed
    // Inputs:      tag — the PostTag being destroyed externally
    // Outputs:     None
    // Description: Clears selection reference when a tag is destroyed externally.
    // -------------------------------------------------------------------------
    public void OnTagDestroyed(PostTag tag)
    {
        if (selectedTag == tag)
            selectedTag = null;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    // -------------------------------------------------------------------------
    // Function:    NotifyController
    // Inputs:      tag — the newly selected PostTag, or null to clear
    // Outputs:     None
    // Description: Pushes selection state to TagEditDeleteController and
    //              shows/hides the TagBar delete button accordingly.
    // -------------------------------------------------------------------------
    void NotifyController(PostTag tag)
    {
        Debug.Log($"TagSelectionManager: NotifyController tag={tag?.name ?? "null"} tagBarController={tagBarController?.name ?? "NULL"}");
        // Show delete button in TagBar only when a tag is selected
        if (tagBarController != null)
            tagBarController.ShowDeleteButton(tag != null);
    }

    // -------------------------------------------------------------------------
    // Function:    ApplyTint
    // Inputs:      tag — PostTag to highlight
    // Outputs:     None
    // Description: Saves original material colors and starts alpha pulse coroutine.
    // -------------------------------------------------------------------------
    void ApplyTint(PostTag tag)
    {
        savedColors.Clear();
        foreach (Renderer r in tag.GetComponentsInChildren<Renderer>())
            foreach (Material m in r.materials)
                if (m.HasProperty("_Color"))
                    savedColors[m] = m.GetColor("_Color");

        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseTag(tag));
    }

    // -------------------------------------------------------------------------
    // Function:    ClearTint
    // Inputs:      tag — PostTag to restore
    // Outputs:     None
    // Description: Stops pulse coroutine and restores original material colors.
    // -------------------------------------------------------------------------
    void ClearTint(PostTag tag)
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        if (tag == null) return;
        foreach (Renderer r in tag.GetComponentsInChildren<Renderer>())
            foreach (Material m in r.materials)
                if (savedColors.TryGetValue(m, out Color orig))
                    m.SetColor("_Color", orig);

        savedColors.Clear();
    }

    // -------------------------------------------------------------------------
    // Function:    PulseTag
    // Inputs:      tag — PostTag to pulse
    // Outputs:     IEnumerator (coroutine)
    // Description: Animates _Color alpha between pulseMinAlpha and pulseMaxAlpha
    //              using a sine wave. Alpha only — no scale changes.
    // -------------------------------------------------------------------------
    IEnumerator PulseTag(PostTag tag)
    {
        while (true)
        {
            if (tag == null) yield break;

            float t     = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, t);

            foreach (Renderer r in tag.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                foreach (Material m in r.materials)
                    if (m.HasProperty("_Color"))
                    {
                        Color c = m.GetColor("_Color");
                        m.SetColor("_Color", new Color(c.r, c.g, c.b, alpha));
                    }
            }

            yield return null;
        }
    }
}
