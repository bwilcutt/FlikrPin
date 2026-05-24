using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ShowKeyboard : MonoBehaviour
{
    public TouchScreenKeyboard keyboard;
    public TextMeshProUGUI inputText;

    public bool clicked = false;
    public string message;
    public bool editing = false;


    public void ShowKeyboardInput()
    {
        if (!clicked)
        {
            keyboard = TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default);
            editing = true;
            clicked = true;
        }
        else
        {
            keyboard.active = false;
            editing = false;
            clicked = false;

        }


    }

    public void ShowKeyboardInputOnly()
    {
        keyboard = TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default);
        editing = true;
        clicked = true;
    }

    public void HideKeyboardInputOnly()
    {
        keyboard.active = false;
        editing = false;
        clicked = false;
    }

    void OnGUI()
    {

        if (keyboard != null && editing)
        {
            inputText.text = keyboard.text;
            message = keyboard.text;
        }

        if (keyboard != null && keyboard.status == TouchScreenKeyboard.Status.Done)
        {
            editing = false;
        }
    }

}
