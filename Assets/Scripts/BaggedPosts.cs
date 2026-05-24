using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaggedPosts : MonoBehaviour
{
    void OnCollisionEnter(Collision collision){

        if(collision.gameObject.tag == "Post"){
            collision.gameObject.SetActive(false);
        }
    }
}
