using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class iOSPlugin : MonoBehaviour
{
#if UNITY_IOS

    [DllImport("__Internal")]

    private static extern void _AskPermission();

    public static void AskPermission()
    {
        _AskPermission();
    }

#else

    public static void AskPermission(){

        Debug.Log("AskPermission only supported for iOS");
    }

#endif
}
