using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using UnityEngine.Networking;

public class JSONReader : MonoBehaviour
{
    public TextAsset textJSON;

    public string database_ip = "http://192.168.2.144:3000";

    [System.Serializable]

    public class Posts
    {
        public string user;
        public string created_at;
        public string _id;
        public string username;

        public string latitude;
        public string longitude;
        public string message;
        public string url;
        public string preview_url;
        public int likes;
        public string privacy;
        public string altitude;
        public string media_type;
        public string media_source;
    }

    [System.Serializable]

    public class PostList
    {
        public List<Posts> posts;

        public List<Posts> yourPosts;


    }

    public class Profile
    {
        public string username;
        public string user;

        public List<string> followers;

        public List<string> following;

    }

    public PostList myPostList = new PostList();
    // Start is called before the first frame update
    void Start()
    {
        //myPostList = JsonUtility.FromJson<PostList>(textJSON.text);
        //ReadData();
            Debug.Log(myPostList.posts);
            StartCoroutine(Download("66074bb37bef5e9cbfe36af9", result => {
            Debug.Log(result);
            }));
        
    }

    public Posts post = new Posts();
    string filePath;
    public void SaveData()
    {
        myPostList.posts.Add(post);
        SaveToJson();

    }
    private void SaveToJson()
    {
        string postData = JsonUtility.ToJson(myPostList);
        filePath = Application.persistentDataPath + "/postData.json";
        Debug.Log(filePath);
        Debug.Log(postData);
        System.IO.File.WriteAllText(filePath, postData);
        Debug.Log("saved");
    }

    public void DeletePost(int i)
    {
        myPostList.posts.RemoveAt(i);
        SaveToJson();
    }
    /*public void ResetFile()
    {

        myPostList.posts.Clear();
        
    }*/
    public Posts GetPostAt(int _index)
    {
        return myPostList.posts[_index];
    }
    public string ReadData()
    {
        filePath = Application.persistentDataPath + "/postData.json";
        if (File.Exists(filePath))
        {
            string loadPlayerData = File.ReadAllText(filePath);
            myPostList = JsonUtility.FromJson<PostList>(loadPlayerData);
            return loadPlayerData;
        }
        return null;
    }

    public void RESTSave(){
        StartCoroutine(Upload(JsonUtility.ToJson(post), result => {
            Debug.Log(result);
        }));
    }

    IEnumerator Download(string id, System.Action<Posts> callback = null)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(database_ip+"/posts/all-posts"))
        {
            yield return request.SendWebRequest();

            if (request.isNetworkError || request.isHttpError)
            {
                Debug.Log(request.error);
                if (callback != null)
                {
                    callback.Invoke(null);
                }
            }
            else
            {
                if (callback != null)
                {
                    //callback.Invoke(Posts.Parse(request.downloadHandler.text));
                    string jtext = "{ \"posts\":" +request.downloadHandler.text + "}";
                    myPostList = JsonUtility.FromJson<PostList>(jtext);
                    Debug.Log(jtext);
                    Debug.Log(myPostList.posts[0].longitude);
                }
            }
        }
    }


    IEnumerator DownloadYourPosts(string id, System.Action<Posts> callback = null)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(database_ip+"/posts/all-posts"))
        {
            yield return request.SendWebRequest();

            if (request.isNetworkError || request.isHttpError)
            {
                Debug.Log(request.error);
                if (callback != null)
                {
                    callback.Invoke(null);
                }
            }
            else
            {
                if (callback != null)
                {
                    //callback.Invoke(Posts.Parse(request.downloadHandler.text));
                    string jtext = "{ \"posts\":" +request.downloadHandler.text + "}";
                    myPostList = JsonUtility.FromJson<PostList>(jtext);
                    Debug.Log(jtext);
                    Debug.Log(myPostList.posts[0].longitude);
                }
            }
        }
    }


    IEnumerator Upload(string profile, System.Action<bool> callback = null)
    {
        using (UnityWebRequest request = new UnityWebRequest(database_ip+"/posts/post", "POST"))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(profile);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();

            if (request.isNetworkError || request.isHttpError)
            {
                Debug.Log(request.error);
                if(callback != null) 
                {
                    callback.Invoke(false);
                }
            }
            else
            {
                if(callback != null) 
                {
                    callback.Invoke(request.downloadHandler.text != "{}");
                }
            }
        }
    }
    
    
}
