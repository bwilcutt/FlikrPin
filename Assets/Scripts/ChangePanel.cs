using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangePanel : MonoBehaviour
{
    public GameObject panel1, panel2;

    // Start is called before the first frame update
    void Start()
    {

    }

    public void SwapPanel()
    {
        panel1.SetActive(false);
        panel2.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {

    }
}
