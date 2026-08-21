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
		BindLegacyScales();
		BindToggle(rtxToggle, "RTXMode", 0, SetRTXMode);
		BindToggle(reflectionsToggle, "Reflections", 1, SetReflections);
		BindToggle(fullscreenToggle, "Fullscreen", 1, SetFullscreen);
		BindToggle(motionBlurToggle, "PP_MotionBlur", 0, v => SetPP("PP_MotionBlur", v));
		BindToggle(bloomToggle, "PP_Bloom", 1, v => SetPP("PP_Bloom", v));
		BindToggle(aoToggle, "PP_AO", 0, v => SetPP("PP_AO", v));
		BindToggle(vignetteToggle, "PP_Vignette", 0, v => SetPP("PP_Vignette", v));
		BindToggle(chromaticToggle, "PP_Chromatic", 0, v => SetPP("PP_Chromatic", v));

#if UNITY_ANDROID || UNITY_IOS
		SetupMobileSlider();
		if (resolutionDropdown != null) resolutionDropdown.gameObject.SetActive(false);
		if (fullscreenToggle != null) fullscreenToggle.gameObject.SetActive(false);
#else
		SetupPcResolutions();
		if (mobileScaleSlider != null) mobileScaleSlider.gameObject.SetActive(false);
#endif
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
		{
			uniqueResolutions.Add(Screen.currentResolution);
		}

		int savedW = PlayerPrefs.GetInt("ResWidth", Screen.width);
		int savedH = PlayerPrefs.GetInt("ResHeight", Screen.height);
		int selected = uniqueResolutions.Count - 1;
		var options = new List<string>();
		for (int i = 0; i < uniqueResolutions.Count; i++)
		{
			var r = uniqueResolutions[i];
			options.Add(r.width + " x " + r.height);
			if (r.width == savedW && r.height == savedH)
				selected = i;
		}

		if (resolutionDropdown != null)
		{
			resolutionDropdown.ClearOptions();
			resolutionDropdown.AddOptions(options);
			resolutionDropdown.SetValueWithoutNotify(selected);
			resolutionDropdown.onValueChanged.AddListener(OnPcResolutionChanged);
		}

		ApplyListedResolution(selected);
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

	private void SetFullscreen(bool enabled)
	{
		PlayerPrefs.SetInt("Fullscreen", enabled ? 1 : 0);
		PlayerPrefs.Save();
		GraphicsBootstrap.ApplyResolution();
	}

	private void SetRTXMode(bool enabled)
	{
		PlayerPrefs.SetInt("RTXMode", enabled ? 1 : 0);
		PlayerPrefs.Save();
		GraphicsBootstrap.ApplyRTX();
		// Do not silently override the user's resolution anymore.
	}

	private void SetReflections(bool enabled)
	{
		PlayerPrefs.SetInt("Reflections", enabled ? 1 : 0);
		PlayerPrefs.Save();
		GraphicsBootstrap.ApplyReflectionsQuality();
		GraphicsBootstrap.ApplyReflectionsToScene();
	}

	private void SetPP(string key, bool enabled)
	{
		PlayerPrefs.SetInt(key, enabled ? 1 : 0);
		PlayerPrefs.Save();
		GraphicsBootstrap.ApplyPostProcess();
	}
}
