using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MyPostOptions : MonoBehaviour
{
    public JSONReader j;
    public int _index;
    public AboutThisPost selectedPostIcon;

    public void EditPost()
    {

    }


    public void DeletePost()
    {

        selectedPostIcon.DeletePost();

    }
}
