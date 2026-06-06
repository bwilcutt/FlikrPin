// =============================================================================
// File:        TagEditDeleteController.cs
// Author:      Bryan Wilcutt
// Date Started: (original)
// Description: Drives the Select / Edit / Delete buttons in the settings panel.
//              Receives tag selection from TagSelectionManager (AR tap) or
//              TagSelectPanel (list UI). Delete currently destroys locally only;
//              server call is stubbed for future use.
//
// SCENE SETUP — add this script to a manager GameObject (e.g. PostCreator).
// Wire up in Inspector:
//   - btnSelect        → Btn_Select  (Button)
//   - btnEdit          → Btn_Edit    (Button)
//   - btnDelete        → Btn_Delete  (Button)
//   - tagSelectPanel   → TagSelectPanel GameObject
//   - textEditPanel    → TextEditPanel GameObject
//   - textEditInput    → TMP_InputField inside TextEditPanel
//   - textEditConfirm  → OK button inside TextEditPanel
//   - textEditCancel   → Cancel button inside TextEditPanel
//   - stickerPicker    → existing StickerPickerWindow
//   - showSticker      → existing ShowSticker (for sticker textures)
//   - jsonReader       → existing JSONReader
// =============================================================================

using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

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

    // -------------------------------------------------------------------------
    // Function:    Awake
    // Inputs:      None
    // Outputs:     None
    // Description: Hides panels before first frame renders.
    // -------------------------------------------------------------------------
    void Awake()
    {
        if (textEditPanel != null)  textEditPanel.SetActive(false);
        if (tagSelectPanel != null) tagSelectPanel.gameObject.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Function:    Start
    // Inputs:      None
    // Outputs:     None
    // Description: Grays out edit/delete buttons until a tag is selected.
    //              Wires all button listeners.
    // -------------------------------------------------------------------------
    void Start()
    {
        SetEditDeleteInteractable(false);

        if (btnSelect != null) btnSelect.onClick.AddListener(OnSelectPressed);
        if (btnEdit   != null) btnEdit.onClick.AddListener(OnEditPressed);
        if (btnDelete != null) btnDelete.onClick.AddListener(OnDeletePressed);

        if (textEditConfirm != null) textEditConfirm.onClick.AddListener(OnTextEditConfirmed);
        if (textEditCancel  != null) textEditCancel.onClick.AddListener(OnTextEditCancelled);
    }

    // ── Button handlers ──────────────────────────────────────────────────

    // -------------------------------------------------------------------------
    // Function:    OnSelectPressed
    // Inputs:      None
    // Outputs:     None
    // Description: Opens the TagSelectPanel list UI.
    // -------------------------------------------------------------------------
    void OnSelectPressed()
    {
        tagSelectPanel.Show();
    }

    // -------------------------------------------------------------------------
    // Function:    OnEditPressed
    // Inputs:      None
    // Outputs:     None
    // Description: Opens the appropriate editor for the selected tag's media type.
    // -------------------------------------------------------------------------
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

    // -------------------------------------------------------------------------
    // Function:    OnDeletePressed
    // Inputs:      None
    // Outputs:     None
    // Description: Deletes the selected tag. Destroys it locally immediately.
    //              If the tag has a postId (loaded from server), also fires a
    //              server DELETE. Freshly-created tags with no postId are
    //              destroyed locally only.
    // -------------------------------------------------------------------------
    public void OnDeletePressed()
    {
        Debug.Log($"TagEditDeleteController: OnDeletePressed fired. selectedTag={(selectedTag == null ? "NULL" : selectedTag.name)}");
        if (selectedTag == null)
        {
            Debug.LogWarning("TagEditDeleteController: OnDeletePressed — no tag selected.");
            return;
        }

        if (!string.IsNullOrEmpty(selectedTag.postId))
        {
            // Server-backed tag — delete on server then destroy locally
            StartCoroutine(DeleteTagOnServer());
        }
        else
        {
            // Local-only tag (not yet saved to server) — destroy immediately
            Debug.Log("TagEditDeleteController: Destroying local-only tag.");
            Destroy(selectedTag.gameObject);
            selectedTag = null;
            SetEditDeleteInteractable(false);
        }
    }

    // ── Selection callbacks ──────────────────────────────────────────────

    // -------------------------------------------------------------------------
    // Function:    OnTagSelected
    // Inputs:      tag — the PostTag that was selected
    // Outputs:     None
    // Description: Called by TagSelectionManager (AR tap) or TagSelectPanel
    //              (list UI). Stores selected tag and enables edit/delete buttons.
    // -------------------------------------------------------------------------
    public void OnTagSelected(PostTag tag)
    {
        selectedTag = tag;
        SetEditDeleteInteractable(true);
        Debug.Log($"TagEditDeleteController: selected tag '{tag.name}' id='{tag.postId}' type='{tag.mediaType}'");
    }

    // -------------------------------------------------------------------------
    // Function:    ClearSelection
    // Inputs:      None
    // Outputs:     None
    // Description: Clears the selected tag and disables edit/delete buttons.
    //              Called by TagSelectionManager on deselect.
    // -------------------------------------------------------------------------
    public void ClearSelection()
    {
        selectedTag = null;
        SetEditDeleteInteractable(false);
    }

    // ── Edit implementations ─────────────────────────────────────────────

    // -------------------------------------------------------------------------
    // Function:    OnImagePicked
    // Inputs:      texture — the new image texture from the gallery
    // Outputs:     None
    // Description: Updates the selected tag's image renderer and pushes to server.
    // -------------------------------------------------------------------------
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

    // -------------------------------------------------------------------------
    // Function:    UpdateVideoOnTag
    // Inputs:      path — local file path to the new video
    // Outputs:     None
    // Description: Updates the selected tag's video player and pushes to server.
    // -------------------------------------------------------------------------
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

    // -------------------------------------------------------------------------
    // Function:    OpenTextEditor
    // Inputs:      None
    // Outputs:     None
    // Description: Opens the text edit panel pre-filled with the selected tag's
    //              current message.
    // -------------------------------------------------------------------------
    void OpenTextEditor()
    {
        if (textEditPanel == null || textEditInput == null || selectedTag == null) return;

        textEditInput.text           = selectedTag.message;
        textEditInput.lineLimit      = 12;
        textEditInput.characterLimit = 288;
        textEditPanel.SetActive(true);
        textEditInput.ActivateInputField();
    }

    // -------------------------------------------------------------------------
    // Function:    OnTextEditConfirmed
    // Inputs:      None
    // Outputs:     None
    // Description: Applies edited text to the tag's bubble and pushes to server.
    // -------------------------------------------------------------------------
    void OnTextEditConfirmed()
    {
        if (selectedTag == null || textEditInput == null) return;

        string newText = textEditInput.text;

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

    // -------------------------------------------------------------------------
    // Function:    OnTextEditCancelled
    // Inputs:      None
    // Outputs:     None
    // Description: Closes the text edit panel without saving.
    // -------------------------------------------------------------------------
    void OnTextEditCancelled()
    {
        if (textEditPanel != null) textEditPanel.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Function:    OnStickerPicked
    // Inputs:      texture — the new sticker texture
    // Outputs:     None
    // Description: Updates the selected tag's sticker renderer and pushes to server.
    // -------------------------------------------------------------------------
    void OnStickerPicked(Texture2D texture)
    {
        if (selectedTag == null || texture == null) return;

        Transform stickerTransform = selectedTag.transform.Find("sticker");
        if (stickerTransform != null)
        {
            Renderer rend = stickerTransform.GetComponent<Renderer>();
            if (rend != null) rend.material.mainTexture = texture;
        }

        int newIndex = 0;
        selectedTag.stickerIndex = newIndex;
        StartCoroutine(PushUpdateToServer());
    }

    // ── Server calls ─────────────────────────────────────────────────────

    // -------------------------------------------------------------------------
    // Function:    PushUpdateToServer
    // Inputs:      None
    // Outputs:     IEnumerator (coroutine)
    // Description: Sends a PUT request to update the selected tag on the server.
    //              Only called for tags that have a postId.
    // -------------------------------------------------------------------------
    IEnumerator PushUpdateToServer()
    {
        if (selectedTag == null || string.IsNullOrEmpty(selectedTag.postId)) yield break;

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

    // -------------------------------------------------------------------------
    // Function:    DeleteTagOnServer
    // Inputs:      None
    // Outputs:     IEnumerator (coroutine)
    // Description: Sends a DELETE request to the server, then destroys the tag
    //              locally on success. Only called for tags with a postId.
    // -------------------------------------------------------------------------
    IEnumerator DeleteTagOnServer()
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

    // -------------------------------------------------------------------------
    // Function:    SetEditDeleteInteractable
    // Inputs:      value — true to enable, false to gray out
    // Outputs:     None
    // Description: Enables or disables the Edit and Delete buttons.
    // -------------------------------------------------------------------------
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
