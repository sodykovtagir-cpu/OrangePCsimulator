using UnityEngine;
using UnityEngine.SceneManagement;

public static class GraphicsBootstrap
{
	private static Resolution nativeResolution;

    // Capture once, before any SetResolution. Android currentResolution may
    // report our reduced render size after the first change / scene reload.
    public static Resolution NativeResolution
    {
        get
        {
            if (nativeResolution.width <= 0 || nativeResolution.height <= 0)
                nativeResolution = Screen.currentResolution;
            return nativeResolution;
        }
    }
    private static int requestedWidth, requestedHeight;
#if !UNITY_ANDROID && !UNITY_IOS
    private static int requestedFullscreen = -1;
#endif

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void OnGameStart()
	{
        // Also reset static request state when entering Play with domain reload off.
        nativeResolution = default(Resolution);
        requestedWidth = requestedHeight = 0;
#if !UNITY_ANDROID && !UNITY_IOS
        requestedFullscreen = -1;
#endif
		ApplyRTX();
		ApplyReflectionsQuality();
		ApplyResolution();
		ApplyFPS();
		SceneManager.sceneLoaded -= OnSceneLoaded;
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		// Предел кадров обязательно восстанавливаем на каждой сцене.
		// Application.targetFrameRate -- глобальная переменная, и любой
		// скрипт в новой сцене может её перебить: именно так настройка
		// «сбрасывалась» после перезахода в игру.
		ApplyFPS();

		ApplyReflectionsToScene();
		ApplyPostProcess();
	}

	public static void ApplyAll()
	{
		ApplyRTX();
		ApplyReflectionsQuality();
		ApplyResolution();
		ApplyFPS();
		ApplyPostProcess();
	}

	public static void ApplyResolution()
	{
        var native = NativeResolution;

		// Always render 1:1. FPS is changed by picking a real output resolution, not a scale.
		QualitySettings.resolutionScalingFixedDPIFactor = 1f;
		ScalableBufferManager.ResizeBuffers(1f, 1f);

		int w = PlayerPrefs.GetInt("ResWidth", 0);
		int h = PlayerPrefs.GetInt("ResHeight", 0);
		if (w <= 0 || h <= 0)
		{
			w = native.width;
			h = native.height;
		}

#if UNITY_ANDROID || UNITY_IOS
        // Recover preferences damaged by repeatedly scaling a scaled size.
        float scale = Mathf.Clamp((float)w / Mathf.Max(1, native.width), .35f, 1f);
        w = Mathf.RoundToInt(native.width * scale);
        h = Mathf.RoundToInt(native.height * scale);
        if (w == requestedWidth && h == requestedHeight) return;
        requestedWidth = w; requestedHeight = h;
        Screen.SetResolution(w, h, true);
#else
		bool fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        int fullscreenValue = fullscreen ? 1 : 0;
        if (w == requestedWidth && h == requestedHeight && fullscreenValue == requestedFullscreen) return;
        requestedWidth = w; requestedHeight = h; requestedFullscreen = fullscreenValue;
		var mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
		int refresh = nativeResolution.refreshRate;
		Screen.SetResolution(w, h, mode, refresh);
#endif
		Debug.Log($"[GraphicsBootstrap] Resolution applied {w}x{h} (1x scale, screen now {Screen.width}x{Screen.height})");
	}

	public static void ApplyRTX()
	{
		bool enabled = PlayerPrefs.GetInt("RTXMode", 0) == 1;

		// БЕЗ RTX СГЛАЖИВАНИЕ ВЫКЛЮЧЕНО ПОЛНОСТЬЮ.
		//
		// Здесь стояло 4x, и это оказалось главной причиной низких кадров на
		// телефонах. MSAA на мобильном GPU стоит дорого вдвойне: он кратно
		// увеличивает трафик к памяти, а любой полноэкранный эффект заставляет
		// ещё и резолвить мультисэмплированный буфер лишним проходом.
		//
		// В оригинале (OrangePCRebirth, GraphicBootstrap.ApplyRTX) в этой ветке
		// стоит ноль -- сверено построчно, остальные значения совпадают.
		// Восемь остаётся только в RTX-режиме, который включают осознанно и на
		// сильном железе.
		QualitySettings.antiAliasing = enabled ? 8 : 0;
		if (enabled)
		{
			QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
			QualitySettings.shadows = ShadowQuality.All;
			QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
			QualitySettings.shadowDistance = 200f;
			QualitySettings.lodBias = 3f;
			QualitySettings.globalTextureMipmapLimit = 0;
			QualitySettings.pixelLightCount = 8;
		}
		else
		{
			QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
			QualitySettings.shadows = ShadowQuality.HardOnly;
			QualitySettings.shadowResolution = ShadowResolution.Medium;
			QualitySettings.shadowDistance = 60f;
			QualitySettings.lodBias = 1f;
			QualitySettings.pixelLightCount = 2;
		}
	}

	/// <summary>
	/// Включены ли отражения в реальном времени.
	/// </summary>
	/// <remarks>
	/// По умолчанию на телефонах выключены. Зеркальные зонды перерисовывают
	/// кубическую карту окружения, то есть сцена рисуется ещё несколько раз за
	/// кадр -- на мобильном GPU это одна из самых дорогих вещей вообще.
	/// Игрок по-прежнему может включить их в настройках: сохранённый выбор
	/// имеет приоритет, значение по умолчанию действует только до него.
	/// </remarks>
	public static int ReflectionsDefault
	{
		get
		{
#if UNITY_ANDROID || UNITY_IOS
			return 0;
#else
			return 1;
#endif
		}
	}

	private static bool ReflectionsEnabled()
	{
		return PlayerPrefs.GetInt("Reflections", ReflectionsDefault) == 1;
	}

