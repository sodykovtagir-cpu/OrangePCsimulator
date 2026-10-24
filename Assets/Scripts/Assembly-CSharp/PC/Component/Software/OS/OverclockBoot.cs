using System;
using System.Collections.Generic;
using PC.Component;
using UnityEngine;

namespace PC.Component.Software.OS
{
    /// <summary>PCOS startup profile; BIOS still needs the original System/boot.bin = pcos.</summary>
    public static class OverclockBoot
    {
        [Serializable]
        public class Profile
        {
            public int version = 1;
            public float cpuFrequency;
            public float gpu = 1f;
            public float ram = 1f;
        }

        public const string Path = "System/overclockerd-boot.bin";
        public const string Command = "boot overclockerd-boot.bin";
        public const string Comment = "// Overclock boot: remove the next line and uncomment boot boot.bin to recover";
        public const string LegacyComment = "// Overclock boot: remove the next line and uncomment run boot.bin to recover";
        public const float UnstableRatio = 1.55f;
        public const float FatalRatio = 1.85f;
        private static readonly Dictionary<Motherboard, Profile> active = new Dictionary<Motherboard, Profile>();

        public static bool Enabled(string autorun)
        {
            bool debug;
            return AutorunConfig.HasBoot(autorun, "overclockerd-boot.bin", out debug);
        }

        public static bool TryRead(FileManager fm, out Profile profile)
        {
            profile = null;
            File file;
            if (fm == null || !fm.TryGetFile(0, Path, out file) || file == null) return false;
            try { profile = JsonUtility.FromJson<Profile>(file.content); }
            catch { return false; }
            return Valid(profile);
        }

        private static bool Valid(Profile p)
        {
            return p != null && p.version == 1 &&
                !float.IsNaN(p.gpu) && !float.IsInfinity(p.gpu) &&
                !float.IsNaN(p.ram) && !float.IsInfinity(p.ram) &&
                !float.IsNaN(p.cpuFrequency) && !float.IsInfinity(p.cpuFrequency) &&
                p.gpu >= 1f && p.gpu <= 2f && p.ram >= 1f && p.ram <= 2f &&
                p.cpuFrequency >= 0f && p.cpuFrequency <= 20f;
        }

        public static bool Unstable(Profile p) => !Valid(p) || p.gpu >= UnstableRatio || p.ram >= UnstableRatio;
        public static bool Fatal(Profile p) => Valid(p) && (p.gpu >= FatalRatio || p.ram >= FatalRatio);

        public static void Apply(Motherboard board, Profile p)
        {
            if (board == null) return;
            bool resetCpu = p == null && active.ContainsKey(board);
            if (p == null) { active.Remove(board); p = new Profile(); }
            else active[board] = p;
            var cards = board.GetHardwares(HardwareType.GPU);
            if (cards != null)
                foreach (var item in cards)
                    if (item is GPU gpu) gpu.OverclockRatio = p.gpu;
            if (p.cpuFrequency > 0f || resetCpu)
            {
                var cpus = board.GetHardwares(HardwareType.CPU);
                if (cpus != null)
                    foreach (var item in cpus)
                        if (item is CPU cpu && !cpu.Damaged)
                            cpu.frequency = resetCpu ? cpu.defaultFrequency : p.cpuFrequency;
            }
        }

        public static float GpuMultiplier(Motherboard board) =>
            board != null && active.TryGetValue(board, out var p) ? p.gpu : 1f;
        public static float RamMultiplier(Motherboard board) =>
            board != null && active.TryGetValue(board, out var p) ? p.ram : 1f;

        public static void BreakExtreme(Motherboard board, Profile p)
        {
            if (board == null || !Fatal(p)) return;
            if (p.gpu >= FatalRatio)
            {
                var cards = board.GetHardwares(HardwareType.GPU);
                if (cards != null)
                    foreach (var item in cards)
                        if (item is GPU gpu && !gpu.Damaged) gpu.Damage();
            }
            if (p.ram >= FatalRatio)
            {
                var memory = board.GetHardwares(HardwareType.RAM);
                if (memory != null)
                    foreach (var item in memory)
                        if (item != null && !item.Damaged) item.Damage();
            }
        }

