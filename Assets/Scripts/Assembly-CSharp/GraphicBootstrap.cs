using UnityEngine;
using UnityEngine.SceneManagement;

public static class GraphicsBootstrap
{
    private static Resolution defaultResolution;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnGameStart()
    {
        Debug.Log("[GraphicsBootstrap] Applying saved settings...");

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
    }

    // =============== –¿«–≈ÿ≈Õ»≈ ===============
    public static void ApplyResolution()
    {
        float scale = PlayerPrefs.GetFloat("TargetResolution", 1f);

        if (defaultResolution.width == 0)
            defaultResolution = Screen.currentResolution;

        int w = Mathf.RoundToInt(defaultResolution.width * scale);
        int h = Mathf.RoundToInt(defaultResolution.height * scale);

        Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
        Debug.Log($"[GraphicsBootstrap] Resolution: {w}x{h} (scale {scale})");
    }

    // =============== RTX ===============
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

        Debug.Log($"[GraphicsBootstrap] RTX: {enabled}");
    }

    // =============== Œ“–¿∆≈Õ»ﬂ („ÎÓ·‡Î¸Ì˚È ÙÎ‡„) ===============
    public static void ApplyReflectionsQuality()
    {
        bool enabled = PlayerPrefs.GetInt("Reflections", 1) == 1;
        QualitySettings.realtimeReflectionProbes = enabled;
        Debug.Log($"[GraphicsBootstrap] Reflections quality: {enabled}");
    }

    // =============== Œ“–¿∆≈Õ»ﬂ (ÔÓ·˚ Ì‡ ÒˆÂÌÂ) ===============
    public static void ApplyReflectionsToScene()
    {
        bool enabled = PlayerPrefs.GetInt("Reflections", 1) == 1;

        var probes = Object.FindObjectsOfType<ReflectionProbe>();
        for (int i = 0; i < probes.Length; i++)
            if (probes[i] != null) probes[i].enabled = enabled;

        Debug.Log($"[GraphicsBootstrap] Applied reflections to {probes.Length} probes: {enabled}");
    }

    // =============== FPS ===============
    public static void ApplyFPS()
    {
        int fps = PlayerPrefs.GetInt("TargetFPS", 60);
        Application.targetFrameRate = fps;
        QualitySettings.vSyncCount = 0;
        Debug.Log($"[GraphicsBootstrap] FPS limit: {fps}");
    }
}