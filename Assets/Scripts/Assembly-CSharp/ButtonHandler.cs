using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonHandler : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler
{
	public string Name;

	public Graphic graphic;

	public Color normalColor;

	public Color pressedColor;

	public void OnPointerEnter(PointerEventData eventData)
	{
		graphic.color = pressedColor;
		InputManager.SetButtonDown(Name);
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		graphic.color = normalColor;
		InputManager.SetButtonUp(Name);
	}

	private void OnDisable()
	{
		graphic.color = normalColor;
		InputManager.SetButtonUp(Name);
	}
}
