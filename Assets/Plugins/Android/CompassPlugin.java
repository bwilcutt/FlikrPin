// =============================================================================
// File:        CompassPlugin.java
// Author:      Bryan Wilcutt
// Date Started: (original)
// Description: Native Android compass plugin for FlikrPin. Registers listeners
//              for the accelerometer, magnetometer, and gyroscope via Android
//              SensorManager, bypassing ARCore's interference with Unity's
//              built-in Input.compass API.
//
//              Heading is computed using:
//              1. SensorManager.getRotationMatrix() — tilt-compensated fusion
//                 of accelerometer + magnetometer.
//              2. SensorManager.remapCoordinateSystem() — corrects axis mapping
//                 when phone is held upright in portrait (normal AR usage).
//              3. Gyroscope integration — smooth short-term rotation.
//              4. Complementary filter — blends gyro (smooth) with mag
//                 (absolute north reference) to eliminate drift and jitter.
//
//              Output: GetHeading() returns degrees clockwise from north [0,360).
// =============================================================================

package com.gametag.compass;

import android.content.Context;
import android.hardware.Sensor;
import android.hardware.SensorEvent;
import android.hardware.SensorEventListener;
import android.hardware.SensorManager;

public class CompassPlugin implements SensorEventListener
{
    private static SensorManager sensorManager;
    private static CompassPlugin instance;

    // ── raw sensor data ───────────────────────────────────────────────────
    private float[] gravity     = new float[3];
    private float[] geomagnetic = new float[3];
    private float[] rotation    = new float[9];
    private float[] remapped    = new float[9];   // axis-remapped rotation matrix
    private float[] orientation = new float[3];

    private boolean hasGravity  = false;
    private boolean hasMagnetic = false;

    // ── gyroscope state ───────────────────────────────────────────────────
    private float   gyroHeading     = 0f;
    private float   magHeading      = 0f;
    private boolean gyroInitialized = false;
    private long    lastGyroTimestamp = 0;

    // ── complementary filter coefficient ──────────────────────────────────
    // α close to 1.0 = trust gyro more (smoother, slower to correct drift)
    // α close to 0.0 = trust magnetometer more (faster but noisier)
    // 0.97 is a standard starting value for ~30Hz sensor update rate
    private static final float ALPHA = 0.97f;

    // ── output ────────────────────────────────────────────────────────────
    private static volatile float heading = 0f;

    // =========================================================================
    // Function:    Start
    // Inputs:      context — Android Context (pass Activity from Unity)
    // Outputs:     None
    // Description: Creates the plugin instance and registers sensor listeners
    //              for accelerometer, magnetometer, and gyroscope.
    // =========================================================================
    public static void Start(Context context)
    {
        instance      = new CompassPlugin();
        sensorManager = (SensorManager) context.getSystemService(Context.SENSOR_SERVICE);

        // Accelerometer — provides gravity vector for tilt correction
        sensorManager.registerListener(instance,
            sensorManager.getDefaultSensor(Sensor.TYPE_ACCELEROMETER),
            SensorManager.SENSOR_DELAY_GAME);

        // Magnetometer — provides absolute north reference
        sensorManager.registerListener(instance,
            sensorManager.getDefaultSensor(Sensor.TYPE_MAGNETIC_FIELD),
            SensorManager.SENSOR_DELAY_GAME);

        // Gyroscope — provides smooth short-term rotation
        Sensor gyro = sensorManager.getDefaultSensor(Sensor.TYPE_GYROSCOPE);
        if (gyro != null)
        {
            sensorManager.registerListener(instance, gyro, SensorManager.SENSOR_DELAY_GAME);
        }
    }

    // =========================================================================
    // Function:    Stop
    // Inputs:      None
    // Outputs:     None
    // Description: Unregisters all sensor listeners to release hardware resources.
    // =========================================================================
    public static void Stop()
    {
        if (sensorManager != null && instance != null)
            sensorManager.unregisterListener(instance);
    }

    // =========================================================================
    // Function:    GetHeading
    // Inputs:      None
    // Outputs:     float — current heading in degrees, clockwise from north [0, 360)
    // Description: Returns the latest fused compass heading. Thread-safe via
    //              volatile on the heading field.
    // =========================================================================
    public static float GetHeading()
    {
        return heading;
    }

    // ── sensor events ─────────────────────────────────────────────────────

    // =========================================================================
    // Function:    onSensorChanged
    // Inputs:      event — SensorEvent from Android SensorManager
    // Outputs:     None
    // Description: Dispatches incoming sensor data to the appropriate handler.
    //              Accelerometer and magnetometer both trigger updateMagHeading()
    //              so the rotation matrix is always recomputed with fresh data.
    // =========================================================================
    @Override
    public void onSensorChanged(SensorEvent event)
    {
        switch (event.sensor.getType())
        {
            case Sensor.TYPE_ACCELEROMETER:
                gravity    = event.values.clone();
                hasGravity = true;
                updateMagHeading();
                break;

            case Sensor.TYPE_MAGNETIC_FIELD:
                geomagnetic = event.values.clone();
                hasMagnetic = true;
                updateMagHeading();
                break;

            case Sensor.TYPE_GYROSCOPE:
                updateGyro(event);
                break;
        }
    }

