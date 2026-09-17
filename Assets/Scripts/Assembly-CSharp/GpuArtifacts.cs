using System.Collections.Generic;
using PC.Component;
using UnityEngine;
using UnityEngine.UI;

// В UnityEngine есть свой класс Display (физический экран устройства), поэтому
// простое имя Display неоднозначно. Явно указываем, что речь о мониторе игры.
using Display = PC.Component.Display;

/// <summary>
/// Артефакты повреждённой видеокарты поверх всего экрана.
/// </summary>
/// <remarks>
/// Эффект живёт в СОБСТВЕННОМ Canvas с высоким sortingOrder, а не просто среди
/// детей канваса экрана. Это принципиально: и загрузка системы
/// (Display.ApplyScreen), и окна приложений, и меню «Пуск» зовут
/// SetAsLastSibling, то есть встают последними в списке потомков — и накрывают
/// собой всё, что было добавлено раньше. Именно поэтому в первой версии полосы
/// были не видны: они оказывались под рабочим столом.
///
/// Сбой выбирается случайно на каждый запуск компьютера, а не рисуется постоянно:
/// повреждённая карта ведёт себя непредсказуемо. Иногда всё проходит нормально,
/// иногда картинку ведёт, иногда компьютер падает в синий экран. Чем сильнее
/// разбита карта, тем реже везёт.
/// </remarks>
public class GpuArtifacts : MonoBehaviour
{
	/// <summary>Что происходит с картинкой на этой загрузке.</summary>
	private enum Glitch
	{
		/// <summary>Повезло: изображение чистое.</summary>
		None = 0,

		/// <summary>Рваные цветные полосы.</summary>
		Stripes = 1,

		/// <summary>Картинку ведёт: смещение и дрожание.</summary>
		Warp = 2,

		/// <summary>Цвета уплыли в один канал.</summary>
		ColorShift = 3,

		/// <summary>Синий экран: загрузка сорвалась.</summary>
		BlueScreen = 4
	}

	[SerializeField]
	[Tooltip("Куда класть эффекты. Обычно канвас экрана монитора.")]
	private RectTransform container;

	[SerializeField]
	[Tooltip("Сколько полос показывать при полном повреждении.")]
	private int maxStripes = 14;

	[SerializeField]
	[Tooltip("Как часто обновлять эффект, в секундах.")]
	private float refreshInterval = 0.12f;

	private readonly List<Image> stripes = new List<Image>();

	/// <summary>
	/// Порядок сортировки слоя артефактов.
	/// </summary>
	/// <remarks>
	/// Заведомо выше всего, что рисует система на экране: артефакты идут от
	/// железа, их не может перекрыть ни одно окно.
	/// </remarks>
	private const int ArtifactSortingOrder = 32000;

	private Display display;
	private RectTransform layer;
	private Image overlay;
	private Text overlayText;

	private float nextRefresh;
	private Glitch current;
	private bool decided;

	private void Awake()
	{
		display = GetComponent<Display>();

		// Если контейнер не задан в инспекторе, берём канвас экрана. Это
		// позволяет навешивать эффект из кода, не правя тринадцать префабов
		// мониторов вручную.
		if (container == null)
		{
			var canvas = GetComponentInChildren<Canvas>();
			if (canvas != null) container = canvas.GetComponent<RectTransform>();
		}
	}

	/// <summary>
	/// Самая разбитая из видеокарт, через которые идёт картинка.
	/// </summary>
	/// <remarks>
	/// Берётся максимум, а не сумма: одной сбоящей карты достаточно, чтобы
	/// испортить изображение, и складывать повреждения разных карт смысла нет.
	/// </remarks>
	private float CurrentStrength()
	{
		if (display == null || !display.Power) return 0f;

		var board = display.ConnectedBoard;
		if (board == null) return 0f;

		var cards = board.GetHardwares(HardwareType.GPU);
		if (cards == null) return 0f;

		float worst = 0f;
		for (int i = 0; i < cards.Count; i++)
		{
			var gpu = cards[i] as GPU;
			if (gpu == null) continue;

			// Мёртвая карта картинку не выдаёт вовсе — это уже не артефакты,
			// а отсутствие сигнала, им занимается сам монитор.
			if (gpu.Damaged) continue;

			if (gpu.ArtifactStrength > worst) worst = gpu.ArtifactStrength;
		}

		return worst;
	}

