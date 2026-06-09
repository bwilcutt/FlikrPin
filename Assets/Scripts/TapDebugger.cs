// ============================================================
// File:        TapDebugger.cs
// Author:      Bryan Wilcutt
// Date:        06/08/2026
// Description: TEMPORARY DEBUG ONLY — delete after issue resolved.
//              Logs every UI element hit by a raycast on tap, and
//              also logs raw touch/mouse input to confirm input
//              is reaching Unity at all. Attach to any active
//              GameObject in the scene.
// ============================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class TapDebugger : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Function:    Update
    // Inputs:      None
    // Outputs:     None
    // Description: Every frame checks for touch or mouse input. On tap,
    //              logs the raw input position and all UI elements hit
    //              by a GraphicRaycaster at that position.
    // -------------------------------------------------------------------------
    void Update()
    {
        Vector2? tapPos = null;

        // New Input System touch
        var ts = Touchscreen.current;
        if (ts != null)
        {
            foreach (var t in ts.touches)
                if (t.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                { tapPos = t.position.ReadValue(); break; }
        }

        // Legacy touch fallback
        if (tapPos == null && Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == UnityEngine.TouchPhase.Began)
                tapPos = t.position;
        }

        // Mouse fallback (editor)
        var mouse = Mouse.current;
        if (tapPos == null && mouse != null && mouse.leftButton.wasPressedThisFrame)
            tapPos = mouse.position.ReadValue();

        if (tapPos == null) return;

        Debug.Log($"TapDebugger: RAW INPUT at {tapPos.Value}");

        // Log every UI element hit at this position
        if (EventSystem.current == null)
        {
            Debug.LogError("TapDebugger: No EventSystem in scene!");
            return;
        }

        var ped = new PointerEventData(EventSystem.current);
        ped.position = tapPos.Value;
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);

        if (results.Count == 0)
        {
            Debug.Log("TapDebugger: No UI elements hit — tap went through to AR.");
        }
        else
        {
            Debug.Log($"TapDebugger: {results.Count} UI element(s) hit:");
            foreach (var r in results)
                Debug.Log($"  [{r.sortingOrder}] {r.gameObject.name} " +
                          $"(parent: {r.gameObject.transform.parent?.name}) " +
                          $"depth={r.depth}");
        }
    }
}