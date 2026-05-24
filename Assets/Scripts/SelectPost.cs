using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SelectPost : MonoBehaviour
{
    public Material sticker;
   
    public void Select()
    {
    //this.transform.parent.gameObject.SetActive(false);
        Debug.Log("mouse down");
        Texture tex = GetComponent<RawImage>().texture;
        sticker.SetTexture("_MainTex",tex);
        this.transform.parent.parent.gameObject.SetActive(false);

    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
