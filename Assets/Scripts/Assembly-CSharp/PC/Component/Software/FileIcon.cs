using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class FileIcon : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
	{
		[SerializeField]
		private Image img;

		[SerializeField]
		private Text nameText;

		private Action<File> callback;

		public File File { get; private set; }

		public Sprite Sprite
		{
			set
            {
				img.sprite = value;
            }
		}

		public void Init(File file, Action<File> callback)
		{
			this.callback = callback;
			this.File = file;
			if (file == null) return;
			if (file.hidden)
			{
				var go = gameObject;
				if (go != null) go.SetActive(false);
			}
			var t = nameText;
			if (t != null) t.text = File.NameWithoutExtension(file.path);
		}

		public void Open()
		{
			var cb = callback;
			if (cb != null) cb(File);
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			if (eventData == null) return;
			if (eventData.dragging) return;
			var cb = callback;
			if (cb != null) cb(File);
		}
	}
}
