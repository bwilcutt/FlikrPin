// =============================================================================
// File:        PlacePrefabInWorld.cs
// Author:      Bryan Wilcutt
// Date Started: (original)
// Description: Reads post data from JSONReader and instantiates the correct
//              prefab (picture, video, text, sticker) at the GPS-derived world
//              position for each post. Adds a BoxCollider at runtime so that
//              TagSelectionManager raycasts can hit spawned tags.
//
//              Compass heading is sourced from CompassManager (native Android
//              plugin). Tag world position is computed with correct radian
//              conversion and standard bearing-to-Unity-XZ mapping.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using TMPro;
using System.Globalization;

public class PlacePrefabInWorld : MonoBehaviour
{
    public JSONReader  j;
    public GameObject  postPicturePrefab;
    public GameObject  postVideoPrefab;
    public GameObject  postTextPrefab;
    public GameObject  postStickerPrefab;
    public GPS         gps;
    public ShowSticker ss;

    // -------------------------------------------------------------------------
    // Function:    Radians
    // Inputs:      degrees — angle in degrees
    // Outputs:     double  — angle in radians
    // Description: Converts degrees to radians.
    // -------------------------------------------------------------------------
    double Radians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    // -------------------------------------------------------------------------
    // Function:    DistanceFromGPS
    // Inputs:      LatObject, LongObject — target GPS coords (decimal degrees)
    //              LatPlayer, LongPlayer — player GPS coords (decimal degrees)
    // Outputs:     double — great-circle distance in metres
    // Description: Haversine great-circle distance between two GPS coordinates.
    // -------------------------------------------------------------------------
    double DistanceFromGPS(double LatObject, double LongObject,
                           double LatPlayer, double LongPlayer)
    {
        return Math.Acos(
                   (Math.Sin(Radians(LatObject))  * Math.Sin(Radians(LatPlayer))) +
                   (Math.Cos(Radians(LatObject))  * Math.Cos(Radians(LatPlayer))) *
                   (Math.Cos(Radians(LongPlayer)  - Radians(LongObject)))
               ) * 6_366_707.0195;
    }

    // -------------------------------------------------------------------------
    // Function:    BearingToTarget
    // Inputs:      lat1, lon1 — observer GPS coords (decimal degrees)
    //              lat2, lon2 — target GPS coords (decimal degrees)
    // Outputs:     double — clockwise bearing from north in degrees [0, 360)
    // Description: Computes the forward bearing from observer to target using
    //              the standard spherical-earth formula.
    //              0° = North, 90° = East, 180° = South, 270° = West.
    // -------------------------------------------------------------------------
    double BearingToTarget(double lat1, double lon1, double lat2, double lon2)
    {
        double lat1r = Radians(lat1);
        double lat2r = Radians(lat2);
        double dLon  = Radians(lon2 - lon1);

        double y    = Math.Sin(dLon) * Math.Cos(lat2r);
        double x    = Math.Cos(lat1r) * Math.Sin(lat2r)
                    - Math.Sin(lat1r) * Math.Cos(lat2r) * Math.Cos(dLon);

        double bearing = Math.Atan2(y, x);           // radians, -π … +π
        bearing = bearing * 180.0 / Math.PI;          // to degrees
        bearing = (bearing + 360.0) % 360.0;          // normalise to [0, 360)
        return bearing;
    }

