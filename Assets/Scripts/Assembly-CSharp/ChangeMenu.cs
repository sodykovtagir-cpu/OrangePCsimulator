using UnityEngine;
using UnityEngine.EventSystems;

public class ChangeMenu : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
{
	private enum Control
	{
		Back = 0,
		ByName = 1
	}

	[SerializeField]
	private string menuName;

	[SerializeField]
	private Control control;

	public void OnPointerClick(PointerEventData eventData) {
		Control control = this.control;
		MenuManager manager = GetComponentInParent<MenuManager>();
		if (manager == null)
		{
			Debug.LogWarning("No MenuManager found in parent hierarchy.");
			return;
		}
		if (control != Control.Back)
		{
			manager.ShowMenu(menuName);
			return;
		}
		manager.Back();
	}
}
