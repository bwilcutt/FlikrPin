using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using System.Text;
using UnityEngine.SceneManagement;

public class SavePost : MonoBehaviour
{
    public GameObject newpost;
    public GPS gps;
    public JSONReader _json;
    public ShowSticker image;
    public TextMeshProUGUI txt;
    public string message = "";
    public string url = "";
    public string preview_url = "";
    public string media_type;

    public void SavePostToJason()
    {
        var post = _json.post;

        // Use real user data from UserInfo instead of hardcoded values
        post.user = UserInfo.userId;
        post.username = UserInfo.username;

        post.latitude = gps.latitude.ToString();
        post.longitude = gps.longitude.ToString();
        post.altitude = newpost.transform.position.z.ToString();
        post.privacy = "everyone";

        post.url = url;
        post.message = message;
        post.media_type = media_type;
        post.media_source = "url";
        post.preview_url = preview_url;

        _json.RESTSave();

        SceneManager.LoadScene("ARScene");
    }
}
