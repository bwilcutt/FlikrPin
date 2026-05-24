using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ActivateButton : MonoBehaviour
{
    public Button button;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void showButton()
    {
        button.gameObject.SetActive(true);
    }

    public void hideButton()
    {
        button.gameObject.SetActive(false);
    }
}
