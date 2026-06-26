using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

public class ResolutionSetting : MonoBehaviour
{
    [Serializable]
    private struct Container
    {
        public ToggleEvent toggle;
        public float scale;
    }

    [Header("Resolution")]
    [SerializeField] private Container[] scales;

    [Header("Graphics Toggles")]
    [SerializeField] private Toggle rtxToggle;

    private static Resolution defaultResolution;

    private void Awake()
    {
        if (scales != null)
        {
            float saved = PlayerPrefs.GetFloat("TargetResolution", 1f);

            for (int i = 0; i < scales.Length; i++)
            {
                var c = scales[i];
                var evt = c.toggle;
                float scale = c.scale;

                if (evt != null && evt.selected != null)
                    evt.selected.AddListener(() => SetResolution(scale));

                var t = evt != null ? evt.GetComponent<Toggle>() : null;
                if (t != null && Mathf.Approximately(scale, saved))
                    t.isOn = true;
            }
        }

        if (rtxToggle != null)
        {
            bool savedRTX = PlayerPrefs.GetInt("RTXMode", 0) == 1;
            rtxToggle.isOn = savedRTX;
            rtxToggle.onValueChanged.AddListener(SetRTXMode);
        }

        RestoreSetting();
        ApplySavedRTX();
    }

    public void SetResolution(float value)
    {
        Set(value);
        PlayerPrefs.SetFloat("TargetResolution", value);
    }

    public static void RestoreSetting()
    {
        if (PlayerPrefs.HasKey("TargetResolution"))
            Set(PlayerPrefs.GetFloat("TargetResolution"));
        else
        {
            Set(1f);
            PlayerPrefs.SetFloat("TargetResolution", 1f);
        }
    }

    private static void Set(float value)
    {
        if (defaultResolution.width == 0)
            defaultResolution = Screen.currentResolution;

        int baseWidth = defaultResolution.width;
        int baseHeight = defaultResolution.height;

        // 🔥 RTX Supersampling (до 200%)
        int w = Mathf.RoundToInt(baseWidth * value);
        int h = Mathf.RoundToInt(baseHeight * value);

        Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
    }

    // ================= RTX MODE =================

    private void SetRTXMode(bool enabled)
    {
        PlayerPrefs.SetInt("RTXMode", enabled ? 1 : 0);

        if (enabled)
            EnableRTX();
        else
            DisableRTX();
    }

    private void ApplySavedRTX()
    {
        bool enabled = PlayerPrefs.GetInt("RTXMode", 0) == 1;

        if (enabled)
            EnableRTX();
        else
            DisableRTX();
    }

    private void EnableRTX()
    {
        Debug.Log("RTX ULTRA ENABLED 🔥");

        // ✅ Supersampling 200%
        Set(2.0f);

        // ✅ MSAA 8x
        QualitySettings.antiAliasing = 8;

        // ✅ Максимальная фильтрация текстур
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;

        // ✅ Максимальные тени
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowDistance = 200f;

        // ✅ Детализация
        QualitySettings.lodBias = 3f;

        // ✅ Максимальные текстуры
        QualitySettings.globalTextureMipmapLimit = 0;

        // ✅ Pixel lights
        QualitySettings.pixelLightCount = 8;

        // ✅ Reflection probes
        QualitySettings.realtimeReflectionProbes = true;

        Debug.Log("RTX MODE ACTIVE ✅");
    }

    private void DisableRTX()
    {
        Debug.Log("RTX Mode Disabled");

        Set(1.0f);

        QualitySettings.antiAliasing = 0;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

        QualitySettings.shadows = ShadowQuality.HardOnly;
        QualitySettings.shadowResolution = ShadowResolution.Medium;
        QualitySettings.shadowDistance = 60f;

        QualitySettings.lodBias = 1f;

        QualitySettings.pixelLightCount = 2;
        QualitySettings.realtimeReflectionProbes = false;

        QualitySettings.vSyncCount = 0;
    }
}