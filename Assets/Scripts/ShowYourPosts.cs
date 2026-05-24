using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using TMPro;
public class ShowYourPosts : MonoBehaviour
{
    public GameObject post;
    public JSONReader j;

    private Texture2D pickedImage;
    public void SetPostList()
    {
       

        
        var _posts = j.myPostList.posts;
        int i = 0;
        foreach (var p in _posts)
        {
            Debug.Log(p.url);
            var aboutThisPost = post.GetComponent<AboutThisPost>();
            aboutThisPost._index = i;
            aboutThisPost.caption = p.message;
            try
            {
                pickedImage = NativeGallery.LoadImageAtPath(p.url, 1024);
                post.GetComponent<RawImage>().texture = pickedImage;
                float ratio = (float)pickedImage.height / (float)pickedImage.width;
                float x = 1; //this will be the width of the quad so set it to whatever...
                float y = x * ratio;
                post.transform.localScale = new Vector3(x, y, 1);
                aboutThisPost.image = pickedImage;
                Instantiate(post, this.transform);
            }
            catch
            {
                
                Debug.Log("Could not load image"+p.url);
                Instantiate(post, this.transform);
            }
            post.transform.GetChild(1).gameObject.GetComponent<TextMeshProUGUI>().text = p.message;
            i++;
            
        }
    }
    
    // Start is called before the first frame update
    void Start()
    {
        SetPostList();
        //iOSPlugin.AskPermission();
    }

   

    
}
