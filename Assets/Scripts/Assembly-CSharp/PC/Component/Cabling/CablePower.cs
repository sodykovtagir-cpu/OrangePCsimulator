using UnityEngine;

namespace PC.Component.Cabling
{
    public static class CablePower
    {
        public static bool Energized(CablePlug input)
        {
            if (input == null || input.Cable == null || input.Cable.Kind != CableKind.Power) return false;
            var source = input.Cable.End(1 - input.End);
            return source != null && source.Connected != null && source.Connected.Powered;
        }

        public static bool BoardHasPower(Motherboard board)
        {
            if (board == null) return false;
            // Legacy branch uses the original PC assembly/power rules, without
            // wall outlets or separate power cords.
            if (!CableMode.Enabled) return true;
            // An integrated mini PC uses its own adapter port.
            if (board.Integrated && PoweredPort(board, PortKind.PcPower)) return true;
            var supplies = board.GetHardwares(HardwareType.Supply);
            if (supplies != null) foreach (var part in supplies)
                if (part is Supply supply && !supply.Damaged && PoweredPort(supply, PortKind.PcPower))
                    return true;
            var pcCase = board.GetComponentInParent<Case>();
            var connector = board.GetComponent<Connector>();
            if (pcCase == null && connector != null && connector.slot != null)
                pcCase = connector.slot.GetComponentInParent<Case>();
            // Only a mini PC may expose its adapter on the case. A normal PC
            // must actually be cabled to its working PSU, never to the case.
            if (board.Integrated && pcCase != null &&
                PoweredPort(pcCase.GetComponent<Item>(), PortKind.PcPower)) return true;
            return false;
        }

        private static bool PoweredPort(Item owner, PortKind kind)
        {
            if (owner == null) return false;
            var ports = owner.GetComponentsInChildren<CablePort>(true);
            foreach (var port in ports)
                if (port.owner == owner && port.kind == kind && Energized(port.plug)) return true;
            return false;
        }

        private static bool VideoPort(CablePort output, Motherboard board)
        {
            if (output == null || board == null) return false;
            if (CablePorts.Board(output) != board) return false;
            if (output.kind == PortKind.GpuVideo)
            {
                var gpu = CablePorts.Card(output);
                return gpu != null && !gpu.Damaged && gpu.Power;
            }
            if (output.kind != PortKind.BoardVideo) return false;
            var cpus = board.GetHardwares(HardwareType.CPU);
            if (cpus != null) foreach (var item in cpus)
                if (item is CPU cpu && cpu.HasIntegratedGraphics) return true;
            return false;
        }

        // Only called on connection, removal and board power transitions, not
        // every frame. Port count is small; disconnected displays are unpowered.
        public static void RefreshAll()
        {
            // Manual monitor linking and motherboard power were the legacy
            // rules; physical cable events must not override that branch.
            if (!CableMode.Enabled) return;
            // Removing either end of a live mains cable immediately removes
            // power from that PC, not merely its image on the monitor.
            foreach (var board in Object.FindObjectsOfType<Motherboard>())
                if (board != null && board.Running && !BoardHasPower(board))
                    board.PowerOff(false);
            foreach (var display in Object.FindObjectsOfType<Display>())
            {
                if (display == null) continue;
                bool powered = !display.Damaged && PoweredPort(display, PortKind.MonitorPower);
                Motherboard videoBoard = null;
                bool gpuRoute = false;
                GPU videoGpu = null;
                if (powered)
                {
                    var ports = display.GetComponentsInChildren<CablePort>(true);
                    foreach (var input in ports)
                    {
                        if (input.owner != display ||
                            (input.kind != PortKind.VgaInput && input.kind != PortKind.HdmiInput) ||
                            input.plug == null) continue;
                        var cable = input.plug.Cable;
                        var other = cable != null ? cable.End(1 - input.plug.End) : null;
                        var output = other != null ? other.Connected : null;
                        var board = CablePorts.Board(output);
                        if (board != null && board.Running && VideoPort(output, board))
                        {
                            videoBoard = board;
                            gpuRoute = output.kind == PortKind.GpuVideo;
                            videoGpu = gpuRoute ? CablePorts.Card(output) : null;
                            break;
                        }
                    }
                }
                if (display.ConnectedBoard != null && display.ConnectedBoard != videoBoard)
                    display.DisconnectBoard();
                if (videoBoard != null && display.ConnectedBoard != videoBoard)
                    videoBoard.ConnectMonitor(display);
                display.VideoFromGpu = gpuRoute;
                display.VideoGpu = videoGpu;
                display.Switch(powered, false);
                display.ShowNoSignal(powered && videoBoard == null);
            }
        }
    }
}
