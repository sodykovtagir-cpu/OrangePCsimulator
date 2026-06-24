using UnityEngine;
using UnityEngine.EventSystems;

public class TouchPad : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IDragHandler           // ← добавлено
{
    public string horizontalAxisName;
    public string verticalAxisName;
    public float sensitivity;
    public bool firstTouch;

    private bool dragging;
    private int id = int.MinValue;  // ← безопасное начальное значение

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!gameObject.activeInHierarchy) return;
        if (!firstTouch || !dragging)
        {
            dragging = true;
            id = eventData.pointerId;
        }
    }

    // Вызывается EventSystem'ом при каждом движении пальца/мыши
    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || eventData.pointerId != id) return;

        Vector2 delta = eventData.delta;  // ← delta из EventSystem, работает везде
        InputManager.UpdateAxis(horizontalAxisName, delta.x * sensitivity);
        InputManager.UpdateAxis(verticalAxisName, delta.y * sensitivity);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (dragging && eventData.pointerId == id)
        {
            dragging = false;
            InputManager.UpdateAxis(horizontalAxisName, 0f);
            InputManager.UpdateAxis(verticalAxisName, 0f);
        }
    }

    private void OnDisable()
    {
        dragging = false;
        InputManager.UnregisterAxis(horizontalAxisName);
        InputManager.UnregisterAxis(verticalAxisName);
    }
}