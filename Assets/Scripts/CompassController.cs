using UnityEngine;

public class CompassController : MonoBehaviour
{
    [Tooltip("Drag CompassNeedle here.")]
    public RectTransform needleImage;

    [Range(1f, 20f)]
    public float smoothSpeed = 8f;

    [Header("Editor Test Mode")]
    public bool editorTestSpin = false;

    private float currentAngle = 0f;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaClass plugin;
#endif

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        plugin = new AndroidJavaClass("com.gametag.compass.CompassPlugin");
        plugin.CallStatic("Start", activity);
        Debug.Log("CompassPlugin started.");
#endif
    }

    void Update()
    {
        if (needleImage == null) return;

#if UNITY_EDITOR
        if (editorTestSpin)
        {
            currentAngle += 45f * Time.deltaTime;
            needleImage.localEulerAngles = new Vector3(0f, 0f, currentAngle);
            return;
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        if (plugin != null)
        {
            float heading = plugin.CallStatic<float>("GetHeading");
            float targetAngle = -heading;
            currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, smoothSpeed * Time.deltaTime);
            needleImage.localEulerAngles = new Vector3(0f, 0f, currentAngle);
            Debug.Log($"[NativeCompass] heading={heading:F1} angle={currentAngle:F1}");
        }
#endif
    }

    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (plugin != null)
            plugin.CallStatic("Stop");
#endif
    }
}
