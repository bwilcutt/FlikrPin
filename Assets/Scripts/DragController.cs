// =============================================================================
// File:        DragController.cs
// Author:      Bryan Wilcutt
// Date Started: 06/06/2026
// Description: Handles drag-and-drop interaction for 2D GameObjects that have
//              a Dragger component. Supports both mouse input (editor/desktop)
//              and single-finger touch input (Android). Picks up a Dragger on
//              contact, moves it each frame, and drops it on release.
// =============================================================================

using UnityEngine;

public class DragController : MonoBehaviour
{
    private bool    _isDragActive  = false;   // True while a Dragger is being held
    private Vector2 _screenPosition;          // Current input position in screen space
    private Vector3 _worldPosition;           // Current input position in world space
    private Dragger _lastDragged;             // The Dragger component currently being dragged

    // -------------------------------------------------------------------------
    // Function:    Update
    // Inputs:      None
    // Outputs:     None
    // Description: Each frame, reads mouse or touch input and either initiates,
    //              continues, or ends a drag operation depending on current state.
    // -------------------------------------------------------------------------
    void Update()
    {
        // If we are dragging and the mouse button is released or touch ended, drop the object
        if (_isDragActive &&
            (Input.GetMouseButtonDown(0) ||
            (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Ended)))
        {
            Drop();
            return;
        }

        // Read current input position — prefer mouse, fall back to first touch
        if (Input.GetMouseButton(0))
        {
            // Mouse input: extract X/Y from the 3D mouse position
            Vector3 mousePos  = Input.mousePosition;
            _screenPosition   = new Vector2(mousePos.x, mousePos.y);
        }
        else if (Input.touchCount > 0)
        {
            // Touch input: use the first finger's position
            _screenPosition = Input.GetTouch(0).position;
        }
        else
        {
            // No input active this frame — nothing to do
            return;
        }

        // Convert screen position to world space for physics and movement
        _worldPosition = Camera.main.ScreenToWorldPoint(_screenPosition);

        if (_isDragActive)
        {
            // Already holding something — move it to follow the input
            Drag();
        }
        else
        {
            // Not dragging yet — check if input is over a Dragger object
            RaycastHit2D hit = Physics2D.Raycast(_worldPosition, Vector2.zero);

            if (hit.collider != null)
            {
                // Check if the hit object has a Dragger component
                Dragger draggable = hit.transform.gameObject.GetComponent<Dragger>();

                if (draggable != null)
                {
                    // Found a draggable — store reference and start the drag
                    _lastDragged = draggable;
                    InitDrag();
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Function:    Drag
    // Inputs:      None
    // Outputs:     None
    // Description: Moves the currently dragged object to the current world
    //              position, following the user's input each frame.
    // -------------------------------------------------------------------------
    void Drag()
    {
        // Snap the dragged object's position to the current world input position
        _lastDragged.transform.position = new Vector2(_worldPosition.x, _worldPosition.y);
    }

    // -------------------------------------------------------------------------
    // Function:    InitDrag
    // Inputs:      None
    // Outputs:     None
    // Description: Begins a drag operation by setting the active drag flag.
    // -------------------------------------------------------------------------
    void InitDrag()
    {
        _isDragActive = true;
    }

    // -------------------------------------------------------------------------
    // Function:    Drop
    // Inputs:      None
    // Outputs:     None
    // Description: Ends the current drag operation by clearing the active flag.
    //              The dragged object stays at its last position.
    // -------------------------------------------------------------------------
    void Drop()
    {
        _isDragActive = false;
    }
}