    // =========================================================================
    // Function:    updateMagHeading
    // Inputs:      None (reads gravity[], geomagnetic[] instance fields)
    // Outputs:     None (writes magHeading, gyroHeading, heading)
    // Description: Computes a tilt-compensated magnetic heading using:
    //              1. getRotationMatrix()        — fuses accel + mag
    //              2. remapCoordinateSystem()    — corrects axes for upright
    //                                             portrait phone hold
    //              3. getOrientation()           — extracts azimuth
    //              4. complementaryBlend()       — fuses with gyro heading
    //
    //              Axis remap: AXIS_X, AXIS_Z maps the screen's Y axis to "up",
    //              which is correct when the phone is held upright in portrait.
    //              Without this remap, heading is only accurate when the phone
    //              is held flat (screen facing up).
    // =========================================================================
    private void updateMagHeading()
    {
        if (!hasGravity || !hasMagnetic) return;

        boolean success = SensorManager.getRotationMatrix(rotation, null, gravity, geomagnetic);
        if (!success) return;

        // Remap axes for upright portrait hold.
        // AXIS_X = X stays as X (left/right)
        // AXIS_Z = device Z becomes the new Y (screen normal becomes "up")
        // Without this, heading is only correct when phone is flat.
        boolean remapSuccess = SensorManager.remapCoordinateSystem(
            rotation,
            SensorManager.AXIS_X,
            SensorManager.AXIS_Z,
            remapped);

        if (!remapSuccess) return;

        SensorManager.getOrientation(remapped, orientation);
        float azimuth = (float) Math.toDegrees(orientation[0]);
        magHeading = (azimuth + 360f) % 360f;

        // Snap gyro to mag on first valid reading
        if (!gyroInitialized)
        {
            gyroHeading     = magHeading;
            heading         = magHeading;
            gyroInitialized = true;
            return;
        }

        // Complementary filter: blend gyro (smooth) with mag (absolute)
        float blended = complementaryBlend(gyroHeading, magHeading, ALPHA);
        gyroHeading = blended;
        heading     = blended;
    }

    // =========================================================================
    // Function:    updateGyro
    // Inputs:      event — SensorEvent from TYPE_GYROSCOPE
    // Outputs:     None (writes gyroHeading, heading)
    // Description: Integrates gyroscope yaw rate (values[2]) over time to
    //              produce a smooth heading estimate. Applies complementary
    //              blend with magHeading on every tick to prevent drift.
    //              dt is clamped to avoid jumps after pause/resume.
    // =========================================================================
    private void updateGyro(SensorEvent event)
    {
        if (!gyroInitialized) return;

        if (lastGyroTimestamp == 0)
        {
            lastGyroTimestamp = event.timestamp;
            return;
        }

        // dt in seconds from nanosecond timestamps
        float dt = (event.timestamp - lastGyroTimestamp) * 1e-9f;
        lastGyroTimestamp = event.timestamp;

        // Clamp dt to avoid jumps after resume/pause
        if (dt <= 0f || dt > 0.1f) return;

        // Gyroscope Z axis (around vertical) gives yaw rate in rad/s.
        // Negated because Android rotation around -Z = clockwise = increasing azimuth.
        float yawRate  = -event.values[2];
        float deltaDeg = (float) Math.toDegrees(yawRate * dt);

        gyroHeading = (gyroHeading + deltaDeg + 360f) % 360f;

        // Continuous blend with mag to prevent gyro drift between mag updates
        gyroHeading = complementaryBlend(gyroHeading, magHeading, ALPHA);
        heading     = gyroHeading;
    }

    // =========================================================================
    // Function:    complementaryBlend
    // Inputs:      gyro  — gyroscope-derived heading in degrees
    //              mag   — magnetometer-derived heading in degrees
    //              alpha — blend weight (0.0 = all mag, 1.0 = all gyro)
    // Outputs:     float — blended heading in degrees [0, 360)
    // Description: Blends two headings using a complementary filter with
    //              correct shortest-path wrapping across the 0/360 boundary.
    // =========================================================================
    private float complementaryBlend(float gyro, float mag, float alpha)
    {
        // Shortest-path angular difference, wrapped to [-180, 180]
        float diff = mag - gyro;
        while (diff >  180f) diff -= 360f;
        while (diff < -180f) diff += 360f;

        float result = gyro + (1f - alpha) * diff;
        return (result + 360f) % 360f;
    }

    @Override
    public void onAccuracyChanged(Sensor sensor, int accuracy) {}
}
