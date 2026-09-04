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

		private bool animating;

		protected RectTransform rect;

		protected Vector2 defaultSize;

		// Сохранённое состояние окна перед разворотом на весь экран.
		private Vector2 restoreAnchorMin;
		private Vector2 restoreAnchorMax;
		private Vector2 restorePivot;
		private Vector2 restorePosition;
		private Vector2 restoreSize;
		private bool hasRestoreState;

		// Сохранённая раскладка на момент сворачивания (чтобы восстановить как было,
		// даже если окно было развёрнуто на весь экран).
		private Vector2 minAnchorMin;
		private Vector2 minAnchorMax;
		private Vector2 minPivot;
		private Vector2 minPosition;
		private Vector2 minSize;
		private bool wasMaximizedWhenMinimized;

		protected bool canDrag = true;

		protected bool canMaximize = true;

		private bool closing;

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

				// Разворачиваем окно НА ВЕСЬ экран без отступов.
				// Панель задач рисуется поверх окон, поэтому остаётся видимой.
				rect.pivot = new Vector2(0.5f, 0.5f);
				rect.anchorMin = Vector2.zero;
				rect.anchorMax = Vector2.one;
				rect.anchoredPosition = Vector2.zero;
				rect.sizeDelta = Vector2.zero;

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
		/// Сворачивает окно: проигрывает анимацию «улёта» в иконку панели задач,
		/// после чего прячет окно (кнопка в таскбаре остаётся).
		/// </summary>
		public virtual void Minimize()
		{
			if (closing || minimized) return;
			if (animating) return;

			minimized = true;
			wasMaximizedWhenMinimized = maximized;

			// Запоминаем текущую раскладку, чтобы вернуться к ней при разворачивании.
			var rt = rect != null ? rect : (transform as RectTransform);
			if (rt != null)
			{
				minAnchorMin = rt.anchorMin;
				minAnchorMax = rt.anchorMax;
				minPivot = rt.pivot;
				minPosition = rt.anchoredPosition;
				minSize = rt.sizeDelta;
			}

			StartCoroutine(MinimizeRoutine());
		}

		private IEnumerator MinimizeRoutine()
		{
			animating = true;

			var rt = transform as RectTransform;
			var os = system;
			Vector3 target = os != null ? os.GetTaskbarIconWorldPos(this) : Vector3.zero;

			var cg = WindowChromeGroup();
			cg.alpha = 1f;

			// Сразу создаём/обновляем кнопку в таскбаре, чтобы было к чему лететь.
			if (os != null) os.OnAppMinimized(this);

			float duration = 0.22f;
			float t = 0f;
			Vector3 startPos = rt != null ? rt.position : Vector3.zero;
			Vector3 startScale = rt != null ? rt.localScale : Vector3.one;
			Vector3 endScale = startScale * 0.12f;

			while (t < duration && rt != null)
			{
				t += Time.unscaledDeltaTime;
				float k = Mathf.Clamp01(t / duration);
				float e = 1f - (1f - k) * (1f - k); // ease-out quad
				rt.position = Vector3.Lerp(startPos, target, e);
				rt.localScale = Vector3.LerpUnclamped(startScale, endScale, e);
				cg.alpha = 1f - k * 0.9f;
				yield return null;
			}

			animating = false;
			gameObject.SetActive(false);

			// Возвращаем трансформ в исходную раскладку (визуально не видно — окно скрыто).
			if (rt != null)
			{
				rt.anchorMin = minAnchorMin;
				rt.anchorMax = minAnchorMax;
				rt.pivot = minPivot;
				rt.sizeDelta = minSize;
				rt.anchoredPosition = minPosition;
				rt.localScale = Vector3.one;
			}
			cg.alpha = 1f;
		}

		/// <summary>
		/// Разворачивает свёрнутое окно обратно и выводит его на передний план.
		/// Если окно было свёрнуто в развёрнутом состоянии — вернёт развёрнутый вид.
		/// </summary>
		public virtual void Restore()
		{
			if (closing) return;

			bool wasHidden = minimized || !gameObject.activeSelf;
			minimized = false;

			var rt = rect != null ? rect : (transform as RectTransform);

			// Раскладку восстанавливаем только если окно реально сворачивали
			// (сохранённое состояние валидно).
			if (wasHidden && rt != null)
			{
				rt.anchorMin = minAnchorMin;
				rt.anchorMax = minAnchorMax;
				rt.pivot = minPivot;
				rt.sizeDelta = minSize;
				rt.anchoredPosition = minPosition;
				rt.localScale = Vector3.one;
			}
			if (wasHidden)
				maximized = wasMaximizedWhenMinimized;

			gameObject.SetActive(true);
			if (rt != null) rt.SetAsLastSibling();

			// Синхронизируем перетаскивание и иконку кнопки с состоянием окна.
			SetDraggable(!maximized);
			if (windowState != null)
			{
				var spr = maximized ? normalSprite : maximizeSprite;
				if (spr != null) windowState.sprite = spr;
			}

			if (wasHidden)
			{
				var os = system;
				if (os != null) os.OnAppRestored(this);
				StartCoroutine(RestoreRoutine());
			}
		}

		private IEnumerator RestoreRoutine()
		{
			animating = true;

			var rt = transform as RectTransform;
			var os = system;
			Vector3 target = os != null ? os.GetTaskbarIconWorldPos(this) : Vector3.zero;

			var cg = WindowChromeGroup();

			float duration = 0.22f;
			float t = 0f;
			Vector3 endPos = rt != null ? rt.position : Vector3.zero;
			Vector3 endScale = rt != null ? rt.localScale : Vector3.one;
			Vector3 startScale = endScale * 0.12f;

			// Стартуем из иконки таскбара.
			if (rt != null)
			{
				rt.position = target;
				rt.localScale = startScale;
			}
			cg.alpha = 0.1f;

			while (t < duration && rt != null)
			{
				t += Time.unscaledDeltaTime;
				float k = Mathf.Clamp01(t / duration);
				float e = k * k; // ease-in
				rt.position = Vector3.Lerp(target, endPos, e);
				rt.localScale = Vector3.LerpUnclamped(startScale, endScale, e);
				cg.alpha = 0.1f + k * 0.9f;
				yield return null;
			}

			if (rt != null)
			{
				rt.position = endPos;
				rt.localScale = endScale;
			}
			cg.alpha = 1f;
			animating = false;
		}

		private CanvasGroup WindowChromeGroup()
		{
			var cg = GetComponent<CanvasGroup>();
			if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
			return cg;
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
