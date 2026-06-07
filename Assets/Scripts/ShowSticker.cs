// =============================================================================
// File:        ShowSticker.cs
// Author:      Bryan Wilcutt
// Date Started: 06/06/2026
// Description: Manages sticker display and selection for FlikrPin posts.
//              Loads sticker textures from a pre-populated stickers array and
//              instantiates them into a scrollable sticker slot grid. Also
//              supports picking an image from the device gallery via
//              NativeGallery for custom post textures.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using TMPro;

public class ShowSticker : MonoBehaviour
{
    // ── Inspector references ──────────────────────────────────────────────
    public GameObject      cell;           // Prefab for a single sticker cell in the grid
    public TextMeshProUGUI txt;            // Debug/status text label
    public GameObject      StickersSlot;  // Parent container for instantiated sticker cells
    public GameObject      previewImage;  // Preview image panel shown before stickers load

    // ── Gallery picker state ──────────────────────────────────────────────
    public Texture2D pickedImage;   // Texture loaded from the gallery
    public Material  post;          // Material on the AR post quad to apply the texture to
    public GameObject pre_post;     // RawImage preview of the pending post
    public Transform quad;          // The AR quad whose scale is adjusted to match image ratio
    public string    _image_path;   // File path of the picked gallery image

    // ── Sticker data ──────────────────────────────────────────────────────
    // Populated in the Inspector with preloaded sticker textures
    public Object[] stickers;

    // ── Private state ─────────────────────────────────────────────────────
    private bool      loaded = false;   // True once sticker cells have been instantiated
    private GameObject scroll;          // The scroll view parent, shown/hidden on repeat opens

    [SerializeField]
    private RawImage m_image;           // RawImage used for gallery image preview

    // -------------------------------------------------------------------------
    // Function:    Start
    // Inputs:      None
    // Outputs:     None
    // Description: Caches a reference to the scroll view parent by walking up
    //              from StickersSlot (slot → viewport → scroll view).
    // -------------------------------------------------------------------------
    void Start()
    {
        if (StickersSlot != null)
        {
            // StickersSlot is inside a Viewport which is inside the ScrollRect
            scroll = StickersSlot.transform.parent.parent.gameObject;
        }
    }

    // -------------------------------------------------------------------------
    // Function:    PickImageFromGallery
    // Inputs:      maxSize — maximum texture dimension in pixels (default 1024)
    // Outputs:     None
    // Description: Opens the native device gallery and lets the user pick an
    //              image. Loads it as a Texture2D, applies it to the post
    //              material and preview RawImage, and scales the AR quad to
    //              match the image's aspect ratio.
    // -------------------------------------------------------------------------
    public void PickImageFromGallery(int maxSize = 1024)
    {
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (path == null) return;   // User cancelled the picker

            // Load the selected image as a texture at the requested max size
            pickedImage = NativeGallery.LoadImageAtPath(path, maxSize);

            // Apply texture to the AR post material and the preview panel
            post.SetTexture("_MainTex", pickedImage);
            pre_post.GetComponent<RawImage>().texture = pickedImage;

            // Scale the AR quad to match the image's aspect ratio
            float ratio = (float)pickedImage.height / (float)pickedImage.width;
            float x     = 1f;           // Base width in world units
            float y     = x * ratio;    // Height scaled to match aspect ratio
            quad.localScale = new Vector3(x, y, 1);

            // Store the file path for later upload reference
            _image_path = path;
        });
    }

    // -------------------------------------------------------------------------
    // Function:    SetStickers
    // Inputs:      None
    // Outputs:     None
    // Description: On first call, instantiates a cell prefab for each sticker
    //              in the stickers array, assigning its texture and index.
    //              On subsequent calls, simply re-shows the scroll view since
    //              cells are already built.
    // -------------------------------------------------------------------------
    public void SetStickers()
    {
        if (!loaded)
        {
            // First time opening — build all sticker cells from the stickers array
            int count = 0;
            foreach (var image in stickers)
            {
                // Configure the cell prefab before instantiating
                cell.GetComponent<RawImage>().texture          = (Texture2D)image;
                cell.GetComponent<SelectSticker>().sticker_index = count;
                count++;

                // Instantiate a copy of the configured cell into the sticker slot container
                Instantiate(cell, StickersSlot.transform);
            }

            loaded = true;
        }
        else
        {
            // Already built — just re-show the scroll view and hide the preview
            scroll.SetActive(true);
            previewImage.SetActive(false);
        }
    }
}
