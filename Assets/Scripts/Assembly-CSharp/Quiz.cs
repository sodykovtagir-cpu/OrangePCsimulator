using UnityEngine;
using UnityEngine.EventSystems;

public class Quiz : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
{
	[SerializeField]
	private string[] links;

	[SerializeField]
	private ConfirmationDialog dialog;

	private static int count;

	public void Count()
	{
		count++;
		if (count > 2)
		{
			count = 0;
			var dlg = dialog;
			var cb = new System.Action(Play);
			if (dlg != null) dlg.Show(cb);
		}
	}

	public void Play()
	{
		if (links == null || links.Length == 0) return;
		int i = Random.Range(0, links.Length);
		Application.OpenURL(links[i]);
	}
}
