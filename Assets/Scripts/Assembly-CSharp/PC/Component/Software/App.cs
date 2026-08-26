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

		protected bool canDrag = true;

		protected bool canMaximize = true;

		protected virtual bool ShowMenuBar => true;

		public virtual bool SingleInstance => true;

		public bool IsDraggable => canDrag;

		public bool IsMaximizable => canMaximize;

		public bool IsMaximized => maximized;

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

		public virtual void Maximize()
		{
			if (!canMaximize && !maximized) return;
			if (rect == null) rect = GetComponent<RectTransform>();
			if (rect == null) return;

			var wasMaximized = maximized;
			maximized = !wasMaximized;

			if (!wasMaximized)
			{
				rect.anchorMin = Vector2.zero;
				rect.anchorMax = Vector2.one;
				rect.anchoredPosition = Vector2.zero;
				rect.sizeDelta = Vector2.zero;
				if (windowState != null && normalSprite != null) windowState.sprite = normalSprite;
			}
			else
			{
				var center = new Vector2(0.5f, 0.5f);
				rect.anchorMin = center;
				rect.anchorMax = center;
				rect.sizeDelta = defaultSize;
				if (windowState != null && maximizeSprite != null) windowState.sprite = maximizeSprite;
			}
		}

		protected void FitToDesktop()
		{
			if (rect == null) return;
			var parent = rect.parent as RectTransform;
			var center = new Vector2(0.5f, 0.5f);
			rect.anchorMin = center;
			rect.anchorMax = center;
			rect.pivot = center;
			if (parent == null)
			{
				rect.sizeDelta = new Vector2(800, 500);
				rect.anchoredPosition = Vector2.zero;
				return;
			}
			float padX = 10f;
			float padTop = 8f;
			float padBottom = 52f;
			var s = parent.rect.size;
			rect.sizeDelta = new Vector2(Mathf.Max(200f, s.x - padX * 2f), Mathf.Max(140f, s.y - padTop - padBottom));
			rect.anchoredPosition = new Vector2(0f, (padBottom - padTop) * 0.5f);
		}

		public virtual void SetDraggable(bool on)
		{
			canDrag = on;
			var drags = GetComponentsInChildren<WindowDrag>(true);
			if (drags == null) return;
			for (int i = 0; i < drags.Length; i++)
			{
				if (drags[i] != null) drags[i].enabled = on;
			}
		}

		public virtual void SetMaximizable(bool on)
		{
			canMaximize = on;
			if (!on && maximized) Maximize();
			if (windowState != null)
			{
				var btn = windowState.GetComponent<Button>();
				if (btn != null) btn.interactable = on;
				windowState.gameObject.SetActive(on);
			}
		}

		protected void SetWindowStateImage(Image img)
		{
			windowState = img;
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
