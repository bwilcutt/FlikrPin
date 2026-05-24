using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SaveMessage : MonoBehaviour
{
    public GameObject post;
    public TextMeshPro caption;
    public ShowKeyboard sk;

    public GameObject postNoImage;
    public PreviewImage ltfurl;

    public ShowKeyboard url;
    public SavePost sp;

    public string chosenSticker;
    public Texture stickerTexture;

    public TextMeshPro captionSticker;
    public string media_type="image";
    public void PushToSave()
    {
        if(media_type=="image"){
            if (post != null)
            {
                post.SetActive(true);
                
                sp.url = url.message;
                ltfurl.url = url.message;
                string previewlink = ltfurl.getHTML();
                sp.preview_url = previewlink;
                    
            }
        }
        else if(media_type=="sticker"){
            if(postNoImage!=null){
                caption = captionSticker;
                postNoImage.SetActive(true);
                postNoImage.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.mainTexture = stickerTexture;
                sp.preview_url = chosenSticker;
            }
        }
        else if(media_type == "none"){
            if(postNoImage!=null){
                postNoImage.SetActive(true);
                postNoImage.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.mainTexture = null;
            }
        }
        
        if (caption != null)
        {
            caption.SetText(sk.message);
            Debug.Log("success");
            sp.message = sk.message;
        }
        else
        {
            Debug.Log("Caption NOT FOUND!!!!!");
        }
        sp.media_type = media_type;
        transform.parent.gameObject.SetActive(false);
    }
}
