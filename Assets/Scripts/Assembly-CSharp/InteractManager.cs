using System.Collections.Generic;
using UnityEngine;

public class InteractManager : MonoBehaviour
{
	[SerializeField]
	private GameObject[] selections;

	private List<Interact> interacts = new List<Interact>();

	public static InteractManager Instance { get; private set; }

	private void Awake()
	{
		Instance = this;
	}

	private void Start()
	{
		if (selections == null) return;
		for (int i = 0; i < selections.Length; i++)
		{
			var go = selections[i];
			if (go == null) continue;
			var btn = go.GetComponent<UnityEngine.UI.Button>();
			if (btn == null) continue;
			var n = go.name;
			btn.onClick.AddListener(() => Interact(n));
		}
	}

	public void AddToList(Interact interact)
	{
		interacts.Add(interact);
		if (selections == null) return;
		foreach (var go in selections)
		{
			if (go != null && interact != null && go.name == interact.Id) go.SetActive(true);
		}
	}

	public void RemoveFromList(Interact interact)
	{
		if (interacts == null) return;
		interacts.Remove(interact);
		if (interact == null || selections == null) return;

		bool any = false;
		for (int i = 0; i < interacts.Count; i++)
		{
			var x = interacts[i];
			if (x != null && x.Id == interact.Id) { any = true; break; }
		}

		if (!any)
		{
			for (int i = 0; i < selections.Length; i++)
			{
				var go = selections[i];
				if (go != null && go.name == interact.Id) go.SetActive(false);
			}
		}
	}

	private void Interact(string name)
	{
		if (interacts == null || string.IsNullOrEmpty(name)) return;
		for (int i = interacts.Count - 1; i >= 0; i--)
		{
			var it = interacts[i];
			if (it != null && it.Id == name) it.OnInteract();
		}
	}
}
