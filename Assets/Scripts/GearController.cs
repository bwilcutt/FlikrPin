using UnityEngine;
using UnityEngine.UI;

public class GearController : MonoBehaviour
{
    [Header("Settings Panel")]
    public GameObject settingsPanel;

    private bool isPanelOpen = false;

    void Start()
    {
        // Make sure panel is hidden at start
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // Add click listener to this gear icon
        Button btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(TogglePanel);
    }

    public void TogglePanel()
    {
        isPanelOpen = !isPanelOpen;
        settingsPanel.SetActive(isPanelOpen);
    }

    // Clicking outside the panel closes it
    void Update()
    {
        if (isPanelOpen && Input.GetMouseButtonDown(0))
        {
            // Check if click was outside the settings panel
            RectTransform rt = settingsPanel.GetComponent<RectTransform>();
            if (!RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition))
            {
                isPanelOpen = false;
                settingsPanel.SetActive(false);
            }
        }
    }
}