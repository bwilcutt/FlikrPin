using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TextInputWindow : MonoBehaviour
{
    public TMP_InputField messageInput;
    public Button okButton;
    public GameObject postTextPrefab;
    public GPS gps;

    private Vector3 dropPosition;

    void Start()
    {
        gameObject.SetActive(false);
    }
	void Update()
	{
	    if (!gameObject.activeSelf) return;
	    
	    if (Input.touchCount > 0)
	    {
		Touch touch = Input.GetTouch(0);
		if (touch.phase == TouchPhase.Began)
		{
		    // Check if touch is outside the window
		    RectTransform rt = GetComponent<RectTransform>();
		    if (!RectTransformUtility.RectangleContainsScreenPoint(rt, touch.position, Camera.main))
		    {
		        Hide();
		    }
		}
	    }
	}
    public void Show(Vector3 position)
    {
        dropPosition = position;
        messageInput.text = "";
        messageInput.lineLimit = 12;
        messageInput.characterLimit = 288;
        gameObject.SetActive(true);
        messageInput.ActivateInputField();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

public void OnOKPressed()
{
    Debug.Log("OnOKPressed called — message: " + messageInput.text);
    
    if (string.IsNullOrEmpty(messageInput.text))
    {
        Debug.Log("Message is empty, hiding window.");
        Hide();
        return;
    }

    if (postTextPrefab == null)
    {
        Debug.LogError("postTextPrefab is not assigned!");
        Hide();
        return;
    }

    Debug.Log("Instantiating postText at position: " + dropPosition);
// Convert screen position to world position in front of camera
    Camera cam = Camera.main;
    Vector3 worldPos = cam.transform.position + cam.transform.forward * 2f;
    GameObject tag = Instantiate(postTextPrefab, worldPos, Quaternion.identity);
    Debug.Log("World position: " + worldPos);
    Debug.Log("Tag instantiated: " + tag.name);

    Transform contentTransform = tag.transform.Find("bubble/content");
    if (contentTransform != null)
    {
        TextMeshPro tmp = contentTransform.GetComponent<TextMeshPro>();
        if (tmp != null) tmp.text = messageInput.text;
        Debug.Log("Content set to: " + messageInput.text);
    }
    else
    {
        Debug.LogWarning("content transform not found on postText prefab!");
    }

    Transform timestampTransform = tag.transform.Find("bubble/timestamp");
    if (timestampTransform != null)
    {
        TextMeshPro tmp = timestampTransform.GetComponent<TextMeshPro>();
        if (tmp != null) tmp.text = System.DateTime.Now.ToString("MMM dd, yyyy h:mm tt");
    }

    Hide();
}
    // Called when user taps outside the window
    public void OnBackgroundTapped()
    {
        Hide();
    }
}
