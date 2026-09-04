using System;
using System.Collections;
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

		protected bool maximized;

		private bool minimized;

		protected RectTransform rect;

		protected Vector2 defaultSize;

		// Сохранённое состояние окна перед разворотом на весь экран.
		private Vector2 restoreAnchorMin;
		private Vector2 restoreAnchorMax;
		private Vector2 restorePivot;
		private Vector2 restorePosition;
		private Vector2 restoreSize;
		private bool hasRestoreState;

		protected bool canDrag = true;

		protected bool canMaximize = true;

		private bool closing;

		// Отступы полноэкранного окна от краёв экрана (снизу — под панель задач).
		private const float MaximizeSideInset = 6f;
		private const float MaximizeTopInset = 6f;
		private const float MaximizeBottomInset = 58f;

		protected virtual bool ShowMenuBar => true;

		public virtual bool SingleInstance => true;

		public virtual bool CanOpenExtension(string ext) => false;

		public bool IsDraggable => canDrag;

		public bool IsMaximizable => canMaximize;

		public bool IsMaximized => maximized;

		public bool IsMinimized => minimized;

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
			var window = transform as RectTransform;
			if (isActiveAndEnabled)
				StartCoroutine(WindowChrome.PlayOpen(window));
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
				// Запоминаем текущее состояние, чтобы потом восстановить.
				restoreAnchorMin = rect.anchorMin;
				restoreAnchorMax = rect.anchorMax;
				restorePivot = rect.pivot;
				restorePosition = rect.anchoredPosition;
				restoreSize = rect.sizeDelta;
				hasRestoreState = true;

				// Растягиваем окно на весь экран (с отступами и без панели задач).
				var parent = rect.parent as RectTransform;
				var center = new Vector2(0.5f, 0.5f);
				rect.pivot = center;
				rect.anchorMin = center;
				rect.anchorMax = center;

				Vector2 size;
				Vector2 pos;
				if (parent != null)
				{
					var s = parent.rect.size;
					size = new Vector2(
						Mathf.Max(200f, s.x - MaximizeSideInset * 2f),
						Mathf.Max(140f, s.y - MaximizeTopInset - MaximizeBottomInset));
					pos = new Vector2(0f, (MaximizeBottomInset - MaximizeTopInset) * 0.5f);
				}
				else
				{
					size = new Vector2(900f, 600f);
					pos = Vector2.zero;
				}
				rect.sizeDelta = size;
				rect.anchoredPosition = pos;

				SetDraggable(false);
				if (windowState != null && normalSprite != null) windowState.sprite = normalSprite;
			}
			else
			{
				RestoreWindowRect();
				SetDraggable(true);
				if (windowState != null && maximizeSprite != null) windowState.sprite = maximizeSprite;
			}
		}

		/// <summary>
		/// Сворачивает окно (прячет его, в таскбаре остаётся кнопка восстановления).
		/// </summary>
		public virtual void Minimize()
		{
			if (closing) return;
			minimized = true;
			gameObject.SetActive(false);
			var os = system;
			if (os != null) os.OnAppMinimized(this);
		}

		/// <summary>
		/// Разворачивает свёрнутое окно обратно и выводит его на передний план.
		/// </summary>
		public virtual void Restore()
		{
			if (closing) return;
			bool wasHidden = minimized;
			minimized = false;
			if (!gameObject.activeSelf)
				gameObject.SetActive(true);
			if (wasHidden)
			{
				var os = system;
				if (os != null) os.OnAppRestored(this);
			}
			var rt = transform as RectTransform;
			if (rt != null) rt.SetAsLastSibling();
		}

		private void RestoreWindowRect()
		{
			if (rect == null) return;
			if (hasRestoreState)
			{
				rect.anchorMin = restoreAnchorMin;
				rect.anchorMax = restoreAnchorMax;
				rect.pivot = restorePivot;
				rect.sizeDelta = restoreSize;
				rect.anchoredPosition = restorePosition;
			}
			else
			{
				var center = new Vector2(0.5f, 0.5f);
				rect.anchorMin = center;
				rect.anchorMax = center;
				rect.pivot = center;
				rect.sizeDelta = defaultSize;
				rect.anchoredPosition = Vector2.zero;
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
			if (closing) return;
			closing = true;
			if (isActiveAndEnabled)
				StartCoroutine(CloseAnimated());
			else
				FinishClose();
		}

		private IEnumerator CloseAnimated()
		{
			yield return WindowChrome.PlayClose(transform as RectTransform);
			FinishClose();
		}

		private void FinishClose()
		{
			var obj = gameObject;
			Destroy(obj);
			var cb = AppClosed;
			if (cb != null) cb();
		}
	}
}
