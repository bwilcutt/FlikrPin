package com.gametag.compass;

import android.content.Context;
import android.hardware.Sensor;
import android.hardware.SensorEvent;
import android.hardware.SensorEventListener;
import android.hardware.SensorManager;

public class CompassPlugin implements SensorEventListener
{
    private static SensorManager  sensorManager;
    private static CompassPlugin  instance;

    // ── raw sensor data ───────────────────────────────────────────────────
    private float[] gravity     = new float[3];
    private float[] geomagnetic = new float[3];
    private float[] rotation    = new float[9];
    private float[] orientation = new float[3];

    private boolean hasGravity  = false;
    private boolean hasMagnetic = false;

    // ── gyroscope state ───────────────────────────────────────────────────
    private float   gyroHeading      = 0f;   // heading integrated from gyro
    private float   magHeading        = 0f;   // heading from accel+mag fusion
    private boolean gyroInitialized   = false;
    private long    lastGyroTimestamp = 0;

    // ── complementary filter coefficient ─────────────────────────────────
    // α close to 1.0 = trust gyro more (smoother, slower to correct drift)
    // α close to 0.0 = trust magnetometer more (faster but noisier)
    // 0.97 is a standard value for ~30Hz sensor update rate
    private static final float ALPHA = 0.97f;

    // ── output ────────────────────────────────────────────────────────────
    private static volatile float heading = 0f;

    // ── lifecycle ──────────────────────────────────────────────────────────
    public static void Start(Context context)
    {
        instance      = new CompassPlugin();
        sensorManager = (SensorManager) context.getSystemService(Context.SENSOR_SERVICE);

        // Accelerometer — for tilt correction
        sensorManager.registerListener(instance,
            sensorManager.getDefaultSensor(Sensor.TYPE_ACCELEROMETER),
            SensorManager.SENSOR_DELAY_GAME);

        // Magnetometer — absolute north reference
        sensorManager.registerListener(instance,
            sensorManager.getDefaultSensor(Sensor.TYPE_MAGNETIC_FIELD),
            SensorManager.SENSOR_DELAY_GAME);

        // Gyroscope — smooth short-term rotation
        Sensor gyro = sensorManager.getDefaultSensor(Sensor.TYPE_GYROSCOPE);
        if (gyro != null)
        {
            sensorManager.registerListener(instance, gyro, SensorManager.SENSOR_DELAY_GAME);
        }
    }

    public static void Stop()
    {
        if (sensorManager != null && instance != null)
            sensorManager.unregisterListener(instance);
    }

    public static float GetHeading()
    {
        return heading;
    }

    // ── sensor events ──────────────────────────────────────────────────────
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

    // ── accelerometer + magnetometer fusion ────────────────────────────────
    private void updateMagHeading()
    {
        if (!hasGravity || !hasMagnetic) return;

        boolean success = SensorManager.getRotationMatrix(rotation, null, gravity, geomagnetic);
        if (!success) return;

        SensorManager.getOrientation(rotation, orientation);
        float azimuth = (float) Math.toDegrees(orientation[0]);
        magHeading = (azimuth + 360f) % 360f;

        // If gyro hasn't been initialized yet, snap to mag heading immediately
        if (!gyroInitialized)
        {
            gyroHeading   = magHeading;
            heading        = magHeading;
            gyroInitialized = true;
            return;
        }

        // Complementary filter: blend gyro (smooth) with mag (absolute)
        float blended = complementaryBlend(gyroHeading, magHeading, ALPHA);
        gyroHeading = blended;
        heading     = blended;
    }

    // ── gyroscope integration ──────────────────────────────────────────────
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

        // Gyroscope Z axis (around vertical) gives yaw rate in rad/s
        // Android: rotation around -Z = turning right = increasing azimuth
        float yawRate = -event.values[2];
        float deltaDeg = (float) Math.toDegrees(yawRate * dt);

        gyroHeading = (gyroHeading + deltaDeg + 360f) % 360f;

        // Apply complementary blend with mag every gyro tick too,
        // so drift correction is continuous even between mag updates
        gyroHeading = complementaryBlend(gyroHeading, magHeading, ALPHA);
        heading     = gyroHeading;
    }

    // ── complementary filter blend ─────────────────────────────────────────
    // Blends two angles correctly across the 0/360 boundary
    private float complementaryBlend(float gyro, float mag, float alpha)
    {
        // Shortest-path difference between mag and gyro
        float diff = mag - gyro;

        // Wrap diff to [-180, 180]
        while (diff >  180f) diff -= 360f;
        while (diff < -180f) diff += 360f;

        float result = gyro + (1f - alpha) * diff;
        return (result + 360f) % 360f;
    }

    @Override
    public void onAccuracyChanged(Sensor sensor, int accuracy) {}
}
