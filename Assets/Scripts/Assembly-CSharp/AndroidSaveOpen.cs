using System;
using System.IO;
using SaveManagement;
using UnityEngine;

/// <summary>Imports Android ACTION_VIEW content URIs into the game's save list.</summary>
public sealed class AndroidSaveOpen : MonoBehaviour
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidSaveOpen instance;
    private float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var go = new GameObject("AndroidSaveOpen");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AndroidSaveOpen>();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.5f;

        try
        {
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var bridge = new AndroidJavaClass("com.orangepcsim.saveopen.OrangeSaveOpen"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (activity == null) return;
                string incoming = bridge.CallStatic<string>("takeIncomingSave", activity);
                if (string.IsNullOrEmpty(incoming)) return;
                int split = incoming.IndexOf('\n');
                if (split <= 0 || split == incoming.Length - 1) return;
                string name = incoming.Substring(0, split);
                string cached = incoming.Substring(split + 1);
                try
                {
                    // JNI bridge already limited input size and validated the suffix.
                    string saved = SaveFileImport.Import(cached, name);
                    Debug.Log("[SaveOpen] Imported " + Path.GetFileName(saved));
                    if (FileMenu.Instance != null) FileMenu.Instance.RefreshFromDisk();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[SaveOpen] Could not import save: " + e.Message);
                }
                finally
                {
                    try { File.Delete(cached); } catch (IOException) { }
                }
            }
        }
        catch (AndroidJavaException e)
        {
            Debug.LogWarning("[SaveOpen] Android open-with bridge unavailable: " + e.Message);
            enabled = false; // avoid repeating if plugin is absent in a broken build
        }
    }
#endif
}
