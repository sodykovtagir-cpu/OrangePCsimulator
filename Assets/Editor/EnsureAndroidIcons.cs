#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Populate Unity's actual Android icon slots before export. Referencing only
/// the obsolete Android m_Icons array in ProjectSettings can leave Round/Legacy
/// empty on the device even when the Editor's default PC icon is configured.
/// </summary>
public sealed class EnsureAndroidIcons : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return -1000; } }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android) return;

        var original = Load("Assets/Sprite/orange PC simulator.jpg");
        AssignSingleLayer(AndroidPlatformIconKind.Legacy, original);
        AssignRoundIcons();

        var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive);
        if (icons == null || icons.Length == 0)
            throw new BuildFailedException("No Android adaptive icon slots found.");

        for (int i = 0; i < icons.Length; i++)
        {
            int size = icons[i].width;
            if (size != icons[i].height)
                throw new BuildFailedException("Unexpected Android icon aspect ratio: " + size);
            string suffix = size == 432 ? "" : "_" + size;
            Texture2D foreground = Load("Assets/AndroidIcons/AdaptiveForeground" + suffix + ".png");
            Texture2D background = Load("Assets/AndroidIcons/AdaptiveBackground" + suffix + ".png");
            if (foreground.width != size || foreground.height != size ||
                background.width != size || background.height != size)
                throw new BuildFailedException("Android adaptive icon has wrong size: " + size);
            // Unity's Android icon inspector uses layer 0 = Background,
            // layer 1 = Foreground (verified in the Player Settings preview).
            // Reversing these rendered only the background on the launcher.
            icons[i].SetTextures(new[] { background, foreground });
        }
        PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive, icons);
        Debug.Log("Android launcher icons: original PC legacy, supplied Round artwork and six adaptive color pairs.");
    }

    private static void AssignRoundIcons()
    {
        var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Round);
        if (slots == null || slots.Length == 0)
            throw new BuildFailedException("Missing Android Round icon slots.");
        for (int i = 0; i < slots.Length; i++)
        {
            int size = slots[i].width;
            var round = Load("Assets/AndroidIcons/RoundIcon_" + size + ".png");
            if (round.width != size || round.height != slots[i].height)
                throw new BuildFailedException("Android Round icon has wrong size: " + size);
            slots[i].SetTexture(round, 0);
        }
        PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Round, slots);
    }

    private static Texture2D Load(string path)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) throw new BuildFailedException("Missing Android icon texture: " + path);
        return texture;
    }

    private static void AssignSingleLayer(PlatformIconKind kind, Texture2D texture)
    {
        var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
        if (slots == null || slots.Length == 0)
            throw new BuildFailedException("Missing Android icon slots for " + kind);
        for (int i = 0; i < slots.Length; i++) slots[i].SetTexture(texture, 0);
        PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
    }
}
#endif
