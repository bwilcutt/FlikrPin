// =============================================================================
// File:        GearController.cs
// Author:      Bryan Wilcutt
// Date:        2026-06-05
// Description: Controls the gear/settings panel toggle. Opens panel on button
//              click, closes it when tapping outside. Uses New Input System.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class GearController : MonoBehaviour
{
    [Header("Settings Panel")]
    public GameObject settingsPanel;

    private bool isPanelOpen = false;

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Start()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Button btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(TogglePanel);
    }

    // -------------------------------------------------------------------------
    // Function:    TogglePanel
    // Inputs:      None
    // Outputs:     None
    // Description: Toggles the settings panel open/closed.
    // -------------------------------------------------------------------------
    public void TogglePanel()
    {
        isPanelOpen = !isPanelOpen;
        if (settingsPanel != null)
            settingsPanel.SetActive(isPanelOpen);
    }

    // -------------------------------------------------------------------------
    // Function:    Update
    // Inputs:      None
    // Outputs:     None
    // Description: Detects taps/clicks outside the settings panel and closes it.
    //              Uses New Input System for both mouse and touch.
    // -------------------------------------------------------------------------
    void Update()
    {
        if (!isPanelOpen) return;

        Vector2? tapPosition = null;

        // Editor / standalone — mouse
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            tapPosition = mouse.position.ReadValue();

        // Device — touch
        if (tapPosition == null)
        {
            foreach (var touch in Touch.activeTouches)
            {
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    tapPosition = touch.screenPosition;
                    break;
                }
            }
        }

        if (tapPosition == null) return;

        RectTransform rt = settingsPanel.GetComponent<RectTransform>();
        if (!RectTransformUtility.RectangleContainsScreenPoint(rt, tapPosition.Value))
        {
            isPanelOpen = false;
            settingsPanel.SetActive(false);
        }
    }
}
