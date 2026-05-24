using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Swipe : MonoBehaviour, IDragHandler, IEndDragHandler
{

    private Vector3 panelLocation;
    public void OnDrag(PointerEventData data){
        float difference = data.pressPosition.x - data.position.x;
    }
        
    public void OnEndDrag(PointerEventData data)
    {

    }

}
