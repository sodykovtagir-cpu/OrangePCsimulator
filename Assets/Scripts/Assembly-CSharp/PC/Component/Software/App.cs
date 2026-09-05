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

		// Токен «поколения» анимации окна. Каждое новое сворачивание/
		// разворачивание/открытие увеличивает его; корутина завершает финализацию
		// (SetActive(false) и т.п.), только если её токен ещё актуален. Это чинит
		// гонку при быстром сворачивании/разворачивании, когда устаревшая корутина
		// успевала спрятать уже развёрнутое окно.
		private int animId;

		private Coroutine animCoroutine;

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
		private Vector2 minOffsetMin;
		private Vector2 minOffsetMax;
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
			// Анимацию открытия всегда запускаем (PlayOpen сам ставит стартовые
			// alpha/scale). Раньше она была только под isActiveAndEnabled, и окна,
			// которые догружают контент и активируются с задержкой (например,
			// онлайн-магазин), появлялись «скачком» без анимации.
			if (window != null)
				BeginAnim(OpenRoutine(window), true);
        }

		private IEnumerator OpenRoutine(RectTransform window)
		{
			yield return WindowChrome.PlayOpen(window);
			// Достигается только если анимацию не перебили (StopCoroutine).
			animating = false;
			animCoroutine = null;
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

				// Разворачиваем окно почти на весь экран: верх и бока в край экрана,
				// а низ заканчивается НАД панелью задач (окно не уходит под таскбар).
				float bottomInset = 0f;
				if (system != null) bottomInset = system.GetTaskbarInsetLocalHeight();
				float side = 0f;
				float top = 0f;

				rect.pivot = new Vector2(0f, 0f);
				rect.anchorMin = Vector2.zero;
				rect.anchorMax = Vector2.one;
				rect.offsetMin = new Vector2(side, bottomInset);
				rect.offsetMax = new Vector2(-side, -top);

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
				minOffsetMin = rt.offsetMin;
				minOffsetMax = rt.offsetMax;
			}

			BeginAnim(MinimizeRoutine(), true);
		}

		private IEnumerator MinimizeRoutine()
		{
			int id = animId;
			animating = true;

			// Нужна корректная раскладка до начала анимации.

			var rt = transform as RectTransform;
			var os = system;
			Vector3 target = os != null ? os.GetTaskbarIconWorldPos(this) : Vector3.zero;

			var cg = WindowChromeGroup();
			cg.alpha = 1f;

			// Сразу создаём/обновляем кнопку в таскбаре, чтобы было к чему лететь.
			if (os != null) os.OnAppMinimized(this);

			// Та же длительность и кривая, что у меню «Пуск»/открытия окон.
			const float duration = 0.2f;
			float t = 0f;
			Vector3 startPos = rt != null ? rt.position : Vector3.zero;
			Vector3 startScale = rt != null ? rt.localScale : Vector3.one;
			Vector3 endScale = startScale * 0.2f;

			while (t < duration && rt != null)
			{
				if (id != animId) yield break; // нас перебили (развернули)
				t += Mathf.Min(Time.unscaledDeltaTime, duration * 0.25f);
				float k = Mathf.Clamp01(t / duration);
				float e = 1f - (1f - k) * (1f - k); // ease-out quad
				rt.position = Vector3.Lerp(startPos, target, e);
				rt.localScale = Vector3.LerpUnclamped(startScale, endScale, e);
				cg.alpha = 1f - k * 0.9f;
				yield return null;
			}

			if (id != animId) yield break; // не финализируем устаревшую анимацию
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
				rt.offsetMin = minOffsetMin;
				rt.offsetMax = minOffsetMax;
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

			// Точку вылета (иконку в таскбаре) захватываем ДО пересоздания кнопок:
			// свежесозданная кнопка в этом кадре ещё не разложена LayoutGroup'ом
			// и вернула бы неверную позицию (у правого края, возле часов).
			Vector3 flyTarget = Vector3.zero;
			if (wasHidden && system != null)
				flyTarget = system.GetTaskbarIconWorldPos(this);

			minimized = false;
			int id = ++animId; // отменяем любую текущую анимацию окна

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
				rt.offsetMin = minOffsetMin;
				rt.offsetMax = minOffsetMax;
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
				BeginAnim(RestoreRoutine(flyTarget, id));
			}
		}

		private IEnumerator RestoreRoutine(Vector3 target, int id)
		{
			animating = true;

			var rt = transform as RectTransform;
			if (rt == null) { animating = false; yield break; }

			var cg = WindowChromeGroup();

			// Та же кривая/длительность, что у открытия окон и меню «Пуск».
			const float duration = 0.2f;
			float t = 0f;

			Vector3 endPos = rt.position;
			Vector3 endScale = rt.localScale;
			Vector3 startScale = endScale * 0.2f;

			// Стартуем прямо из иконки таскбара (до первого кадра — без мелькания).
			rt.position = target;
			rt.localScale = startScale;
			cg.alpha = 0f;

			while (t < duration)
			{
				if (id != animId) yield break; // нас перебили (свернули)
				t += Mathf.Min(Time.unscaledDeltaTime, duration * 0.25f);
				float k = Mathf.Clamp01(t / duration);
				float e = 1f - (1f - k) * (1f - k); // ease-out quad (как PlayOpen/PlayStartMenu)
				rt.position = Vector3.Lerp(target, endPos, e);
				rt.localScale = Vector3.LerpUnclamped(startScale, endScale, e);
				cg.alpha = k;
				yield return null;
			}

			if (id != animId) yield break;
			rt.position = endPos;
			rt.localScale = endScale;
			cg.alpha = 1f;
			animating = false;
		}

		/// <summary>
		/// Запускает новую анимацию окна, отменяя предыдущую (свернуть/развернуть
		/// больше не дерутся за трансформ и не прячут уже развёрнутое окно).
		/// </summary>
		private Coroutine BeginAnim(IEnumerator routine, bool newGeneration = false)
		{
			if (animCoroutine != null) StopCoroutine(animCoroutine);
			if (newGeneration) animId++;
			animating = true;
			animCoroutine = StartCoroutine(routine);
			return animCoroutine;
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
