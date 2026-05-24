using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;


/// <summary>
/// Drives the Select / Edit / Delete buttons in the settings panel.
/// 
/// SCENE SETUP — add this script to a manager GameObject (e.g. PostCreator).
/// Wire up in Inspector:
///   - btnSelect        → Btn_Select  (Button)
///   - btnEdit          → Btn_Edit    (Button)
///   - btnDelete        → Btn_Delete  (Button)
///   - tagSelectPanel   → TagSelectPanel GameObject
///   - textEditPanel    → TextEditPanel GameObject (see below)
///   - textEditInput    → TMP_InputField inside TextEditPanel
///   - textEditConfirm  → OK button inside TextEditPanel
///   - textEditCancel   → Cancel button inside TextEditPanel
///   - stickerPicker    → existing StickerPickerWindow
///   - showSticker      → existing ShowSticker (for sticker textures)
///   - jsonReader       → existing JSONReader
/// 
/// TextEditPanel SCENE SETUP:
///   Canvas
///     └── TextEditPanel           ← MUST be INACTIVE in the Inspector by default
///           ├── Background        (semi-transparent overlay)
///           ├── Window
///           │     ├── TitleText   (TextMeshProUGUI)
///           │     ├── MessageInput (TMP_InputField, multiline)
///           │     └── OKButton    (Button)
///           └── CancelButton      (Button)
///
/// TagSelectPanel SCENE SETUP:
///   Canvas
///     └── TagSelectPanel          ← MUST be INACTIVE in the Inspector by default
///           ├── Background
///           └── Window
///                 ├── TitleText
///                 ├── ScrollView  (ScrollRect)
///                 │     └── Viewport
///                 │           └── Content   ← assign this to TagSelectPanel.contentParent
///                 └── CloseButton
/// </summary>
public class TagEditDeleteController : MonoBehaviour
{
    [Header("Settings Panel Buttons")]
    public Button btnSelect;
    public Button btnEdit;
    public Button btnDelete;

    [Header("Panels")]
    public TagSelectPanel tagSelectPanel;
    public GameObject textEditPanel;
    public TMP_InputField textEditInput;
    public Button textEditConfirm;
    public Button textEditCancel;

    [Header("Existing References")]
    public StickerPickerWindow stickerPicker;
    public ShowSticker showSticker;
    public JSONReader jsonReader;

    // Currently selected tag
    private PostTag selectedTag;

    // ── Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        // Hide panels immediately — before any frame renders.
        // This is the fix for the dialog appearing on startup.
        // Even if the GameObject is accidentally left active in the scene,
        // Awake() runs before the first frame so nothing is visible.
        if (textEditPanel != null)  textEditPanel.SetActive(false);
        if (tagSelectPanel != null) tagSelectPanel.gameObject.SetActive(false);
    }

    void Start()
    {
        // Buttons start ghosted until a tag is selected
        SetEditDeleteInteractable(false);

        if (btnSelect != null) btnSelect.onClick.AddListener(OnSelectPressed);
        if (btnEdit   != null) btnEdit.onClick.AddListener(OnEditPressed);
        if (btnDelete != null) btnDelete.onClick.AddListener(OnDeletePressed);

        if (textEditConfirm != null) textEditConfirm.onClick.AddListener(OnTextEditConfirmed);
        if (textEditCancel  != null) textEditCancel.onClick.AddListener(OnTextEditCancelled);
    }

    // ── Button handlers ──────────────────────────────────────────────────

    void OnSelectPressed()
    {
        tagSelectPanel.Show();
    }

    void OnEditPressed()
    {
        if (selectedTag == null) return;

        switch (selectedTag.mediaType)
        {
            case "image":
                GalleryPicker.Instance.OpenGallery(OnImagePicked);
                break;

            case "video":
                NativeCamera.RecordVideo((path) =>
                {
                    if (path == null) return;
                    UpdateVideoOnTag(path);
                }, NativeCamera.Quality.High, 15);
                break;

            case "text":
                OpenTextEditor();
                break;

            case "sticker":
                stickerPicker.OnStickerSelected = OnStickerPicked;
                stickerPicker.Show();
                break;
        }
    }

    void OnDeletePressed()
    {
        if (selectedTag == null) return;
        StartCoroutine(DeleteTag());
    }

    // ── Selection callback ───────────────────────────────────────────────

    /// <summary>Called by TagSelectPanel when the user picks a row.</summary>
    public void OnTagSelected(PostTag tag)
    {
        selectedTag = tag;
        SetEditDeleteInteractable(true);
        Debug.Log($"TagEditDeleteController: selected tag {tag.postId} ({tag.mediaType})");
    }

    // ── Edit implementations ─────────────────────────────────────────────

    void OnImagePicked(Texture2D texture)
    {
        if (selectedTag == null || texture == null) return;

        Transform imageTransform = selectedTag.transform.Find("image");
        if (imageTransform != null)
        {
            Renderer rend = imageTransform.GetComponent<Renderer>();
            if (rend != null) rend.material.mainTexture = texture;
        }

        selectedTag.mediaUrl    = "(gallery updated)";
        selectedTag.mediaSource = "gallery";

        StartCoroutine(PushUpdateToServer());
    }

    void UpdateVideoOnTag(string path)
    {
        if (selectedTag == null) return;

        Transform videoTransform = selectedTag.transform.Find("video");
        if (videoTransform != null)
        {
            var vt = videoTransform.GetComponent<VideoTag>();
            if (vt != null) vt.SetVideoURL("file://" + path);
        }

        selectedTag.mediaUrl    = path;
        selectedTag.mediaSource = "gallery";

        StartCoroutine(PushUpdateToServer());
    }

    void OpenTextEditor()
    {
        if (textEditPanel == null || textEditInput == null || selectedTag == null) return;

        textEditInput.text           = selectedTag.message;
        textEditInput.lineLimit      = 12;
        textEditInput.characterLimit = 288;
        textEditPanel.SetActive(true);
        textEditInput.ActivateInputField();
    }

    void OnTextEditConfirmed()
    {
        if (selectedTag == null || textEditInput == null) return;

        string newText = textEditInput.text;

        // Update the world-space bubble text immediately
        Transform contentTransform = selectedTag.transform.Find("bubble/content");
        if (contentTransform != null)
        {
            TextMeshPro tmp = contentTransform.GetComponent<TextMeshPro>();
            if (tmp != null) tmp.text = newText;
        }

        selectedTag.message = newText;
        textEditPanel.SetActive(false);

        StartCoroutine(PushUpdateToServer());
    }

    void OnTextEditCancelled()
    {
        if (textEditPanel != null) textEditPanel.SetActive(false);
    }

    void OnStickerPicked(Texture2D texture)
    {
        if (selectedTag == null || texture == null) return;

        Transform stickerTransform = selectedTag.transform.Find("sticker");
        if (stickerTransform != null)
        {
            Renderer rend = stickerTransform.GetComponent<Renderer>();
            if (rend != null) rend.material.mainTexture = texture;
        }

        // Match by texture name to find the sticker index (ShowSticker is optional)
        int newIndex = 0;

        selectedTag.stickerIndex = newIndex;
        StartCoroutine(PushUpdateToServer());
    }

    // ── Server calls ─────────────────────────────────────────────────────

    IEnumerator PushUpdateToServer()
    {
        if (selectedTag == null) yield break;

        string url = jsonReader.database_ip + "/posts/update";

        PostUpdatePayload payload = new PostUpdatePayload
        {
            _id           = selectedTag.postId,
            message       = selectedTag.message,
            url           = selectedTag.mediaUrl,
            media_source  = selectedTag.mediaSource,
            preview_url   = selectedTag.previewUrl,
            media_type    = selectedTag.mediaType,
            sticker_index = selectedTag.stickerIndex.ToString()
        };

        string json = JsonUtility.ToJson(payload);

        using (UnityWebRequest request = new UnityWebRequest(url, "PUT"))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            byte[] body = Encoding.UTF8.GetBytes(json);
            request.uploadHandler   = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
                Debug.Log("TagEditDeleteController: Update succeeded.");
            else
                Debug.LogWarning("TagEditDeleteController: Update failed — " + request.error);
        }
    }

    IEnumerator DeleteTag()
    {
        string url = jsonReader.database_ip + "/posts/" + selectedTag.postId;

        using (UnityWebRequest request = UnityWebRequest.Delete(url))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("TagEditDeleteController: Deleted post " + selectedTag.postId);
                Destroy(selectedTag.gameObject);
                selectedTag = null;
                SetEditDeleteInteractable(false);
            }
            else
            {
                Debug.LogWarning("TagEditDeleteController: Delete failed — " + request.error);
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    void SetEditDeleteInteractable(bool value)
    {
        if (btnEdit   != null) btnEdit.interactable   = value;
        if (btnDelete != null) btnDelete.interactable = value;
    }

    // ── Payload ──────────────────────────────────────────────────────────

    [System.Serializable]
    class PostUpdatePayload
    {
        public string _id;
        public string message;
        public string url;
        public string preview_url;
        public string media_source;
        public string media_type;
        public string sticker_index;
    }
}
