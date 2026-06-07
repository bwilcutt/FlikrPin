// =============================================================================
// File:        ARPlacement.cs
// Author:      Bryan Wilcutt
// Date Started: (original)
// Description: Handles AR object placement via raycast against detected planes.
//              Shows a placement indicator at the raycast hit point and spawns
//              the target prefab on first touch. Only one object is spawned;
//              the indicator is hidden once the object exists.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARPlacement : MonoBehaviour
{
    public GameObject      arObjectToSpawn;
    public GameObject      placementIndicator;

    private GameObject     spawnedObject;
    private Pose           placementPose;
    private ARRaycastManager aRRaycastManager;
    private bool           placementPoseIsValid = false;

    // -------------------------------------------------------------------------
    // Function:    Start
    // Inputs:      None
    // Outputs:     None
    // Description: Finds the ARRaycastManager in the scene.
    // -------------------------------------------------------------------------
    void Start()
    {
        aRRaycastManager = FindObjectOfType<ARRaycastManager>();
    }

    // -------------------------------------------------------------------------
    // Function:    Update
    // Inputs:      None
    // Outputs:     None
    // Description: Each frame updates the placement pose and indicator, and
    //              spawns the object on first touch if no object exists yet.
    // -------------------------------------------------------------------------
    void Update()
    {
        UpdatePlacementPose();
        UpdatePlacementIndicator();

        if (spawnedObject == null &&
            placementPoseIsValid &&
            Input.touchCount > 0 &&
            Input.GetTouch(0).phase == TouchPhase.Began)
        {
            ARPlaceObject();
        }
    }

    // -------------------------------------------------------------------------
    // Function:    UpdatePlacementIndicator
    // Inputs:      None
    // Outputs:     None
    // Description: Shows and positions the placement indicator when the pose is
    //              valid and no object has been spawned yet. Hides it otherwise.
    // -------------------------------------------------------------------------
    void UpdatePlacementIndicator()
    {
        if (spawnedObject == null && placementPoseIsValid)
        {
            placementIndicator.SetActive(true);
            placementIndicator.transform.SetPositionAndRotation(placementPose.position, placementPose.rotation);
        }
        else
        {
            placementIndicator.SetActive(false);
        }
    }

    // -------------------------------------------------------------------------
    // Function:    UpdatePlacementPose
    // Inputs:      None
    // Outputs:     None
    // Description: Casts a ray from screen center against AR planes. Updates
    //              placementPose and placementPoseIsValid from the first hit.
    // -------------------------------------------------------------------------
    void UpdatePlacementPose()
    {
        var screenCenter = Camera.main.ViewportToScreenPoint(new Vector3(0.5f, 0.5f));
        var hits         = new List<ARRaycastHit>();
        aRRaycastManager.Raycast(screenCenter, hits, TrackableType.Planes);

        placementPoseIsValid = hits.Count > 0;
        if (placementPoseIsValid)
            placementPose = hits[0].pose;
    }

    // -------------------------------------------------------------------------
    // Function:    ARPlaceObject
    // Inputs:      None
    // Outputs:     None
    // Description: Instantiates arObjectToSpawn at the current placement pose.
    // -------------------------------------------------------------------------
    void ARPlaceObject()
    {
        spawnedObject = Instantiate(arObjectToSpawn, placementPose.position, placementPose.rotation);
    }
}
