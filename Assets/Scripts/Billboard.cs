// =============================================================================
// File:        Billboard.cs
// Author:      Bryan Wilcutt
// Date Started: (original)
// Description: Keeps this GameObject facing the camera each frame using one
//              of two modes: LookAtCamera (faces the camera position directly)
//              or CameraForward (aligns with the camera's forward direction).
// =============================================================================

using UnityEngine;

public class Billboard : MonoBehaviour
{
    [SerializeField] private BillboardType billboardType;

    public enum BillboardType { LookAtCamera, CameraForward }

    // -------------------------------------------------------------------------
    // Function:    LateUpdate
    // Inputs:      None
    // Outputs:     None
    // Description: Orients this transform toward the camera after all other
    //              updates have run, ensuring correct draw-order behaviour.
    // -------------------------------------------------------------------------
    void LateUpdate()
    {
        switch (billboardType)
        {
            case BillboardType.LookAtCamera:
                transform.LookAt(Camera.main.transform.position, Vector3.up);
                break;

            case BillboardType.CameraForward:
                transform.forward = Camera.main.transform.forward;
                break;
        }
    }
}
