using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using TMPro;

public class PostTypeWindow : MonoBehaviour
{
    [Header("References")]
    public GPS gps;
    public JSONReader jsonReader;
    public TextInputWindow textInputWindow;
    public PlacePrefabInWorld placer;
    public StickerPickerWindow stickerPickerWindow;
    public PlaceStickerTag placeStickerTag;

    [Header("Buttons")]
    public Button btnText;       // Top-left    — speech bubble
    public Button btnFriends;    // Top-right   — people icon
    public Button btnSticker;    // Center      — sticker/heart icon
    public Button btnVideo;      // Bottom-left — movie camera
    public Button btnMedia;      // Bottom-right — camera + photo (gallery)

    private Vector3 dropPosition;
    private ARRaycastManager arRaycastManager;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (placer == null)
            placer = FindAnyObjectByType<PlacePrefabInWorld>();

        arRaycastManager = FindAnyObjectByType<ARRaycastManager>();

        Debug.Log("Awake — placer: " + (placer != null) +
                  ", prefab: " + (placer?.postPicturePrefab != null ? placer.postPicturePrefab.name : "NULL") +
                  ", arRaycastManager: " + (arRaycastManager != null));

        WireButtons(); // Awake fires even when GameObject is inactive
    }

    void Start()
    {
        // GameObject starts hidden — shown only via Show()
    }

    // ── Button wiring ────────────────────────────────────────────────────────

    void WireButtons()
    {
    Debug.Log("WireButtons called!");
    Debug.Log("btnText is: " + (btnText != null ? "assigned" : "NULL"));
    Debug.Log("btnFriends is: " + (btnFriends != null ? "assigned" : "NULL"));
    Debug.Log("btnSticker is: " + (btnSticker != null ? "assigned" : "NULL"));
    Debug.Log("btnVideo is: " + (btnVideo != null ? "assigned" : "NULL"));
    Debug.Log("btnMedia is: " + (btnMedia != null ? "assigned" : "NULL"));    
        if (btnText    != null) btnText.onClick.AddListener(OnTextSelected);
        if (btnFriends != null) btnFriends.onClick.AddListener(OnFriendsSelected);
        if (btnSticker != null) btnSticker.onClick.AddListener(OnStickerSelected);
        if (btnVideo   != null) btnVideo.onClick.AddListener(OnVideoSelected);
        if (btnMedia   != null) btnMedia.onClick.AddListener(OnMediaSelected);
    }

    // ── Show / Hide ──────────────────────────────────────────────────────────

    // Called by whatever triggers the post type window (e.g. a long press).
    // Captures the AR world position before any native UI (gallery/camera)
    // takes over and Unity loses focus.
    public void Show(Vector3 fallbackPosition)
    {
        dropPosition = GetARDropPosition(fallbackPosition);
        Debug.Log("PostTypeWindow.Show() — dropPosition set to: " + dropPosition);
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // ── AR drop position ─────────────────────────────────────────────────────

    // Raycasts against AR planes from screen center.
    // Falls back to 2 m in front of the camera if no plane is hit.
    Vector3 GetARDropPosition(Vector3 fallback)
    {
        if (arRaycastManager != null)
        {
            var hits = new List<ARRaycastHit>();
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

            if (arRaycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon))
            {
                Debug.Log("AR plane hit — using raycast position.");
                return hits[0].pose.position;
            }
        }

        Debug.Log("No AR plane hit — using camera forward fallback.");
        return Camera.main.transform.position + Camera.main.transform.forward * 2f;
    }

    // ── Button handlers ──────────────────────────────────────────────────────

    void OnTextSelected()
    {
        Debug.Log("PostTypeWindow: Text selected.");
        Hide();

        if (textInputWindow != null)
            textInputWindow.Show(dropPosition);
        else
            Debug.LogWarning("PostTypeWindow: textInputWindow is not assigned.");
    }

    void OnFriendsSelected()
    {
        Debug.Log("PostTypeWindow: Invite Friends selected.");
        Hide();

        // TODO: open friends / invite panel
    }

    void OnStickerSelected()
    {
        Debug.Log("PostTypeWindow: Sticker selected.");
        Hide();

        if (stickerPickerWindow != null)
        {
            stickerPickerWindow.OnStickerSelected = (texture) =>
            {
                placeStickerTag.PlaceSticker(texture, dropPosition);
            };
            stickerPickerWindow.Show();
        }
        else
        {
            Debug.LogWarning("PostTypeWindow: stickerPickerWindow is not assigned.");
        }
    }

    void OnVideoSelected()
    {
        Debug.Log("PostTypeWindow: Video selected.");
        Hide();

        // dropPosition already captured in Show() before the camera opens
        NativeCamera.RecordVideo((path) =>
        {
            if (path == null)
            {
                Debug.Log("Video recording cancelled.");
                return;
            }
            Debug.Log("Video recorded: " + path);
            PlaceVideoTag(path);
        }, NativeCamera.Quality.High, 30);
    }

    void OnMediaSelected()
    {
        Debug.Log("PostTypeWindow: Media (gallery) selected.");
        Hide();

        // dropPosition already captured in Show() before the gallery opens
        GalleryPicker.Instance.OpenGallery((texture) =>
        {
            Debug.Log("Media picked, placing tag at: " + dropPosition);
            PlacePictureTag(texture);
        });
    }

    // ── Place helpers ────────────────────────────────────────────────────────

    void PlacePictureTag(Texture2D texture)
    {
        if (placer == null)               { Debug.LogError("PostTypeWindow: placer is null!"); return; }
        if (placer.postPicturePrefab == null) { Debug.LogError("PostTypeWindow: postPicturePrefab is null!"); return; }

        GameObject instance = Instantiate(placer.postPicturePrefab, dropPosition, Quaternion.identity);

        // Apply texture to image quad
        Transform imageTransform = instance.transform.Find("image");
        if (imageTransform != null)
        {
            Renderer rend = imageTransform.GetComponent<Renderer>();
            if (rend != null)
                rend.material.mainTexture = texture;
        }

        // Timestamp — positioned just below the image quad
        Transform timestampTransform = instance.transform.Find("timestamp");
        if (timestampTransform != null)
        {
            TextMeshPro tmp = timestampTransform.GetComponent<TextMeshPro>();
            if (tmp != null)
            {
                tmp.text = System.DateTime.Now.ToString("MMM dd, yyyy hh:mm tt");
                tmp.alignment = TextAlignmentOptions.Center;
            }

            float imageHalfHeight = (imageTransform != null) ? imageTransform.localScale.y / 2f : 0.3f;
            timestampTransform.localPosition = new Vector3(0f, -imageHalfHeight - 0.05f, 0f);
        }
    }

    void PlaceVideoTag(string videoPath)
    {
        if (placer == null)               { Debug.LogError("PostTypeWindow: placer is null!"); return; }
        if (placer.postVideoPrefab == null)   { Debug.LogError("PostTypeWindow: postVideoPrefab is null!"); return; }

        Debug.Log("PlaceVideoTag — instantiating at: " + dropPosition + ", path: " + videoPath);
        GameObject instance = Instantiate(placer.postVideoPrefab, dropPosition, Quaternion.identity);

        // Prefer VideoTag component on root; fall back to VideoPlayer on "video" child
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
                if (vp != null)
                    vp.url = "file://" + videoPath;
            }
            else
            {
                Debug.LogWarning("PlaceVideoTag: no 'video' child found on postVideoPrefab.");
            }
        }

        // Timestamp
        Transform timestampTransform = instance.transform.Find("timestamp");
        if (timestampTransform != null)
        {
            TextMeshPro tmp = timestampTransform.GetComponent<TextMeshPro>();
            if (tmp != null)
                tmp.text = System.DateTime.Now.ToString("MMM dd, yyyy hh:mm tt");
        }
    }
}
