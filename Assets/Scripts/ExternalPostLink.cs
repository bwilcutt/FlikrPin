using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExternalPostLink : MonoBehaviour
{
    // Start is called before the first frame update
    private void OnMouseDown()
    {
        Application.OpenURL("http://unity3d.com/");
    }
}
