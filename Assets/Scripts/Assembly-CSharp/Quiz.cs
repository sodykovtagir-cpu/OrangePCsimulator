using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Quiz : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
{
	[SerializeField]
	private string[] links;

	[SerializeField]
	private ConfirmationDialog dialog;

	[Header("Remote quiz (from site admin panel)")]
	[Tooltip("Как часто опрашивать сервер на наличие квиза от админа (сек).")]
	[SerializeField]
	private float pollInterval = 45f;

	private static int count;

	private Text titleText;
	private Text bodyText;
	private bool busy;

	private void Start()
	{
		// Тексты Title/Body живут в панели диалога (объект dialog).
		if (dialog != null)
		{
			titleText = FindText(dialog.transform, "Title");
			bodyText = FindText(dialog.transform, "Body");
		}
		StartCoroutine(PollRemote());
	}

	private static Text FindText(Transform root, string name)
	{
		var all = root.GetComponentsInChildren<Text>(true);
		for (int i = 0; i < all.Length; i++)
			if (all[i] != null && all[i].name == name)
				return all[i];
		return null;
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		var dlg = dialog;
		var cb = new System.Action(Play);
		if (dlg != null) dlg.Show(cb);
	}

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

	/// <summary>Квиз, присланный с админ-панели сайта (админ → api.php?action=quiz).</summary>
	public void TriggerRemote(string link, string title, string body)
	{
		if (string.IsNullOrEmpty(link)) return;
		links = new string[] { link };
		if (titleText != null && !string.IsNullOrEmpty(title)) titleText.text = title;
		if (bodyText != null && !string.IsNullOrEmpty(body)) bodyText.text = body;
		var dlg = dialog;
		if (dlg != null) dlg.Show(Play);
		else Play();
	}

	/// <summary>Периодически спрашиваем сервер: админ отправил квиз? (one-shot на сервере).</summary>
	private IEnumerator PollRemote()
	{
		yield return new WaitForSeconds(3f);
		while (true)
		{
			if (WorkshopClient.Instance == null)
			{
				var go = new GameObject("WorkshopClient");
				go.AddComponent<WorkshopClient>();
			}
			var client = WorkshopClient.Instance;
			if (client != null && !busy)
			{
				busy = true;
				client.GetQuiz((r, err) =>
				{
					busy = false;
					if (err != null || r == null || !r.show) return;
					if (string.IsNullOrEmpty(r.link)) return;
					TriggerRemote(r.link, r.title, r.body);
				});
			}
			yield return new WaitForSeconds(pollInterval);
		}
	}
}
