using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch;

[RequireComponent(requiredComponent: typeof(ARRaycastManager),
    requiredComponent2: typeof(ARPlaneManager))]
public class PlaceObjectOnPlane : MonoBehaviour
{
    [Header("Post Type Window")]
    public PostTypeWindow postTypeWindow;

    private ARRaycastManager aRRaycastManager;
    private ARPlaneManager aRPlaneManager;
    private List<ARRaycastHit> hits = new List<ARRaycastHit>();

    private void Awake()
    {
        aRRaycastManager = GetComponent<ARRaycastManager>();
        aRPlaneManager = GetComponent<ARPlaneManager>();
    }

    private void OnEnable()
    {
        EnhancedTouch.TouchSimulation.Enable();
        EnhancedTouch.EnhancedTouchSupport.Enable();
        EnhancedTouch.Touch.onFingerDown += FingerDown;
    }

    private void OnDisable()
    {
        EnhancedTouch.TouchSimulation.Disable();
        EnhancedTouch.EnhancedTouchSupport.Disable();
        EnhancedTouch.Touch.onFingerDown -= FingerDown;
    }

    private void FingerDown(EnhancedTouch.Finger finger)
    {
        if (finger.index != 0) return;

        // Don't open panel if it's already open
        if (postTypeWindow != null && postTypeWindow.gameObject.activeSelf) return;

        Vector3 dropPosition = Vector3.zero;

        if (aRRaycastManager.Raycast(finger.currentTouch.screenPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            dropPosition = hits[0].pose.position;
        }
        else
        {
            // Fallback: 2m in front of camera
            dropPosition = Camera.main.transform.position + Camera.main.transform.forward * 2f;
        }

        if (postTypeWindow != null)
        {
            postTypeWindow.Show(dropPosition);
        }
        else
        {
            Debug.LogWarning("PlaceObjectOnPlane: postTypeWindow is not assigned!");
        }
    }
}
