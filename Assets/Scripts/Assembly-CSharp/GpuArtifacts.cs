using System.Collections.Generic;
using PC.Component;
using UnityEngine;
using UnityEngine.UI;

// В UnityEngine есть свой класс Display (физический экран устройства), поэтому
// простое имя Display неоднозначно. Явно указываем, что речь о мониторе игры.
using Display = PC.Component.Display;

/// <summary>
/// Рисует артефакты повреждённой видеокарты поверх экрана монитора.
/// </summary>
/// <remarks>
/// Вешается на монитор рядом с Display. Раз в несколько кадров спрашивает у
/// подключённой материнской платы, есть ли среди её видеокарт повреждённые, и
/// выводит поверх картинки цветные полосы — тем чаще и заметнее, чем сильнее
/// разбита карта.
///
/// Полосы рисуются обычными Image в отдельном Canvas поверх рабочего стола, а
/// не шейдером: шейдер пришлось бы гонять на каждый пиксель экрана каждый
/// кадр, а это ровно та нагрузка, от которой мы только что избавлялись. Здесь
/// же несколько прямоугольников, которые к тому же переставляются не каждый
/// кадр.
/// </remarks>
public class GpuArtifacts : MonoBehaviour
{
	[SerializeField]
	[Tooltip("Куда класть полосы. Обычно канвас экрана монитора.")]
	private RectTransform container;

	[SerializeField]
	[Tooltip("Сколько полос показывать при полном повреждении.")]
	private int maxStripes = 14;

	[SerializeField]
	[Tooltip("Как часто переставлять полосы, в секундах.")]
	private float refreshInterval = 0.12f;

	private readonly List<Image> stripes = new List<Image>();

	private Display display;
	private float nextRefresh;

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

	private void LateUpdate()
	{
		float strength = CurrentStrength();

		if (strength <= 0f)
		{
			HideAll();
			return;
		}

		float now = Time.unscaledTime;
		if (now < nextRefresh) return;
		nextRefresh = now + refreshInterval;

		Redraw(strength);
	}

	private void HideAll()
	{
		for (int i = 0; i < stripes.Count; i++)
		{
			if (stripes[i] != null && stripes[i].enabled) stripes[i].enabled = false;
		}
	}

	private void Redraw(float strength)
	{
		var parent = container != null ? container : transform as RectTransform;
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

			// Высота полосы и её цвет случайны — так это и выглядит на живой
			// сбоящей карте: рваные горизонтальные линии разного оттенка.
			float h = Random.Range(2f, 6f + 18f * strength);
			float y = Random.Range(-size.y * 0.5f, size.y * 0.5f);
			float w = Random.Range(size.x * 0.25f, size.x);

			var rt = stripe.rectTransform;
			rt.sizeDelta = new Vector2(w, h);
			rt.anchoredPosition = new Vector2(
				Random.Range(-size.x * 0.5f, size.x * 0.5f), y);

			var color = Random.value < 0.5f
				? new Color(Random.value, Random.value, Random.value)
				: new Color(0f, Random.value, Random.value);
			color.a = Mathf.Lerp(0.35f, 0.9f, strength);
			stripe.color = color;
		}
	}

	private void EnsureStripes(int want, RectTransform parent)
	{
		while (stripes.Count < want)
		{
			var go = new GameObject("Artifact", typeof(RectTransform), typeof(Image));
			var rt = go.GetComponent<RectTransform>();
			rt.SetParent(parent, false);
			rt.anchorMin = new Vector2(0.5f, 0.5f);
			rt.anchorMax = new Vector2(0.5f, 0.5f);
			rt.pivot = new Vector2(0.5f, 0.5f);

			var img = go.GetComponent<Image>();

			// Полосы не должны перехватывать нажатия: игрок обязан попадать
			// по кнопкам рабочего стола сквозь них.
			img.raycastTarget = false;

			stripes.Add(img);
		}
	}
}
