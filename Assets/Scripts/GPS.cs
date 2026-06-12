// =============================================================================
// File:        GPS.cs
// Author:      Bryan Wilcutt
// Date Started: 05/25/2025
// Description: Singleton GPS manager. Continuously polls device location,
//              gates updates by horizontal accuracy threshold, and smooths
//              accepted readings through independent 1-D Kalman filters on
//              latitude and longitude.
//
//              Other scripts read GPS.Instance.Latitude / .Longitude instead
//              of the old public fields so they always get the latest filtered
//              value.
//
//              FusedLocationManager (native Android plugin) feeds higher-
//              quality fixes into this class when available; Unity's built-in
//              Input.location is used as the fallback on all other platforms.
// =============================================================================

using System.Collections;
using UnityEngine;

public class GPS : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static GPS Instance { get; private set; }

    // ── Inspector tunables ────────────────────────────────────────────────────
    [Header("Accuracy Gate")]
    [Tooltip("Fixes with horizontalAccuracy worse than this (metres) are discarded.")]
    public float maxAcceptableAccuracyMetres = 15f;

    [Tooltip("Desired accuracy hint passed to Input.location.Start() (metres).")]
    public float desiredAccuracyMetres = 1f;

    [Tooltip("Minimum distance change before Input.location issues a new fix (metres).")]
    public float updateDistanceMetres = 0.5f;

    [Header("Kalman Filter")]
    [Tooltip("Initial process-noise covariance. Lower = trust sensor more; higher = smooth more aggressively.")]
    public float kalmanQ = 0.0001f;

    [Tooltip("Initial measurement-noise covariance. Tune to match typical GPS noise.")]
    public float kalmanR = 0.01f;

    // ── Public read-only state ────────────────────────────────────────────────
    /// <summary>Current filtered latitude in decimal degrees.</summary>
    public float Latitude  { get; private set; }

    /// <summary>Current filtered longitude in decimal degrees.</summary>
    public float Longitude { get; private set; }

    /// <summary>Last accepted altitude in metres.</summary>
    public float Altitude  { get; private set; }

    /// <summary>Horizontal accuracy of the last accepted raw fix (metres).</summary>
    public float HorizontalAccuracy { get; private set; }

    /// <summary>True once at least one valid fix has been accepted.</summary>
    public bool  HasFix    { get; private set; }

    // Legacy public fields — kept so existing scripts that read gps.latitude /
    // gps.longitude directly continue to compile without changes.
    public float latitude  => Latitude;
    public float longitude => Longitude;
    public float altitude  => Altitude;

    // ── Kalman state (one filter per axis) ────────────────────────────────────
    private KalmanFilter1D _kalmanLat;
    private KalmanFilter1D _kalmanLon;

    // ── Private ───────────────────────────────────────────────────────────────
    private bool _locationStarted = false;

    // =========================================================================
    // Function:    Awake
    // Inputs:      None
    // Outputs:     None
    // Description: Singleton enforcement and Kalman filter initialisation.
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

        _kalmanLat = new KalmanFilter1D(kalmanQ, kalmanR);
        _kalmanLon = new KalmanFilter1D(kalmanQ, kalmanR);
    }

    // =========================================================================
    // Function:    Start
    // Inputs:      None
    // Outputs:     None
    // Description: Kicks off the location service coroutine.
    // =========================================================================
    void Start()
    {
        StartCoroutine(StartLocationService());
    }

    // =========================================================================
    // Function:    StartLocationService
    // Inputs:      None
    // Outputs:     IEnumerator (coroutine)
    // Description: Initialises Unity's built-in location service with the
    //              configured accuracy and distance parameters, then waits up
    //              to 20 seconds for the first fix. On success, transitions
    //              to continuous polling via PollLocation().
    // =========================================================================
    private IEnumerator StartLocationService()
    {
        if (!Input.location.isEnabledByUser)
        {
            Debug.LogWarning("GPS: Location services are disabled by the user.");
            yield break;
        }

        Input.location.Start(desiredAccuracyMetres, updateDistanceMetres);
        Debug.Log("GPS: Location service starting…");

        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1f);
            maxWait--;
        }

        if (maxWait <= 0)
        {
            Debug.LogWarning("GPS: Timed out waiting for location service.");
            yield break;
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.LogWarning("GPS: Location service failed to start.");
            yield break;
        }

        _locationStarted = true;
        Debug.Log("GPS: Location service running — beginning continuous poll.");
        StartCoroutine(PollLocation());
    }

    // =========================================================================
    // Function:    PollLocation
    // Inputs:      None
    // Outputs:     IEnumerator (coroutine)
    // Description: Polls Input.location every second. Each fix is passed
    //              through the accuracy gate; fixes that pass are fed into
    //              the Kalman filters and the public Latitude/Longitude
    //              properties are updated.
    //
    //              If FusedLocationManager is present its data is preferred
    //              over Input.location because it incorporates WiFi/cell tower
    //              fusion from the Android FusedLocationProviderClient.
    // =========================================================================
    private IEnumerator PollLocation()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            float rawLat, rawLon, rawAlt, rawAcc;