    // -------------------------------------------------------------------------
    // Function:    GetCompassHeading
    // Inputs:      None
    // Outputs:     float — compass heading in degrees (0 = North, clockwise)
    // Description: Returns the current device heading from CompassManager.
    //              Returns 0 in the Unity Editor.
    // -------------------------------------------------------------------------
    float GetCompassHeading()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (CompassManager.Instance != null)
            return CompassManager.Instance.Heading;
#endif
        return 0f;
    }

    // -------------------------------------------------------------------------
    // Function:    GetPostPosition
    // Inputs:      LatObject, LonObject — tag GPS coords (decimal degrees)
    //              PlayerLat, PlayerLon — player GPS coords (decimal degrees)
    // Outputs:     Vector3 — Unity world-space XZ position for the tag
    //                        (Y is always 0; caller may adjust for height)
    // Description: Converts GPS bearing + distance into a Unity world position
    //              relative to the origin (player).
    //
    //              Maths:
    //                bearing   = clockwise angle from north to the tag
    //                relBearing = bearing adjusted so "forward" in Unity (+Z)
    //                             aligns with the device's current heading
    //                X = distance * sin(relBearing)   → East/West
    //                Z = distance * cos(relBearing)   → North/South (Unity forward)
    // -------------------------------------------------------------------------
    Vector3 GetPostPosition(double LatObject, double LonObject,
                            double PlayerLat, double PlayerLon)
    {
        double distance     = DistanceFromGPS(LatObject, LonObject, PlayerLat, PlayerLon);
        double bearing      = BearingToTarget(PlayerLat, PlayerLon, LatObject, LonObject);
        float  compassHeading = GetCompassHeading();

        // Relative bearing: how many degrees off straight-ahead (device heading) is the tag?
        double relBearing   = Radians(bearing - compassHeading);

        float x = (float)(distance * Math.Sin(relBearing));   // East (+) / West (-)
        float z = (float)(distance * Math.Cos(relBearing));   // Forward (+) / Back (-)

        return new Vector3(x, 0f, z);
    }

    // -------------------------------------------------------------------------
    // Function:    AlignTimestampBelowMedia
    // Inputs:      timestampTransform — the timestamp child transform
    //              mediaTransform     — the media child transform to align below
    //              timestampText      — optional text to set on the TMP component
    // Outputs:     None
    // Description: Positions the timestamp TMP directly below a media object.
    // -------------------------------------------------------------------------
    void AlignTimestampBelowMedia(Transform timestampTransform,
                                  Transform mediaTransform,
                                  string    timestampText = null)
    {
        if (timestampTransform == null) return;

        TextMeshPro tmp = timestampTransform.GetComponent<TextMeshPro>();
        if (tmp != null)
        {
            if (timestampText != null)
                tmp.text = timestampText;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        float mediaHalfHeight = (mediaTransform != null) ? mediaTransform.localScale.y / 2f : 0.3f;
        float padding         = 0.05f;
        timestampTransform.localPosition = new Vector3(0f, -mediaHalfHeight - padding, 0f);
    }

    // -------------------------------------------------------------------------
    // Function:    PlaceObjectInWorld
    // Inputs:      None
    // Outputs:     None
    // Description: Iterates all posts from JSONReader, selects the correct
    //              prefab, instantiates it at the GPS-derived world position,
    //              adds a BoxCollider for tap detection, and configures PostTag
    //              data and visual components.
    // -------------------------------------------------------------------------
    void PlaceObjectInWorld()
    {
        var _posts = j.myPostList.posts;
        foreach (var post in _posts)
        {
            Debug.Log("post: " + post.url);

            GameObject prefabToUse = null;

            if      (post.media_type == "image")   prefabToUse = postPicturePrefab;
            else if (post.media_type == "video")   prefabToUse = postVideoPrefab;
            else if (post.media_type == "text")    prefabToUse = postTextPrefab;
            else if (post.media_type == "sticker") prefabToUse = postStickerPrefab;

            if (prefabToUse == null)
            {
                Debug.LogWarning("Unknown media type: " + post.media_type);
                continue;
            }

            float _latitude  = float.Parse(post.latitude,  CultureInfo.InvariantCulture.NumberFormat);
            float _longitude = float.Parse(post.longitude, CultureInfo.InvariantCulture.NumberFormat);

            Vector3    position = GetPostPosition(_latitude, _longitude, gps.latitude, gps.longitude);
            GameObject instance = Instantiate(prefabToUse, position, Quaternion.identity);

            // Add BoxCollider at runtime so TagSelectionManager raycasts can hit this tag.
            BoxCollider col = instance.AddComponent<BoxCollider>();
            col.size = new Vector3(0.5f, 0.5f, 0.1f);

            // Configure PostTag with identity and data needed for edit/delete.
            PostTag postTag = instance.GetComponent<PostTag>();
            if (postTag != null)
            {
                postTag.postId      = post._id;
                postTag.ownerId     = post.user;
                postTag.mediaType   = post.media_type;
                postTag.mediaUrl    = post.url;
                postTag.previewUrl  = post.preview_url;
                postTag.mediaSource = post.media_source;
                postTag.message     = post.message;

                if (post.media_type == "sticker" && !string.IsNullOrEmpty(post.preview_url))
                    int.TryParse(post.preview_url, out postTag.stickerIndex);
            }

            Transform timestampTransform = instance.transform.Find("bubble/timestamp");

            // Configure visuals based on post type.
            if (post.media_type == "image")
            {
                Transform imageTransform = instance.transform.Find("image");
                if (imageTransform != null)
                {
                    var loadPost = imageTransform.GetComponent<LoadTextureFromURL>();
                    if (loadPost != null)
                    {
                        loadPost.TextureURL = post.preview_url != "" ? post.preview_url : post.url;
                        loadPost.source     = post.media_source;
                    }
                }
                AlignTimestampBelowMedia(timestampTransform, imageTransform, "");
            }
            else if (post.media_type == "video")
            {
                Transform videoTransform = instance.transform.Find("video");
                if (videoTransform != null)
                {
                    var loadPost = videoTransform.GetComponent<LoadTextureFromURL>();
                    if (loadPost != null)
                    {
                        loadPost.TextureURL = post.preview_url != "" ? post.preview_url : post.url;
                        loadPost.source     = post.media_source;
                    }
                }
                AlignTimestampBelowMedia(timestampTransform, videoTransform, "");
            }
            else if (post.media_type == "text")
            {
                Transform contentTransform = instance.transform.Find("bubble/content");
                if (contentTransform != null)
                {
                    TextMeshPro tmp = contentTransform.GetComponent<TextMeshPro>();
                    if (tmp != null) tmp.text = post.message;
                }
                AlignTimestampBelowMedia(timestampTransform, contentTransform, "");
            }
            else if (post.media_type == "sticker")
            {
                Transform stickerTransform = instance.transform.Find("sticker");
                if (stickerTransform != null)
                {
                    int i = Int32.Parse(post.preview_url);
                    Texture stickerTexture = (Texture)ss.stickers[i];
                    stickerTransform.GetComponent<Renderer>().material.mainTexture = stickerTexture;
                }
                AlignTimestampBelowMedia(timestampTransform, stickerTransform, "");
            }

            Debug.Log("Placed post of type: " + post.media_type + " at: " + position);
        }
    }

    // -------------------------------------------------------------------------
    // Function:    Start
    // Inputs:      None
    // Outputs:     None
    // Description: Starts GPS location services and schedules PlaceObjectInWorld
    //              after a 3-second delay to allow GPS to warm up.
    //              CompassPlugin is now started by CompassManager (not here).
    // -------------------------------------------------------------------------
    void Start()
    {
        Input.location.Start();
        Debug.Log("PlacePrefabInWorld: starting to place posts");
        Invoke("PlaceObjectInWorld", 3);
    }

    void Update() { }
}
