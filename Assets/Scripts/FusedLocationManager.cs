// =============================================================================
// File:        FusedLocationManager.cs
// Author:      Bryan Wilcutt
// Date Started: 06/11/2026
// Description: Singleton Unity bridge to the native Android
//              FusedLocationPlugin.java. Polls the plugin every second and
//              exposes raw fix data (latitude, longitude, altitude, accuracy)
//              to GPS.cs, which applies accuracy gating and Kalman filtering
//              on top.
//
//              On non-Android platforms (Editor, iOS) this manager does
//              nothing — GPS.cs falls back to Input.location automatically.
//
//              Follows the same pattern as CompassManager.cs.
// =============================================================================

using System;
using UnityEngine;

public class FusedLocationManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static FusedLocationManager Instance { get; private set; }

    // ── Inspector tunables ────────────────────────────────────────────────────
    [Header("Update Rate")]
    [Tooltip("How often (ms) to request location updates from the fused provider.")]
    public long updateIntervalMs = 1000L;

    [Tooltip("Fastest update interval (ms) — fixes delivered faster if available.")]
    public long fastestIntervalMs = 500L;

    // ── Public read-only fix data ─────────────────────────────────────────────
    /// <summary>True once the plugin has delivered at least one valid fix.</summary>
    public bool  HasFix        { get; private set; }

    /// <summary>Raw (unfiltered) latitude from the fused provider.</summary>
    public float RawLatitude   { get; private set; }

    /// <summary>Raw (unfiltered) longitude from the fused provider.</summary>
    public float RawLongitude  { get; private set; }

    /// <summary>Raw altitude in metres from the fused provider.</summary>
    public float RawAltitude   { get; private set; }

    /// <summary>Horizontal accuracy in metres from the fused provider.</summary>
    public float RawAccuracy   { get; private set; }

    // ── Private ───────────────────────────────────────────────────────────────
#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaClass  _pluginClass;
    private bool              _pluginRunning = false;
#endif

    // =========================================================================
    // Function:    Awake
    // Inputs:      None
    // Outputs:     None
    // Description: Singleton enforcement. Persists across scene loads and
    //              starts the native fused location plugin on Android.
    // =========================================================================
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        StartPlugin();
    }

    // =========================================================================
    // Function:    StartPlugin
    // Inputs:      None
    // Outputs:     None
    // Description: Loads FusedLocationPlugin via JNI and calls its static
    //              Start() method with the Android Activity context and the
    //              configured update intervals. No-op in the Unity Editor.
    // =========================================================================
    private void StartPlugin()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass  unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            _pluginClass = new AndroidJavaClass("com.gametag.location.FusedLocationPlugin");
            _pluginClass.CallStatic("Start", activity, updateIntervalMs, fastestIntervalMs);

            _pluginRunning = true;
            Debug.Log("FusedLocationManager: Plugin started.");
        }
        catch (Exception e)
        {
            Debug.LogWarning("FusedLocationManager: Plugin init failed — " + e.Message +
                             ". Falling back to Input.location.");
        }
#endif
    }

    // =========================================================================
    // Function:    Update
    // Inputs:      None
    // Outputs:     None
    // Description: Polls the native plugin every frame for the latest fix.
    //              Skips polling if the plugin never started. Sets HasFix true
    //              once a non-zero fix is received.
    // =========================================================================
    void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_pluginRunning || _pluginClass == null) return;

        try
        {
            float lat = _pluginClass.CallStatic<float>("GetLatitude");
            float lon = _pluginClass.CallStatic<float>("GetLongitude");
            float alt = _pluginClass.CallStatic<float>("GetAltitude");
            float acc = _pluginClass.CallStatic<float>("GetAccuracy");

            // Ignore default zero values before the first real fix arrives
            if (lat == 0f && lon == 0f) return;

            RawLatitude  = lat;
            RawLongitude = lon;
            RawAltitude  = alt;
            RawAccuracy  = acc;
            HasFix       = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("FusedLocationManager: Poll error — " + e.Message);
        }
#endif
    }

    // =========================================================================
    // Function:    OnDestroy
    // Inputs:      None
    // Outputs:     None
    // Description: Calls FusedLocationPlugin.Stop() to unregister Android
    //              location callbacks and prevent resource leaks.
    // =========================================================================
    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_pluginRunning && _pluginClass != null)
        {
            try
            {
                _pluginClass.CallStatic("Stop");
                Debug.Log("FusedLocationManager: Plugin stopped.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("FusedLocationManager: Stop error — " + e.Message);
            }
        }
#endif
    }
}