#if UNITY_ANDROID && !UNITY_EDITOR
            // Prefer fused provider when available
            if (FusedLocationManager.Instance != null && FusedLocationManager.Instance.HasFix)
            {
                rawLat = FusedLocationManager.Instance.RawLatitude;
                rawLon = FusedLocationManager.Instance.RawLongitude;
                rawAlt = FusedLocationManager.Instance.RawAltitude;
                rawAcc = FusedLocationManager.Instance.RawAccuracy;
            }
            else
            {
                rawLat = Input.location.lastData.latitude;
                rawLon = Input.location.lastData.longitude;
                rawAlt = Input.location.lastData.altitude;
                rawAcc = Input.location.lastData.horizontalAccuracy;
            }
#else
            rawLat = Input.location.lastData.latitude;
            rawLon = Input.location.lastData.longitude;
            rawAlt = Input.location.lastData.altitude;
            rawAcc = Input.location.lastData.horizontalAccuracy;
#endif

            // ── Accuracy gate ─────────────────────────────────────────────
            if (rawAcc > maxAcceptableAccuracyMetres)
            {
                Debug.Log($"GPS: Fix rejected — accuracy {rawAcc:F1}m > threshold {maxAcceptableAccuracyMetres}m");
                continue;
            }

            // ── Kalman filter ─────────────────────────────────────────────
            // Seed the filters with the first accepted reading so they don't
            // have to converge from zero.
            if (!HasFix)
            {
                _kalmanLat.Seed(rawLat);
                _kalmanLon.Seed(rawLon);
                HasFix = true;
            }

            Latitude            = _kalmanLat.Update(rawLat);
            Longitude           = _kalmanLon.Update(rawLon);
            Altitude            = rawAlt;
            HorizontalAccuracy  = rawAcc;

            Debug.Log($"GPS: Fix accepted — raw ({rawLat:F6}, {rawLon:F6}) " +
                      $"filtered ({Latitude:F6}, {Longitude:F6}) acc={rawAcc:F1}m");
        }
    }

    // =========================================================================
    // Function:    OnDestroy
    // Inputs:      None
    // Outputs:     None
    // Description: Stops the Unity location service when this object is
    //              destroyed to prevent resource leaks.
    // =========================================================================
    void OnDestroy()
    {
        if (_locationStarted)
            Input.location.Stop();
    }
}

// =============================================================================
// Class:       KalmanFilter1D
// Description: Minimal scalar (1-D) Kalman filter for smoothing a single
//              noisy signal such as a GPS latitude or longitude stream.
//
//              State:   x  — current best estimate
//              P:       p  — estimate error covariance
//              Q:       process noise covariance (how much the true value drifts)
//              R:       measurement noise covariance (how noisy the sensor is)
//
//              Each call to Update() performs one predict + correct cycle and
//              returns the new smoothed estimate.
// =============================================================================
public class KalmanFilter1D
{
    // ── Filter parameters ─────────────────────────────────────────────────────
    private readonly float _q;   // Process noise
    private readonly float _r;   // Measurement noise

    // ── Filter state ──────────────────────────────────────────────────────────
    private float _x;            // Current state estimate
    private float _p;            // Current error covariance
    private bool  _seeded;       // True once Seed() has been called

    // =========================================================================
    // Function:    KalmanFilter1D (constructor)
    // Inputs:      q — process noise covariance
    //              r — measurement noise covariance
    // Outputs:     KalmanFilter1D instance
    // Description: Initialises filter parameters. Call Seed() with the first
    //              real measurement before calling Update().
    // =========================================================================
    public KalmanFilter1D(float q, float r)
    {
        _q = q;
        _r = r;
        _p = 1f;
    }

    // =========================================================================
    // Function:    Seed
    // Inputs:      initialValue — the first trusted measurement
    // Outputs:     None
    // Description: Sets the initial state estimate to a known value so the
    //              filter does not have to converge from zero. Call once before
    //              the first Update().
    // =========================================================================
    public void Seed(float initialValue)
    {
        _x      = initialValue;
        _p      = 1f;
        _seeded = true;
    }

    // =========================================================================
    // Function:    Update
    // Inputs:      measurement — new raw sensor reading
    // Outputs:     float — smoothed state estimate
    // Description: Runs one Kalman predict + correct step:
    //                Predict:  p = p + q
    //                Gain:     k = p / (p + r)
    //                Correct:  x = x + k * (measurement - x)
    //                          p = (1 - k) * p
    //              Returns the corrected state estimate.
    // =========================================================================
    public float Update(float measurement)
    {
        if (!_seeded) Seed(measurement);

        // Predict — propagate uncertainty forward
        _p += _q;

        // Kalman gain — how much to trust this measurement vs current estimate
        float k = _p / (_p + _r);

        // Correct — blend estimate with measurement weighted by gain
        _x += k * (measurement - _x);
        _p  = (1f - k) * _p;

        return _x;
    }
}
