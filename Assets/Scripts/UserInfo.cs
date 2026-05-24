using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UserInfo
{
    public static string userId = "";
    public static string email = "";
    public static string username = "";
    public static string[] following;
    public static string[] followers;

    [System.Serializable]
    public class MyInfo
    {
        public string _id;
        public string email;
        public string username;
        public string[] following;
        public string[] followers;
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public static void SetupUserData(string data)
    {
        MyInfo myInfo = JsonUtility.FromJson<MyInfo>(data);

        UserInfo.userId = myInfo._id;
        UserInfo.email = myInfo.email;
        UserInfo.username = myInfo.username;
        UserInfo.following = myInfo.following;
        UserInfo.followers = myInfo.followers;
    }
}
