using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using TMPro;

public class PostTypeWindow : MonoBehaviour
{
    [Header("References")]
    public TextInputWindow    textInputWindow;
    public PlacePrefabInWorld placer;
    public StickerPickerWindow stickerPickerWindow;
    public PlaceStickerTag    placeStickerTag;
    public CameraCapture      cameraCapture;


    [Header("Target Icon")]
    public GameObject targetIcon;

    [Header("Buttons")]
    public Button btnText;
    public Button btnFriends;
    public Button btnSticker;
    public Button btnVideo;
    public Button btnMedia;
    public Button btnScavenger;

    [Header("Audio")]
    public AudioClip clickSound;

    [Header("Fade Settings")]
    public float fadeDuration = 0.25f;

    private CanvasGroup  canvasGroup;
    private AudioSource  audioSource;
    private Vector3      dropPosition;
    private ARRaycastManager arRaycastManager;
    private ARSession    arSession;
    private bool         isVisible = false;

    void Awake()
    {
        if (placer == null)
            placer = FindAnyObjectByType<PlacePrefabInWorld>();

        arRaycastManager = FindAnyObjectByType<ARRaycastManager>();
        arSession        = FindAnyObjectByType<ARSession>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        if (clickSound != null)
            audioSource.clip = clickSound;

        canvasGroup.alpha          = 0f;
        canvasGroup.interactable   = false;
        canvasGroup.blocksRaycasts = false;

        WireButtons();
    }

    void Start()
    {
        if (targetIcon != null)
        {
            Button tb = targetIcon.GetComponent<Button>();
            if (tb == null) tb = targetIcon.AddComponent<Button>();
            tb.onClick.AddListener(OnTargetIconPressed);
        }

        // Pre-warm sticker picker so copy finishes before first tap
        if (stickerPickerWindow != null)
            stickerPickerWindow.Preload();
    }

    void WireButtons()
    {
        if (btnText      != null) { btnText.onClick.AddListener(OnTextSelected);           AddPointerDownSound(btnText.gameObject); }
        if (btnFriends   != null) { btnFriends.onClick.AddListener(OnFriendsSelected);     AddPointerDownSound(btnFriends.gameObject); }
        if (btnSticker   != null) { btnSticker.onClick.AddListener(OnStickerSelected);     AddPointerDownSound(btnSticker.gameObject); }
        if (btnVideo     != null) { btnVideo.onClick.AddListener(OnVideoSelected);         AddPointerDownSound(btnVideo.gameObject); }
        if (btnMedia     != null) { btnMedia.onClick.AddListener(OnMediaSelected);         AddPointerDownSound(btnMedia.gameObject); }
        if (btnScavenger != null) { btnScavenger.onClick.AddListener(OnScavengerSelected); AddPointerDownSound(btnScavenger.gameObject); }
    }

    void AddPointerDownSound(GameObject go)
    {
        var trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        entry.callback.AddListener((_) => PlayClick());
        trigger.triggers.Add(entry);
    }

    void OnTargetIconPressed()
    {
        if (!isVisible) Show(Vector3.zero);
        else            Hide();
    }

    public void Show()              { Show(Vector3.zero); }

    public void Show(Vector3 fallbackPosition)
    {
        dropPosition = GetARDropPosition(fallbackPosition);
        Debug.Log("PostTypeWindow.Show() — dropPosition: " + dropPosition);

        isVisible = true;
        if (targetIcon != null) targetIcon.SetActive(false);

        StopAllCoroutines();
        StartCoroutine(Fade(0f, 1f, true));
    }

    public void Hide()
    {
        isVisible = false;
        StopAllCoroutines();
        StartCoroutine(Fade(1f, 0f, false));
    }

