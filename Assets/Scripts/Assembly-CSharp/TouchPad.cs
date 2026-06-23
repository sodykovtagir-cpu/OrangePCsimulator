using UnityEngine;
using UnityEngine.EventSystems;

public class TouchPad : MonoBehaviour, IPointerDownHandler, IEventSystemHandler, IPointerUpHandler
{
	public string horizontalAxisName;

	public string verticalAxisName;

	public float sensitivity;

	public bool firstTouch;

	private bool dragging;

	private int id;

	public void OnPointerDown(PointerEventData eventData)
	{
		if (gameObject != null && gameObject.activeInHierarchy)
		{
			if (!firstTouch || !dragging)
			{
				dragging = true;
				if (eventData != null) id = eventData.pointerId;
			}
		}
	}

	private void Update()
	{
		if (!dragging || id == -1) return;

		int touchCount = Input.touchCount;
		if (touchCount <= 0) return;

		for (int i = 0; i < touchCount; i++)
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
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		if (dragging && eventData != null && eventData.pointerId == id)
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
