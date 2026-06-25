using UnityEngine;
using UnityEngine.EventSystems;

public class TouchPad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public string horizontalAxisName;
    public string verticalAxisName;
    public float sensitivity;
    public bool firstTouch;

    private bool dragging;
    private int id = -1;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!gameObject.activeInHierarchy) return;

        dragging = true;
        id = eventData.pointerId;
    }

    private void Update()
    {
        if (!dragging)
            return;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            if (touch.fingerId == id)
            {
                Vector2 delta = touch.deltaPosition;

                InputManager.UpdateAxis(horizontalAxisName, delta.x * sensitivity);
                InputManager.UpdateAxis(verticalAxisName, delta.y * sensitivity);
                return;
            }
        }

        InputManager.UpdateAxis(horizontalAxisName, 0f);
        InputManager.UpdateAxis(verticalAxisName, 0f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == id)
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