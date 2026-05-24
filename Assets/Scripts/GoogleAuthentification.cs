using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Google;
using TMPro;
using Random = UnityEngine.Random;


public class GoogleAuthetification : MonoBehaviour
{
    public string database_ip = "http://192.168.2.144:3000";
    public TextMeshProUGUI errorMessageText;
    public GameObject errorMessageObject;
    public GameObject loginPanel;
    private string webClientId = "544754913426-90rhp4r62jh56kq1d8jnfqlghipq1j1f.apps.googleusercontent.com";
    private GoogleSignInConfiguration configuration;

    [System.Serializable]
    public class User
    {
        public string email;

        public string username;

    }

    // Defer the configuration creation until Awake so the web Client ID
    // Can be set via the property inspector in the Editor.
void Awake()
{
    SceneManager.LoadScene("ARScene");
    return;

    configuration = new GoogleSignInConfiguration
    {
        WebClientId = webClientId,
        RequestIdToken = true
    };

    if (PlayerPrefs.GetInt("Signedin") == 1)
    {
        OnSignInSilently();
    }
}

    public void OnSignIn()
    {
        GoogleSignIn.Configuration = configuration;
        GoogleSignIn.Configuration.UseGameSignIn = false;
        GoogleSignIn.Configuration.RequestIdToken = true;
        GoogleSignIn.Configuration.RequestEmail = true;

        GoogleSignIn.DefaultInstance.SignIn().ContinueWith(OnAuthenticationFinished, TaskScheduler.FromCurrentSynchronizationContext());
    }


    internal void OnAuthenticationFinished(Task<GoogleSignInUser> task)
    {

        if (task.IsFaulted)
        {
            using (IEnumerator<System.Exception> enumerator =
                    task.Exception.InnerExceptions.GetEnumerator())
            {
                if (enumerator.MoveNext())
                {
                    GoogleSignIn.SignInException error =
                            (GoogleSignIn.SignInException)enumerator.Current;
                    //Debug.LogError("Got Error: " + error.Status + " " + error.Message);

                    errorMessageObject.SetActive(true);
                    errorMessageText.text = errorMessageText.text + " ( error code 1 )";
                    PlayerPrefs.SetInt("Signedin", 0);
                    PlayerPrefs.Save();
                }
                else
                {
                    //Debug.LogError("Got Unexpected Exception?!?" + task.Exception);
                    errorMessageObject.SetActive(true);
                    errorMessageText.text = errorMessageText.text + " ( error code 2 )";
                    PlayerPrefs.SetInt("Signedin", 0);
                    PlayerPrefs.Save();
                }
            }
        }
        else if (task.IsCanceled)
        {
            //Debug.LogError("Canceled");
            //errorMessageObject.SetActive(true);
            //errorMessageText.text = "3";
            PlayerPrefs.SetInt("Signedin", 0);
            PlayerPrefs.Save();
        }
        else
        {
            //Debug.LogError("Welcome: " + task.Result.Email + "!");

            if (task.Result.Email.Length == 0)
            {
                if (PlayerPrefs.HasKey("Email") &&
                    PlayerPrefs.GetString("Email").Length > 1)
                {
                    findUser(PlayerPrefs.GetString("Email"));
                }
                else
                {
                    errorMessageObject.SetActive(true);
                    errorMessageText.text = errorMessageText.text + " ( error code 4 )";
                    PlayerPrefs.SetInt("Signedin", 0);
                    PlayerPrefs.Save();
                }
            }
            else
            {
                PlayerPrefs.SetInt("Signedin", 1);
                PlayerPrefs.SetString("Email", task.Result.Email);
                PlayerPrefs.Save();

                findUser(task.Result.Email);
            }

        }
    }

    public void findUser(string userEmail)
    {
        StartCoroutine(DownloadUser(userEmail, result =>
        {
            //Debug.Log(result);

            if (result == true)
            {
                SceneManager.LoadScene("ARScene");
            }
            else //otherwise make a new user
            {
                RESTSaveUser();
            }
        }));
    }

