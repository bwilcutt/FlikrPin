using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using TMPro;
public class ShowSticker : MonoBehaviour
{
    //public Image image;
    public GameObject cell;
    public TextMeshProUGUI txt;
    public GameObject StickersSlot;
    public GameObject previewImage;
    private List<string> GetAllGalleryImagePaths()//don't really use this anymore 
    {
        List<string> results = new List<string>();
        HashSet<string> allowedExtesions = new HashSet<string>() { ".png", ".jpg", ".jpeg" };

        try
        {
            AndroidJavaClass mediaClass = new AndroidJavaClass("android.provider.MediaStore$Images$Media");

            // Set the tags for the data we want about each image.  This should really be done by calling; 
            //string dataTag = mediaClass.GetStatic<string>("DATA");
            // but I couldn't get that to work...

            const string dataTag = "_data";
            txt.text = "trying...";

            string[] projection = new string[] { dataTag };
            AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = player.GetStatic<AndroidJavaObject>("currentActivity");

            string[] urisToSearch = new string[] { "EXTERNAL_CONTENT_URI", "INTERNAL_CONTENT_URI" };
            foreach (string uriToSearch in urisToSearch)
            {
                txt.text = uriToSearch;
                AndroidJavaObject externalUri = mediaClass.GetStatic<AndroidJavaObject>(uriToSearch);
                AndroidJavaObject finder = currentActivity.Call<AndroidJavaObject>("managedQuery", externalUri, projection, null, null, null);
                bool foundOne = finder.Call<bool>("moveToFirst");
                while (foundOne)
                {
                    txt.text = "found one";
                    int dataIndex = finder.Call<int>("getColumnIndex", dataTag);
                    string data = finder.Call<string>("getString", dataIndex);
                    if (allowedExtesions.Contains(Path.GetExtension(data).ToLower()))
                    {
                        string path = @"file:///" + data;
                        results.Add(path);
                    }

                    foundOne = finder.Call<bool>("moveToNext");
                }
            }
        }
        catch (System.Exception e)
        {
            // do something with error...
            txt.text = "Could not access photos";
        }

        return results;
    }

    [SerializeField]
    private RawImage m_image;

    public void SetImage()//this is to set selected images to be in a cell like a sticker
    {
        List<string> galleryImages = GetAllGalleryImagePaths();
        
        foreach(string imagePath in galleryImages)
        {
            txt.text = imagePath;
            Texture2D t = new Texture2D(2, 2);
            (new WWW(galleryImages[0])).LoadImageIntoTexture(t);
            m_image.texture = t;
            cell.GetComponent<RawImage>().texture = t;
            Instantiate(cell, this.transform);
        }
        
    }
    public Texture2D pickedImage;
    public Material post;
    public GameObject pre_post;
    public Transform quad;
    public string _image_path;

    public Object[] stickers;
    public void PickImageFromGallery(int maxSize = 1024)//pick image from gallery to post
    {
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (path != null)
            {
                // Create Texture from selected image
                pickedImage = NativeGallery.LoadImageAtPath(path, maxSize);
                post.SetTexture("_MainTex", pickedImage);
                pre_post.GetComponent<RawImage>().texture = pickedImage;
                float ratio = (float)pickedImage.height / (float)pickedImage.width;
                float x = 1; //this will be the width of the quad so set it to whatever...
                float y = x * ratio;
                _image_path = path;
                txt.text = pickedImage.height.ToString()+" "+ pickedImage.width+"\n from: "+_image_path;
                quad.localScale = new Vector3(x, y, 1);
            }
        });
        
    }

    bool loaded = false;
    GameObject scroll;
    

    void Start(){
        if(StickersSlot!=null){
            scroll = StickersSlot.transform.parent.parent.gameObject;
        }
        
    }
    public void SetStickers()
    {
        //Object[] images = Resources.LoadAll("default stickers");
        Debug.Log("loading stickers");
        if(!loaded){
            int count = 0;
            foreach (var image in stickers)
            {


                cell.GetComponent<RawImage>().texture = (Texture2D)image;
                cell.GetComponent<SelectSticker>().sticker_index = count;
                count++;
                Instantiate(cell, StickersSlot.transform);
            }
            loaded = true;
            Debug.Log("not loaded");
        }
        else{
            scroll.SetActive(true);
            Debug.Log("loaded");
            previewImage.SetActive(false);
        }
    }
    // Start is called before the first frame update
    


}
