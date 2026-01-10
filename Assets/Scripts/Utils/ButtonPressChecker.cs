using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonPressChecker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("ボタン押された");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log("ボタン離された");
    }
}
