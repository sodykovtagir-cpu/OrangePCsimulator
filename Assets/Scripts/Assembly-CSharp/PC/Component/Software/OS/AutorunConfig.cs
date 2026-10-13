using System;
using System.Collections.Generic;
using PC.Component.Software;

namespace PC.Component.Software.OS
{
    /// <summary>On-disk startup contract. Never silently repair an intentionally edited file.</summary>
    public static class AutorunConfig
    {
        public const string Path = "System/Autorun.conf";
        public const string Marker = "System/autorun.initialized";
        public const string Default = "//Initialize system DO NOT REMOVE!!!!\nrun boot.bin\ninit devices\ninit user\n";

        // Old saves did not have Autorun.conf. Create it only once; after that a
        // deleted/edited file must be allowed to cause the in-game boot failure.
        public static void Ensure(FileManager fm)
        {
            if (fm == null || fm.Exists(0, Marker)) return;
            if (!fm.Exists(0, Path)) fm.Write(0, Path, Default);
            if (!fm.Exists(0, Path)) return; // do not mark an unsuccessful migration
            var marker = fm.Write(0, Marker, "1");
            if (marker != null) marker.hidden = true;
        }

        public static string Read(FileManager fm)
        {
            File file;
            return fm != null && fm.TryGetFile(0, Path, out file) && file != null
                ? file.content ?? "" : "";
        }

        public static bool HasLine(string content, string required)
        {
            if (string.IsNullOrEmpty(content)) return false;
            var lines = content.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
                if (string.Equals(lines[i].Trim(), required, StringComparison.Ordinal)) return true;
            return false;
        }

        public static string[] UserCommands(string content)
        {
            var commands = new List<string>();
            if (string.IsNullOrEmpty(content)) return commands.ToArray();
            foreach (var raw in content.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)
                    || line == "run boot.bin" || line == "init devices" || line == "init user") continue;
                commands.Add(line);
            }
            return commands.ToArray();
        }

        public static bool AppendCommand(FileManager fm, string command)
        {
            if (fm == null || string.IsNullOrEmpty(command)) return false;
            File file;
            if (!fm.TryGetFile(0, Path, out file) || file == null) return false;
            string current = file.content ?? "";
            if (HasLine(current, command)) return true;
            string next = current.TrimEnd('\r', '\n') + "\n" + command + "\n";
            var written = fm.Write(0, Path, next);
            return written != null && written.content == next;
        }
    }
}
