using System;
using System.Runtime.CompilerServices;
using PC.Component.Software.OS;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public abstract class App : MonoBehaviour
	{
		[Serializable]
		public class MenuItem
		{
			public Sprite icon;

			public UnityEvent onClick;
		}

		[SerializeField]
		private string appName;

		[SerializeField]
		private Sprite icon;

		[SerializeField]
		private string fileName;

		[SerializeField]
		private Sprite fileIcon;

		public int size;

		public MenuItem[] MenuBar;

		protected OS.OperatingSystem system;

		[SerializeField]
		private Sprite maximizeSprite;

		[SerializeField]
		private Sprite normalSprite;

		[SerializeField]
		private Image windowState;

		private bool maximized;

		protected RectTransform rect;

		private Vector2 defaultSize;

		protected virtual bool ShowMenuBar => true;

		public Sprite FileIcon
		{
			get
			{
				return fileIcon;
			}
			private set
            {
				fileIcon = value;
            }
		}

		public string FileName
		{
			get
			{
				return fileName;
			}
			private set
            {
				fileName = value;
            }
		}

		public string AppName => appName;

		public Sprite Icon => icon;

		public event Action AppClosed;

		public void Init(OS.OperatingSystem system)
        {
			this.system = system;
        }

		public virtual void Open(string content)
		{
			if (!ShowMenuBar) return;
			var os = system;
			if (os != null) os.ShowMenuBar(this);
		}

		protected virtual void Start()
		{
			rect = GetComponent<RectTransform>();
			if (rect != null) defaultSize = rect.sizeDelta;
		}

		public void Maximize()
		{
			var wasMaximized = maximized;
			maximized = !wasMaximized;

			if (!wasMaximized)
			{
				rect.anchorMin = Vector2.zero;
				rect.anchorMax = Vector2.one;
				rect.sizeDelta = Vector2.zero;
				windowState.sprite = normalSprite;
			}
			else
			{
				var center = new Vector2(0.5f, 0.5f);
				rect.anchorMin = center;
				rect.anchorMax = center;
				rect.sizeDelta = defaultSize;
				windowState.sprite = maximizeSprite;
			}
		}

		public void SetDefaultSize(Vector2 size)
		{
			defaultSize = size;
			if (maximized) return;
			var r = rect;
			if (r != null) r.sizeDelta = size;
		}

		public virtual void Close()
		{
			var obj = gameObject;
			Destroy(obj);
			var cb = AppClosed;
			if (cb != null) cb();
		}
	}
}
