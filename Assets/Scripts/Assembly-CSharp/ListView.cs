using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class ListView : MonoBehaviour
{
	[SerializeField]
	private ListViewElement itemPrefab;

	[SerializeField]
	private Transform itemParent;

	private int selectedIndex;

	private List<ListViewElement> items = new List<ListViewElement>();

	public int SelectedIndex
	{
		get
		{
			return selectedIndex;
		}
		private set
        {
			selectedIndex = value;
        }
	}

	public event Action<int> SelectedIndexChanged;

	public void Add(ListViewItem item)
	{
		var element = Instantiate(itemPrefab, itemParent);
		element.Init();
		if (element.graphic != null) element.graphic.CrossFadeAlpha(0f, 0f, true);
		if (element.text != null) element.text.text = item.text;
		if (item.icon != null && element.icon != null) element.icon.sprite = item.icon;
		items.Add(element);
		int index = items.Count - 1;
		var btn = element.Button;
		if (btn != null) btn.onClick.AddListener(() =>
		{
			selectedIndex = index;
			SelectedIndexChanged?.Invoke(selectedIndex);
		});
	}

	public void AddRange(IEnumerable<ListViewItem> items)
	{
		if (items == null) return;
		foreach (var item in items) Add(item);
	}

	public void RemoveAt(int index)
	{
		if (index < 0) return;
		if (items == null) return;
		if (index >= items.Count) return;

		var element = items[index];
		if (element != null)
		{
			var go = element.gameObject;
			if (go != null) Destroy(go);
		}

		items.RemoveAt(index);

		if (selectedIndex != -1)
		{
			selectedIndex = -1;
			SelectedIndexChanged?.Invoke(selectedIndex);
		}

		UpdateButtonListeners(index);
	}

	public void Clear()
	{
		if (items == null) return;
		foreach (var e in items)
			if (e != null && e.gameObject != null) Destroy(e.gameObject);
		items.Clear();
		if (selectedIndex != -1)
		{
			selectedIndex = -1;
			SelectedIndexChanged?.Invoke(selectedIndex);
		}
	}

	private void UpdateSelection(int index)
	{
		if (items == null) return;

		foreach (var e in items)
			if (e != null && e.graphic != null)
				e.graphic.CrossFadeAlpha(0f, 0f, true);

		if (index < 0 || index >= items.Count) return;

		var selected = items[index];
		if (selected != null && selected.graphic != null)
			selected.graphic.CrossFadeAlpha(1f, 0f, true);

		if (selectedIndex != index)
		{
			selectedIndex = index;
			SelectedIndexChanged?.Invoke(selectedIndex);
		}
	}

	private void UpdateButtonListeners(int i)
	{
		if (items == null) return;
		for (; i < items.Count; i++)
		{
			var e = items[i];
			var btn = e != null ? e.Button : null;
			if (btn == null) continue;
			btn.onClick.RemoveAllListeners();
			var idx = i;
			btn.onClick.AddListener(() =>
			{
				selectedIndex = idx;
				SelectedIndexChanged?.Invoke(selectedIndex);
			});
		}
	}
}
