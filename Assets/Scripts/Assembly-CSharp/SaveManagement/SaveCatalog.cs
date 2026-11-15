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
                if (folder == null)
                {
                    folder = directory;
                    // Compact metadata/cover cache only; no world parsing here.
                    // Published before the background validation starts.
                    snapshot = ReadMenuCache(directory);
                    foreach (var entry in snapshot) cache[entry.Path] = entry;
                    if (snapshot.Length > 0) revision++;
                }
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
                        if (item == null || (item.Loader != null && item.Loader.IsPreviewOnly) || item.Size != info.Length || item.ModifiedUtc != info.LastWriteTimeUtc)
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
                    var ready = rows.ToArray();
                    bool changed;
                    lock (gate)
                    {
                        changed = next.Count != cache.Count;
                        foreach (var pair in next)
                        {
                            Entry old;
                            if (!cache.TryGetValue(pair.Key, out old) || !object.ReferenceEquals(old, pair.Value)) changed = true;
                        }
                        cache = next; snapshot = ready; Error = null; revision++;
                    }
                    if (changed) WriteMenuCache(ready);
                }
                catch (Exception e) { lock (gate) { Error = e.Message; revision++; } }
                lock (gate)
                {
                    if (again) { again = false; continue; }
                    scanning = false; return;
                }
            }
        }
        // Disposable bounded cache, not a save format. Only filenames and menu
        // fields are persisted: never the alternate world/site state/coins.
        const int CacheVersion = 1, MaxRows = 4096, MaxCacheBytes = 12 * 1024 * 1024;
        static string CachePath(string directory) { return System.IO.Path.Combine(directory, ".menu-index-v1.cache"); }
        static string ReadText(BinaryReader reader, int max)
        {
            int size = reader.ReadInt32();
            if (size < 0 || size > max) throw new InvalidDataException("Cache string budget");
            var bytes = reader.ReadBytes(size);
            if (bytes.Length != size) throw new EndOfStreamException();
            return new System.Text.UTF8Encoding(false, true).GetString(bytes);
        }
        static void Text(BinaryWriter writer, string value)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty);
            writer.Write(bytes.Length); writer.Write(bytes);
        }
        static Entry[] ReadMenuCache(string directory)
        {
            try
            {
                string path = CachePath(directory);
                if (!File.Exists(path) && File.Exists(path + ".previous")) path += ".previous";
                if (!File.Exists(path) || new FileInfo(path).Length > MaxCacheBytes) return new Entry[0];
                using (var reader = new BinaryReader(File.OpenRead(path)))
                {
                    if (reader.ReadInt32() != CacheVersion) return new Entry[0];
                    int count = reader.ReadInt32();
                    if (count < 0 || count > MaxRows) return new Entry[0];
                    var entries = new List<Entry>(); var seen = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < count; i++)
                    {
                        string name = ReadText(reader, 1024);
                        if (name != System.IO.Path.GetFileName(name) || name.IndexOfAny(new[]{'/', '\\'}) >= 0 ||
                            !name.EndsWith(SaveUtility.extension, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Cache filename");
                        string savePath = System.IO.Path.Combine(directory, name);
                        if (!seen.Add(savePath)) throw new InvalidDataException("Duplicate cache entry");
                        long size = reader.ReadInt64(); if (size < 0) throw new InvalidDataException();
                        var modified = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
                        var display = new GameData { roomName = ReadText(reader, 4096),
                            hardcore = reader.ReadBoolean(), workshopId = reader.ReadInt32(), icon = ReadText(reader, 524288) };
                        entries.Add(new Entry { Path = savePath, Size = size, ModifiedUtc = modified,
                            Loader = DataLoader.FromPreview(savePath, display) });
                    }
                    if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException();
                    return entries.ToArray();
                }
            }
            catch { return new Entry[0]; } // cache failure must never break saves
        }
        static void ReplaceMenuCache(string path, string pending)
        {
            string previous = path + ".previous";
            if (File.Exists(previous)) File.Delete(previous);
            File.Move(path, previous);
            try { File.Move(pending, path); }
            catch { if (!File.Exists(path)) File.Move(previous, path); throw; }
            File.Delete(previous);
        }
        static void WriteMenuCache(Entry[] entries)
        {
            string path = CachePath(folder), pending = path + ".tmp";
            try
            {
                Directory.CreateDirectory(folder);
                using (var memory = new MemoryStream())
                using (var writer = new BinaryWriter(memory))
                {
                    var usable = new List<Entry>();
                    foreach (var e in entries)
                        if (e.Loader != null && !e.Loader.IsPreviewOnly && usable.Count < MaxRows) usable.Add(e);
                    writer.Write(CacheVersion); writer.Write(usable.Count);
                    int covers = 0, coverBytes = 0;
                    foreach (var e in usable)
                    {
                        var data = e.Loader.GameData;
                        string name = data.roomName ?? string.Empty;
                        if (name.Length > 1024) name = name.Substring(0, 1024);
                        string icon = data.icon ?? string.Empty;
                        // Match the RAM texture budget; don't copy all embedded
                        // screenshots/worlds into a second giant save file.
                        if (covers >= 64 || icon.Length > 524288 || coverBytes + icon.Length > 4 * 1024 * 1024) icon = string.Empty;
                        if (icon.Length > 0) { covers++; coverBytes += icon.Length; }
                        Text(writer, System.IO.Path.GetFileName(e.Path)); writer.Write(e.Size); writer.Write(e.ModifiedUtc.Ticks);
                        Text(writer, name); writer.Write(data.hardcore); writer.Write(data.workshopId); Text(writer, icon);
                    }
                    writer.Flush();
                    if (memory.Length > MaxCacheBytes) return;
                    var dataBytes = memory.ToArray();
                    using (var stream = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None))
                    { stream.Write(dataBytes, 0, dataBytes.Length); stream.Flush(true); }
                }
                // A cache is disposable. Unlike save transactions this never
                // invokes recovery, rewrites a .opc or touches .bak/.pending.
                if (!File.Exists(path)) { File.Move(pending, path); return; }
                try { File.Replace(pending, path, null); }
                catch (PlatformNotSupportedException) { ReplaceMenuCache(path, pending); }
                catch (IOException) { if (File.Exists(pending)) ReplaceMenuCache(path, pending); }
            }
            catch { /* read-only/full storage: retain the in-memory list */ }
            finally { try { if (File.Exists(pending)) File.Delete(pending); } catch { } }
        }

    }
}
