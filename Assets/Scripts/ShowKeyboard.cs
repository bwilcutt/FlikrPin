// =============================================================================
// File:        ShowKeyboard.cs
// Author:      Bryan Wilcutt
// Date Started: 06/06/2026
// Description: Manages the on-screen touch keyboard for text input in FlikrPin.
//              Supports toggling the keyboard open/close, forcing it open or
//              closed independently, and reading the typed text each GUI frame
//              into a TextMeshPro field and a public string for other scripts.
// =============================================================================

using UnityEngine;
using TMPro;

public class ShowKeyboard : MonoBehaviour
{
    public TouchScreenKeyboard keyboard;   // Reference to the active keyboard session
    public TextMeshProUGUI     inputText;  // TMP field that mirrors keyboard text live

    public bool   clicked  = false;   // Tracks whether the keyboard toggle is currently open
    public string message;            // Latest text from the keyboard, readable by other scripts
    public bool   editing  = false;   // True while the keyboard is open and accepting input

    // -------------------------------------------------------------------------
    // Function:    ShowKeyboardInput
    // Inputs:      None
    // Outputs:     None
    // Description: Toggles the touch keyboard open or closed. First call opens
    //              it; second call closes it. Manages clicked and editing state.
    // -------------------------------------------------------------------------
    public void ShowKeyboardInput()
    {
        if (!clicked)
        {
            // Open the default keyboard with an empty starting string
            keyboard = TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default);
            editing  = true;
            clicked  = true;
        }
        else
        {
            // Close the keyboard and reset state
            keyboard.active = false;
            editing         = false;
            clicked         = false;
        }
    }

    // -------------------------------------------------------------------------
    // Function:    ShowKeyboardInputOnly
    // Inputs:      None
    // Outputs:     None
    // Description: Opens the touch keyboard unconditionally without toggling.
    //              Use when you need to force the keyboard open regardless of
    //              current clicked state.
    // -------------------------------------------------------------------------
    public void ShowKeyboardInputOnly()
    {
        keyboard = TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default);
        editing  = true;
        clicked  = true;
    }

    // -------------------------------------------------------------------------
    // Function:    HideKeyboardInputOnly
    // Inputs:      None
    // Outputs:     None
    // Description: Closes the touch keyboard unconditionally without toggling.
    //              Use when you need to force the keyboard closed regardless of
    //              current clicked state.
    // -------------------------------------------------------------------------
    public void HideKeyboardInputOnly()
    {
        keyboard.active = false;
        editing         = false;
        clicked         = false;
    }

    // -------------------------------------------------------------------------
    // Function:    OnGUI
    // Inputs:      None
    // Outputs:     None
    // Description: Called by Unity each GUI frame. While the keyboard is open
    //              and editing, mirrors keyboard.text to the TMP field and the
    //              public message string. Clears editing flag when the user
    //              confirms input (keyboard status == Done).
    // -------------------------------------------------------------------------
    void OnGUI()
    {
        // Mirror keyboard text to UI and public message while keyboard is active
        if (keyboard != null && editing)
        {
            inputText.text = keyboard.text;
            message        = keyboard.text;
        }

        // Stop editing when the user taps Done on the keyboard
        if (keyboard != null && keyboard.status == TouchScreenKeyboard.Status.Done)
            editing = false;
    }
}
