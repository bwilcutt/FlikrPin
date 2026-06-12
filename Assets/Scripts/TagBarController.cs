// ============================================================
// File:        TagBarController.cs
// Author:      Bryan Wilcutt
// Date:        2026-06-07
// Description: Replaces PostTypeWindow / TagWheel with a vertical
//              TagBar panel that slides in from the right edge of
//              the screen when the Target Icon is tapped.  The user
//              selects a tag type and the bar slides back out to the
//              right.  Tapping the full-screen backdrop (outside the
//              bar) cancels and slides the bar away.
//
//              Drop-in replacement for PostTypeWindow.cs — all
//              downstream callers (TextInputWindow, StickerPicker,
//              CameraCapture, etc.) are wired identically.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using TMPro;

public class TagBarController : MonoBehaviour
{
    // --------------------------------------------------------
    // Inspector fields
    // --------------------------------------------------------
    public bool IsVisible => isVisible;
    
    [Header("References")]
    public TextInputWindow     textInputWindow;
    public PlacePrefabInWorld  placer;
    public StickerPickerWindow stickerPickerWindow;
    public PlaceStickerTag     placeStickerTag;
    public CameraCapture       cameraCapture;

    [Header("Target Icon")]
    public GameObject targetIcon;

    [Header("TagBar Panel")]
    [Tooltip("The RectTransform of the TagBar panel itself (the sliding strip).")]
    public RectTransform tagBarPanel;

    [Tooltip("Full-screen transparent backdrop that catches outside taps to cancel.")]
    public Button backdropButton;

    [Header("Buttons")]
    public Button btnText;
    public Button btnFriends;
    public Button btnSticker;
    public Button btnVideo;
    public Button btnMedia;
    public Button btnCamera;
    public Button btnScavenger;
    [Tooltip("Delete button — shown only when a tag is selected. Wire in Inspector.")]
    public Button btnTrashcan;

    [Header("Audio")]
    public AudioClip clickSound;

    [Header("Slide Settings")]
    [Tooltip("Seconds for the slide-in / slide-out animation.")]
    public float slideDuration = 0.25f;

    [Tooltip("Easing curve for the slide (leave as EaseOut for a natural feel).")]
    public AnimationCurve slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // --------------------------------------------------------
    // Private state
    // --------------------------------------------------------

    private AudioSource       audioSource;
    private Vector3           dropPosition;
    private ARRaycastManager  arRaycastManager;
    private ARAnchorManager   arAnchorManager;
    private ARSession         arSession;
    private bool              isVisible    = false;
    private Coroutine         slideRoutine = null;
    private float             barWidth;

    // --------------------------------------------------------
    // Unity lifecycle
    // --------------------------------------------------------

    /// <summary>
    /// Function:   Awake
    /// Inputs:     none
    /// Outputs:    none
    /// Description: Caches scene references, sets up AudioSource,
    ///              and positions the TagBar fully off-screen to the
    ///              right so it is invisible before first Show().
    /// </summary>
    void Awake()
    {
        Debug.Log("TagBarController.Awake() — begin");

        if (placer == null)
            placer = FindAnyObjectByType<PlacePrefabInWorld>();

        arRaycastManager = FindAnyObjectByType<ARRaycastManager>();
        arAnchorManager  = FindAnyObjectByType<ARAnchorManager>();
        if (arAnchorManager == null)
            Debug.LogWarning("TagBarController: ARAnchorManager not found — anchoring disabled.");
        arSession        = FindAnyObjectByType<ARSession>();

        // Audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        if (clickSound != null)
            audioSource.clip = clickSound;

        // Park the bar off-screen — defer barWidth measurement to Start()
        // because rect may not be valid yet in Awake on some Unity versions
        if (tagBarPanel != null)
        {
            tagBarPanel.gameObject.SetActive(false);
            Debug.Log($"TagBarController.Awake() — tagBarPanel found: {tagBarPanel.name}, deactivated.");
        }
        else
        {
            Debug.LogError("TagBarController: tagBarPanel is NOT assigned in Inspector!");
        }

        if (backdropButton != null)
        {
            backdropButton.gameObject.SetActive(false);
            backdropButton.onClick.AddListener(OnBackdropTapped);
            Debug.Log("TagBarController.Awake() — backdropButton wired.");
        }
        else
        {
            Debug.LogWarning("TagBarController: backdropButton is not assigned.");
        }

        WireButtons();
        Debug.Log("TagBarController.Awake() — complete");
    }

