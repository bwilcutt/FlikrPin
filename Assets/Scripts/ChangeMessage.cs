using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeMessage : MonoBehaviour
{
    // Start is called before the first frame update
    private void OnMouseDown()
    {
        TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default, false, false, false);
    }
}
