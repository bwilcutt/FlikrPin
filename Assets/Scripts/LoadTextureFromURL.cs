using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class LoadTextureFromURL : MonoBehaviour
{
    //Load texture from image path to the post
    public string TextureURL = "";
    public string source = "";
    public Texture2D pickedImage;

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(DownloadImage(TextureURL));
    }

    public void UpdateImage(){
        StartCoroutine(DownloadImage(TextureURL));
    }

    // Update is called once per frame
    void Update()
    {

    }

    

    IEnumerator DownloadImage(string MediaUrl)
    {
        if (source == "url")
        {
            UnityWebRequest request = UnityWebRequestTexture.GetTexture(MediaUrl);
            yield return request.SendWebRequest();
            if (request.isNetworkError || request.isHttpError)
                Debug.Log(request.error);
            else{
                pickedImage = ((DownloadHandlerTexture)request.downloadHandler).texture;
                this.gameObject.GetComponent<Renderer>().material.mainTexture = pickedImage;
                float ratio = (float)pickedImage.height / (float)pickedImage.width;
                float x = 1; //this will be the width of the quad so set it to whatever...
                float y = x * ratio;
                this.gameObject.transform.localScale = new Vector3(x, y, 1);
            }
        }
        else if(source == "gallery")
        {
            LoadImageFromGallery(MediaUrl);
        }
    }

    
    void LoadImageFromGallery(string MediaUrl)
    {
        pickedImage = NativeGallery.LoadImageAtPath(MediaUrl, 1024);
        this.gameObject.GetComponent<Renderer>().material.mainTexture = pickedImage;
        float ratio = (float)pickedImage.height / (float)pickedImage.width;
        float x = 1; //this will be the width of the quad so set it to whatever...
        float y = x * ratio;
        this.gameObject.transform.localScale = new Vector3(x, y, 1);
    }
}
