using UnityEngine;

namespace PC.Component.Cabling
{
    public static class CablePorts
    {
        // Offsets are deliberately serialized on the device via child transforms:
        // designers may move the generated port to the exact rear-panel socket.
        public static void Install(Supply supply)
        {
            CablePort.Add(supply, PortKind.PcPower, "AC input",
                new Vector3(0f, 0f, RearZ(supply, -.13f)));
        }

        public static void Install(Motherboard board)
        {
            if (board == null) return;
            float rear = RearZ(board, -.12f);
            CablePort.Add(board, PortKind.BoardVideo, "Board video", new Vector3(-.12f, 0f, rear));
            if (board.Integrated)
                CablePort.Add(board, PortKind.PcPower, "Mini PC AC input", new Vector3(.12f, 0f, rear));
        }

        public static void Install(GPU gpu)
        {
            CablePort.Add(gpu, PortKind.GpuVideo, "GPU video",
                new Vector3(0f, 0f, RearZ(gpu, -.12f)));
        }

        public static void Install(Display display)
        {
            if (display == null) return;
            bool crt = display is Monitor && display.spawnId == "CRT_Monitor";
            float rear = RearZ(display, -.11f);
            CablePort.Add(display, crt ? PortKind.VgaInput : PortKind.HdmiInput,
                crt ? "VGA input" : "HDMI input", new Vector3(-.12f, 0f, rear));
            CablePort.Add(display, PortKind.MonitorPower, "Display AC input", new Vector3(.12f, 0f, rear));
        }

        public static void Install(Case pcCase)
        {
            if (pcCase == null) return;
            var owner = pcCase.GetComponent<Item>();
            if (owner == null) return;
            float rear = RearZ(owner, -.23f);
            CablePort.Add(owner, PortKind.PcPower, "Case AC input", new Vector3(.24f, .15f, rear));
            CablePort.Add(owner, PortKind.BoardVideo, "Case board video", new Vector3(-.24f, .15f, rear));
            CablePort.Add(owner, PortKind.GpuVideo, "Case GPU video", new Vector3(-.24f, -.10f, rear));
        }

        private static float RearZ(Item owner, float fallback)
        {
            // Scene meshes have very different scales (CRT depth > 2 units,
            // PSU < 1). A hard-coded -0.1 port is buried inside their solids.
            float back = float.PositiveInfinity;
            foreach (var box in owner.GetComponents<BoxCollider>())
                if (box.enabled && !box.isTrigger)
                    back = Mathf.Min(back, box.center.z - box.size.z * .5f);
            return float.IsInfinity(back) ? fallback : Mathf.Min(fallback, back - .07f);
        }

        public static Motherboard Board(CablePort port)
        {
            if (port == null || port.owner == null) return null;
            if (port.owner is Motherboard mb) return mb;
            if (port.owner is GPU gpu)
            {
                var parent = gpu.GetComponentInParent<Motherboard>();
                if (parent != null) return parent;
                var connector = gpu.GetComponent<Connector>();
                if (connector != null && connector.slot != null)
                    return connector.slot.GetComponentInParent<Motherboard>();
            }
            if (port.owner is Supply supply)
            {
                var parent = supply.GetComponentInParent<Motherboard>();
                if (parent != null) return parent;
                var connector = supply.GetComponent<Connector>();
                if (connector != null && connector.slot != null)
                {
                    var pcCase = connector.slot.GetComponentInParent<Case>();
                    if (pcCase != null && pcCase.Motherboard != null)
                        return pcCase.Motherboard.Hardware as Motherboard;
                }
            }
            var pcCase = port.owner.GetComponent<Case>();
            return pcCase != null && pcCase.Motherboard != null
                ? pcCase.Motherboard.Hardware as Motherboard : null;
        }

        public static GPU Card(CablePort port)
        {
            if (port == null) return null;
            if (port.owner is GPU gpu) return gpu;
            var board = Board(port);
            var cards = board != null ? board.GetHardwares(HardwareType.GPU) : null;
            if (cards != null) foreach (var item in cards)
                if (item is GPU card && !card.Damaged) return card;
            return null;
        }
    }
}
