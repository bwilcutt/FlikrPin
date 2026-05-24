using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using static JSONReader;
using TMPro;
using UnityEngine.EventSystems;
public class LikeAPost : MonoBehaviour, IPointerDownHandler
{
    public Texture redHeart;
    public Texture whiteHeart;
    
    public JSONReader db;
    public bool liked = false;
    public Posts post;
    // Start is called before the first frame update
    void Start(){
        post = this.transform.parent.GetComponent<AboutPost>().post;
        this.transform.GetChild(0).GetComponent<TextMeshPro>().text = post.likes.ToString();
    }

    void updateLikes(int addedLikes){

        post.likes += addedLikes;
        this.transform.GetChild(0).GetComponent<TextMeshPro>().text = post.likes.ToString();
    }
    void LikeThisPost(){
        
        
        
        if(!liked){
            StartCoroutine(LikePost(result => {
            Debug.Log(result);
        }));
            this.gameObject.GetComponent<Renderer>().material.mainTexture = redHeart;
            updateLikes(1);
            liked = true;
        }
        else{
            StartCoroutine(UnLikePost(result => {
            Debug.Log(result);
        }));
            this.gameObject.GetComponent<Renderer>().material.mainTexture = whiteHeart;
            updateLikes(-1);
            liked = false;
        }
      
    }
    public void OnPointerDown(PointerEventData pointerEventData)
    {
        //Output the name of the GameObject that is being clicked
        Debug.Log(name + "Game Object Click in Progress");
        LikeThisPost();
    }
        IEnumerator LikePost(System.Action<bool> callback = null)
    {
        using (UnityWebRequest request = new UnityWebRequest(db.database_ip+"/posts/like", "PUT"))
        {
            var postID = post._id;
            Debug.Log(postID);
            string likeRequest = "{ \"_id\": \"" + postID+"\" }";
            request.SetRequestHeader("Content-Type", "application/json");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(likeRequest);
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
    IEnumerator UnLikePost(System.Action<bool> callback = null)
    {
        using (UnityWebRequest request = new UnityWebRequest(db.database_ip+"/posts/unlike", "PUT"))
        {
            var postID = post._id;
            Debug.Log(postID);
            string likeRequest = "{ \"_id\": \"" + postID+"\" }";
            request.SetRequestHeader("Content-Type", "application/json");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(likeRequest);
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
