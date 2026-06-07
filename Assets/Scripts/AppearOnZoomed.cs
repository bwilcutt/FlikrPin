// =============================================================================
// File:        AppearOnZoomed.cs
// Author:      Bryan Wilcutt
// Date Started: (original)
// Description: Shows or hides a list of GameObjects based on whether this
//              object's local X scale exceeds a threshold. Used to reveal
//              detail elements only when a tag is sufficiently zoomed in.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

public class AppearOnZoomed : MonoBehaviour
{
    public List<GameObject> objects;
    public double           limit_scale;

    // -------------------------------------------------------------------------
    // Function:    Update
    // Inputs:      None
    // Outputs:     None
    // Description: Each frame, shows all objects in the list if this transform's
    //              local X scale exceeds limit_scale, otherwise hides them.
    // -------------------------------------------------------------------------
    void Update()
    {
        bool zoomed = transform.localScale.x > limit_scale;
        foreach (GameObject obj in objects)
            obj.SetActive(zoomed);
    }
}
