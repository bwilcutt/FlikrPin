// =============================================================================
// File:        BaggedPosts.cs
// Author:      Bryan Wilcutt
// Date Started: (original)
// Description: Deactivates any Post-tagged GameObject that collides with this
//              object. Intended for a collection trigger or "bag" volume that
//              removes posts from the world on contact.
// =============================================================================

using UnityEngine;

public class BaggedPosts : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Function:    OnCollisionEnter
    // Inputs:      collision — collision data from the physics engine
    // Outputs:     None
    // Description: Hides any colliding GameObject tagged "Post".
    // -------------------------------------------------------------------------
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Post")
            collision.gameObject.SetActive(false);
    }
}
