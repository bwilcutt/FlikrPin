using UnityEngine;

/// <summary>
/// Instantiates a sticker prefab at a world position,
/// applies the chosen texture, and sets it up as a
/// billboard PostTag.
/// </summary>
public class PlaceStickerTag : MonoBehaviour
{
    [Header("References")]
    public GameObject stickerPrefab;    // Prefab with a quad Renderer + PostTag component
    public PlacePrefabInWorld placer;   // Used to get drop position if needed

    /// <summary>
    /// Place a sticker at the given world position with the given texture.
    /// </summary>
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

        // Instantiate at the drop position
        GameObject instance = Instantiate(stickerPrefab, worldPosition, Quaternion.identity);
        instance.name = "StickerTag_" + texture.name;

        // Apply texture — look for a Renderer on the root or a child named "image"
        Renderer rend = instance.GetComponent<Renderer>();
        if (rend == null)
        {
            Transform imageChild = instance.transform.Find("image");
            if (imageChild != null)
                rend = imageChild.GetComponent<Renderer>();
        }

        if (rend != null)
        {
            // Keep natural aspect ratio like a picture tag
            float ratio = (float)texture.height / (float)texture.width;
            instance.transform.localScale = new Vector3(1f, ratio, 1f);
            rend.material.mainTexture = texture;
        }
        else
        {
            Debug.LogWarning("PlaceStickerTag: No Renderer found on stickerPrefab or its 'image' child.");
        }

        // Ensure PostTag is set to Sticker type and billboard mode
        PostTag postTag = instance.GetComponent<PostTag>();
        if (postTag != null)
        {
            postTag.postType = PostTag.PostType.Sticker;
            postTag.isFlat = false;  // billboard, not flat on ground
        }

        // Set timestamp if present
        Transform timestamp = instance.transform.Find("timestamp");
        if (timestamp != null)
        {
            TMPro.TextMeshPro tmp = timestamp.GetComponent<TMPro.TextMeshPro>();
            if (tmp != null)
                tmp.text = System.DateTime.Now.ToString("MMM dd, yyyy hh:mm tt");
        }

        Debug.Log("PlaceStickerTag: Placed sticker '" + texture.name + "' at " + worldPosition);
    }
}