    /// <summary>
    /// Function:   Start
    /// Inputs:     none
    /// Outputs:    none
    /// Description: Wires the Target Icon tap, measures bar width
    ///              after layout has settled, and pre-warms sticker picker.
    /// </summary>
    void Start()
    {
        // Wire target icon
        if (targetIcon != null)
        {
            Button tb = targetIcon.GetComponent<Button>();
            if (tb == null) tb = targetIcon.AddComponent<Button>();
            tb.onClick.RemoveAllListeners();
            tb.onClick.AddListener(OnTargetIconPressed);
            Debug.Log($"TagBarController.Start() — targetIcon wired: {targetIcon.name}");
        }
        else
        {
            Debug.LogError("TagBarController: targetIcon is NOT assigned in Inspector!");
        }

        // Measure bar width now that layout has built
        MeasureBarWidth();

        if (stickerPickerWindow != null)
            stickerPickerWindow.Preload();

        Debug.Log($"TagBarController.Start() — barWidth={barWidth}, HiddenX={HiddenX()}, VisibleX={VisibleX()}");
    }

    // --------------------------------------------------------
    // Width measurement
    // --------------------------------------------------------

    /// <summary>
    /// Function:   MeasureBarWidth
    /// Inputs:     none
    /// Outputs:    none
    /// Description: Forces a canvas layout rebuild and reads the
    ///              panel width.  Falls back to sizeDelta.x if
    ///              rect.width is still zero (can happen when the
    ///              panel is inactive).
    /// </summary>
    void MeasureBarWidth()
    {
        if (tagBarPanel == null) return;

        // Temporarily activate to force layout measurement
        bool wasActive = tagBarPanel.gameObject.activeSelf;
        tagBarPanel.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(tagBarPanel);

        barWidth = tagBarPanel.rect.width;
        if (barWidth <= 0f) barWidth = tagBarPanel.sizeDelta.x;
        if (barWidth <= 0f) barWidth = 200f; // hard fallback — check Inspector if hit

        Debug.Log($"TagBarController.MeasureBarWidth() — rect.width={tagBarPanel.rect.width} sizeDelta.x={tagBarPanel.sizeDelta.x} final barWidth={barWidth}");

        // Park it off-screen then restore active state
        SetBarX(HiddenX());
        tagBarPanel.gameObject.SetActive(wasActive);
    }

    // --------------------------------------------------------
    // Button wiring
    // --------------------------------------------------------

    /// <summary>
    /// Function:   WireButtons
    /// Inputs:     none
    /// Outputs:    none
    /// Description: Attaches onClick listeners and pointer-down
    ///              sound triggers to all tag-type buttons.
    /// </summary>
    void WireButtons()
    {
        if (btnText      != null) { btnText.onClick.AddListener(OnTextSelected);           AddPointerDownSound(btnText.gameObject); }
        if (btnFriends   != null) { btnFriends.onClick.AddListener(OnFriendsSelected);     AddPointerDownSound(btnFriends.gameObject); }
        if (btnSticker   != null) { btnSticker.onClick.AddListener(OnStickerSelected);     AddPointerDownSound(btnSticker.gameObject); }
        if (btnVideo     != null) { btnVideo.onClick.AddListener(OnVideoSelected);         AddPointerDownSound(btnVideo.gameObject); }
        if (btnMedia     != null) { btnMedia.onClick.AddListener(OnMediaSelected);         AddPointerDownSound(btnMedia.gameObject); }
        if (btnCamera    != null) { btnCamera.onClick.AddListener(OnCameraSelected);       AddPointerDownSound(btnCamera.gameObject); }
        if (btnScavenger != null) { btnScavenger.onClick.AddListener(OnScavengerSelected); AddPointerDownSound(btnScavenger.gameObject); }
        Debug.Log("TagBarController: WireButtons — btnTrashcan=" + (btnTrashcan != null ? btnTrashcan.name : "NULL"));
        if (btnTrashcan    != null) { btnTrashcan.onClick.AddListener(OnDeleteSelected);       AddPointerDownSound(btnTrashcan.gameObject);
                                    btnTrashcan.gameObject.SetActive(false); } // hidden until a tag is selected
    }