    IEnumerator Fade(float from, float to, bool interactive)
    {
        if (interactive)
        {
            canvasGroup.interactable   = true;
            canvasGroup.blocksRaycasts = true;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = to;

        if (!interactive)
        {
            canvasGroup.interactable   = false;
            canvasGroup.blocksRaycasts = false;
            if (targetIcon != null) targetIcon.SetActive(true);
        }
    }

    Vector3 GetARDropPosition(Vector3 fallback)
    {
        if (arRaycastManager != null)
        {
            var hits = new List<ARRaycastHit>();
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

            if (arRaycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon))
            {
                Debug.Log("PostTypeWindow: AR plane hit.");
                return hits[0].pose.position;
            }
        }

        Debug.Log("PostTypeWindow: No AR plane — using camera forward fallback.");
        return Camera.main.transform.position + Camera.main.transform.forward * 2f;
    }

    void PlayClick()
    {
        if (audioSource != null && clickSound != null)
            audioSource.Play();
    }

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
        Debug.Log("PostTypeWindow: Friends selected.");
        Hide();
    }

    public void OnStickerSelected()
    {
        Debug.Log("PostTypeWindow: Sticker selected.");
        Hide();

        if (stickerPickerWindow != null && placeStickerTag != null)
        {
            stickerPickerWindow.OnStickerSelected = (texture) =>
            {
                placeStickerTag.PlaceSticker(texture, dropPosition);
            };
            stickerPickerWindow.Show();
        }
        else
        {
            if (stickerPickerWindow == null)
                Debug.LogWarning("PostTypeWindow: stickerPickerWindow is not assigned.");
            if (placeStickerTag == null)
                Debug.LogWarning("PostTypeWindow: placeStickerTag is not assigned.");
        }
    }

    public void OnVideoSelected()
    {
        Debug.Log("PostTypeWindow: Video selected.");
        Hide();

        if (cameraCapture == null)
        {
            Debug.LogWarning("PostTypeWindow: cameraCapture is not assigned.");
            return;
        }

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
        Debug.Log("PostTypeWindow: Media selected.");
        Hide();

        GalleryPicker.Instance.OpenGallery((texture) =>
        {
            Debug.Log("Media picked, placing tag at: " + dropPosition);
            PlacePictureTag(texture);
        });
    }

    public void OnScavengerSelected()
    {
        Debug.Log("PostTypeWindow: Picture selected.");
        Hide();

        if (cameraCapture == null)
        {
            Debug.LogWarning("PostTypeWindow: cameraCapture is not assigned.");
            return;
        }

        if (arSession != null) arSession.enabled = false;

        cameraCapture.OnCancelled  = () => { if (arSession != null) arSession.enabled = true; };
        cameraCapture.OnPhotoReady = (texture) =>
        {
            if (arSession != null) arSession.enabled = true;
            PlacePictureTag(texture);
        };
        cameraCapture.TakePhoto();
    }

    void PlacePictureTag(Texture2D texture)
    {
        if (placer == null)                   { Debug.LogError("placer is null!");             return; }
        if (placer.postPicturePrefab == null) { Debug.LogError("postPicturePrefab is null!"); return; }

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

        // Reset debounce so placement tap doesn't immediately select this tag
        if (TagSelectionManager.Instance != null)
            TagSelectionManager.Instance.ResetDebounce();
    }

    void PlaceVideoTag(string videoPath)
    {
        if (placer == null)                { Debug.LogError("placer is null!");           return; }
        if (placer.postVideoPrefab == null){ Debug.LogError("postVideoPrefab is null!"); return; }

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
                Debug.LogWarning("PlaceVideoTag: no 'video' child found on postVideoPrefab.");
        }

        Transform timestampTransform = instance.transform.Find("timestamp");
        if (timestampTransform != null)
        {
            TextMeshPro tmp = timestampTransform.GetComponent<TextMeshPro>();
            if (tmp != null)
                tmp.text = System.DateTime.Now.ToString("MMM dd, yyyy hh:mm tt");
        }

        // Reset debounce so placement tap doesn't immediately select this tag
        if (TagSelectionManager.Instance != null)
            TagSelectionManager.Instance.ResetDebounce();
    }
}