        // Do not overwrite unrelated startup commands. Switching is reversible
        // from another PC, even when this PCOS never reaches its desktop.
        public static string EnableLine(string content)
        {
            var lines = new List<string>();
            bool inserted = false;
            foreach (var raw in (content ?? "").Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line == Command || line == Command + " -debug" ||
                    line == "run overclockerd-boot.bin" || line == Comment || line == LegacyComment) continue;
                bool stock = line == "boot boot.bin" || line == "boot boot.bin -debug" ||
                    line == "//boot boot.bin" || line == "//boot boot.bin -debug" ||
                    line == "run boot.bin" || line == "//run boot.bin";
                if (stock)
                {
                    if (!inserted)
                    {
                        bool debug = line.EndsWith(" -debug", StringComparison.Ordinal) ||
                            AutorunConfig.DebugRequested(content);
                        lines.Add("//boot boot.bin" + (debug ? " -debug" : ""));
                        lines.Add(Comment);
                        lines.Add(Command + (debug ? " -debug" : ""));
                        inserted = true;
                    }
                }
                else if (raw.Length > 0) lines.Add(raw);
            }
            if (!inserted) return null; // do not resurrect a deleted boot directive
            return string.Join("\n", lines.ToArray()) + "\n";
        }

        public static string DisableLine(string content)
        {
            var lines = new List<string>();
            bool restored = false;
            bool debug = AutorunConfig.DebugRequested(content) ||
                AutorunConfig.HasLine(content, "//boot boot.bin -debug");
            foreach (var raw in (content ?? "").Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line == Command || line == Command + " -debug" ||
                    line == "run overclockerd-boot.bin" || line == Comment || line == LegacyComment) continue;
                if (line == "//boot boot.bin" || line == "//boot boot.bin -debug" ||
                    line == "//run boot.bin" || line == "boot boot.bin" ||
                    line == "boot boot.bin -debug" || line == "run boot.bin")
                {
                    if (!restored) { lines.Add("boot boot.bin" + (debug ? " -debug" : "")); restored = true; }
                }
                else if (raw.Length > 0) lines.Add(raw);
            }
            if (!restored) lines.Insert(0, "boot boot.bin" + (debug ? " -debug" : ""));
            return string.Join("\n", lines.ToArray()) + "\n";
        }

        public static bool Save(FileManager fm, Profile p)
        {
            if (!Valid(p) || fm == null) return false;
            File autorun;
            if (!fm.TryGetFile(0, AutorunConfig.Path, out autorun) || autorun == null) return false;
            string updated = EnableLine(autorun.content);
            if (updated == null) return false;
            File previous;
            bool hadProfile = fm.TryGetFile(0, Path, out previous) && previous != null;
            string oldBody = hadProfile ? previous.content : null;
            bool wasHidden = hadProfile && previous.hidden;
            string body = JsonUtility.ToJson(p);
            var script = fm.Write(0, Path, body);
            File onDisk;
            if (script == null || !fm.TryGetFile(0, Path, out onDisk) ||
                onDisk == null || onDisk.content != body) return false;
            var written = fm.Write(0, AutorunConfig.Path, updated);
            if (written == null || written.content != updated)
            {
                // Replacing an already active profile must never leave the PC
                // with a new dangerous clock and the previous startup line.
                if (hadProfile) { fm.Write(0, Path, oldBody); onDisk.hidden = wasHidden; }
                else fm.Delete(0, Path);
                return false;
            }
            onDisk.hidden = true;
            return true;
        }

        // A stock preset removes both the startup command and its custom file.
        // Keep unrelated autorun commands and never touch System/boot.bin.
        public static bool ResetToStock(FileManager fm)
        {
            if (fm == null || !fm.TryGetFile(0, AutorunConfig.Path, out var autorun) || autorun == null)
                return false;
            // Never silently repair a missing/user-replaced boot directive.
            bool stockDebug;
            if (!Enabled(autorun.content) &&
                !AutorunConfig.HasBoot(autorun.content, "boot.bin", out stockDebug) &&
                !AutorunConfig.HasLine(autorun.content, "//boot boot.bin") &&
                !AutorunConfig.HasLine(autorun.content, "//boot boot.bin -debug") &&
                !AutorunConfig.HasLine(autorun.content, "//run boot.bin")) return false;
            string body = DisableLine(autorun.content);
            var written = fm.Write(0, AutorunConfig.Path, body);
            if (written == null || written.content != body) return false;
            fm.Delete(0, Path);
            return !fm.Exists(0, Path);
        }

        public static bool Recover(Storage disk)
        {
            File autorun;
            if (disk == null || !disk.TryGetFile(AutorunConfig.Path, out autorun) || autorun == null)
                return false;
            if (!Enabled(autorun.content) &&
                !( (AutorunConfig.HasLine(autorun.content, Comment) ||
                    AutorunConfig.HasLine(autorun.content, LegacyComment)) &&
                   (AutorunConfig.HasLine(autorun.content, "//boot boot.bin") ||
                    AutorunConfig.HasLine(autorun.content, "//boot boot.bin -debug") ||
                    AutorunConfig.HasLine(autorun.content, "//run boot.bin")))) return false;
            string body = DisableLine(autorun.content);
            var written = disk.Write(AutorunConfig.Path, body);
            return written != null && written.content == body;
        }
    }
}
