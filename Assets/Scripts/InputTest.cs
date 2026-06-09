// ============================================================
// File:        InputTest.cs
// Author:      Bryan Wilcutt
// Date:        06/08/2026
// Description: TEMPORARY DEBUG ONLY — delete after issue resolved.
//              Tests every possible input method to determine
//              which one receives touches on this device.
//              Attach to any active GameObject.
// ============================================================

using UnityEngine;
using UnityEngine.InputSystem;

public class InputTest : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Function:    Update
    // Inputs:      None
    // Outputs:     None
    // Description: Polls every available input method each frame and logs
    //              the first touch/click detected by any method.
    // -------------------------------------------------------------------------
    void Update()
    {
        // Method 1: New Input System Touchscreen
        var ts = Touchscreen.current;
        if (ts != null)
        {
            foreach (var t in ts.touches)
            {
                if (t.press.wasPressedThisFrame)
                {
                    Debug.Log("InputTest: [Method 1 - Touchscreen.press] touch at " + t.position.ReadValue());
                    return;
                }
            }
        }
        else
        {
            // Log once that touchscreen is null
            if (Time.frameCount % 300 == 0)
                Debug.Log("InputTest: Touchscreen.current is NULL");
        }

        // Method 2: Legacy Input.GetTouch
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == UnityEngine.TouchPhase.Began)
            {
                Debug.Log("InputTest: [Method 2 - Input.GetTouch] touch at " + t.position);
                return;
            }
        }

        // Method 3: Legacy Input.GetMouseButtonDown
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("InputTest: [Method 3 - Input.GetMouseButtonDown] at " + Input.mousePosition);
            return;
        }

        // Method 4: New Input System Mouse
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Debug.Log("InputTest: [Method 4 - Mouse.current] at " + mouse.position.ReadValue());
            return;
        }
    }
}
