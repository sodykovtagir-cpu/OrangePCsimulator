using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.OS
{
	public class OpenDialog : MonoBehaviour
	{
		[SerializeField]
		private OperatingSystem system;

		[SerializeField]
		private ListView listView;

		[SerializeField]
		private Button selectButton;

		private File[] files;

		private Action<File> fileSelected;

		private void Awake()
		{
			if (listView != null) listView.SelectedIndexChanged += SelectedIndexChanged;
		}

		private void SelectedIndexChanged(int index)
		{
			var lv = listView;
			var btn = selectButton;
			if (lv != null && btn != null) btn.interactable = lv.SelectedIndex != -1;
		}

		public void SelectFile(string extension, Action<File> fileSelected)
		{
			SelectFile(new[] { extension }, fileSelected);
		}

		public void SelectFile(string[] extensions, Action<File> fileSelected)
		{
			this.fileSelected = fileSelected;

			var lv = listView;
			if (lv == null) return;
			lv.Clear();

			var sys = system;
			if (sys == null) return;

			var all = sys.AllStorage;
			if (all == null || all.Count == 0) return;

			var storage = all[0];
			var src = storage != null ? storage.files : null;
			if (src == null) return;

			var list = new List<File>();
			bool any = extensions == null || extensions.Length == 0;
			if (!any)
			{
				for (int i = 0; i < extensions.Length; i++)
				{
					if (extensions[i] == "*") { any = true; break; }
				}
			}

			for (int i = 0; i < src.Count; i++)
			{
				var f = src[i];
				if (f == null || f.hidden) continue;
				if (any || MatchesAny(f.Extension(), extensions)) list.Add(f);
			}

			files = list.ToArray();

			for (int i = 0; i < files.Length; i++)
			{
				var f = files[i];
				var icon = sys.GetFileSprite(f.path);
				lv.Add(new ListViewItem(f.path, icon));
			}

			var go = gameObject;
			if (go != null) go.SetActive(true);
		}

		private static bool MatchesAny(string ext, string[] extensions)
		{
			if (extensions == null) return false;
			for (int i = 0; i < extensions.Length; i++)
			{
				if (string.Equals(ext, extensions[i], StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		public void Select()
		{
			var cb = fileSelected;
			if (cb != null)
			{
				var lv = listView;
				var arr = files;
				int i = lv != null ? lv.SelectedIndex : -1;
				if (arr != null && i >= 0 && i < arr.Length) cb(arr[i]);
			}
			var go = gameObject;
			if (go != null) go.SetActive(false);
		}

		public void Cancel()
		{
			var go = gameObject;
			if (go != null) go.SetActive(false);
		}
	}
}
