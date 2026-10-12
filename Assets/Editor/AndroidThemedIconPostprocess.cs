#if UNITY_EDITOR
using System;
using System.IO;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Unity 2022 offers Background/Foreground, but no Android 13 monochrome slot.
/// Preserve its normal adaptive icon and add a separate v33 themed layer to
/// every generated adaptive-icon XML (including round icon, if present).
/// </summary>
public sealed class AndroidThemedIconPostprocess : IPostGenerateGradleAndroidProject
{
    public int callbackOrder { get { return 500; } }

    public void OnPostGenerateGradleAndroidProject(string basePath)
    {
        string mono = Path.Combine(Application.dataPath, "AndroidIcons", "AdaptiveMonochrome.png");
        if (!File.Exists(mono)) throw new FileNotFoundException("Android monochrome icon missing", mono);

        string libraryRes = Path.Combine(basePath, "src", "main", "res");
        string launcherRes = Path.GetFullPath(Path.Combine(basePath, "..", "launcher", "src", "main", "res"));
        int count = PatchResourceTree(libraryRes, mono);
        if (!string.Equals(Path.GetFullPath(libraryRes), launcherRes, StringComparison.OrdinalIgnoreCase))
            count += PatchResourceTree(launcherRes, mono);

        if (count == 0)
            Debug.LogWarning("Android themed icon: no adaptive-icon XML found in generated Gradle project. Normal color icon is unchanged.");
        else
            Debug.Log("Android themed icon: generated " + count + " v33 monochrome icon definitions.");
    }

    private static int PatchResourceTree(string resRoot, string mono)
    {
        if (!Directory.Exists(resRoot)) return 0;
        int count = 0;
        foreach (string file in Directory.GetFiles(resRoot, "*.xml", SearchOption.AllDirectories))
        {
            string folder = Path.GetDirectoryName(file);
            if (folder == null || !folder.EndsWith("-anydpi-v26", StringComparison.OrdinalIgnoreCase)) continue;

            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch (Exception) { continue; }
            if (doc.Root == null || doc.Root.Name.LocalName != "adaptive-icon") continue;

            XNamespace android = "http://schemas.android.com/apk/res/android";
            if (doc.Root.Element("monochrome") == null)
                doc.Root.Add(new XElement("monochrome", new XAttribute(android + "drawable", "@drawable/orange_pc_monochrome")));

            // Only API 33+ sees the themed override. Older Android versions
            // keep Unity's full-color v26 adaptive-icon XML untouched.
            string v33Folder = folder.Substring(0, folder.Length - 3) + "v33";
            Directory.CreateDirectory(v33Folder);
            doc.Save(Path.Combine(v33Folder, Path.GetFileName(file)));
            count++;
        }

        if (count > 0)
        {
            string drawable = Path.Combine(resRoot, "drawable-nodpi");
            Directory.CreateDirectory(drawable);
            File.Copy(mono, Path.Combine(drawable, "orange_pc_monochrome.png"), true);
        }
        return count;
    }
}
#endif