	/// <summary>
	/// Выбрать, как поведёт себя карта на этой загрузке.
	/// </summary>
	/// <remarks>
	/// Решение принимается ОДИН раз за загрузку, а не каждый кадр: иначе виды
	/// сбоя мелькали бы вперемешку, и это выглядело бы как мусор, а не как
	/// неисправное железо.
	///
	/// Шанс, что всё обойдётся, падает с ростом повреждения: слегка задетая
	/// карта чаще стартует нормально, добитая — почти никогда.
	/// </remarks>
	private Glitch PickGlitch(float strength)
	{
		if (Random.value > strength) return Glitch.None;

		float roll = Random.value;

		// Синий экран — самый тяжёлый исход, поэтому он заметно вероятнее у
		// сильно разбитой карты и почти не встречается у слегка задетой.
		if (roll < 0.15f * strength) return Glitch.BlueScreen;
		if (roll < 0.45f) return Glitch.Stripes;
		if (roll < 0.75f) return Glitch.Warp;
		return Glitch.ColorShift;
	}

	private void LateUpdate()
	{
		float strength = CurrentStrength();

		if (strength <= 0f)
		{
			// Загрузка кончилась или карта цела — убираем всё и забываем
			// решение, чтобы следующий запуск разыграл сбой заново.
			HideAll();
			decided = false;
			return;
		}

		if (!decided)
		{
			decided = true;
			current = PickGlitch(strength);
			ResetVisuals();
		}

		if (current == Glitch.None)
		{
			HideAll();
			return;
		}

		float now = Time.unscaledTime;
		if (now < nextRefresh) return;
		nextRefresh = now + refreshInterval;

		Redraw(strength);
	}

	private void Redraw(float strength)
	{
		// Зум перекладывает канвас и меняет режим отрисовки, окна при каждом
		// касании зовут SetAsLastSibling. Подтверждаем своё место каждый раз:
		// операции дешёвые, а иначе слой рано или поздно окажется погребён.
		var top = Parent();
		if (top != null)
		{
			top.SetAsLastSibling();

			var c = top.GetComponent<Canvas>();
			if (c != null)
			{
				c.overrideSorting = true;
				c.sortingOrder = ArtifactSortingOrder;
			}
		}

		switch (current)
		{
			case Glitch.Stripes:
				DrawStripes(strength);
				break;
			case Glitch.Warp:
				DrawWarp(strength);
				break;
			case Glitch.ColorShift:
				DrawColorShift(strength);
				break;
			case Glitch.BlueScreen:
				DrawBlueScreen();
				break;
		}
	}

	// ================= виды сбоя =================

	/// <summary>Рваные цветные полосы поперёк экрана.</summary>
	private void DrawStripes(float strength)
	{
		var parent = Parent();
		if (parent == null) return;

		int want = Mathf.Max(1, Mathf.RoundToInt(maxStripes * strength));
		EnsureStripes(want, parent);

		var size = parent.rect.size;

		for (int i = 0; i < stripes.Count; i++)
		{
			var stripe = stripes[i];
			if (stripe == null) continue;

			if (i >= want)
			{
				stripe.enabled = false;
				continue;
			}

			stripe.enabled = true;

			float h = Random.Range(2f, 6f + 18f * strength);
			float w = Random.Range(size.x * 0.25f, size.x);

			var rt = stripe.rectTransform;
			rt.sizeDelta = new Vector2(w, h);
			rt.anchoredPosition = new Vector2(
				Random.Range(-size.x * 0.5f, size.x * 0.5f),
				Random.Range(-size.y * 0.5f, size.y * 0.5f));

			var color = Random.value < 0.5f
				? new Color(Random.value, Random.value, Random.value)
				: new Color(0f, Random.value, Random.value);
			color.a = Mathf.Lerp(0.35f, 0.9f, strength);
			stripe.color = color;
		}
	}

