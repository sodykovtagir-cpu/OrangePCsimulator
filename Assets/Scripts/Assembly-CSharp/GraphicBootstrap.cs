using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.PostProcessing;

public static class GraphicsBootstrap
{
	private static Resolution nativeResolution;
	private static PostProcessVolume volume;
	private static PostProcessProfile profile;
	private static MotionBlur motionBlur;
	private static Bloom bloom;
	private static ColorGrading colorGrading;
	private static Vignette vignette;
	private static ChromaticAberration chromatic;
	private static PostProcessResources ppResources;

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
		EnsurePostProcess();
		AttachLayersToCameras();
		ApplyPostProcess();
	}

	public static void ApplyAll()
	{
		ApplyRTX();
		ApplyReflectionsQuality();
		ApplyResolution();
		ApplyFPS();
		EnsurePostProcess();
		AttachLayersToCameras();
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
		QualitySettings.antiAliasing = 0;
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

	private static void EnsurePostProcess()
	{
		if (volume != null && profile != null)
			return;

		var go = new GameObject("RuntimePostProcess");
		Object.DontDestroyOnLoad(go);
		go.layer = 0;

		volume = go.AddComponent<PostProcessVolume>();
		volume.isGlobal = true;
		volume.priority = 100f;
		volume.weight = 1f;
		profile = ScriptableObject.CreateInstance<PostProcessProfile>();
		volume.sharedProfile = profile;
		volume.profile = profile;

		motionBlur = profile.AddSettings<MotionBlur>();
		motionBlur.enabled.Override(false);
		motionBlur.shutterAngle.Override(270f);
		motionBlur.sampleCount.Override(16);

		bloom = profile.AddSettings<Bloom>();
		bloom.enabled.Override(false);
		bloom.intensity.Override(2.5f);
		bloom.threshold.Override(0.9f);
		bloom.softKnee.Override(0.5f);

		vignette = profile.AddSettings<Vignette>();
		vignette.enabled.Override(false);
		vignette.intensity.Override(0.35f);

		chromatic = profile.AddSettings<ChromaticAberration>();
		chromatic.enabled.Override(false);
		chromatic.intensity.Override(0.4f);

		colorGrading = profile.AddSettings<ColorGrading>();
		colorGrading.enabled.Override(true);
	}

	public static void AttachLayersToCameras()
	{
		var cams = Camera.allCameras;
		for (int i = 0; i < cams.Length; i++)
			SetupCamera(cams[i]);
	}

	private static PostProcessResources GetResources()
	{
		if (ppResources != null) return ppResources;
		var found = Resources.FindObjectsOfTypeAll<PostProcessResources>();
		if (found != null && found.Length > 0)
			ppResources = found[0];
		return ppResources;
	}

	private static void SetupCamera(Camera cam)
	{
		if (cam == null) return;
		cam.allowHDR = true;
		cam.allowMSAA = false;
		cam.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.MotionVectors;

		var res = GetResources();
		if (res == null)
		{
			Debug.LogWarning("[GraphicsBootstrap] PostProcessResources not found; skip PP layer.");
			return;
		}

		var layerComp = cam.GetComponent<PostProcessLayer>();
		if (layerComp == null)
			layerComp = cam.gameObject.AddComponent<PostProcessLayer>();

		layerComp.Init(res);
		layerComp.volumeTrigger = cam.transform;
		layerComp.volumeLayer = ~0;
		layerComp.enabled = true;
		layerComp.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
	}

	public static void ApplyPostProcess()
	{
		EnsurePostProcess();
		AttachLayersToCameras();

		SetFx(motionBlur, "PP_MotionBlur", 0, on =>
		{
			motionBlur.shutterAngle.Override(on ? 270f : 0f);
		});
		SetFx(bloom, "PP_Bloom", 0, on =>
		{
			bloom.intensity.Override(on ? 2.5f : 0f);
		});
		SetFx(vignette, "PP_Vignette", 0, on =>
		{
			vignette.intensity.Override(on ? 0.35f : 0f);
		});
		SetFx(chromatic, "PP_Chromatic", 0, on =>
		{
			chromatic.intensity.Override(on ? 0.4f : 0f);
		});

		if (colorGrading != null)
		{
			colorGrading.enabled.Override(true);
			colorGrading.postExposure.Override(PlayerPrefs.GetFloat("PP_Exposure", 0f));
		}

		if (volume != null)
			volume.weight = 1f;

		Debug.Log("[GraphicsBootstrap] Post-process applied"
			+ " MB=" + PlayerPrefs.GetInt("PP_MotionBlur", 0)
			+ " Bloom=" + PlayerPrefs.GetInt("PP_Bloom", 0));
	}

	private static void SetFx(PostProcessEffectSettings fx, string key, int def, System.Action<bool> extra)
	{
		if (fx == null) return;
		bool on = PlayerPrefs.GetInt(key, def) == 1;
		fx.enabled.Override(on);
		extra?.Invoke(on);
	}
}
