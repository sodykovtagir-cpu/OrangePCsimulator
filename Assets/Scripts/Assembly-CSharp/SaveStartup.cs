using System;
using System.Collections;
using System.Collections.Generic;
using SaveManagement;
using UnityEngine;

public sealed class SaveStartup : MonoBehaviour
{
    static SaveStartup instance;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    static void Begin()
    {
        try { SaveCatalog.Initialize(SaveUtility.GetFolderPath()); }
        catch (Exception e) { Debug.LogWarning("[Saves] Startup index: " + e.Message); }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (instance != null) return;
        var go = new GameObject("Save preloader");
        DontDestroyOnLoad(go); instance = go.AddComponent<SaveStartup>();
    }
    IEnumerator Start()
    {
        Begin(); Application.lowMemory += OnLowMemory;
        int seen = -1;
        while (true)
        {
            if (seen != SaveCatalog.Revision)
            {
                seen = SaveCatalog.Revision;
                var entries = SaveCatalog.Snapshot();
                SaveCoverCache.Prune(entries);
                for (int i = 0; i < Math.Min(64, entries.Length); i++)
                {
                    var data = entries[i].Loader != null ? entries[i].Loader.GameData : null;
                    if (data != null) SaveCoverCache.Get(data.icon);
                    yield return null; // texture upload is main-thread only; spread it out
                }
            }
            yield return null;
        }
    }
    void OnLowMemory() { SaveCoverCache.Clear(); }
    void OnDestroy()
    {
        if (instance != this) return;
        Application.lowMemory -= OnLowMemory; SaveCoverCache.Clear(); instance = null;
    }
}

public static class SaveCoverCache
{
    const int Limit = 64;
    const long ByteBudget = 8*1024*1024;
    static long bytesUsed;
    static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    public static Texture2D Get(string encoded)
    {
        if (string.IsNullOrEmpty(encoded) || encoded.Length > 524288) return null;
        Texture2D texture;
        if (textures.TryGetValue(encoded, out texture)) return texture;
        texture = null;
        try
        {
            var bytes = Convert.FromBase64String(encoded);
            // Save screenshots are PNG; reject huge/malformed covers before GPU allocation.
            if (bytes.Length < 24 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71) return null;
            long w = ((long)bytes[16]<<24)|((long)bytes[17]<<16)|((long)bytes[18]<<8)|bytes[19];
            long h = ((long)bytes[20]<<24)|((long)bytes[21]<<16)|((long)bytes[22]<<8)|bytes[23];
            if (w < 1 || h < 1 || w > 512 || h > 512 || bytesUsed+w*h*4 > ByteBudget) return null;
            texture = new Texture2D(2,2,TextureFormat.RGBA32,false);
            if (!texture.LoadImage(bytes, true)) { UnityEngine.Object.Destroy(texture); texture = null; }
        }
        catch (Exception) { if (texture != null) UnityEngine.Object.Destroy(texture); texture = null; }
        // Do not evict textures referenced by visible save rows. Bound the cache;
        // rows beyond the first 64 can simply use the default cover.
        if (textures.Count >= Limit) { if (texture != null) UnityEngine.Object.Destroy(texture); return null; }
        textures[encoded] = texture; if (texture != null) bytesUsed += (long)texture.width*texture.height*4; return texture;
    }
    public static void Prune(SaveCatalog.Entry[] entries)
    {
        var keep = new HashSet<string>();
        for (int i=0; i<Math.Min(Limit,entries.Length); i++)
            if (entries[i].Loader != null && !string.IsNullOrEmpty(entries[i].Loader.GameData.icon)) keep.Add(entries[i].Loader.GameData.icon);
        foreach (string key in new List<string>(textures.Keys))
            if (!keep.Contains(key)) { var texture=textures[key]; if(texture!=null){bytesUsed-=(long)texture.width*texture.height*4;UnityEngine.Object.Destroy(texture);}textures.Remove(key); }
    }
    public static void Clear()
    {
        foreach (var texture in textures.Values) if (texture != null) UnityEngine.Object.Destroy(texture);
        textures.Clear(); bytesUsed=0;
    }
}
