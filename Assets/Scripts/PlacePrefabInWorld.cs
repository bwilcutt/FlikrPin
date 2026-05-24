using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using TMPro;
using System.Globalization;

public class PlacePrefabInWorld : MonoBehaviour
{
    public JSONReader j;
    public GameObject postPicturePrefab;
    public GameObject postVideoPrefab;
    public GameObject postTextPrefab;
    public GameObject postStickerPrefab;
    public GPS gps;
    public ShowSticker ss;

    // Native compass plugin
    private AndroidJavaObject compassPlugin;

    double Radians(double degrees)
    {
        return (degrees * (Math.PI)) / 180;
    }

    double toDegrees(double radian)
    {
        return (radian * 180) / Math.PI;
    }

    double DistanceFromGPS(double LatObject, double LongObject, double LatPlayer, double LongPlayer)
    {
        return Math.Acos((Math.Sin(Radians(LatObject)) * Math.Sin(Radians(LatPlayer))) +
               (Math.Cos(Radians(LatObject)) * Math.Cos(Radians(LatPlayer))) *
               (Math.Cos(Radians(LongPlayer) - Radians(LongObject)))) * 6366707.0195;
    }

    double angleFromCoordinate(double lat1, double long1, double lat2, double long2)
    {
        long1 = Radians(long1);
        long2 = Radians(long2);
        lat1  = Radians(lat1);
        lat2  = Radians(lat2);

        double dLon = (long2 - long1);
        double y    = Math.Sin(dLon) * Math.Cos(lat2);
        double x    = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);

        double brng = Math.Atan2(y, x);
        brng = toDegrees(brng);
        brng = (brng + 360) % 360;
        brng = 360 - brng;

        return brng;
    }

    float GetNativeHeading()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (compassPlugin != null)
                return compassPlugin.CallStatic<float>("GetHeading");
        }
        catch (Exception e)
        {
            Debug.LogWarning("CompassPlugin heading error: " + e.Message);
        }
#endif
        return 0f;
    }

    Vector3 getPostPosition(double LatObject, double LongObject, double PlayerLat, double PlayerLong)
    {
        double distance = DistanceFromGPS(LatObject, LongObject, PlayerLat, PlayerLong);
        double angle    = angleFromCoordinate(PlayerLat, PlayerLong, LatObject, LongObject);

        float  compassHeading = GetNativeHeading();
        double theta = (angle - compassHeading) + 90;
        float  x = (float)(distance * Math.Cos(theta));
        float  y = (float)(distance * Math.Sin(theta));

        return new Vector3(x, 0, y);
    }

    // Helper: positions the timestamp TMP directly below a media transform, centered.
    void AlignTimestampBelowMedia(Transform timestampTransform, Transform mediaTransform, string timestampText = null)
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
        float padding = 0.05f;
        timestampTransform.localPosition = new Vector3(0f, -mediaHalfHeight - padding, 0f);
    }

    void placeObjectInWorld()
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

            Vector3    position = getPostPosition(_latitude, _longitude, gps.latitude, gps.longitude);
            GameObject instance = Instantiate(prefabToUse, position, Quaternion.identity);

            // Configure PostTag with identity + data needed for edit/delete
            PostTag postTag = instance.GetComponent<PostTag>();
            if (postTag != null)
            {
                postTag.postId       = post._id;
                postTag.ownerId      = post.user;
                postTag.mediaType    = post.media_type;
                postTag.mediaUrl     = post.url;
                postTag.previewUrl   = post.preview_url;
                postTag.mediaSource  = post.media_source;
                postTag.message      = post.message;

                if (post.media_type == "sticker" && !string.IsNullOrEmpty(post.preview_url))
                    int.TryParse(post.preview_url, out postTag.stickerIndex);
            }

            Transform timestampTransform = instance.transform.Find("bubble/timestamp");

            // Configure visuals based on post type
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

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass pluginClass = new AndroidJavaClass("com.gametag.compass.CompassPlugin");
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity   = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            pluginClass.CallStatic("Start", activity);
            compassPlugin = pluginClass;
            Debug.Log("CompassPlugin started in PlacePrefabInWorld");
        }
        catch (Exception e)
        {
            Debug.LogWarning("CompassPlugin init failed: " + e.Message);
        }
#endif
        Input.location.Start();
        Debug.Log("starting to place posts");
        Invoke("placeObjectInWorld", 3);
    }

    void Update() { }
}
