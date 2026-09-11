using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace OrangePC.NativeDialogs
{
    // Shared desktop backend for NativeFilePicker and NativeGallery.
    // Synchronous native dialogs return on the calling Unity thread, so callbacks
    // can safely update Textures/UI. No runtime Unity UI or extra native DLL needed.
    public static class DesktopFilePicker
    {
        private static bool busy;
        private static string lastDirectory;
        public static bool IsBusy { get { return busy; } }

        public static bool IsSupported
        {
            get
            {
#if !UNITY_EDITOR && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX)
                return true;
#else
                return false;
#endif
            }
        }

        public static string[] PickFiles(string title, string[] fileTypes, bool multiple)
        {
            if (busy || !IsSupported) return null;
            busy = true;
            var previousLock = UnityEngine.Cursor.lockState;
            bool previousCursor = UnityEngine.Cursor.visible;
            UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            try
            {
                var patterns = NormalizePatterns(fileTypes);
                if (string.IsNullOrEmpty(title)) title = "Select file";
                string[] selected;
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
                selected = PickWindows(title, patterns, multiple);
#elif !UNITY_EDITOR && UNITY_STANDALONE_OSX
                selected = PickMac(title, multiple);
#elif !UNITY_EDITOR && UNITY_STANDALONE_LINUX
                selected = PickLinux(title, patterns, multiple);
#else
                selected = null;
#endif
                if (selected == null || selected.Length == 0) return null;
                var valid = new List<string>();
                foreach (var path in selected)
                    if (!string.IsNullOrEmpty(path) && File.Exists(path)) valid.Add(path);
                if (valid.Count == 0) return null;
                lastDirectory = Path.GetDirectoryName(valid[0]);
                return valid.ToArray();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[DesktopFilePicker] " + e.Message);
                return null;
            }
            finally
            {
                UnityEngine.Cursor.lockState = previousLock;
                UnityEngine.Cursor.visible = previousCursor;
                busy = false;
            }
        }

        // Accept the MIME types / UTIs used by mobile call sites, as well as
        // plain extensions. Never put caller-provided paths or shell syntax in filters.
        public static string[] NormalizePatterns(string[] fileTypes)
        {
            var result = new List<string>();
            bool all = fileTypes == null || fileTypes.Length == 0;
            if (fileTypes != null)
            {
                foreach (var raw in fileTypes)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    foreach (var part in raw.ToLowerInvariant().Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string value = part.Trim();
                        string extensions = null;
                        switch (value)
                        {
                            case "*": case "*.*": case "*/*": case "public.item": case "public.content": case "public.data":
                            case "application/octet-stream": all = true; break;
                            case "image/*": case "public.image": extensions = "png,jpg,jpeg"; break;
                            case "image/jpeg": case "public.jpeg": extensions = "jpg,jpeg"; break;
                            case "image/png": case "public.png": extensions = "png"; break;
                            case "video/*": case "public.movie": case "public.video": extensions = "mp4,mov,webm,avi,m4v,wmv"; break;
                            case "video/mp4": case "public.mpeg-4": extensions = "mp4,m4v"; break;
                            case "video/quicktime": case "com.apple.quicktime-movie": extensions = "mov"; break;
                            case "audio/*": case "public.audio": extensions = "mp3,wav,aac,flac,ogg,m4a"; break;
                            case "audio/mpeg": case "public.mp3": extensions = "mp3"; break;
                            case "audio/wav": case "audio/x-wav": extensions = "wav"; break;
                            case "text/plain": case "public.plain-text": case "public.text": extensions = "txt"; break;
                            default:
                                if (value.StartsWith("*.")) value = value.Substring(2);
                                else if (value.StartsWith(".")) value = value.Substring(1);
                                else if (value.StartsWith("public.")) value = value.Substring(7);
                                if (value.Length > 0 && value.Length <= 20)
                                {
                                    bool safe = true;
                                    foreach (char c in value) if (!char.IsLetterOrDigit(c) && c != '_' && c != '-') { safe = false; break; }
                                    if (safe) extensions = value;
                                    else all = true;
                                }
                                else all = true;
                                break;
                        }
                        if (extensions == null) continue;
                        foreach (var extension in extensions.Split(','))
                        {
                            string pattern = "*." + extension;
                            if (!result.Contains(pattern)) result.Add(pattern);
                        }
                    }
                }
            }
            if (all || result.Count == 0) return new[] { "*.*" };
            return result.ToArray();
        }

        public static string WindowsFilter(string[] patterns)
        {
            string pattern = string.Join(";", patterns);
            if (pattern == "*.*") return "All files\0*.*\0\0";
            return "Supported files (" + pattern + ")\0" + pattern + "\0All files\0*.*\0\0";
        }

        public static string[] ParseWindowsSelection(string buffer)
        {
            if (string.IsNullOrEmpty(buffer)) return null;
            var parts = buffer.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;
            if (parts.Length == 1) return new[] { parts[0] };
            var paths = new string[parts.Length - 1];
            for (int i = 1; i < parts.Length; i++) paths[i - 1] = Path.Combine(parts[0], parts[i]);
            return paths;
        }

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int size;
            public IntPtr owner, instance;
            [MarshalAs(UnmanagedType.LPWStr)] public string filter;
            public IntPtr customFilter;
            public int maxCustomFilter, filterIndex;
            public IntPtr file;
            public int maxFile;
            public IntPtr fileTitle;
            public int maxFileTitle;
            [MarshalAs(UnmanagedType.LPWStr)] public string initialDirectory;
            [MarshalAs(UnmanagedType.LPWStr)] public string title;
            public int flags;
            public short fileOffset, extensionOffset;
            [MarshalAs(UnmanagedType.LPWStr)] public string defaultExtension;
            public IntPtr customData, hook, templateName, reserved;
            public int reservedValue, flagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOpenFileNameW(ref OpenFileName dialog);
        [DllImport("comdlg32.dll", ExactSpelling = true)]
        private static extern int CommDlgExtendedError();
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetActiveWindow();

        private static string[] PickWindows(string title, string[] patterns, bool multiple)
        {
            const int capacity = 32768;
            IntPtr buffer = Marshal.AllocHGlobal(capacity * 2);
            try
            {
                Marshal.Copy(new char[capacity], 0, buffer, capacity);
                var dialog = new OpenFileName
                {
                    size = Marshal.SizeOf(typeof(OpenFileName)), owner = GetActiveWindow(),
                    filter = WindowsFilter(patterns), filterIndex = 1,
                    file = buffer, maxFile = capacity, initialDirectory = lastDirectory,
                    title = title,
                    // Explorer, file/path must exist, no cwd changes, no Recent Documents.
                    flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008 | 0x02000000 | (multiple ? 0x00000200 : 0)
                };
                if (!GetOpenFileNameW(ref dialog))
                {
                    int error = CommDlgExtendedError();
                    if (error != 0) throw new IOException("Windows file dialog error 0x" + error.ToString("X"));
                    return null; // cancel is not an error
                }
                return ParseWindowsSelection(Marshal.PtrToStringUni(buffer, capacity));
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
#endif

#if !UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX)
        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string RunDialog(string executable, string arguments)
        {
            var start = new ProcessStartInfo(executable, arguments)
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = false, CreateNoWindow = true
            };
            using (var process = Process.Start(start))
            {
                if (process == null) return null;
                string selected = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0 ? selected.TrimEnd('\r', '\n') : null;
            }
        }
#endif

#if !UNITY_EDITOR && UNITY_STANDALONE_OSX
        private static string[] PickMac(string title, bool multiple)
        {
            string prompt = title.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string script = multiple
                ? "set pickedFiles to choose file with prompt \"" + prompt + "\" with multiple selections allowed\nset output to \"\"\nrepeat with itemPath in pickedFiles\nset output to output & POSIX path of itemPath & linefeed\nend repeat\nreturn output"
                : "POSIX path of (choose file with prompt \"" + prompt + "\")";
            string selected = RunDialog("/usr/bin/osascript", "-e " + QuoteArgument(script));
            return string.IsNullOrEmpty(selected) ? null : selected.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }
#endif

#if !UNITY_EDITOR && UNITY_STANDALONE_LINUX
        private static string[] PickLinux(string title, string[] patterns, bool multiple)
        {
            string selected;
            try
            {
                string filter = string.Join(" ", patterns);
                selected = RunDialog("zenity", "--file-selection --title=" + QuoteArgument(title)
                    + " --file-filter=" + QuoteArgument("Supported files | " + filter)
                    + " --file-filter=" + QuoteArgument("All files | *")
                    + (multiple ? " --multiple --separator=" + QuoteArgument("\n") : ""));
            }
            catch (System.ComponentModel.Win32Exception)
            {
                selected = RunDialog("kdialog", "--getopenfilename " + QuoteArgument(lastDirectory ?? ".")
                    + " " + QuoteArgument(string.Join(" ", patterns)) + " --title " + QuoteArgument(title)
                    + (multiple ? " --multiple --separate-output" : ""));
            }
            return string.IsNullOrEmpty(selected) ? null : selected.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }
#endif
    }
}
