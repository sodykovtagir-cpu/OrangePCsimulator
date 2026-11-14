using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SaveManagement
{
    // Worker owns disk IO, authentication and header JSON decoding. No textures,
    // GameObjects or Application paths are accessed by this worker.
    public static class SaveCatalog
    {
        public sealed class Entry
        {
            public string Path, Error;
            public long Size;
            public DateTime ModifiedUtc;
            public DataLoader Loader;
        }
        static readonly object gate = new object();
        static Dictionary<string, Entry> cache = new Dictionary<string, Entry>(StringComparer.Ordinal);
        static Entry[] snapshot = new Entry[0];
        static string folder;
        static bool scanning, again, recovered;
        static int revision;
        public static bool IsScanning { get { lock (gate) return scanning; } }
        public static int Revision { get { lock (gate) return revision; } }
        public static string Error { get; private set; }
        public static Entry[] Snapshot() { lock (gate) return (Entry[])snapshot.Clone(); }
        public static void Initialize(string directory)
        {
            lock (gate)
            {
                if (folder != null && folder != directory) throw new InvalidOperationException("Save folder already initialized");
                folder = directory;
            }
            Refresh();
        }
        public static void Refresh()
        {
            lock (gate)
            {
                if (folder == null) return;
                if (scanning) { again = true; return; }
                scanning = true;
                Task.Run((Action)Scan);
            }
        }
        public static void Invalidate(string path)
        {
            lock (gate) { cache.Remove(path); }
            Refresh();
        }
        static void Scan()
        {
            for (;;)
            {
                try
                {
                    if (!recovered) { SaveStorage.RecoverDirectory(folder); recovered = true; }
                    var next = new Dictionary<string, Entry>(StringComparer.Ordinal);
                    foreach (string path in Directory.GetFiles(folder, "*" + SaveUtility.extension))
                    {
                        var info = new FileInfo(path); Entry item;
                        lock (gate) cache.TryGetValue(path, out item);
                        if (item == null || item.Size != info.Length || item.ModifiedUtc != info.LastWriteTimeUtc)
                        {
                            item = new Entry { Path = path, Size = info.Length, ModifiedUtc = info.LastWriteTimeUtc };
                            try { item.Loader = new DataLoader(path); item.Loader.LoadHeaderFromPath(); }
                            catch (Exception e) { item.Loader = null; item.Error = e.Message; }
                            // A concurrent commit is picked up by its Invalidate/refresh.
                        }
                        next[path] = item;
                    }
                    var rows = new List<Entry>(next.Values);
                    rows.Sort((a,b) => { int order = b.ModifiedUtc.CompareTo(a.ModifiedUtc); return order != 0 ? order : StringComparer.Ordinal.Compare(a.Path,b.Path); });
                    lock (gate) { cache = next; snapshot = rows.ToArray(); Error = null; revision++; }
                }
                catch (Exception e) { lock (gate) { Error = e.Message; revision++; } }
                lock (gate)
                {
                    if (again) { again = false; continue; }
                    scanning = false; return;
                }
            }
        }
    }
}
