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
    [Tooltip("The TagEditDeleteController that owns the server delete/edit logic.")]
    public TagEditDeleteController tagEditDeleteController;

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

        if (tagEditDeleteController == null)
            Debug.LogWarning("TagSelectionManager: tagEditDeleteController is not assigned — server deletes will not work.");
    }

    // -------------------------------------------------------------------------
    // Function:    Update
    // Inputs:      None
    // Outputs:     None
    // Description: Polls for tap/click each frame using Touchscreen and Mouse
    //              from the New Input System. Ignores UI touches. Checks all
    //              live PostTags and selects the tapped one, or opens TagBar
    //              if no tag is hit.
    // -------------------------------------------------------------------------
    void Update()
    {
        Vector2? tapPosition = null;

        // Editor / standalone — mouse click
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            tapPosition = mouse.position.ReadValue();
            Debug.Log("TagSelectionManager: mouse click at " + tapPosition);
        }

        // Device — touchscreen
        if (tapPosition == null)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        tapPosition = touch.position.ReadValue();
                        Debug.Log("TagSelectionManager: touch began at " + tapPosition);
                        break;
                    }
                }
            }
        }

        // Legacy input fallback — catches touches when Touchscreen.current is null
        if (tapPosition == null && Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == UnityEngine.TouchPhase.Began)
            {
                tapPosition = t.position;
                Debug.Log("TagSelectionManager: legacy touch at " + tapPosition);
            }
        }

        if (tapPosition == null) return;

        // Debounce
        if (Time.time - lastTapTime < tapDebounceSeconds) return;
        lastTapTime = Time.time;

        // Ignore taps that land on a UI element
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            var ped = new PointerEventData(EventSystem.current);
            ped.position = tapPosition.Value;
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);
            foreach (var r in results)
                Debug.Log("UI blocker: " + r.gameObject.name + " on " + r.gameObject.transform.parent?.name);
            return;
        }

        // Skip one tap cycle after trashcan fires
        if (suppressNextTap)
        {
            suppressNextTap = false;
            Debug.Log("TagSelectionManager: Suppressing tap (trashcan just fired).");
            return;
        }

        Debug.Log($"TagSelectionManager: Tap at screen pos {tapPosition.Value}");
        FindAndSelectTagAtPoint(tapPosition.Value);
    }

    // -------------------------------------------------------------------------
    // Function:    FindAndSelectTagAtPoint
    // Inputs:      screenPos — the screen-space tap position
    // Outputs:     None
    // Description: For every live PostTag, checks screen-space proximity to
    //              the tap. Selects the nearest tag within tapRadiusPx, or
    //              opens the TagBar if no tag is hit.
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
                ClearTint(selectedTag);
                selectedTag = null;
                NotifyController(null);
                Debug.Log("TagSelectionManager: Deselected (re-tap).");
            }
            else
            {
                if (selectedTag != null) ClearTint(selectedTag);
                selectedTag = tapped;
                ApplyTint(selectedTag);
                NotifyController(selectedTag);
                Debug.Log($"TagSelectionManager: Selected '{tapped.name}'.");
            }
        }
        else
        {
            // Tapped empty space — deselect and open TagBar
            if (selectedTag != null)
            {
                ClearTint(selectedTag);
                selectedTag = null;
                NotifyController(null);
            }

            Debug.Log("TagSelectionManager: Tap outside all tags — opening TagBar.");

            if (tagBarController != null && !tagBarController.IsVisible)
            {
                Vector3 fallback = Camera.main.transform.position + Camera.main.transform.forward * 2f;
                tagBarController.Show(fallback);
            }
            else if (tagBarController == null)
            {
                Debug.LogWarning("TagSelectionManager: tagBarController is null — cannot open TagBar.");
            }
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
        if (tagEditDeleteController != null)
            tagEditDeleteController.OnDeletePressed();
        else
        {
            Debug.LogWarning("TagSelectionManager: No controller assigned — destroying locally only.");
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
    // Description: Pushes selection state to TagEditDeleteController.
    // -------------------------------------------------------------------------
    void NotifyController(PostTag tag)
    {
        if (tagEditDeleteController == null) return;
        if (tag != null)
            tagEditDeleteController.OnTagSelected(tag);
        else
            tagEditDeleteController.ClearSelection();
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