	/// <summary>
	/// Картинку ведёт: изображение дрожит и смещается.
	/// </summary>
	/// <remarks>
	/// Двигается сам канвас загрузки, а не копия: так «плывёт» реальная
	/// картинка, включая текст BIOS, и не нужно ничего перерисовывать.
	/// </remarks>
	private void DrawWarp(float strength)
	{
		// Двигаем САМ экран, а не слой артефактов: слой прозрачный, смещать
		// его бессмысленно. Так «плывёт» настоящая картинка вместе с окнами и
		// текстом, и ничего не нужно перерисовывать.
		var screen = ScreenRoot();
		if (screen == null) return;

		float amp = 6f + 30f * strength;
		screen.anchoredPosition = new Vector2(
			Random.Range(-amp, amp), Random.Range(-amp * 0.5f, amp * 0.5f));

		// Изредка кадр ещё и подрезает по вертикали — как срыв синхронизации.
		if (Random.value < 0.25f * strength)
		{
			float squash = Random.Range(0.85f, 1f);
			screen.localScale = new Vector3(1f, squash, 1f);
		}
		else
		{
			screen.localScale = Vector3.one;
		}
	}

	/// <summary>Цвета уплыли: экран заливает одним каналом.</summary>
	private void DrawColorShift(float strength)
	{
		var img = EnsureOverlay();
		if (img == null) return;

		img.enabled = true;

		var tint = Random.value < 0.5f
			? new Color(1f, 0f, Random.Range(0.4f, 1f))
			: new Color(0f, Random.Range(0.4f, 1f), 1f);
		tint.a = Mathf.Lerp(0.15f, 0.5f, strength);
		img.color = tint;

		if (overlayText != null) overlayText.enabled = false;
	}

	/// <summary>Синий экран: загрузка сорвалась.</summary>
	private void DrawBlueScreen()
	{
		var img = EnsureOverlay();
		if (img == null) return;

		HideStripes();

		img.enabled = true;
		img.color = new Color(0f, 0.15f, 0.6f, 1f);

		if (overlayText != null)
		{
			overlayText.enabled = true;
			overlayText.text = "VIDEO_DRIVER_FAILURE";
		}
	}

	// ================= служебное =================

	/// <summary>
	/// Создать объект интерфейса на том же слое, что и родитель.
	/// </summary>
	/// <remarks>
	/// КРИТИЧНО. Камера экрана монитора снимает только слой UI
	/// (DisplayManager: cullingMask = LayerMask.GetMask("UI")), а new GameObject
	/// создаёт объект на слое Default. Такой объект физически существует,
	/// занимает место в иерархии, но камерой не снимается — и артефактов не
	/// видно вообще, хотя код исправно работает.
	/// </remarks>
	private static GameObject NewUiObject(string name, RectTransform parent,
		params System.Type[] components)
	{
		var go = new GameObject(name, components);
		if (parent != null) go.layer = parent.gameObject.layer;
		return go;
	}

	/// <summary>
	/// Слой, на котором рисуются артефакты.
	/// </summary>
	/// <remarks>
	/// Создаётся один раз как отдельный Canvas поверх экрана. Своё
	/// перекрытие (overrideSorting) с большим sortingOrder гарантирует, что
	/// эффект окажется выше любых окон и рабочего стола, сколько бы раз те ни
	/// звали SetAsLastSibling.
	/// </remarks>
	private RectTransform Parent()
	{
		if (layer != null) return layer;

		var host = container != null ? container : transform as RectTransform;
		if (host == null) return null;

		var go = NewUiObject("GPU Artifacts", host, typeof(RectTransform), typeof(Canvas));
		var rt = go.GetComponent<RectTransform>();
		rt.SetParent(host, false);
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;

		var c = go.GetComponent<Canvas>();
		c.overrideSorting = true;
		c.sortingOrder = ArtifactSortingOrder;

		layer = rt;
		return layer;
	}

