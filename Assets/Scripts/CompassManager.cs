// =============================================================================
// File:        CompassManager.cs
// Author:      Bryan Wilcutt
// Date Started: 06/06/2026
// Description: Singleton that owns the native Android CompassPlugin instance.
//              Both CompassBar and PlacePrefabInWorld read heading through
//              this class so there is exactly one plugin instance and one
//              heading value in the app at all times.
// =============================================================================

using System;
using UnityEngine;

public class CompassManager : MonoBehaviour
{
    // ── singleton ──────────────────────────────────────────────────────────
    // Static reference accessible from any script via CompassManager.Instance
    public static CompassManager Instance { get; private set; }

    // ── private fields ─────────────────────────────────────────────────────
#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaClass pluginClass;   // JNI reference to CompassPlugin.java
#endif

    private float _heading       = 0f;     // Last heading value returned by the plugin
    private bool  _pluginRunning = false;  // True once the plugin started successfully

    // ── public API ─────────────────────────────────────────────────────────
    /// <summary>
    /// Current compass heading in degrees. 0 = North, 90 = East, clockwise.
    /// Returns 0 in the Unity Editor.
    /// </summary>
    public float Heading => _heading;

    // -------------------------------------------------------------------------
    // Function:    Awake
    // Inputs:      None
    // Outputs:     None
    // Description: Enforces singleton pattern — destroys any duplicate instance.
    //              Marks this GameObject to persist across scene loads, then
    //              starts the native CompassPlugin.
    // -------------------------------------------------------------------------
    void Awake()
    {
        // If another instance already exists, destroy this duplicate and bail
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Register this as the singleton instance
        Instance = this;

        // Keep this GameObject alive when loading new scenes
        DontDestroyOnLoad(gameObject);

        // Start the native sensor plugin
        StartPlugin();
    }

    // -------------------------------------------------------------------------
    // Function:    StartPlugin
    // Inputs:      None
    // Outputs:     None
    // Description: Initialises the native CompassPlugin via JNI on Android.
    //              Passes the current Unity Activity as context so the plugin
    //              can register its SensorManager listeners. No-op in editor.
    // -------------------------------------------------------------------------
    void StartPlugin()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            // Get the current Android Activity from Unity's player class
            AndroidJavaClass  unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            // Load the plugin class and call its static Start() method with the Activity
            pluginClass = new AndroidJavaClass("com.gametag.compass.CompassPlugin");
            pluginClass.CallStatic("Start", activity);

            // Flag that the plugin is running so Update() will poll it
            _pluginRunning = true;
        }
        catch (Exception e)
        {
            // Plugin failed to load — heading will remain 0
            Debug.LogWarning("CompassManager: CompassPlugin init failed — " + e.Message);
        }
#endif
    }

    // -------------------------------------------------------------------------
    // Function:    Update
    // Inputs:      None
    // Outputs:     None
    // Description: Polls CompassPlugin.GetHeading() every frame and caches the
    //              result in _heading so CompassBar and PlacePrefabInWorld can
    //              read it without making their own JNI calls.
    // -------------------------------------------------------------------------
    void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Don't poll if the plugin never started successfully
        if (!_pluginRunning || pluginClass == null) return;

        try
        {
            // Call the static GetHeading() method on the Java plugin
            _heading = pluginClass.CallStatic<float>("GetHeading");
        }
        catch (Exception e)
        {
            Debug.LogWarning("CompassManager: GetHeading error — " + e.Message);
        }
#endif
    }

    // -------------------------------------------------------------------------
    // Function:    OnDestroy
    // Inputs:      None
    // Outputs:     None
    // Description: Calls CompassPlugin.Stop() to unregister all Android sensor
    //              listeners when this manager is destroyed, preventing leaks.
    // -------------------------------------------------------------------------
    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_pluginRunning && pluginClass != null)
        {
            try
            {
                // Unregister accelerometer, magnetometer, and gyroscope listeners
                pluginClass.CallStatic("Stop");
            }
            catch (Exception e)
            {
                Debug.LogWarning("CompassManager: Stop error — " + e.Message);
            }
        }
#endif
    }
}
