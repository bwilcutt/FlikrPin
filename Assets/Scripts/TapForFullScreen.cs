using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TapForFullScreen : MonoBehaviour
{
    public Transform p;
    void OnMouseDown()
    {
        this.transform.SetParent(p);
        
    }
}