	/// <summary>Сам экран монитора — то, что видит игрок.</summary>
	/// <summary>
	/// Картинка экрана — то, что должно «плыть» при искажении.
	/// </summary>
	/// <remarks>
	/// КРИТИЧНО: это ДОЧЕРНИЙ объект канваса, а не сам канвас. При нажатии на
	/// монитор Display.ZoomIn переводит канвас в ScreenSpaceOverlay, а Unity у
	/// такого канваса каждый кадр сама переписывает позицию и размер
	/// RectTransform — экран обязан совпадать с экраном устройства. Любое наше
	/// смещение молча затиралось бы в тот же кадр, и искажение пропадало
	/// именно в приближении. Вдобавок ZoomOut принудительно сбрасывает
	/// localPosition, localScale и sizeDelta канваса, то есть мы бы ещё и
	/// дрались с зумом за одни и те же поля.
	///
	/// Содержимое экрана ApplyScreen кладёт внутрь канваса отдельным
	/// RectTransform — вот его и двигаем. Он одинаково свободен и в мировом
	/// режиме, и в приближении.
	/// </remarks>
	private RectTransform ScreenRoot()
	{
		if (container == null) return transform as RectTransform;

		for (int i = 0; i < container.childCount; i++)
		{
			var child = container.GetChild(i) as RectTransform;
			if (child == null) continue;

			// Свой же слой двигать нельзя: он прозрачный, и на нём рисуются
			// полосы, которые обязаны оставаться на месте.
			if (layer != null && child == layer) continue;
			if (!child.gameObject.activeInHierarchy) continue;

			return child;
		}

		return container;
	}

	/// <summary>Вернуть экран в исходное состояние перед новым видом сбоя.</summary>
	private void ResetVisuals()
	{
		// Сбрасывать нужно ЭКРАН, который двигал Warp. Если этого не сделать,
		// картинка останется перекошенной уже после того, как эффект прошёл, и
		// это будет выглядеть поломкой игры, а не видеокарты.
		var screen = ScreenRoot();
		if (screen != null)
		{
			screen.anchoredPosition = Vector2.zero;
			screen.localScale = Vector3.one;
		}

		HideStripes();
		if (overlay != null) overlay.enabled = false;
		if (overlayText != null) overlayText.enabled = false;
	}

	private void HideAll()
	{
		ResetVisuals();
	}

	private void HideStripes()
	{
		for (int i = 0; i < stripes.Count; i++)
		{
			if (stripes[i] != null && stripes[i].enabled) stripes[i].enabled = false;
		}
	}

	private Image EnsureOverlay()
	{
		if (overlay != null) return overlay;

		var parent = Parent();
		if (parent == null) return null;

		var go = NewUiObject("Artifact Overlay", parent, typeof(RectTransform), typeof(Image));
		var rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;

		overlay = go.GetComponent<Image>();
		overlay.raycastTarget = false;
		overlay.enabled = false;

		// Текст для синего экрана лежит внутри заливки.
		var textGo = NewUiObject("Artifact Text", rt, typeof(RectTransform), typeof(Text));
		var trt = textGo.GetComponent<RectTransform>();
		trt.SetParent(rt, false);
		trt.anchorMin = Vector2.zero;
		trt.anchorMax = Vector2.one;
		trt.offsetMin = Vector2.zero;
		trt.offsetMax = Vector2.zero;

		overlayText = textGo.GetComponent<Text>();
		overlayText.alignment = TextAnchor.MiddleCenter;
		overlayText.color = Color.white;
		overlayText.raycastTarget = false;
		overlayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		overlayText.fontSize = 28;
		overlayText.enabled = false;

		return overlay;
	}

	private void EnsureStripes(int want, RectTransform parent)
	{
		while (stripes.Count < want)
		{
			var go = NewUiObject("Artifact", parent, typeof(RectTransform), typeof(Image));
			var rt = go.GetComponent<RectTransform>();
			rt.SetParent(parent, false);
			rt.anchorMin = new Vector2(0.5f, 0.5f);
			rt.anchorMax = new Vector2(0.5f, 0.5f);
			rt.pivot = new Vector2(0.5f, 0.5f);

			var img = go.GetComponent<Image>();

			// Полосы не должны перехватывать нажатия: игрок обязан попадать
			// по кнопкам экрана сквозь них.
			img.raycastTarget = false;

			stripes.Add(img);
		}
	}
}
