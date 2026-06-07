// =============================================================================
// File:        ChangeScene.cs
// Author:      Bryan Wilcutt
// Date Started: 06/06/2026
// Description: Utility component that loads a named scene on demand.
//              Assign sceneName in the Inspector and call SwapScene() from
//              a UI button or other event.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeScene : MonoBehaviour
{
    // Scene to load — set in Inspector
    public string sceneName;

    // -------------------------------------------------------------------------
    // Function:    SwapScene
    // Inputs:      None
    // Outputs:     None
    // Description: Loads the scene specified by sceneName. Called from UI
    //              button events or other scripts.
    // -------------------------------------------------------------------------
    public void SwapScene()
    {
        // Load the named scene, replacing the current one
        SceneManager.LoadScene(sceneName);
    }
}
