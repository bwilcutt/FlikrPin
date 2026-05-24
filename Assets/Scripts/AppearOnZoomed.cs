using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AppearOnZoomed : MonoBehaviour
{
    public List<GameObject> objects;
    public double limit_scale;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(this.gameObject.transform.localScale.x > limit_scale){
            foreach(GameObject i in objects){
                i.SetActive(true);
            }
        }
        else{
            foreach(GameObject i in objects){
                i.SetActive(false);
            }
        }
    }
}
