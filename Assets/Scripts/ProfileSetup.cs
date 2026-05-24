using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ProfileSetup : MonoBehaviour
{
    public TextMeshProUGUI usernameText;
    public TextMeshProUGUI followingText;
    public TextMeshProUGUI followersText;

    // Start is called before the first frame update
    void Start()
    {
        usernameText.text = UserInfo.username;
        followingText.text = "" + UserInfo.following.Length;
        followersText.text = "" + UserInfo.followers.Length;
    }

    // Update is called once per frame
    void Update()
    {

    }

}
