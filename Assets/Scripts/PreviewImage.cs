using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Text.RegularExpressions;

public class PreviewImage : MonoBehaviour
{
    // Start is called before the first frame update
    public string url;

    public string preview_link;
    public LoadTextureFromURL load;
    void Start()
    {
        //getHTML(url_link);
    }

    
    public string getHTML(){
        if(url.StartsWith("https://www.instagram.com")){
                string realInstaLink = @"(https://www.instagram.com/.*?/)\?igsh";
                Regex r = new Regex(realInstaLink, RegexOptions.IgnoreCase);
                Match m = r.Match(url);
                string link = m.Groups[1].ToString();
                Debug.Log(link);
                preview_link = link+"media/?size=l";
                load.TextureURL = preview_link;
                Debug.Log("instagram");
        }
        else{
            using (WebClient client = new WebClient ())
            {
                string htmlCode = client.DownloadString(url);
                Debug.Log(htmlCode);
                
                string pattern = @"property=""og:image"".*?content=""(http.*?)""";
                string sentence = "Who writes these notes?";

                
                if(url.StartsWith("https://photos.")){
                    pattern = @"property=""og:image"".*?content=""(http.*?)=.*?""";
                    Debug.Log("GooglePhotos");
                }
                
                
                Regex r = new Regex(pattern, RegexOptions.IgnoreCase);
                Match m = r.Match(htmlCode);
                Debug.Log(m.Groups[1]);
                preview_link = m.Groups[1].ToString();
                load.TextureURL = preview_link;
                
                
            }
        }
        load.UpdateImage();
        return preview_link;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
