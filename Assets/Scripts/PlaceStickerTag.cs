// =============================================================================
// File:        PlaceStickerTag.cs
// Author:      Bryan Wilcutt
// Date Started: (original)
// Description: Instantiates the postSticker prefab at a world position,
//              applies the chosen texture, and configures it as a flat
//              PostTag. PostTag.isFlat handles orientation — the component
//              stays enabled so TagSelectionManager can find and select it.
// =============================================================================

using UnityEngine;
using TMPro;

public class PlaceStickerTag : MonoBehaviour
{
    [Header("References")]
    public GameObject stickerPrefab;     // postSticker prefab
    public Material   stickerMaterial;   // Assign FlikrPin/StickerUnlit material in Inspector
    public Material   selectionMaterial; // Same as stickerMaterial — kept for legacy wiring

    // -------------------------------------------------------------------------
    // Function:    PlaceSticker
    // Inputs:      texture       — the sticker texture to apply
    //              worldPosition — world-space position to place the sticker
    // Outputs:     None
    // Description: Instantiates the sticker prefab, applies the texture with
    //              correct aspect ratio, and configures PostTag.isFlat so the
    //              tag keeps its dropped orientation without disabling PostTag.
    //              Keeping PostTag enabled allows TagSelectionManager to find
    //              and select sticker tags for deletion.
    // -------------------------------------------------------------------------
    public void PlaceSticker(Texture2D texture, Vector3 worldPosition)
    {
        if (stickerPrefab == null)
        {
            Debug.LogError("PlaceStickerTag: stickerPrefab is not assigned!");
            return;
        }

        if (texture == null)
        {
            Debug.LogError("PlaceStickerTag: texture is null!");
            return;
        }

        // Instantiate flat on ground
        Quaternion flatRotation = Quaternion.Euler(90f, 0f, 0f);
        GameObject instance     = Instantiate(stickerPrefab, worldPosition, flatRotation);
        instance.name           = "StickerTag_" + texture.name;

        // Find renderer — try root first, then child named "sticker"
        Renderer rend = instance.GetComponent<Renderer>();
        if (rend == null)
        {
            Transform stickerChild = instance.transform.Find("sticker");
            if (stickerChild != null)
                rend = stickerChild.GetComponent<Renderer>();
        }

        if (rend != null)
        {
            // Preserve natural aspect ratio
            float ratio = (float)texture.height / (float)texture.width;
            instance.transform.localScale = new Vector3(1f, ratio, 1f);

            // Apply texture — use Unlit/Transparent so PNG alpha works
            // Use stickerMaterial if assigned in Inspector (should be FlikrPin/StickerUnlit).
            // Falls back to Unlit/Transparent if nothing assigned.
            Material mat = stickerMaterial != null
                ? new Material(stickerMaterial)
                : new Material(Shader.Find("Unlit/Transparent"));

            mat.mainTexture = texture;
            // Disable backface culling so sticker is visible from both sides
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            rend.material   = mat;
            Debug.Log($"PlaceStickerTag: using shader '{mat.shader.name}' hasColor={mat.HasProperty("_Color")}");

        }
        else
        {
            Debug.LogWarning("PlaceStickerTag: No Renderer found on stickerPrefab.");
        }

        // Configure PostTag — use isFlat to keep dropped orientation.
        // Do NOT disable PostTag; it must stay enabled for TagSelectionManager
        // to find this tag via FindObjectsByType<PostTag>().
        PostTag postTag = instance.GetComponent<PostTag>();
        if (postTag != null)
        {
            postTag.isFlat    = true;
            postTag.mediaType = "sticker";
        }

        // Set timestamp
        Transform timestamp = instance.transform.Find("timestamp");
        if (timestamp != null)
        {
            TextMeshPro tmp = timestamp.GetComponent<TextMeshPro>();
            if (tmp != null)
                tmp.text = System.DateTime.Now.ToString("MMM dd, yyyy hh:mm tt");
        }

        // Reset debounce so the placement tap doesn't immediately select this tag
        if (TagSelectionManager.Instance != null)
            TagSelectionManager.Instance.ResetDebounce();

        Debug.Log("PlaceStickerTag: Placed '" + texture.name + "' at " + worldPosition);
    }
}
