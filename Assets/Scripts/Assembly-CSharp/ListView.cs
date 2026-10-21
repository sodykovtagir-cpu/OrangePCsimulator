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

	private int selectedIndex = -1;

	private List<ListViewElement> items = new List<ListViewElement>();

    private bool hasSelectionTint;
    private Color selectionTint;

    public void SetSelectionTint(Color tint)
    {
        selectionTint = tint;
        hasSelectionTint = true;
        for (int i = 0; i < items.Count; i++)
        {
            var image = items[i] != null ? items[i].graphic : null;
            if (image == null) continue;
            image.color = tint;
            image.CrossFadeAlpha(i == selectedIndex ? 1f : 0f, 0f, true);
        }
    }

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
        if (element.graphic != null)
        {
            if (hasSelectionTint) element.graphic.color = selectionTint;
            element.graphic.CrossFadeAlpha(0f, 0f, true);
        }
		if (element.text != null) element.text.text = item.text;
		if (item.icon != null && element.icon != null) element.icon.sprite = item.icon;
		items.Add(element);
		int index = items.Count - 1;
		var btn = element.Button;
		if (btn != null) btn.onClick.AddListener(() =>
		{
            UpdateSelection(index);
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
            foreach (var item in items)
                if (item != null && item.graphic != null)
                    item.graphic.CrossFadeAlpha(0f, 0f, true);
            selectedIndex = -1;
            SelectedIndexChanged?.Invoke(selectedIndex);
        }

		UpdateButtonListeners(index);
	}

	public void Clear()
	{
		if (items == null) return;
		foreach (var e in items)
			if (e != null && e.gameObject != null)
			{
				e.gameObject.SetActive(false);
				Destroy(e.gameObject);
			}
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

        selectedIndex = index;
        // Repeat the event on a second tap so OpenDialog can open folders.
        SelectedIndexChanged?.Invoke(selectedIndex);
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
                UpdateSelection(idx);
			});
		}
	}
}