    /// <summary>
    /// Function:   AddPointerDownSound
    /// Inputs:     go — the button GameObject to attach the trigger to
    /// Outputs:    none
    /// Description: Attaches an EventTrigger PointerDown entry that
    ///              plays the click sound on finger contact.
    /// </summary>
    void AddPointerDownSound(GameObject go)
    {
        var trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        var entry   = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        entry.callback.AddListener((_) => PlayClick());
        trigger.triggers.Add(entry);
    }

    // --------------------------------------------------------
    // Show / Hide — public API
    // --------------------------------------------------------

    /// <summary>
    /// Function:   OnTargetIconPressed
    /// Inputs:     none
    /// Outputs:    none
    /// Description: Toggles the TagBar open/closed when Target Icon tapped.
    /// </summary>
    void OnTargetIconPressed()
    {
        Debug.Log($"TagBarController.OnTargetIconPressed() — isVisible={isVisible}");
        if (!isVisible) Show(new Vector2(Screen.width / 2f, Screen.height / 2f));
        else            Hide();
    }

    /// <summary>
    /// Function:   Show (parameterless overload)
    /// Inputs:     none
    /// Outputs:    none
    /// Description: Convenience overload — raycasts from screen center.
    ///              Used by Inspector Button wiring and any caller that
    ///              does not have a finger screen position available.
    /// </summary>
    public void Show() { Show(new Vector2(Screen.width / 2f, Screen.height / 2f)); }

    /// <summary>
    /// Function:   Show
    /// Inputs:     screenPos — the finger-up screen-space position to use
    ///             for the AR drop raycast. Pass screen center if the
    ///             caller does not have a precise finger position.
    /// Outputs:    none
    /// Description: Raycasts against AR planes from screenPos to capture
    ///              the drop position, hides the Target Icon, activates
    ///              the backdrop, and slides the TagBar in from the right.
    /// </summary>
    public void Show(Vector2 screenPos)
    {
        dropPosition = GetARDropPosition(screenPos);
        Debug.Log($"TagBarController.Show() — screenPos={screenPos} dropPosition={dropPosition} barWidth={barWidth} fromX={HiddenX()} toX={VisibleX()}");

        isVisible = true;

        if (targetIcon != null) targetIcon.SetActive(false);

        if (tagBarPanel != null)
        {
            tagBarPanel.gameObject.SetActive(true);
            // Re-measure in case it was zero (inactive during Start)
            if (barWidth <= 0f) MeasureBarWidth();
            SetBarX(HiddenX());
        }

        if (backdropButton != null)
            backdropButton.gameObject.SetActive(true);

        SlideBar(HiddenX(), VisibleX());
    }

    /// <summary>
    /// Function:   Hide
    /// Inputs:     none
    /// Outputs:    none
    /// Description: Slides the TagBar back out to the right, then
    ///              deactivates the panel and backdrop and restores
    ///              the Target Icon.
    /// </summary>
    public void Hide()
    {
        Debug.Log("TagBarController.Hide()");
        isVisible = false;
        SlideBar(VisibleX(), HiddenX(), onComplete: () =>
        {
            if (tagBarPanel != null)
                tagBarPanel.gameObject.SetActive(false);

            if (backdropButton != null)
                backdropButton.gameObject.SetActive(false);

            if (targetIcon != null)
                targetIcon.SetActive(true);
        });
    }

    // --------------------------------------------------------
    // Backdrop / cancel
    // --------------------------------------------------------

    /// <summary>
    /// Function:   OnBackdropTapped
    /// Inputs:     none
    /// Outputs:    none
    /// Description: Called when the user taps outside the TagBar. Cancels.
    /// </summary>
    void OnBackdropTapped()
    {
        Debug.Log("TagBarController: backdrop tapped — cancelling.");
        Hide();
    }

    // --------------------------------------------------------
    // Slide animation
    // --------------------------------------------------------

    /// <summary>
    /// Function:   SlideBar
    /// Inputs:     fromX      — starting anchoredPosition.x
    ///             toX        — ending anchoredPosition.x
    ///             onComplete — optional callback fired when done
    /// Outputs:    none
    /// Description: Animates tagBarPanel.anchoredPosition.x between
    ///              fromX and toX over slideDuration seconds.
    /// </summary>
    void SlideBar(float fromX, float toX, System.Action onComplete = null)
    {
        if (slideRoutine != null)
            StopCoroutine(slideRoutine);

        slideRoutine = StartCoroutine(SlideRoutine(fromX, toX, onComplete));
    }

