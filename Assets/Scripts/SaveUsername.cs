using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.EventSystems;
using System.Threading.Tasks;
using System.IO;
using static JSONReader;
using System.Text;

public class SaveUsername : MonoBehaviour
{
    public string database_ip = "http://192.168.0.8:3000";
    public Button saveButton;
    public TextMeshProUGUI usernameText;
    public TextMeshProUGUI statusMessageText;
    public GameObject statusMessageObject;


    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void saveUser()
    {
        StartCoroutine(saveUserInDb(usernameText.text, result =>
                {
                    //Debug.Log(result);

                    if (result == true)
                    {
                        saveButton.gameObject.SetActive(false);
                        UserInfo.username = usernameText.text;

                        //show for a second a message that the userame was updated successfully
                        statusMessageText.text = "Username updated successfully";
                        statusMessageObject.GetComponent<Image>().color = new Color32(58, 255, 30, 100);
                        statusMessageObject.SetActive(true);
                    }
                    else
                    {
                        saveButton.gameObject.SetActive(false);

                        //show error message 
                        statusMessageText.text = "Error conecting to server, try again later";
                        statusMessageObject.GetComponent<Image>().color = new Color32(255, 58, 30, 100);
                        statusMessageObject.SetActive(true);

                        usernameText.text = UserInfo.username;
                    }
                }));
    }

    IEnumerator saveUserInDb(string username, System.Action<bool> callback = null)
    {
        using (UnityWebRequest request = new UnityWebRequest(database_ip + "/users/username/", "PUT"))
        {
            string usernameRequest = "{ \"_id\": \"" + UserInfo.userId + "\", \"username\": \"" + username + "\" }";
            request.SetRequestHeader("Content-Type", "application/json");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(usernameRequest);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();

            if (request.isNetworkError || request.isHttpError)
            {
                Debug.Log(request.error);
                if (callback != null)
                {
                    //error connecting to server message
                    statusMessageText.text = "Error conecting to server, try again later";
                    statusMessageObject.GetComponent<Image>().color = new Color32(255, 58, 30, 100);
                    statusMessageObject.SetActive(true);

                    saveButton.gameObject.SetActive(false);
                    usernameText.text = UserInfo.username;
                }
            }
            else
            {
                if (callback != null)
                {
                    if (request.downloadHandler.text != "{}" &&
                        request.downloadHandler.text != "" &&
                        request.downloadHandler.text != null
                    )
                    {
                        callback.Invoke(true);
                    }
                    else
                    {
                        callback.Invoke(false);
                    }
                }
            }
        }
    }
}