    IEnumerator DownloadUser(string userEmail, System.Action<bool> callback = null)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(database_ip + "/users/one-user/" + userEmail))
        {
            yield return request.SendWebRequest();

            if (request.isNetworkError || request.isHttpError)
            {
                Debug.Log(request.error);
                if (callback != null)
                {
                    errorMessageObject.SetActive(true);
                    errorMessageText.text = "Issue connecting to our Servers. Check to make sure you are connected to the internet through Data or Wifi ( error code 5 )";
                    PlayerPrefs.SetInt("Signedin", 0);
                    PlayerPrefs.Save();
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
                        UserInfo.SetupUserData(request.downloadHandler.text);
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

    public void RESTSaveUser()
    {
        User user = new User();

        user.email = PlayerPrefs.GetString("Email");
        user.username = CreateUsername();

        StartCoroutine(SaveUser(JsonUtility.ToJson(user), result =>
        {
            //Debug.Log(result);

            if (result == true)
            {
                SceneManager.LoadScene("ARScene");
            }
            else //DB didn't send back the created user for some reason
            {
                errorMessageObject.SetActive(true);
                errorMessageText.text = "Something went wrong trying to create your account. Please restart the app and try again ( error code 5 )";
                PlayerPrefs.SetInt("Signedin", 0);
                PlayerPrefs.Save();
            }
        }));
    }

    IEnumerator SaveUser(string profile, System.Action<bool> callback = null)
    {
        using (UnityWebRequest request = new UnityWebRequest(database_ip + "/users/add-user", "POST"))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(profile);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();

            if (request.isNetworkError || request.isHttpError)
            {
                Debug.Log(request.error);
                if (callback != null)
                {
                    errorMessageObject.SetActive(true);
                    errorMessageText.text = "Issue connecting to our Servers. Check to make sure you are connected to the internet through Data or Wifi ( error code 5 )";
                    PlayerPrefs.SetInt("Signedin", 0);
                    PlayerPrefs.Save();
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
                        UserInfo.SetupUserData(request.downloadHandler.text);
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

    public string CreateUsername()
    {

        string[] colors = { "Pink", "Purple", "Magenta", "Blue", "Yellow", "Orange", "Green", "Mint", "Red", "Neon", "Silver",
                            "Gray", "Ruby", "Emerald", "Gold", "Lime", "Rose", "Plum", "Teal", "Aqua", "Peach", "Navy",
                             "Hazel", "Pearl", "Diamond", "Crystal", "Scarlet" };

        string[] animals = {"Capybara", "Elephant", "Hippo", "Rhino", "Panda", "Lion", "Corgi", "Poodle", "Pigeon", "Platypus",
                            "Bear", "Gator", "Goose", "Fox", "Squid", "Octopus", "Frog", "Turtle", "Worm", "moose", "zebra",
                            "Emu", "Koala", "Shark", "Dolphin", "Wolf", "Tiger", "Whale", "Penguin", "Rabbit", "Owl",
                            "Sloth", "Raven", "Monkey", "Bigfoot"};

        int colorIndex = Random.Range(0, colors.Length - 1);
        int animalIndex = Random.Range(0, animals.Length - 1);

        return colors[colorIndex] + animals[animalIndex];
    }

    public void OnSignOut()
    {
        //Debug.LogError("Calling SignOut");

        GoogleSignIn.DefaultInstance.SignOut();
        GoogleSignIn.DefaultInstance.Disconnect();

        PlayerPrefs.SetInt("Signedin", 0);
        PlayerPrefs.Save();

        SceneManager.LoadScene("Signin");
    }


    public void OnSignInSilently()
    {
        GoogleSignIn.Configuration = configuration;
        GoogleSignIn.Configuration.UseGameSignIn = false;
        GoogleSignIn.Configuration.RequestIdToken = true;
        //Debug.LogError("Calling SignIn Silently");

        GoogleSignIn.DefaultInstance.SignInSilently()
              .ContinueWith(OnAuthenticationFinished, TaskScheduler.FromCurrentSynchronizationContext());

    }

}
