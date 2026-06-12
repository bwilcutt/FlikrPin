using UnityEngine;

public class PostTag : MonoBehaviour
{
    [Header("Distance Settings")]
    public float maxDistance = 50f;
    public float selectableDistance = 10f;

    [Header("Scale Settings")]
    public float minScale = 0.05f;
    public float maxScale = 1f;

    [Header("Sticker Settings")]
    public bool isFlat = false;

    public enum PostType { Picture, Video, Text, Sticker }
    [Header("Post Type")]
    public PostType postType;

    [Header("Post Identity")]
    [HideInInspector] public string postId      = "";
    [HideInInspector] public string ownerId     = "";

    // ── Data fields used by TagEditDeleteController ───────────────────────
    [HideInInspector] public string mediaType   = "";   // "image" | "video" | "text" | "sticker"
    [HideInInspector] public string mediaUrl    = "";   // original url
    [HideInInspector] public string previewUrl  = "";   // preview_url
    [HideInInspector] public string mediaSource = "";   // "url" | "gallery"
    [HideInInspector] public string message     = "";   // text content
    [HideInInspector] public int    stickerIndex = 0;  // index into ShowSticker.stickers
    [HideInInspector] public float  stickerAspectRatio = 1f; // height/width ratio, set by PlaceStickerTag at placement time

    [Header("References")]
    public TextMesh timestampText;

    private Transform cameraTransform;
    private Collider  postCollider;
    private bool      isVisible = false;


    // Fixed world position — set once at placement, enforced every frame
    // to prevent ARCore world-origin drift from moving the tag.
    private Vector3 _fixedPosition;
    private bool    _positionLocked = false;

    // -------------------------------------------------------------------------
    // Function:    LockPosition
    // Inputs:      worldPos — the world position to lock this tag to
    // Outputs:     None
    // Description: Called once after instantiation to record the intended
    //              world position. Update() enforces it every frame.
    // -------------------------------------------------------------------------
    public void LockPosition(Vector3 worldPos)
    {
        _fixedPosition  = worldPos;
        _positionLocked = true;
    }

    void Start()
    {
        cameraTransform = Camera.main.transform;
        postCollider    = GetComponentInChildren<Collider>();

        if (postType == PostType.Sticker && isFlat)
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        // Lock to spawn position if not explicitly set by caller
        if (!_positionLocked)
            LockPosition(transform.position);
    }

    void Update()
    {
        // Enforce fixed world position every frame — prevents ARCore drift.
        // Skip if parented to an ARAnchor — the anchor handles positioning.
        if (_positionLocked && transform.parent == null)
            transform.position = _fixedPosition;

        if (cameraTransform == null) return;

        float distance = Vector3.Distance(transform.position, cameraTransform.position);

        if (distance > maxDistance)
        {
            if (isVisible) SetVisible(false);
            return;
        }

        if (!isVisible) SetVisible(true);

        // Billboard (skip if flat sticker)
        if (!(postType == PostType.Sticker && isFlat))
        {
            Vector3 lookDir = cameraTransform.position - transform.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(-lookDir);
        }

        // Logarithmic scale — for flat stickers, preserve the XY aspect ratio
        // set by PlaceStickerTag at placement time (stored in stickerAspectRatio).
        // Using Vector3.one * scale on a sticker would clobber that aspect ratio
        // every frame, shrinking the visual footprint and breaking tap detection.
        float t     = 1f - Mathf.Log(1f + distance) / Mathf.Log(1f + maxDistance);
        float scale = Mathf.Lerp(minScale, maxScale, t);

        if (postType == PostType.Sticker && isFlat)
            transform.localScale = new Vector3(scale, scale * stickerAspectRatio, scale);
        else
            transform.localScale = Vector3.one * scale;

        // Enable/disable collider based on selectable distance
        if (postCollider != null)
            postCollider.enabled = distance <= selectableDistance;
    }

    void SetVisible(bool visible)
    {
        isVisible = visible;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = visible;
    }

    public void SetTimestamp(string timestamp)
    {
        if (timestampText != null)
            timestampText.text = timestamp;
    }

    public void SetFlat(bool flat)
    {
        isFlat = flat;
        if (postType == PostType.Sticker)
            transform.rotation = flat ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
    }
}
