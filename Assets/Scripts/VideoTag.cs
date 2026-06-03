using UnityEngine;
using UnityEngine.Video;

public class VideoTag : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public float maxDistance = 50f;

    private Transform mainCamera;
    private bool isInitialized = false;

    void Start()
    {
        mainCamera = Camera.main.transform;

        if (videoPlayer != null)
        {
            videoPlayer.isLooping = true;
            videoPlayer.Prepare();
            videoPlayer.prepareCompleted += OnVideoPrepared;
        }
    }

void OnVideoPrepared(VideoPlayer vp)
{
    isInitialized = true;

    // Resize the quad to match the video's actual aspect ratio
    int videoWidth  = vp.texture.width;
    int videoHeight = vp.texture.height;

    if (videoWidth > 0 && videoHeight > 0)
    {
        float ratio = (float)videoHeight / (float)videoWidth;
        float x = 1f; // base width — matches your image quad convention
        float y = x * ratio;

        // Apply to the video quad child, not the root (root handles distance scaling)
        Transform videoQuad = transform.Find("video");
        if (videoQuad != null)
        {
            videoQuad.localScale    = new Vector3(x, y, 1f);
            videoQuad.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        else
        {
            transform.localScale    = new Vector3(x, y, 1f); // fallback
            transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
    }

    // Show first frame immediately
    vp.Play();
    vp.Pause();
}

    void Update()
    {
        if (mainCamera == null || videoPlayer == null || !isInitialized) return;

        // Billboard — always face camera
        transform.LookAt(transform.position + mainCamera.rotation * Vector3.forward,
                         mainCamera.rotation * Vector3.up);

        // Distance scaling (logarithmic)
        float distance = Vector3.Distance(transform.position, mainCamera.position);
        float t = Mathf.Clamp01(1f - (distance / maxDistance));
        float scale = Mathf.Lerp(0.05f, 1f, Mathf.Log(1f + t * (2.718f - 1f)));
        transform.localScale = new Vector3(scale, scale, scale);

        // Play/pause based on distance
        if (distance <= maxDistance)
        {
            if (!videoPlayer.isPlaying)
                videoPlayer.Play();
        }
        else
        {
            if (videoPlayer.isPlaying)
                videoPlayer.Pause();
        }
    }

    public void SetVideoURL(string url)
    {
        if (videoPlayer == null) return;
        videoPlayer.url = url;
        videoPlayer.Prepare();
    }
}
