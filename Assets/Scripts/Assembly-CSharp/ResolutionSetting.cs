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

	private readonly List<Resolution> uniqueResolutions = new List<Resolution>();

	private void Awake()
	{
		AutoFindControls();
		BindLegacyScales();
		BindToggle(rtxToggle, "RTXMode", 0, SetRTXMode);
		BindToggle(reflectionsToggle, "Reflections", 1, SetReflections);
		BindToggle(fullscreenToggle, "Fullscreen", 1, SetFullscreen);
		BindToggle(motionBlurToggle, "PP_MotionBlur", 0, SetMotionBlur);
		BindToggle(bloomToggle, "PP_Bloom", 0, SetBloom);
		BindToggle(aoToggle, "PP_AO", 0, SetAO);
		BindToggle(vignetteToggle, "PP_Vignette", 0, SetVignette);
		BindToggle(chromaticToggle, "PP_Chromatic", 0, SetChromatic);

#if UNITY_ANDROID || UNITY_IOS
		SetupMobileSlider();
		if (resolutionDropdown != null) resolutionDropdown.gameObject.SetActive(false);
		if (fullscreenToggle != null) fullscreenToggle.gameObject.SetActive(false);
#else
		SetupPcResolutions();
		if (mobileScaleSlider != null) mobileScaleSlider.gameObject.SetActive(false);
#endif
	}

	private void Start()
	{
		GraphicsBootstrap.AttachLayersToCameras();
		GraphicsBootstrap.ApplyPostProcess();
	}

	private void AutoFindControls()
	{
		var toggles = GetComponentsInChildren<Toggle>(true);
		for (int i = 0; i < toggles.Length; i++)
		{
			var t = toggles[i];
			string id = Identify(t);
			if (string.IsNullOrEmpty(id)) continue;
			if (id.Contains("motion") || id.Contains("blur")) Assign(ref motionBlurToggle, t);
			else if (id.Contains("bloom")) Assign(ref bloomToggle, t);
			else if (id.Contains("vignette")) Assign(ref vignetteToggle, t);
			else if (id.Contains("chromatic") || id.Contains("aberr")) Assign(ref chromaticToggle, t);
			else if (id.Contains("ao") || id.Contains("occlusion") || id.Contains("ssao")) Assign(ref aoToggle, t);
			else if (id.Contains("full")) Assign(ref fullscreenToggle, t);
			else if (id.Contains("rtx") || id.Contains("высок") || id.Contains("high")) Assign(ref rtxToggle, t);
			else if (id.Contains("reflect") || id.Contains("отраж")) Assign(ref reflectionsToggle, t);
		}

		if (resolutionDropdown == null)
			resolutionDropdown = GetComponentInChildren<Dropdown>(true);
		if (mobileScaleSlider == null)
			mobileScaleSlider = GetComponentInChildren<Slider>(true);
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
		for (int d = 0; d < 3 && tr != null; d++)
		{
			parts += " " + tr.name;
			var label = tr.GetComponentInChildren<Text>(true);
			if (label != null) parts += " " + label.text;
			tr = tr.parent;
		}
		return parts.ToLowerInvariant();
	}

	private void BindLegacyScales()
	{
		if (scales == null) return;
		float saved = PlayerPrefs.GetFloat("TargetResolution", 1f);
		for (int i = 0; i < scales.Length; i++)
		{
			var c = scales[i];
			var evt = c.toggle;
			float scale = c.scale;
			var t = evt != null ? evt.GetComponent<Toggle>() : null;
			if (t != null)
			{
				if (Mathf.Approximately(scale, saved))
					t.SetIsOnWithoutNotify(true);
				t.onValueChanged.AddListener(isOn =>
				{
					if (isOn) SetScaleResolution(scale);
				});
			}
			if (evt != null && evt.selected != null)
				evt.selected.AddListener(() => SetScaleResolution(scale));
		}
	}

	private static void BindToggle(Toggle toggle, string key, int defaultValue, Action<bool> onChanged)
	{
		if (toggle == null) return;
		bool saved = PlayerPrefs.GetInt(key, defaultValue) == 1;
		toggle.SetIsOnWithoutNotify(saved);
		toggle.onValueChanged.RemoveAllListeners();
		toggle.onValueChanged.AddListener(v => onChanged(v));
	}

	private void SetupMobileSlider()
	{
		if (mobileScaleSlider == null) return;
		mobileScaleSlider.minValue = 0.35f;
		mobileScaleSlider.maxValue = 1f;
		float saved = PlayerPrefs.GetFloat("TargetResolution", 1f);
		saved = Mathf.Clamp(saved, 0.35f, 1f);
		mobileScaleSlider.SetValueWithoutNotify(saved);
		UpdateMobileLabel(saved);
		mobileScaleSlider.onValueChanged.AddListener(v =>
		{
			PlayerPrefs.SetFloat("TargetResolution", v);
			PlayerPrefs.Save();
			UpdateMobileLabel(v);
			GraphicsBootstrap.ApplyResolution();
		});
	}

	private void UpdateMobileLabel(float scale)
	{
		if (mobileScaleLabel == null) return;
		mobileScaleLabel.text = Mathf.RoundToInt(scale * 100f) + "%";
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
			resolutionDropdown.onValueChanged.RemoveAllListeners();
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
		PlayerPrefs.SetFloat("TargetResolution", 1f);
		PlayerPrefs.Save();
		GraphicsBootstrap.ApplyResolution();
	}

	public void SetResolution(float value)
	{
		SetScaleResolution(value);
	}

	private void SetScaleResolution(float value)
	{
		PlayerPrefs.SetFloat("TargetResolution", value);
		if (value > 0f)
		{
			var native = Screen.currentResolution;
			PlayerPrefs.SetInt("ResWidth", Mathf.RoundToInt(native.width * value));
			PlayerPrefs.SetInt("ResHeight", Mathf.RoundToInt(native.height * value));
		}
		PlayerPrefs.Save();
		GraphicsBootstrap.ApplyResolution();
	}

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
		GraphicsBootstrap.ApplyPostProcess();
	}
}
