using UnityEngine;
using UnityEngine.SceneManagement;

public static class GraphicsBootstrap
{
	private static Resolution nativeResolution;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void OnGameStart()
	{
		if (nativeResolution.width == 0)
			nativeResolution = Screen.currentResolution;

		ApplyRTX();
		ApplyReflectionsQuality();
		ApplyResolution();
		ApplyFPS();
		SceneManager.sceneLoaded -= OnSceneLoaded;
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
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
		if (nativeResolution.width == 0)
			nativeResolution = Screen.currentResolution;

		// Always render 1:1. FPS is changed by picking a real output resolution, not a scale.
		QualitySettings.resolutionScalingFixedDPIFactor = 1f;
		ScalableBufferManager.ResizeBuffers(1f, 1f);

		int w = PlayerPrefs.GetInt("ResWidth", 0);
		int h = PlayerPrefs.GetInt("ResHeight", 0);
		if (w <= 0 || h <= 0)
		{
			w = nativeResolution.width;
			h = nativeResolution.height;
		}

#if UNITY_ANDROID || UNITY_IOS
		Screen.SetResolution(w, h, true);
#else
		bool fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
		var mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
		int refresh = nativeResolution.refreshRate;
		Screen.SetResolution(w, h, mode, refresh);
#endif
		Debug.Log($"[GraphicsBootstrap] Resolution applied {w}x{h} (1x scale, screen now {Screen.width}x{Screen.height})");
	}

	public static void ApplyRTX()
	{
		bool enabled = PlayerPrefs.GetInt("RTXMode", 0) == 1;
		// MSAA kills Post Processing motion blur / AO. Use FXAA on the PP layer instead.
		QualitySettings.antiAliasing = enabled ? 8 : 4;
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

	public static void ApplyReflectionsQuality()
	{
		bool enabled = PlayerPrefs.GetInt("Reflections", 1) == 1;
		QualitySettings.realtimeReflectionProbes = enabled;
	}

	public static void ApplyReflectionsToScene()
	{
		bool enabled = PlayerPrefs.GetInt("Reflections", 1) == 1;
		var probes = Object.FindObjectsOfType<ReflectionProbe>();
		for (int i = 0; i < probes.Length; i++)
			if (probes[i] != null) probes[i].enabled = enabled;
	}

	public static void ApplyFPS()
	{
		int fps = PlayerPrefs.GetInt("TargetFps", PlayerPrefs.GetInt("TargetFPS", 60));
		Application.targetFrameRate = fps;
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
		cam.allowHDR = true;
		cam.allowMSAA = true;
		if (PlayerPrefs.GetInt("PP_AO", 0) == 1)
			cam.depthTextureMode |= DepthTextureMode.Depth;

		var fx = cam.GetComponent<SimpleScreenFx>();
		if (fx == null)
			fx = cam.gameObject.AddComponent<SimpleScreenFx>();

		fx.bloom = PlayerPrefs.GetInt("PP_Bloom", 0) == 1;
		fx.vignette = PlayerPrefs.GetInt("PP_Vignette", 0) == 1;
		fx.chromatic = PlayerPrefs.GetInt("PP_Chromatic", 0) == 1;
		fx.motionBlur = PlayerPrefs.GetInt("PP_MotionBlur", 0) == 1;
		fx.ao = PlayerPrefs.GetInt("PP_AO", 0) == 1;
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
