using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonText : MonoBehaviour, IPointerDownHandler, IEventSystemHandler, IPointerUpHandler
{
	[SerializeField]
	private Color pressedColor = new Color(0, 0, 0, 1f);

	private Text text;

	private Color oldColor;

	private void Start()
	{
		text = GetComponentInChildren<Text>();
		if (text != null) oldColor = text.color;
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		if (text != null) text.color = pressedColor;
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		if (text != null) text.color = oldColor;
	}
}