	public static void ApplyReflectionsQuality()
	{
		bool enabled = ReflectionsEnabled();
		QualitySettings.realtimeReflectionProbes = enabled;
	}

	public static void ApplyReflectionsToScene()
	{
		bool enabled = ReflectionsEnabled();
		var probes = Object.FindObjectsOfType<ReflectionProbe>();
		for (int i = 0; i < probes.Length; i++)
			if (probes[i] != null) probes[i].enabled = enabled;
	}

	/// <summary>
	/// Сколько кадров показывать, пока игрок ничего не выбрал.
	/// </summary>
	/// <remarks>
	/// Берём настоящую частоту экрана. Раньше у каждого места был свой ответ:
	/// здесь 60, в FpsSetting 30 или 60, в слайдере настроек 60 -- и при первом
	/// запуске выигрывал тот, кто отработал последним. На экране 120 Гц игра
	/// упиралась в чужое число вместо родной развёртки.
	///
	/// Ноль и мусор от драйвера отбрасываем: Screen.currentResolution.refreshRate
	/// на части устройств возвращает 0, а иногда завышенное значение.
	/// </remarks>
	public static int TargetFpsDefault
	{
		get
		{
			int hz = 0;
			try { hz = Screen.currentResolution.refreshRate; } catch { }

			if (hz < 30) hz = 60;
			return Mathf.Clamp(hz, 30, 240);
		}
	}

	/// <summary>Сохранённый предел кадров, либо частота экрана.</summary>
	public static int TargetFps
	{
		get
		{
			int fallback = TargetFpsDefault;
			int fps = PlayerPrefs.GetInt("TargetFps", PlayerPrefs.GetInt("TargetFPS", fallback));
			return Mathf.Clamp(fps, 30, 240);
		}
	}

	public static void ApplyFPS()
	{
		Application.targetFrameRate = TargetFps;
		QualitySettings.vSyncCount = 0;
	}

	public static void AttachLayersToCameras()
	{
		var cams = Object.FindObjectsOfType<Camera>();
		for (int i = 0; i < cams.Length; i++)
			SetupCamera(cams[i]);
	}

	private static void SetupCamera(Camera cam)
	{
		if (cam == null) return;
		// Пропускаем камеры, которые рендерят в RenderTexture (экраны внутриигровых
		// мониторов из DisplayManager): пост-эффекты там не нужны — это портит
		// картинку на мониторах и жжёт производительность лишними blit-проходами.
		if (cam.targetTexture != null) return;

		bool bloom = PlayerPrefs.GetInt("PP_Bloom", 0) == 1;
		bool vignette = PlayerPrefs.GetInt("PP_Vignette", 0) == 1;
		bool grain = PlayerPrefs.GetInt("PP_Grain", PlayerPrefs.GetInt("PP_Chromatic", 0)) == 1;
		bool motionBlur = PlayerPrefs.GetInt("PP_MotionBlur", 0) == 1;
		bool ao = PlayerPrefs.GetInt("PP_AO", 0) == 1;

		// The package owns Motion Blur. SimpleScreenFx must never blur again.
		bool stockBlur = StockMotionBlur.Configure(cam, motionBlur);
		bool simpleEffects = bloom || vignette || grain || ao;

		// HDR нужен только под пост-обработку: без неё расширенный диапазон
		// некуда применять, а кадровый буфер становится вдвое толще. На
		// телефоне это чистый расход пропускной способности.
		// Only bloom needs HDR here; motion/vignette/grain/AO do not.
		cam.allowHDR = bloom;
		cam.allowMSAA = true;

		cam.depthTextureMode = (ao || stockBlur)
			? cam.depthTextureMode | DepthTextureMode.Depth
			: cam.depthTextureMode & ~DepthTextureMode.Depth;

		cam.depthTextureMode = stockBlur ? cam.depthTextureMode | DepthTextureMode.MotionVectors : cam.depthTextureMode & ~DepthTextureMode.MotionVectors;

		var fx = cam.GetComponent<SimpleScreenFx>();

		// КЛЮЧЕВОЕ ДЛЯ ТЕЛЕФОНОВ: если ни один эффект не включён, компонент
		// обязан быть выключен, а не просто "ничего не делать".
		//
		// Сам факт живого OnRenderImage заставляет Unity рисовать камеру в
		// промежуточную текстуру вместо прямой отрисовки на экран, а затем
		// копировать её обратно. Мобильные GPU считают кадр плитками и такой
		// разрыв конвейера переносят особенно плохо -- вся сцена лишний раз
		// уезжает в память и читается назад. Раньше внутри стоял
		// Graphics.Blit(src, dest) "на всякий случай": формально безобидный,
		// по факту полноэкранный проход каждый кадр при полностью выключенных
		// эффектах.
		if (!simpleEffects)
		{
			if (fx != null) fx.enabled = false;
			return;
		}

		if (fx == null)
			fx = cam.gameObject.AddComponent<SimpleScreenFx>();

		fx.bloom = bloom;
		fx.vignette = vignette;
		fx.grain = grain;
		fx.ao = ao;
		fx.enabled = true;
	}

	public static void ApplyPostProcess()
	{
		AttachLayersToCameras();
		Debug.Log("[GraphicsBootstrap] Post-process applied"
			+ " MB=" + PlayerPrefs.GetInt("PP_MotionBlur", 0)
			+ " Bloom=" + PlayerPrefs.GetInt("PP_Bloom", 0)
			+ " Vig=" + PlayerPrefs.GetInt("PP_Vignette", 0)
			+ " CA=" + PlayerPrefs.GetInt("PP_Chromatic", 0));
	}
}
