using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class TextInputWindow : MonoBehaviour
{
    [Header("References")]
    public TMP_InputField messageInput;
    public Button btnOK;
    public Button btnCancel;
    public GameObject postTextPrefab;

    [Header("Fade Settings")]
    public float fadeDuration = 0.25f;

    private CanvasGroup canvasGroup;
    private Vector3 dropPosition;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (btnOK != null) btnOK.onClick.AddListener(OnOKPressed);
        if (btnCancel != null) btnCancel.onClick.AddListener(OnCancelPressed);
    }

    public void Show(Vector3 position)
    {
        dropPosition = position;
        messageInput.text = "";
        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    public void Hide()
    {
        StopAllCoroutines();
        StartCoroutine(FadeOut());
    }

    IEnumerator FadeIn()
    {
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
        yield return null;
        messageInput.ActivateInputField();
    }

    IEnumerator FadeOut()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    void OnOKPressed()
    {
        if (string.IsNullOrEmpty(messageInput.text))
        {
            Hide();
            return;
        }

        if (postTextPrefab == null)
        {
            Debug.LogError("postTextPrefab is not assigned!");
            Hide();
            return;
        }

        // Place tag in world in front of camera
        Camera cam = Camera.main;
        Vector3 worldPos = cam.transform.position + cam.transform.forward * 2f;
        GameObject tag = Instantiate(postTextPrefab, worldPos, Quaternion.identity);

        // Set text content
        Transform contentTransform = tag.transform.Find("bubble/content");
        if (contentTransform != null)
        {
            TextMeshPro tmp = contentTransform.GetComponent<TextMeshPro>();
            if (tmp != null) tmp.text = messageInput.text;
        }
        else
            Debug.LogWarning("content transform not found on postText prefab!");

        // Set timestamp
        Transform timestampTransform = tag.transform.Find("bubble/timestamp");
        if (timestampTransform != null)
        {
            TextMeshPro tmp = timestampTransform.GetComponent<TextMeshPro>();
            if (tmp != null) tmp.text = System.DateTime.Now.ToString("MMM dd, yyyy h:mm tt");
        }

        Hide();
    }

    void OnCancelPressed()
    {
        Debug.Log("Text input cancelled.");
        Hide();
    }
}