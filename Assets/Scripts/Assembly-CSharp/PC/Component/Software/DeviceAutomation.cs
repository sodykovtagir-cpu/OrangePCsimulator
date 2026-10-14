using System.Collections;
using UnityEngine;

namespace PC.Component.Software
{
    /// <summary>Real motherboard power/OS/app actions shared by manager and Terminal.</summary>
    public static class DeviceAutomation
    {
        public static Motherboard Find(int id)
        {
            foreach (var board in UnityEngine.Object.FindObjectsOfType<Motherboard>())
                if (board != null && board.Id == id) return board;
            return null;
        }

        public static Motherboard[] All() => UnityEngine.Object.FindObjectsOfType<Motherboard>();

        // A Mini_ITX/ATX board is used by both kinds of device. Identify the
        // chassis through its external slots, which Case attaches to the board
        // when the board is plugged in. Never classify by motherboard.name.
        public static bool IsMiner(Motherboard board)
        {
            if (board == null) return false;
            Case chassis = null;
            foreach (var slot in board.GetSlots(HardwareType.GPU))
            {
                if (slot == null) continue;
                chassis = slot.GetComponentInParent<Case>();
                if (chassis != null) break;
            }
            if (chassis == null)
                foreach (var slot in board.GetSlots(HardwareType.Drive))
                {
                    if (slot == null) continue;
                    chassis = slot.GetComponentInParent<Case>();
                    if (chassis != null) break;
                }
            if (chassis == null) chassis = board.GetComponentInParent<Case>();
            return chassis != null && (chassis.name.StartsWith("Miner", System.StringComparison.OrdinalIgnoreCase)
                || chassis.name.StartsWith("BigMiner", System.StringComparison.OrdinalIgnoreCase));
        }

        public static bool Supports(Motherboard board, string app)
        {
            if (board == null) return false;
            var os = board.System as global::PC.Component.Software.OS.OperatingSystem;
            if (os != null && os.Ready) return os.IsAppInstalled(app);
            var drives = board.GetHardwares(HardwareType.Drive);
            if (drives == null) return false;
            foreach (var drive in drives)
                if (drive is Storage disk && disk.ContainsFile(app + ".exe")) return true;
            return false;
        }

        public static int RunAll(global::PC.Component.Software.OS.OperatingSystem source, string action)
        {
            string app = action == "video" ? "Video Player" : "EZ Mining";
            if (action != "video" && action != "bitcoin" && action != "cash") return 0;
            int count = 0;
            foreach (var board in All())
            {
                if (!Supports(board, app)) continue;
                if (Run(source, board, action)) count++;
            }
            return count;
        }

        public static bool Run(global::PC.Component.Software.OS.OperatingSystem source, Motherboard board, string action, string argument = "")
        {
            if (source == null || board == null) return false;
            if (action == "startup")
                return board.Running || string.IsNullOrEmpty(board.Boot());
            if (action == "shutdown")
            {
                var os = board.System as global::PC.Component.Software.OS.OperatingSystem;
                if (os == null || !os.Ready) return false;
                os.PowerClicked();
                return true;
            }
            if (action != "bitcoin" && action != "cash" && action != "video"
                && action != "app" && action != "file") return false;
            if ((action == "bitcoin" || action == "cash") && !Supports(board, "EZ Mining")) return false;
            if (action == "video" && !Supports(board, "Video Player")) return false;
            if (action == "app" && !Supports(board, argument)) return false;
            if (!board.Running && !string.IsNullOrEmpty(board.Boot())) return false;
            // Host this coroutine on the OS, NOT the Terminal window: .tmn
            // windows close immediately after a command, before BIOS finishes.
            source.StartCoroutine(WhenReady(board, action, argument));
            return true;
        }

        private static IEnumerator WhenReady(Motherboard board, string action, string argument)
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            global::PC.Component.Software.OS.OperatingSystem os = null;
            while (board != null && board.Running && Time.realtimeSinceStartup < deadline)
            {
                os = board.System as global::PC.Component.Software.OS.OperatingSystem;
                if (os != null && os.Ready) break;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (os == null || !os.Ready) yield break;
            if (action == "bitcoin" || action == "cash")
                os.TryLaunchApp("EZ Mining", action);
            else if (action == "video")
                os.TryLaunchApp("Video Player");
            else if (action == "app")
                os.TryLaunchApp(argument);
            else if (action == "file")
            {
                File file;
                if (os.FileManager != null && os.FileManager.TryGetFile(0, argument, out file))
                    os.OpenFile(file);
            }
        }
    }
}
