// =============================================================================
// File:        ActivateButton.cs
// Author:      Bryan Wilcutt
// Date Started: (original)
// Description: Utility component that exposes show/hide methods for a Button
//              GameObject, callable from UI events or other scripts.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;

public class ActivateButton : MonoBehaviour
{
    public Button button;

    // -------------------------------------------------------------------------
    // Function:    showButton
    // Inputs:      None
    // Outputs:     None
    // Description: Makes the assigned button visible and interactable.
    // -------------------------------------------------------------------------
    public void showButton()
    {
        button.gameObject.SetActive(true);
    }

    // -------------------------------------------------------------------------
    // Function:    hideButton
    // Inputs:      None
    // Outputs:     None
    // Description: Hides the assigned button.
    // -------------------------------------------------------------------------
    public void hideButton()
    {
        button.gameObject.SetActive(false);
    }
}
