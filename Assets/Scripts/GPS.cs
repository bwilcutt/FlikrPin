using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class GPS : MonoBehaviour
{
    public static GPS Instance { set; get; }
    public float latitude;
    public float longitude;

    public float altitude;

    //test service
    //public TextMeshProUGUI tmp;

    // Start is called before the first frame update
    void Start()
    {
        Instance = this;
        //DontDestroyOnLoad(gameObject);
        StartCoroutine(StartLocationService());
        
    }

    private IEnumerator StartLocationService()
    {
        if (!Input.location.isEnabledByUser)
        {
            Debug.Log("user has not enabled GPS");
            //tmp.text = "Please Enable GPS";
            yield break;
        }

        Input.location.Start();
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if(maxWait <= 0)
        {
            Debug.Log("time out");
            yield break;
        }

        if(Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.Log("unabled to determined device location");
            //tmp.text = "No device location";
            yield break ;
        }

        latitude = Input.location.lastData.latitude;
        longitude = Input.location.lastData.longitude;
        altitude = Input.location.lastData.altitude;
        Debug.Log(latitude);
        //tmp.text = "lat: "+latitude.ToString()+" long:"+longitude.ToString();
        yield break;
    }
}
