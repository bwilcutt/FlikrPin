using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class SelectSticker : MonoBehaviour
{
    public SaveMessage sm;
    public GameObject previewImage;
    public GameObject scroll;

    public int sticker_index;

    public void Select(){
        
        sm.media_type = "sticker";
        Texture texture = GetComponent<RawImage>().texture;
        sm.stickerTexture = texture;
        previewImage.SetActive(true);
        previewImage.GetComponent<RawImage>().texture = texture;
        scroll = this.transform.parent.parent.parent.gameObject;
        scroll.SetActive(false);
        sm.chosenSticker = sticker_index.ToString();

    }
    
}
