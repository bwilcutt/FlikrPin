using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Swipe left to hide the sidebar, swipe right from left edge to show it.
/// Uses the New Input System (UnityEngine.InputSystem).
/// Attach to SidebarPanel.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SidebarController : MonoBehaviour
{
    [Header("Animation")]
    public float slideDuration = 0.22f;
    public AnimationCurve slideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Swipe Detection")]
    public float swipeMinDeltaX = 15f;
    public float swipeMaxDeltaY = 200f;
    public float edgeSwipeWidth = 40f;

    private RectTransform rt;
    private float panelWidth;
    private bool isVisible   = true;
    private bool isAnimating = false;

    // Mouse tracking
    private Vector2 mouseStart;
    private bool mouseTracking = false;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        Canvas.ForceUpdateCanvases();
        panelWidth = rt.rect.width;
        if (panelWidth <= 0f) panelWidth = 142f;
        Debug.Log($"SidebarController Awake — panelWidth={panelWidth}");
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        HandleMouse();
        HandleTouch();
    }

    // ── Mouse (editor / standalone) ──────────────────────────────────────

    void HandleMouse()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        if (IsAnyWindowOpen()) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            mouseStart   = mouse.position.ReadValue();
            mouseTracking = true;
            Debug.Log($"Mouse down at {mouseStart}");
        }

        if (mouseTracking && mouse.leftButton.wasReleasedThisFrame)
        {
            Vector2 delta = mouse.position.ReadValue() - mouseStart;
            Debug.Log($"Mouse up — delta={delta}");
            mouseTracking = false;
            EvaluateSwipe(delta, mouseStart);
        }
    }

    // ── Touch ────────────────────────────────────────────────────────────

[Header("UI Windows")]
public GameObject postTypeWindow;
public GameObject textInputWindow;

bool IsAnyWindowOpen()
{
    if (postTypeWindow != null && postTypeWindow.activeSelf) return true;
    if (textInputWindow != null && textInputWindow.activeSelf) return true;
    return false;
}

void HandleTouch()
{
    if (IsAnyWindowOpen()) return;

    foreach (var touch in Touch.activeTouches)
    {
        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            mouseStart    = touch.startScreenPosition;
            mouseTracking = true;
            Debug.Log($"Touch began at {mouseStart}");
        }
        else if (mouseTracking &&
                 (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                  touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled))
        {
            Vector2 delta = touch.screenPosition - mouseStart;
            Debug.Log($"Touch ended — delta={delta}");
            mouseTracking = false;
            EvaluateSwipe(delta, mouseStart);
        }
    }
}
    // ── Swipe logic ──────────────────────────────────────────────────────

    void EvaluateSwipe(Vector2 delta, Vector2 startPos)
    {
        Debug.Log($"EvaluateSwipe: deltaX={delta.x:F1} deltaY={delta.y:F1} " +
                  $"startX={startPos.x:F1} isVisible={isVisible}");

        if (Mathf.Abs(delta.y) > swipeMaxDeltaY)
        {
            Debug.Log("Rejected — too vertical");
            return;
        }
        if (Mathf.Abs(delta.x) < swipeMinDeltaX)
        {
            Debug.Log("Rejected — too short");
            return;
        }

        if (delta.x < 0 && isVisible)
        {
            Debug.Log("Hiding sidebar.");
            Hide();
        }
        else if (delta.x > 0 && !isVisible && startPos.x <= edgeSwipeWidth)
        {
            Debug.Log("Showing sidebar.");
            Show();
        }
    }

    // ── Public API ───────────────────────────────────────────────────────

    public void Show()   => SetVisible(true);
    public void Hide()   => SetVisible(false);
    public void Toggle() => SetVisible(!isVisible);

    void SetVisible(bool show)
    {
        if (isAnimating || isVisible == show) return;
        isVisible = show;
        StopAllCoroutines();
        StartCoroutine(Slide(show));
    }

    IEnumerator Slide(bool show)
    {
        isAnimating = true;
        float startX  = rt.anchoredPosition.x;
        float endX    = show ? 0f : -panelWidth;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = slideCurve.Evaluate(Mathf.Clamp01(elapsed / slideDuration));
            rt.anchoredPosition = new Vector2(
                Mathf.Lerp(startX, endX, t),
                rt.anchoredPosition.y);
            yield return null;
        }

        rt.anchoredPosition = new Vector2(endX, rt.anchoredPosition.y);
        isAnimating = false;
        Debug.Log($"Slide complete — x={rt.anchoredPosition.x}");
    }
}
