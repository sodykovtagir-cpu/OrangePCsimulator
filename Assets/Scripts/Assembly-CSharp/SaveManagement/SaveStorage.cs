using System;
using System.IO;
using System.Text;

namespace SaveManagement
{
    // Never truncate the only good copy. Android can kill a paused application
    // between writes; retain a durable previous version until the new commit.
    public static class SaveStorage
    {
        static readonly object gate = new object();
        public static void WriteAtomic(string path, string encoded)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Save path is empty");
            lock (gate)
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                string pending = path + ".pending", backup = path + ".bak";
                byte[] bytes = new UTF8Encoding(false).GetBytes(encoded);
                using (var stream = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (!File.Exists(path)) { File.Move(pending, path); return; }
                try { File.Replace(pending, path, backup); }
                catch (PlatformNotSupportedException) { ReplacePortable(path, pending, backup); }
                catch (IOException) { if (!File.Exists(pending)) throw; ReplacePortable(path, pending, backup); }
            }
        }
        static void ReplacePortable(string path, string pending, string backup)
        {
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(path, backup);
            try { File.Move(pending, path); }
            catch { if (!File.Exists(path)) File.Move(backup, path); throw; }
        }
        static bool Valid(string path)
        {
            if (!File.Exists(path)) return false;
            try { var loader = new DataLoader(path); loader.LoadHeaderFromPath(); return loader.GameData != null; }
            catch { return false; }
        }
        public static void RecoverDirectory(string folder)
        {
            lock (gate)
            {
                var paths = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                foreach (string suffix in new[] { ".bak", ".pending" })
                    foreach (string sidecar in Directory.GetFiles(folder, "*.opc" + suffix))
                        paths.Add(sidecar.Substring(0, sidecar.Length - suffix.Length));
                foreach (string path in paths)
                {
                    if (Valid(path)) continue;
                    string source = Valid(path + ".pending") ? path + ".pending" : Valid(path + ".bak") ? path + ".bak" : null;
                    if (source == null) continue;
                    // Keep a damaged original for diagnosis, not as a visible save.
                    if (File.Exists(path)) File.Move(path, path + ".damaged-" + Guid.NewGuid().ToString("N"));
                    File.Copy(source, path);
                }
            }
        }
        public static void Delete(string path)
        {
            lock (gate)
                foreach (string suffix in new[] { ".pending", ".bak", "" })
                    if (File.Exists(path + suffix)) File.Delete(path + suffix);
            SaveCatalog.Invalidate(path);
        }
    }
}
