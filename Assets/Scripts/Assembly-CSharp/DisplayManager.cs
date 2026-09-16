using System.Collections.Generic;
using UnityEngine;

public class DisplayManager : MonoBehaviour
{
	private Dictionary<Canvas, (Camera, RenderTexture)> camDict = new Dictionary<Canvas, (Camera, RenderTexture)>();

	public static DisplayManager Instance { get; private set; }

	private void Awake()
    {
		Instance = this;
    }

	/// <summary>
	/// Во сколько раз уменьшить экраны внутриигровых мониторов.
	/// </summary>
	/// <remarks>
	/// Раньше здесь стояло безусловное Mathf.Max(width, 1280) и
	/// Mathf.Max(height, 720): монитор просит текстуру 256x256, а получает
	/// 1280x720 — в четырнадцать раз больше пикселей. Вместе с MSAA x4 это
	/// давало 3.7 миллиона пикселей на КАЖДЫЙ экран каждый кадр, при том что
	/// весь экран телефона — около миллиона.
	///
	/// Экран ПК занимает небольшую часть кадра и разглядывать его в упор
	/// игрок начинает редко, поэтому нижняя граница теперь зависит от
	/// выбранного уровня качества, а не выкручена в максимум всегда.
	/// </remarks>
	private static void ResolveSize(ref int width, ref int height)
	{
		// Уровни качества проекта: 0 Very Low .. 5 Ultra.
		int level = QualitySettings.GetQualityLevel();

		int minSide;
		int maxSide;
		if (level <= 1)
		{
			// Very Low / Low — сюда попадают слабые телефоны.
			minSide = 256;
			maxSide = 512;
		}
		else if (level <= 3)
		{
			minSide = 512;
			maxSide = 1024;
		}
		else
		{
			minSide = 1280;
			maxSide = 2048;
		}

		width = Mathf.Clamp(width, minSide, maxSide);
		height = Mathf.Clamp(height, Mathf.RoundToInt(minSide * 0.5625f), maxSide);
	}

	/// <summary>
	/// Сглаживание для экранов: на слабых устройствах его быть не должно.
	/// </summary>
	/// <remarks>
	/// MSAA x4 умножает работу растеризатора вчетверо. Для текста на экране
	/// монитора оно почти не заметно, а стоит дороже самого рендера.
	/// </remarks>
	private static int ResolveAntiAliasing()
	{
		int level = QualitySettings.GetQualityLevel();
		if (level <= 1) return 1;
		if (level <= 3) return 2;
		return 4;
	}

	public RenderTexture CreateDisplay(Canvas canvas, int width, int height)
	{
		ResolveSize(ref width, ref height);

		var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
		rt.antiAliasing = ResolveAntiAliasing();
		rt.filterMode = FilterMode.Bilinear;

		// Анизотропия нужна поверхностям, уходящим вдаль под углом. Экран
		// монитора игрок видит почти фронтально, поэтому четыре выборки на
		// пиксель здесь тратились впустую.
		rt.anisoLevel = 0;

		var go = new GameObject("Display Camera");
		go.transform.position = new Vector3(500f, 0f, 0f);
		var cam = go.AddComponent<Camera>();
		cam.cullingMask = LayerMask.GetMask("UI");
		cam.targetTexture = rt;
		cam.backgroundColor = Color.black;
		cam.clearFlags = CameraClearFlags.SolidColor;

		// Камера не рендерит сама: вместо шестидесяти кадров в секунду ей
		// задаётся своя частота. Содержимое экрана — это интерфейс ОС, он
		// меняется на порядок реже кадров игры, и разница на глаз незаметна.
		cam.enabled = false;
		var pacer = go.AddComponent<DisplayCameraPacer>();
		pacer.Setup(cam);

		camDict.Add(canvas, (cam, rt));
		canvas.worldCamera = cam;
		cam.Render();
		return rt;
	}

	public void SetDisplayActive(Canvas canvas, bool value)
	{
		if (camDict == null || !camDict.TryGetValue(canvas, out var entry)) return;
		var cam = entry.Item1;
		if (cam == null) return;

		// Монитор ушёл из поля зрения — не рисуем его вообще. Раньше камера
		// продолжала работать, просто результат никто не видел.
		var pacer = cam.GetComponent<DisplayCameraPacer>();
		if (pacer != null) pacer.SetVisible(value);

		if (value) cam.Render();
	}

	/// <summary>
	/// Перерисовать экран монитора один раз.
	/// </summary>
	/// <remarks>
	/// Вызывается, когда содержимое действительно изменилось. Это дешевле
	/// постоянно включённой камеры во столько раз, во сколько реальная
	/// частота изменений ниже частоты кадров.
	/// </remarks>
	public void RequestRedraw(Canvas canvas)
	{
		if (canvas == null || camDict == null) return;
		if (!camDict.TryGetValue(canvas, out var entry)) return;
		var cam = entry.Item1;
		if (cam != null) cam.Render();
	}

	public void RemoveDisplay(UnityEngine.Canvas canvas)
	{
		if (camDict != null && camDict.TryGetValue(canvas, out var entry))
		{
			Destroy(entry.Item1);
			Destroy(entry.Item2);
			camDict.Remove(canvas);
		}
	}
}
