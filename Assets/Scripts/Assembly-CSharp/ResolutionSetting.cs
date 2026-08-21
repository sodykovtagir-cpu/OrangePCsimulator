using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResolutionSetting : MonoBehaviour
{
	[Serializable]
	private struct Container
	{
		public ToggleEvent toggle;
		public float scale;
	}

	[Header("Legacy scale toggles (optional)")]
	[SerializeField] private Container[] scales;

	[Header("Graphics Toggles")]
	[SerializeField] private Toggle rtxToggle;
	[SerializeField] private Toggle reflectionsToggle;
	[SerializeField] private Toggle fullscreenToggle;
	[SerializeField] private Toggle motionBlurToggle;
	[SerializeField] private Toggle bloomToggle;
	[SerializeField] private Toggle aoToggle;
	[SerializeField] private Toggle vignetteToggle;
	[SerializeField] private Toggle chromaticToggle;

	[Header("PC resolution")]
	[SerializeField] private Dropdown resolutionDropdown;

	[Header("Mobile resolution")]
	[SerializeField] private Slider mobileScaleSlider;
	[SerializeField] private Text mobileScaleLabel;

	[Header("FPS")]
	[SerializeField] private Slider fpsSlider;
	[SerializeField] private Text fpsLabel;

	private readonly List<Resolution> uniqueResolutions = new List<Resolution>();
	private bool bound;

	private void Awake()
	{
		HideLegacyScaleButtons();
	}

	private void Start()
	{
		WireAll();
		GraphicsBootstrap.AttachLayersToCameras();
		GraphicsBootstrap.ApplyPostProcess();
	}

	private void OnEnable()
	{
		if (bound)
			GraphicsBootstrap.ApplyPostProcess();
	}

	private void WireAll()
	{
		AutoFindControls();
		BindToggle(rtxToggle, "RTXMode", 0, SetRTXMode);
		BindToggle(reflectionsToggle, "Reflections", 1, SetReflections);
		BindToggle(fullscreenToggle, "Fullscreen", 1, SetFullscreen);
		BindToggle(motionBlurToggle, "PP_MotionBlur", 0, SetMotionBlur);
		BindToggle(bloomToggle, "PP_Bloom", 0, SetBloom);
		BindToggle(aoToggle, "PP_AO", 0, SetAO);
		BindToggle(vignetteToggle, "PP_Vignette", 0, SetVignette);
		BindToggle(chromaticToggle, "PP_Chromatic", 0, SetChromatic);

		Debug.Log("[Settings] Wired toggles"
			+ " MB=" + (motionBlurToggle != null)
			+ " Bloom=" + (bloomToggle != null)
			+ " FS=" + (fullscreenToggle != null)
			+ " RTX=" + (rtxToggle != null)
			+ " Refl=" + (reflectionsToggle != null));

		bool mobile = Application.isMobilePlatform;
#if UNITY_ANDROID || UNITY_IOS
		mobile = true;
#endif
		if (mobile)
		{
			SetupMobileSlider();
			if (resolutionDropdown != null) resolutionDropdown.gameObject.SetActive(false);
			HideFullscreenRow();
		}
		else
		{
			SetupPcResolutions();
			if (mobileScaleSlider != null) mobileScaleSlider.gameObject.SetActive(false);
		}
		SetupFpsSlider();
		bound = true;
	}

	private Transform SearchRoot()
	{
		var canvas = GetComponentInParent<Canvas>();
		if (canvas != null) return canvas.transform;
		return transform.root;
	}

	private void AutoFindControls()
	{
		var root = SearchRoot();
		var toggles = root.GetComponentsInChildren<Toggle>(true);
		for (int i = 0; i < toggles.Length; i++)
		{
			var t = toggles[i];
			string id = Identify(t);
			if (string.IsNullOrEmpty(id)) continue;
			if (id.Contains("motion") || id.Contains("blur")) Assign(ref motionBlurToggle, t);
			else if (id.Contains("bloom")) Assign(ref bloomToggle, t);
			else if (id.Contains("vignette")) Assign(ref vignetteToggle, t);
			else if (id.Contains("chromatic") || id.Contains("aberr")) Assign(ref chromaticToggle, t);
			else if (id.Contains("occlusion") || id.Contains("ssao")) Assign(ref aoToggle, t);
			else if (id.Contains("full")) Assign(ref fullscreenToggle, t);
			else if (id.Contains("rtx") || id.Contains("высок")) Assign(ref rtxToggle, t);
			else if (id.Contains("reflect") || id.Contains("отраж")) Assign(ref reflectionsToggle, t);
		}

		if (resolutionDropdown == null)
			resolutionDropdown = root.GetComponentInChildren<Dropdown>(true);

		var sliders = root.GetComponentsInChildren<Slider>(true);
		for (int i = 0; i < sliders.Length; i++)
		{
			var sl = sliders[i];
			if (sl == null) continue;
			string id = IdentifySlider(sl);
			if (id.Contains("fps") || id.Contains("frame") || id.Contains("кадр"))
				AssignSlider(ref fpsSlider, sl);
			else if (id.Contains("scale") || id.Contains("resol") || id.Contains("разреш") || id.Contains("mobile"))
				AssignSlider(ref mobileScaleSlider, sl);
		}
	}

	private static void Assign(ref Toggle field, Toggle found)
	{
		if (field == null) field = found;
	}

	private static string Identify(Toggle t)
	{
		if (t == null) return "";
		var parts = t.name;
		var tr = t.transform;
		for (int d = 0; d < 4 && tr != null; d++)
		{
			parts += " " + tr.name;
			var labels = tr.GetComponentsInChildren<Text>(true);
			for (int i = 0; i < labels.Length && i < 4; i++)
			{
				if (labels[i] != null) parts += " " + labels[i].text;
			}
			tr = tr.parent;
		}
		return parts.ToLowerInvariant();
	}

	private void HideFullscreenRow()
	{
		if (fullscreenToggle != null)
		{
			var p = fullscreenToggle.transform.parent;
			if (p != null && p.GetComponent<Canvas>() == null && p.childCount <= 5)
				p.gameObject.SetActive(false);
			else
				fullscreenToggle.gameObject.SetActive(false);
		}

		var root = SearchRoot();
		if (root == null) return;
		var labels = root.GetComponentsInChildren<Text>(true);
		for (int i = 0; i < labels.Length; i++)
		{
			var t = labels[i];
			if (t == null || string.IsNullOrEmpty(t.text)) continue;
			string s = t.text.ToLowerInvariant();
			if (s.Contains("fullscreen") || s.Contains("full screen")
				|| s.Contains("полный экран") || s.Contains("полноэкран"))
				t.gameObject.SetActive(false);
		}
	}

	private void HideLegacyScaleButtons()
	{
		if (scales == null) return;
		for (int i = 0; i < scales.Length; i++)
		{
			if (scales[i].toggle != null)
				scales[i].toggle.gameObject.SetActive(false);
		}
	}

	private static void BindToggle(Toggle toggle, string key, int defaultValue, Action<bool> onChanged)
	{
		if (toggle == null) return;
		bool saved = PlayerPrefs.GetInt(key, defaultValue) == 1;
		toggle.SetIsOnWithoutNotify(saved);
		toggle.onValueChanged.AddListener(v => onChanged(v));
	}

	private void SetupMobileSlider()
	{
		if (mobileScaleSlider == null) return;
		mobileScaleSlider.minValue = 0.35f;
		mobileScaleSlider.maxValue = 1f;
		float saved = 1f;
		int sw = PlayerPrefs.GetInt("ResWidth", 0);
		var native = Screen.currentResolution;
		if (sw > 0 && native.width > 0)
			saved = Mathf.Clamp((float)sw / native.width, 0.35f, 1f);
		mobileScaleSlider.SetValueWithoutNotify(saved);
		UpdateMobileLabel(saved);
		mobileScaleSlider.onValueChanged.AddListener(v =>
		{
			int w = Mathf.Max(320, Mathf.RoundToInt(native.width * v));
			int h = Mathf.Max(240, Mathf.RoundToInt(native.height * v));
			PlayerPrefs.SetInt("ResWidth", w);
			PlayerPrefs.SetInt("ResHeight", h);
			PlayerPrefs.Save();
			UpdateMobileLabel(v);
			GraphicsBootstrap.ApplyResolution();
		});
	}

	private void SetupFpsSlider()
	{
		if (fpsSlider == null) return;

		int maxHz = Screen.currentResolution.refreshRate;
		if (maxHz < 30) maxHz = 60;
		int maxFps = Mathf.Clamp(maxHz, 30, 240);

		fpsSlider.minValue = 30;
		fpsSlider.maxValue = maxFps;
		fpsSlider.wholeNumbers = true;

		int saved = PlayerPrefs.GetInt("TargetFps", PlayerPrefs.GetInt("TargetFPS", 60));
		saved = Mathf.Clamp(saved, 30, maxFps);
		fpsSlider.SetValueWithoutNotify(saved);
		UpdateFpsLabel(saved);

		if (fpsLabel == null)
		{
			var texts = fpsSlider.GetComponentsInChildren<Text>(true);
			if (texts != null && texts.Length > 0) fpsLabel = texts[0];
			if (fpsSlider.transform.parent != null)
			{
				var ptexts = fpsSlider.transform.parent.GetComponentsInChildren<Text>(true);
				for (int i = 0; i < ptexts.Length; i++)
				{
					if (ptexts[i] == null) continue;
					string s = ptexts[i].text.ToLowerInvariant();
					if (s.Contains("fps") || s.Contains("кадр")) { fpsLabel = ptexts[i]; break; }
				}
			}
		}

		fpsSlider.onValueChanged.AddListener(v =>
		{
			int fps = Mathf.RoundToInt(v);
			PlayerPrefs.SetInt("TargetFps", fps);
			PlayerPrefs.SetInt("TargetFPS", fps);
			PlayerPrefs.Save();
			Application.targetFrameRate = fps;
			QualitySettings.vSyncCount = 0;
			UpdateFpsLabel(fps);
		});
	}

	private void UpdateFpsLabel(int fps)
	{
		if (fpsLabel == null) return;
		fpsLabel.text = fps + " FPS";
	}

	private void SetupPcResolutions()
	{
		uniqueResolutions.Clear();
		var raw = Screen.resolutions;
		var seen = new HashSet<string>();
		for (int i = 0; i < raw.Length; i++)
		{
			var r = raw[i];
			string key = r.width + "x" + r.height;
			if (seen.Add(key))
				uniqueResolutions.Add(r);
		}

		if (uniqueResolutions.Count == 0)
			uniqueResolutions.Add(Screen.currentResolution);

		int savedW = PlayerPrefs.GetInt("ResWidth", 0);
		int savedH = PlayerPrefs.GetInt("ResHeight", 0);
		int selected = uniqueResolutions.Count - 1;
		var options = new List<string>();
		for (int i = 0; i < uniqueResolutions.Count; i++)
		{
			var r = uniqueResolutions[i];
			options.Add(r.width + " x " + r.height);
			if (savedW > 0 && r.width == savedW && r.height == savedH)
				selected = i;
		}

		if (resolutionDropdown != null)
		{
			resolutionDropdown.ClearOptions();
			resolutionDropdown.AddOptions(options);
			resolutionDropdown.SetValueWithoutNotify(Mathf.Clamp(selected, 0, uniqueResolutions.Count - 1));
			resolutionDropdown.onValueChanged.AddListener(OnPcResolutionChanged);
		}
	}

	private void OnPcResolutionChanged(int index)
	{
		ApplyListedResolution(index);
	}

	private void ApplyListedResolution(int index)
	{
		if (index < 0 || index >= uniqueResolutions.Count) return;
		var r = uniqueResolutions[index];
		PlayerPrefs.SetInt("ResWidth", r.width);
		PlayerPrefs.SetInt("ResHeight", r.height);
		PlayerPrefs.Save();
		GraphicsBootstrap.ApplyResolution();
	}

	public void SetResolution(float value) { }

	public void SetFullscreen(bool enabled)
	{
		PlayerPrefs.SetInt("Fullscreen", enabled ? 1 : 0);
		PlayerPrefs.Save();
		GraphicsBootstrap.ApplyResolution();
	}

	public void SetRTXMode(bool enabled)
	{
		PlayerPrefs.SetInt("RTXMode", enabled ? 1 : 0);
		PlayerPrefs.Save();
		GraphicsBootstrap.ApplyRTX();
	}

	public void SetReflections(bool enabled)
	{
		PlayerPrefs.SetInt("Reflections", enabled ? 1 : 0);
		PlayerPrefs.Save();
		GraphicsBootstrap.ApplyReflectionsQuality();
		GraphicsBootstrap.ApplyReflectionsToScene();
	}

	public void SetMotionBlur(bool enabled) { SetPP("PP_MotionBlur", enabled); }
	public void SetBloom(bool enabled) { SetPP("PP_Bloom", enabled); }
	public void SetAO(bool enabled) { SetPP("PP_AO", enabled); }
	public void SetVignette(bool enabled) { SetPP("PP_Vignette", enabled); }
	public void SetChromatic(bool enabled) { SetPP("PP_Chromatic", enabled); }

	private void SetPP(string key, bool enabled)
	{
		PlayerPrefs.SetInt(key, enabled ? 1 : 0);
		PlayerPrefs.Save();
		Debug.Log("[Settings] " + key + " = " + enabled);
		GraphicsBootstrap.ApplyPostProcess();
	}
}
