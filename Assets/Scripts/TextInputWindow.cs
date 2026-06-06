// =============================================================================
// File:        TextInputWindow.cs
// Author:      Bryan Wilcutt
// Date Started: (original)
// Description: Modal text input window for creating postText tags. Fades in/out,
//              captures user message, and instantiates the postText prefab in
//              world space. TagSelectionManager handles tap selection via
//              screen-space proximity — no collider needed.
// =============================================================================

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

    // -------------------------------------------------------------------------
    // Function:    Awake
    // Inputs:      None
    // Outputs:     None
    // Description: Initializes CanvasGroup, hides window, wires button listeners.
    // -------------------------------------------------------------------------
    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha          = 0f;
        canvasGroup.interactable   = false;
        canvasGroup.blocksRaycasts = false;

        if (btnOK     != null) btnOK.onClick.AddListener(OnOKPressed);
        if (btnCancel != null) btnCancel.onClick.AddListener(OnCancelPressed);
    }

    // -------------------------------------------------------------------------
    // Function:    Show
    // Inputs:      position — world-space drop position for the tag
    // Outputs:     None
    // Description: Stores drop position and fades the window in.
    // -------------------------------------------------------------------------
    public void Show(Vector3 position)
    {
        dropPosition = position;
        messageInput.text = "";
        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    // -------------------------------------------------------------------------
    // Function:    Hide
    // Inputs:      None
    // Outputs:     None
    // Description: Fades the window out.
    // -------------------------------------------------------------------------
    public void Hide()
    {
        StopAllCoroutines();
        StartCoroutine(FadeOut());
    }

    // -------------------------------------------------------------------------
    // Function:    FadeIn
    // Inputs:      None
    // Outputs:     IEnumerator (coroutine)
    // Description: Animates alpha from 0 to 1, then activates the input field.
    // -------------------------------------------------------------------------
    IEnumerator FadeIn()
    {
        canvasGroup.interactable   = true;
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

    // -------------------------------------------------------------------------
    // Function:    FadeOut
    // Inputs:      None
    // Outputs:     IEnumerator (coroutine)
    // Description: Animates alpha from 1 to 0 and disables interaction.
    // -------------------------------------------------------------------------
    IEnumerator FadeOut()
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha          = 0f;
        canvasGroup.interactable   = false;
        canvasGroup.blocksRaycasts = false;
    }

    // -------------------------------------------------------------------------
    // Function:    OnOKPressed
    // Inputs:      None
    // Outputs:     None
    // Description: Instantiates postText prefab at camera-forward position,
    //              sets text content and timestamp.
    // -------------------------------------------------------------------------
    void OnOKPressed()
    {
        if (string.IsNullOrEmpty(messageInput.text))
        {
            Hide();
            return;
        }

        if (postTextPrefab == null)
        {
            Debug.LogError("TextInputWindow: postTextPrefab is not assigned!");
            Hide();
            return;
        }

        // Place tag in world in front of camera
        Camera cam       = Camera.main;
        Vector3 worldPos = cam.transform.position + cam.transform.forward * 2f;
        GameObject tag   = Instantiate(postTextPrefab, worldPos, Quaternion.identity);

        // Set text content
        Transform contentTransform = tag.transform.Find("bubble/content");
        if (contentTransform != null)
        {
            TextMeshPro tmp = contentTransform.GetComponent<TextMeshPro>();
            if (tmp != null) tmp.text = messageInput.text;
        }
        else
            Debug.LogWarning("TextInputWindow: bubble/content not found on postText prefab!");

        // Set timestamp
        Transform timestampTransform = tag.transform.Find("bubble/timestamp");
        if (timestampTransform != null)
        {
            TextMeshPro tmp = timestampTransform.GetComponent<TextMeshPro>();
            if (tmp != null) tmp.text = System.DateTime.Now.ToString("MMM dd, yyyy h:mm tt");
        }

        // Reset debounce so the placement tap doesn't immediately select this tag
        if (TagSelectionManager.Instance != null)
            TagSelectionManager.Instance.ResetDebounce();

        Hide();
    }

    // -------------------------------------------------------------------------
    // Function:    OnCancelPressed
    // Inputs:      None
    // Outputs:     None
    // Description: Cancels text entry and hides the window.
    // -------------------------------------------------------------------------
    void OnCancelPressed()
    {
        Debug.Log("TextInputWindow: Text input cancelled.");
        Hide();
    }
}
