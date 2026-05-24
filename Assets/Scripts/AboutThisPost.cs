using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AboutThisPost : MonoBehaviour
{
    public int _index;
    public JSONReader j;
    public ShowYourPosts syp;
    public GameObject postList;
    public GameObject selectedPost;
    public Texture2D image;
    public string caption;

    //TODO edit post and set the 
    public void DeletePost()
    {
        j.DeletePost(_index);
        //syp.SetPostList();
        selectedPost.SetActive(false);
        postList.SetActive(true);
        Destroy(gameObject);
       
    }

    public void ViewPost()
    {
        postList.SetActive(false);
        try {
            selectedPost.transform.GetChild(0).GetComponent<RawImage>().texture = image;
        }
        catch
        {
            Debug.Log("could not find texture");
        }
        
        selectedPost.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = caption;
        selectedPost.transform.GetChild(2).GetComponent<MyPostOptions>().selectedPostIcon = this;
        selectedPost.SetActive(true);
    }


}
