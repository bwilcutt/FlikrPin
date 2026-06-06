// =============================================================================
// File:        TagSelectionManager.cs
// Author:      Bryan Wilcutt
// Date:        2026-06-05
// Description: Singleton that manages tap-to-select on PostTag objects using
//              screen-space bounds rather than physics raycasts or center-point
//              proximity. Each tap projects the combined renderer bounds of every
//              live PostTag to screen space and checks if the tap falls inside
//              that rectangle. This means the selectable area exactly matches
//              the visible footprint of the tag regardless of distance or scale.
//
//              On selection:
//                - Applies a cyan tint to the selected tag
//                - Notifies TagEditDeleteController so sidebar buttons activate
//              On deselect (re-tap or tap empty space):
//                - Restores original tint
//                - Clears TagEditDeleteController selection
//
//              Attach to any persistent scene GameObject (e.g. PostCreator).
//              Wire TagEditDeleteController and BtnTrashcan in Inspector.
//              No colliders required on tag prefabs.
// =============================================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class TagSelectionManager : MonoBehaviour
{
    public static TagSelectionManager Instance { get; private set; }

    [Header("Sidebar Controller")]
    [Tooltip("The TagEditDeleteController that owns the server delete/edit logic.")]
    public TagEditDeleteController tagEditDeleteController;

    [Header("Trashcan Button")]
    [Tooltip("Wire the sidebar trashcan Button here.")]
    public Button trashcanButton;

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

    private PostTag selectedTag     = null;
    private Camera  arCamera        = null;
    private bool    suppressNextTap = false; // prevents trashcan tap from also deselecting
    private float   lastTapTime     = -1f;       // prevents double-tap within debounce window
    private float   tapDebounceSeconds = 0.5f;     // minimum seconds between taps
    private Coroutine pulseCoroutine  = null;          // active pulse coroutine
    private Dictionary<Material, Color> savedColors = new Dictionary<Material, Color>(); // saved material colors

    // -------------------------------------------------------------------------
    // Function:    Awake
    // Inputs:      None
    // Outputs:     None
    // Description: Enforces singleton pattern.
    // -------------------------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // -------------------------------------------------------------------------
    // Function:    OnEnable / OnDisable
    // Inputs:      None
    // Outputs:     None
    // Description: Enables/disables New Input System enhanced touch support.
    // -------------------------------------------------------------------------
    void OnEnable()  { EnhancedTouchSupport.Enable(); }
    void OnDisable() { EnhancedTouchSupport.Disable(); }

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
    // Description: Polls for tap/click each frame. Ignores UI touches. Checks
    //              all live PostTags via screen-space bounds and selects the
    //              tapped one. Tapping empty space deselects.
    // -------------------------------------------------------------------------
    void Update()
    {
        Vector2? tapPosition = null;

        // Editor / standalone — mouse click
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            tapPosition = mouse.position.ReadValue();

        // Device — first touch began this frame
        if (tapPosition == null)
        {
            foreach (var touch in Touch.activeTouches)
            {
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    tapPosition = touch.screenPosition;
                    break;
                }
            }
        }

        if (tapPosition == null) return;

        // Debounce — ignore taps within tapDebounceSeconds of the last processed tap
        if (Time.time - lastTapTime < tapDebounceSeconds) return;
        lastTapTime = Time.time;

        // Ignore taps that land on a UI element
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            var ped = new UnityEngine.EventSystems.PointerEventData(EventSystem.current);
            ped.position = tapPosition.Value;
            var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);
            foreach (var r in results)
                Debug.Log("UI blocker: " + r.gameObject.name + " on " + r.gameObject.transform.parent?.name);
            return;
        }

        // Skip one tap cycle after trashcan fires to prevent immediate deselect
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
    // Description: For every live PostTag, computes a screen-space bounding rect
    //              from its combined renderer bounds (all 8 world-space corners
    //              projected to screen). Selects the tapped tag, or clears
    //              selection if the tap lands outside all tags. When multiple
    //              tags overlap, the one closest to the camera wins.
    // -------------------------------------------------------------------------
    void FindAndSelectTagAtPoint(Vector2 screenPos)
    {
        PostTag[] allTags = FindObjectsByType<PostTag>(FindObjectsSortMode.None);

        PostTag tapped          = null;
        float   nearestCamDist  = float.MaxValue;

        foreach (PostTag tag in allTags)
        {
            // Skip tags behind the camera
            Vector3 viewportPos = arCamera.WorldToViewportPoint(tag.transform.position);
            if (viewportPos.z <= 0f) continue;

            // Project tag center to screen space and check distance to tap
            Vector3 screenPosTag = arCamera.WorldToScreenPoint(tag.transform.position);
            float   dist         = Vector2.Distance(screenPos, new Vector2(screenPosTag.x, screenPosTag.y));

            Debug.Log($"TagSelectionManager: Tag '{tag.name}' screen pos {screenPosTag}, dist {dist:F1}px");

            if (dist < nearestCamDist)
            {
                nearestCamDist = dist;
                tapped         = tag;
            }
        }

        // Only select if within tap radius
        if (tapped != null && nearestCamDist > tapRadiusPx)
            tapped = null;

        if (tapped != null)
        {
            // Re-tap same tag = deselect
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
            // Tapped empty space
            if (selectedTag != null)
            {
                ClearTint(selectedTag);
                selectedTag = null;
                NotifyController(null);
            }
            Debug.Log("TagSelectionManager: Tap outside all tags — deselected.");
        }
    }

    // -------------------------------------------------------------------------
    // Function:    ResetDebounce
    // Inputs:      None
    // Outputs:     None
    // Description: Resets the tap debounce timer. Call this after programmatically
    //              placing a tag so the placement tap doesn't immediately select
    //              the newly created tag.
    // -------------------------------------------------------------------------
    public void ResetDebounce()
    {
        // Set lastTapTime to future so debounce blocks for a full tapDebounceSeconds
        lastTapTime = Time.time + tapDebounceSeconds;

        // Also clear any accidental selection that occurred during tag placement
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
    //              TagEditDeleteController so the server DELETE is fired.
    //              Falls back to local Destroy if controller is not wired.
    // -------------------------------------------------------------------------
    public void TryDeleteSelected()
    {
        if (selectedTag == null)
        {
            Debug.Log("TagSelectionManager: Trashcan tapped — no tag selected.");
            return;
        }

        suppressNextTap = true; // prevent this same tap from also deselecting via Update
        if (tagEditDeleteController != null)
        {
            tagEditDeleteController.OnDeletePressed();
        }
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
    // Description: For each child Renderer on the tag, creates a duplicate
    //              GameObject with the FlikrPin/SelectionOutline shader applied.
    //              The outline mesh sits on top of the original, showing only
    //              a colored border around the tag's silhouette.
    // -------------------------------------------------------------------------
    // -------------------------------------------------------------------------
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
    //              using a sine wave. No scale changes — alpha only.
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
