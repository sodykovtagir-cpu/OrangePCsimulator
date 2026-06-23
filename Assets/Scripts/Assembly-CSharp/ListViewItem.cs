using UnityEngine;

public struct ListViewItem
{
	public string text;

	public Sprite icon;

	public ListViewItem(string text, Sprite icon = null)
	{
		this.text = text;
		this.icon = icon;
	}
}
