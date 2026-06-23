using UnityEngine;
using UnityEngine.EventSystems;

public class OpenLink : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
{
	[SerializeField]
	private string url;

	public void OnPointerClick(PointerEventData eventData)
	{
		Application.OpenURL(url);
	}
}
