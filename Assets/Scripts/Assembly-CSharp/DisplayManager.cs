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

	/// <summary>Предельная ширина текстуры экрана.</summary>
	private const int MaxWidth = 1280;

	/// <summary>Предельная высота текстуры экрана.</summary>
	private const int MaxHeight = 720;

	/// <summary>
	/// Подобрать размер текстуры экрана.
	/// </summary>
	/// <remarks>
	/// Резкость оставлена полной: экран вписывается в 1280x720 на любом
	/// устройстве, как было изначально. Понижать размер ради кадров себя не
	/// оправдало — монитор разглядывают вблизи, мыло видно сразу. Выигрыш даёт
	/// не размер текстуры, а частота её перерисовки (см. DisplayCameraPacer).
	///
	/// Что здесь исправлено — пропорции. Раньше стояло безусловное
	/// Mathf.Max(width, 1280) и Mathf.Max(height, 720), то есть любой экран
	/// приводился к 16:9. CurvedMonitor просит 512x256, это 2:1, и картинку
	/// растягивало. Теперь запрошенное соотношение вписывается в рамку целиком:
	/// 512x256 -> 1280x640.
	///
	/// Вписывание идёт по ОБЕИМ сторонам, а не по длинной. Иначе квадратный
	/// экран 256x256 превратился бы в 1280x1280 — это в полтора раза больше
	/// пикселей, чем давал старый код, то есть оптимизация наоборот.
	/// </remarks>
	private static void ResolveSize(ref int width, ref int height)
	{
		if (width <= 0 || height <= 0) return;

		float k = Mathf.Min((float)MaxWidth / width, (float)MaxHeight / height);

		width = Mathf.Max(64, Mathf.RoundToInt(width * k));
		height = Mathf.Max(64, Mathf.RoundToInt(height * k));
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
