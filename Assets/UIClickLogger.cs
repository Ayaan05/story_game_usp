using UnityEngine;
using UnityEngine.EventSystems;

// Attach this to a UI Button (or any UI element) to log pointer events for debugging
public class UIClickLogger : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"UIClickLogger: {gameObject.name} OnPointerClick (button was clicked)");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"UIClickLogger: {gameObject.name} OnPointerDown");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log($"UIClickLogger: {gameObject.name} OnPointerEnter");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log($"UIClickLogger: {gameObject.name} OnPointerExit");
    }
}
