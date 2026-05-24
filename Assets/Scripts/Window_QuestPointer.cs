using UnityEngine;
using UnityEngine.EventSystems;

public class Window_QuestPointer : MonoBehaviour, IPointerClickHandler
{
    public PostTypeWindow postTypeWindow;

    private Vector3 tapPosition;

    public void OnPointerClick(PointerEventData eventData)
    {
        // Don't open panel if it's already open
        if (postTypeWindow != null && postTypeWindow.gameObject.activeSelf) return;

        tapPosition = eventData.position;
        if (postTypeWindow != null)
        {
            postTypeWindow.Show(tapPosition);
        }
        else
        {
            Debug.LogWarning("PostTypeWindow not assigned on Window_QuestPointer!");
        }
    }
}