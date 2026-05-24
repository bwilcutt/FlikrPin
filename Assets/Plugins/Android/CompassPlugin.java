package com.gametag.compass;

import android.content.Context;
import android.hardware.Sensor;
import android.hardware.SensorEvent;
import android.hardware.SensorEventListener;
import android.hardware.SensorManager;

public class CompassPlugin implements SensorEventListener
{
    private static SensorManager sensorManager;
    private static float heading = 0f;
    private static CompassPlugin instance;

    private float[] gravity = new float[3];
    private float[] geomagnetic = new float[3];
    private float[] rotation = new float[9];
    private float[] orientation = new float[3];
    private boolean hasGravity = false;
    private boolean hasMagnetic = false;

    public static void Start(Context context)
    {
        instance = new CompassPlugin();
        sensorManager = (SensorManager) context.getSystemService(Context.SENSOR_SERVICE);
        sensorManager.registerListener(instance,
            sensorManager.getDefaultSensor(Sensor.TYPE_ACCELEROMETER),
            SensorManager.SENSOR_DELAY_UI);
        sensorManager.registerListener(instance,
            sensorManager.getDefaultSensor(Sensor.TYPE_MAGNETIC_FIELD),
            SensorManager.SENSOR_DELAY_UI);
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

    @Override
    public void onSensorChanged(SensorEvent event)
    {
        if (event.sensor.getType() == Sensor.TYPE_ACCELEROMETER)
        {
            gravity = event.values.clone();
            hasGravity = true;
        }
        if (event.sensor.getType() == Sensor.TYPE_MAGNETIC_FIELD)
        {
            geomagnetic = event.values.clone();
            hasMagnetic = true;
        }

        if (hasGravity && hasMagnetic)
        {
            boolean success = SensorManager.getRotationMatrix(rotation, null, gravity, geomagnetic);
            if (success)
            {
                SensorManager.getOrientation(rotation, orientation);
                float azimuth = (float) Math.toDegrees(orientation[0]);
                heading = (azimuth + 360) % 360;
            }
        }
    }

    @Override
    public void onAccuracyChanged(Sensor sensor, int accuracy) {}
}
