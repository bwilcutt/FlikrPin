using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_IOS
using UnityEngine.iOS;
using UnityEngine.Apple.ReplayKit;
#endif

public class ScreenRecorder : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
#if UNITY_IOS
    public void TakeScreenShot()
    {

        if (!ReplayKit.APIAvailable)
        {
            return;
        }
        var recording = ReplayKit.isRecording;

        try
        {
            recording = !recording;
            if (recording)
            {
                ReplayKit.StartRecording(true, true);
                Debug.Log("Started recording");
            }
            else
            {
                ReplayKit.StopRecording();
                Debug.Log("Stopped recording");

            }
        }
        catch (System.Exception e)
        {
            Debug.Log(e.ToString());
        }

    }
    public void ShowPreview()
    {
        if (ReplayKit.recordingAvailable)
        {
            Debug.Log("Loading Preview");
            ReplayKit.Preview();

        }
        else
        {
            Debug.Log("Preview not available");
        }
    }
#endif



}