    IEnumerator SlideRoutine(float fromX, float toX, System.Action onComplete)
    {
        Debug.Log($"TagBarController.SlideRoutine() — fromX={fromX} toX={toX}");
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t      = Mathf.Clamp01(elapsed / slideDuration);
            float easedT = slideCurve.Evaluate(t);
            SetBarX(Mathf.Lerp(fromX, toX, easedT));
            yield return null;
        }

        SetBarX(toX);
        slideRoutine = null;
        Debug.Log($"TagBarController.SlideRoutine() — done, final X={toX}");
        onComplete?.Invoke();
    }

    // --------------------------------------------------------
    // Position helpers
    // --------------------------------------------------------

    /// <summary>
    /// Function:   SetBarX
    /// Inputs:     x — anchoredPosition.x to apply to tagBarPanel
    /// Outputs:    none
    /// Description: Sets the horizontal anchored position of the TagBar.
    /// </summary>
    void SetBarX(float x)
    {
        if (tagBarPanel == null) return;
        Vector2 pos = tagBarPanel.anchoredPosition;
        pos.x = x;
        tagBarPanel.anchoredPosition = pos;
    }

    /// <summary>
    /// Function:   VisibleX
    /// Inputs:     none
    /// Outputs:    float — anchoredPosition.x when bar is fully on-screen
    /// Description: Returns 0 — bar is right-anchored, pivot at right edge.
    /// </summary>
    float VisibleX() => 0f;

    /// <summary>
    /// Function:   HiddenX
    /// Inputs:     none
    /// Outputs:    float — anchoredPosition.x when bar is fully off-screen
    /// Description: Returns barWidth so panel sits entirely off the right edge.
    /// </summary>
    float HiddenX() => barWidth;

    // --------------------------------------------------------
    // AR drop position
    // --------------------------------------------------------

    /// <summary>
    // -------------------------------------------------------------------------
    // Function:    GetARDropPosition
    // Inputs:      screenPos — screen-space tap position
    // Outputs:     Vector3   — world position for tag placement
    // Description: Raycasts against AR planes. If a plane is hit, attaches an
    //              ARAnchor to the trackable for maximum stability. If no plane
    //              is detected, creates a standalone ARAnchor at the camera-
    //              forward fallback position. Stores anchor in _lastAnchor.
    // -------------------------------------------------------------------------
    private ARAnchor _lastAnchor = null;

    Vector3 GetARDropPosition(Vector2 screenPos)
    {
        _lastAnchor = null;

        if (arRaycastManager != null)
        {
            var hits = new List<ARRaycastHit>();
            if (arRaycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon))
            {
                Debug.Log($"TagBarController: AR plane hit at screenPos={screenPos}.");
                if (arAnchorManager != null)
                {
                    _lastAnchor = arAnchorManager.AttachAnchor(
                        hits[0].trackable as ARPlane, hits[0].pose);
                    if (_lastAnchor != null)
                        Debug.Log("TagBarController: ARAnchor attached to plane.");
                }
                return hits[0].pose.position;
            }
        }

        // No plane detected — create standalone anchor at camera-forward position
        Vector3 fallbackPos = Camera.main.transform.position +
                              Camera.main.transform.forward * 2f;
        Debug.Log($"TagBarController: No AR plane — using camera forward fallback.");

        if (arAnchorManager != null)
        {
            var anchorGO = new GameObject("StandaloneAnchor");
            anchorGO.transform.position = fallbackPos;
            _lastAnchor = anchorGO.AddComponent<ARAnchor>();
            Debug.Log("TagBarController: Standalone ARAnchor created at fallback position.");
        }

        return fallbackPos;
    }

    // --------------------------------------------------------
    // Audio
    // --------------------------------------------------------

    /// <summary>
    /// Function:   PlayClick
    /// Inputs:     none
    /// Outputs:    none
    /// Description: Plays the click sound via the cached AudioSource.
    /// </summary>
    void PlayClick()
    {
        if (audioSource != null && clickSound != null)
            audioSource.Play();
    }

    // --------------------------------------------------------
    // Button handlers
    // --------------------------------------------------------

    void OnTextSelected()
    {
        Debug.Log("TagBarController: Text selected.");
        Hide();
        if (textInputWindow != null)
            textInputWindow.Show(dropPosition);
        else
            Debug.LogWarning("TagBarController: textInputWindow is not assigned.");
    }

    void OnFriendsSelected()
    {
        Debug.Log("TagBarController: Friends selected.");
        Hide();
    }

    public void OnStickerSelected()
    {
        Debug.Log("TagBarController: Sticker selected.");
        Hide();
        if (stickerPickerWindow != null && placeStickerTag != null)
        {
            stickerPickerWindow.OnStickerSelected = (texture) =>
            {
                placeStickerTag.PlaceSticker(texture, dropPosition, _lastAnchor);
            };
            stickerPickerWindow.Show();
        }
        else
        {
            if (stickerPickerWindow == null) Debug.LogWarning("TagBarController: stickerPickerWindow is not assigned.");
            if (placeStickerTag == null)     Debug.LogWarning("TagBarController: placeStickerTag is not assigned.");
        }
    }

    public void OnVideoSelected()
    {
        Debug.Log("TagBarController: Video selected.");
        Hide();
        if (cameraCapture == null) { Debug.LogWarning("TagBarController: cameraCapture is not assigned."); return; }
        if (arSession != null) arSession.enabled = false;
        cameraCapture.OnCancelled  = () => { if (arSession != null) arSession.enabled = true; };
        cameraCapture.OnVideoReady = (videoPath) =>
        {
            if (arSession != null) arSession.enabled = true;
            PlaceVideoTag(videoPath);
        };
        cameraCapture.TakeVideo();
    }

    public void OnMediaSelected()
    {
        Debug.Log("TagBarController: Media selected.");
        Hide();
        GalleryPicker.Instance.OpenGallery((texture) =>
        {
            Debug.Log("TagBarController: Media picked, placing tag at: " + dropPosition);
            PlacePictureTag(texture);
        });
    }

    // -------------------------------------------------------------------------
    // Function:    OnCameraSelected
    // Inputs:      None
    // Outputs:     None
    // Description: Opens the device camera for a single photo capture.
    //              Disables ARSession during capture to avoid conflicts.
    //              On success, places a picture tag at dropPosition via
    //              PlacePictureTag(). Re-enables ARSession on completion
    //              or cancellation.
    // -------------------------------------------------------------------------
    public void OnCameraSelected()
    {
        Debug.Log("TagBarController: Camera selected.");
        Hide();

        if (cameraCapture == null)
        {
            Debug.LogWarning("TagBarController: cameraCapture is not assigned.");
            return;
        }

        if (arSession != null) arSession.enabled = false;

        cameraCapture.OnCancelled = () =>
        {
            if (arSession != null) arSession.enabled = true;
        };

        cameraCapture.OnPhotoReady = (texture) =>
        {
            if (arSession != null) arSession.enabled = true;
            PlacePictureTag(texture);
        };

        cameraCapture.TakePhoto();
    }

    public void OnScavengerSelected()
    {
        Debug.Log("TagBarController: Scavenger selected.");
        Hide();
        if (cameraCapture == null) { Debug.LogWarning("TagBarController: cameraCapture is not assigned."); return; }
        if (arSession != null) arSession.enabled = false;
        cameraCapture.OnCancelled  = () => { if (arSession != null) arSession.enabled = true; };
        cameraCapture.OnPhotoReady = (texture) =>
        {
            if (arSession != null) arSession.enabled = true;
            PlacePictureTag(texture);
        };
        cameraCapture.TakePhoto();
    }

    // --------------------------------------------------------
    // Delete button visibility
    // --------------------------------------------------------

    // -------------------------------------------------------------------------
    // Function:    ShowDeleteButton
    // Inputs:      show — true to reveal the delete button, false to hide it
    // Outputs:     None
    // Description: Called by TagSelectionManager whenever selection changes.
    //              Reveals btnTrashcan when a tag is selected so the user can
    //              delete it from the TagBar, hides it otherwise.
    // -------------------------------------------------------------------------
    public void ShowDeleteButton(bool show)
    {
        Debug.Log($"TagBarController: ShowDeleteButton({show}) btnTrashcan={btnTrashcan?.name ?? "NULL"}");
        if (btnTrashcan != null)
            btnTrashcan.gameObject.SetActive(show);
    }

    // -------------------------------------------------------------------------
    // Function:    OnDeleteSelected
    // Inputs:      None
    // Outputs:     None
    // Description: Called when the user taps the delete button in the TagBar.
    //              Delegates to TagSelectionManager.TryDeleteSelected() which
    //              handles both local Destroy and server DELETE via
    //              TagEditDeleteController. Hides the TagBar after firing.
    // -------------------------------------------------------------------------
    void OnDeleteSelected()
    {
        Debug.Log("TagBarController: Delete selected.");
        Hide();
        if (TagSelectionManager.Instance != null)
            TagSelectionManager.Instance.TryDeleteSelected();
        else
            Debug.LogWarning("TagBarController: TagSelectionManager.Instance is null — cannot delete.");
    }

    // --------------------------------------------------------
    // Tag placement helpers
    // --------------------------------------------------------

    /// <summary>
    /// Function:   PlacePictureTag
    /// Inputs:     texture — Texture2D from camera or gallery
    /// Outputs:    none
    /// Description: Instantiates postPicturePrefab at dropPosition,
    ///              applies texture, stamps timestamp, resets debounce.
    /// </summary>
    void PlacePictureTag(Texture2D texture)
    {
        if (placer == null)                   { Debug.LogError("TagBarController: placer is null!");             return; }
        if (placer.postPicturePrefab == null) { Debug.LogError("TagBarController: postPicturePrefab is null!"); return; }

        GameObject instance = Instantiate(placer.postPicturePrefab, dropPosition, Quaternion.identity);

        Transform imageTransform = instance.transform.Find("image");
        if (imageTransform != null)
        {
            Renderer rend = imageTransform.GetComponent<Renderer>();
            if (rend != null) rend.material.mainTexture = texture;
        }

        Transform timestampTransform = instance.transform.Find("timestamp");
        if (timestampTransform != null)
        {
            TextMeshPro tmp = timestampTransform.GetComponent<TextMeshPro>();
            if (tmp != null)
            {
                tmp.text      = System.DateTime.Now.ToString("MMM dd, yyyy hh:mm tt");
                tmp.alignment = TextAlignmentOptions.Center;
            }
            float imageHalfHeight = (imageTransform != null) ? imageTransform.localScale.y / 2f : 0.3f;
            timestampTransform.localPosition = new Vector3(0f, -imageHalfHeight - 0.05f, 0f);
        }

        if (TagSelectionManager.Instance != null)
            TagSelectionManager.Instance.ResetDebounce();
    }

    /// <summary>
    /// Function:   PlaceVideoTag
    /// Inputs:     videoPath — device file path to recorded video
    /// Outputs:    none
    /// Description: Instantiates postVideoPrefab at dropPosition,
    ///              sets video URL, stamps timestamp, resets debounce.
    /// </summary>
    void PlaceVideoTag(string videoPath)
    {
        if (placer == null)                { Debug.LogError("TagBarController: placer is null!");           return; }
        if (placer.postVideoPrefab == null){ Debug.LogError("TagBarController: postVideoPrefab is null!"); return; }

        GameObject instance = Instantiate(placer.postVideoPrefab, dropPosition, Quaternion.identity);

        VideoTag videoTag = instance.GetComponent<VideoTag>();
        if (videoTag != null)
        {
            videoTag.SetVideoURL("file://" + videoPath);
        }
        else
        {
            Transform videoTransform = instance.transform.Find("video");
            if (videoTransform != null)
            {
                VideoPlayer vp = videoTransform.GetComponent<VideoPlayer>();
                if (vp != null) vp.url = "file://" + videoPath;
            }
            else
                Debug.LogWarning("TagBarController: no 'video' child found on postVideoPrefab.");
        }

        Transform timestampTransform = instance.transform.Find("timestamp");
        if (timestampTransform != null)
        {
            TextMeshPro tmp = timestampTransform.GetComponent<TextMeshPro>();
            if (tmp != null)
                tmp.text = System.DateTime.Now.ToString("MMM dd, yyyy hh:mm tt");
        }

        if (TagSelectionManager.Instance != null)
            TagSelectionManager.Instance.ResetDebounce();
    }
}
