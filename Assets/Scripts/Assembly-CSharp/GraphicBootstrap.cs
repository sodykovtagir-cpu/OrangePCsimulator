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
	private static AmbientOcclusion ao;
	private static ColorGrading colorGrading;
	private static Vignette vignette;
	private static ChromaticAberration chromatic;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void OnGameStart()
	{
		if (nativeResolution.width == 0)
			nativeResolution = Screen.currentResolution;

		ApplyAll();
		SceneManager.sceneLoaded -= OnSceneLoaded;
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		ApplyReflectionsToScene();
		EnsurePostProcess();
		ApplyPostProcess();
	}

	public static void ApplyAll()
	{
		ApplyRTX();
		ApplyReflectionsQuality();
		ApplyResolution();
		ApplyFPS();
		EnsurePostProcess();
		ApplyPostProcess();
	}

	public static void ApplyResolution()
	{
		if (nativeResolution.width == 0)
			nativeResolution = Screen.currentResolution;

#if UNITY_ANDROID || UNITY_IOS
		float scale = PlayerPrefs.GetFloat("TargetResolution", 1f);
		scale = Mathf.Clamp(scale, 0.35f, 1f);
		int w = Mathf.Max(320, Mathf.RoundToInt(nativeResolution.width * scale));
		int h = Mathf.Max(240, Mathf.RoundToInt(nativeResolution.height * scale));
		Screen.SetResolution(w, h, true);
#else
		bool fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
		int w = PlayerPrefs.GetInt("ResWidth", 0);
		int h = PlayerPrefs.GetInt("ResHeight", 0);

		if (w <= 0 || h <= 0)
		{
			float scale = PlayerPrefs.GetFloat("TargetResolution", 1f);
			w = Mathf.RoundToInt(nativeResolution.width * Mathf.Clamp(scale, 0.25f, 2f));
			h = Mathf.RoundToInt(nativeResolution.height * Mathf.Clamp(scale, 0.25f, 2f));
		}

		var mode = fullscreen ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
		int refresh = nativeResolution.refreshRate;
		Screen.SetResolution(w, h, mode, refresh);
#endif
		Debug.Log($"[GraphicsBootstrap] Resolution applied {Screen.width}x{Screen.height}");
	}

	public static void ApplyRTX()
	{
		bool enabled = PlayerPrefs.GetInt("RTXMode", 0) == 1;
		if (enabled)
		{
			QualitySettings.antiAliasing = 8;
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
			QualitySettings.antiAliasing = 0;
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
		if (volume != null) return;

		var layer = LayerMask.NameToLayer("PostProcessing");
		if (layer < 0) layer = 0;

		var go = new GameObject("RuntimePostProcess");
		Object.DontDestroyOnLoad(go);
		volume = go.AddComponent<PostProcessVolume>();
		volume.isGlobal = true;
		volume.priority = 100f;
		profile = ScriptableObject.CreateInstance<PostProcessProfile>();
		volume.sharedProfile = profile;
		volume.profile = profile;

		motionBlur = profile.AddSettings<MotionBlur>();
		bloom = profile.AddSettings<Bloom>();
		ao = profile.AddSettings<AmbientOcclusion>();
		colorGrading = profile.AddSettings<ColorGrading>();
		vignette = profile.AddSettings<Vignette>();
		chromatic = profile.AddSettings<ChromaticAberration>();

		var cams = Object.FindObjectsOfType<Camera>();
		for (int i = 0; i < cams.Length; i++)
		{
			var cam = cams[i];
			if (cam == null) continue;
			var layerComp = cam.GetComponent<PostProcessLayer>();
			if (layerComp == null)
			{
				layerComp = cam.gameObject.AddComponent<PostProcessLayer>();
				layerComp.volumeTrigger = cam.transform;
				layerComp.volumeLayer = ~0;
				layerComp.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
			}
		}
	}

	public static void ApplyPostProcess()
	{
		EnsurePostProcess();
		if (motionBlur != null)
		{
			bool on = PlayerPrefs.GetInt("PP_MotionBlur", 0) == 1;
			motionBlur.enabled.Override(on);
			motionBlur.shutterAngle.Override(on ? 270f : 0f);
		}
		if (bloom != null)
		{
			bool on = PlayerPrefs.GetInt("PP_Bloom", 1) == 1;
			bloom.enabled.Override(on);
			bloom.intensity.Override(on ? 0.35f : 0f);
		}
		if (ao != null)
		{
			bool on = PlayerPrefs.GetInt("PP_AO", 0) == 1;
			ao.enabled.Override(on);
		}
		if (vignette != null)
		{
			bool on = PlayerPrefs.GetInt("PP_Vignette", 0) == 1;
			vignette.enabled.Override(on);
			vignette.intensity.Override(on ? 0.28f : 0f);
		}
		if (chromatic != null)
		{
			bool on = PlayerPrefs.GetInt("PP_Chromatic", 0) == 1;
			chromatic.enabled.Override(on);
			chromatic.intensity.Override(on ? 0.25f : 0f);
		}
		if (colorGrading != null)
		{
			colorGrading.enabled.Override(true);
			colorGrading.postExposure.Override(PlayerPrefs.GetFloat("PP_Exposure", 0f));
		}
	}
}
