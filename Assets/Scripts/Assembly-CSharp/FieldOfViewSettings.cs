using UnityEngine;

// Shared by gameplay camera startup, the settings slider and zoom restoration.
// Never applies user FOV to a monitor/preview render-texture camera.
public static class FieldOfViewSettings
{
    public const float Default = 60f;

    public static float Sanitize(float value, float fallback = Default)
    {
        if (float.IsNaN(fallback) || float.IsInfinity(fallback)) fallback = Default;
        fallback = Mathf.Clamp(fallback, 1f, 179f);
        if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
        return Mathf.Clamp(value, 1f, 179f);
    }

    public static float ReadSaved(float fallback = Default)
    {
        return Sanitize(PlayerPrefs.GetFloat("FOV", fallback), fallback);
    }

    public static void ApplySaved(Camera camera)
    {
        if (camera == null || camera.orthographic || camera.targetTexture != null) return;
        camera.fieldOfView = ReadSaved(camera.fieldOfView);
    }
}
