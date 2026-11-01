using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using PC.Component.Cabling;

namespace PC.Component.Networking
{
    // Place on the SAME GameObject as the user's router Item to inherit its
    // saved ID. Physical LAN ports/cables can later call ConnectUpstream.
    [RequireComponent(typeof(Item))]
    public sealed class RouterDevice : MonoBehaviour, ISave
    {
        [SerializeField] private string ssid = "Typo-Link";
        [SerializeField] private string password = "typolink123";
        [SerializeField, Min(1f)] private float wifiRange = 55f;
        [SerializeField, Min(0f)] private float powerWatts = 12f;
        [SerializeField, Min(1f)] private float maxLanLength = 500f;
        [SerializeField, Range(1, 16)] private int lanPorts = 4;
        private int upstreamId;
        private bool upstreamWireless;
        private int upstreamLanPortIndex;
        private string upstreamPassword = "";
        private Item item;
        private CablePort powerPort;
        private readonly CablePort[] lanSockets = new CablePort[4];
        public RouterIndicators Indicators { get; private set; }

        private void Awake()
        {
            item = GetComponent<Item>();
            Indicators = GetComponent<RouterIndicators>();
            if (Indicators == null) Indicators = gameObject.AddComponent<RouterIndicators>();
            if (!CableMode.Enabled) return; // no extra physics in no-wire rooms
            var socket = transform.Find("Router/ports/Power");
            if (socket != null) powerPort = CablePort.AddAuthored(item, PortKind.RouterPower,
                "Router AC input", socket.Find("Trigger"), socket.Find("Pivot"));
            // Front LEDs run +Z to -Z; rear sockets run in the opposite
            // direction, independently of Unity's duplicate-name suffixes.
            string[] names = { "LAN (3)", "LAN (2)", "LAN (1)", "LAN" };
            for (int i = 0; i < 4; i++)
            {
                socket = transform.Find("Router/ports/" + names[i]);
                if (socket != null) lanSockets[i] = CablePort.AddAuthored(item, PortKind.RouterLan,
                    "Router LAN " + (i + 1), socket.Find("Trigger"), socket.Find("Pivot"));
            }
        }

        public CablePort LanSocket(int index) => index >= 1 && index <= 4 ? lanSockets[index - 1] : null;
        public bool HasMainsCable => powerPort != null && CablePower.Energized(powerPort.plug);
        public int PortNumber(CablePort port)
        {
            for (int i = 0; i < lanSockets.Length; i++)
                if (lanSockets[i] == port) return i + 1;
            return 0;
        }

        public bool PhysicalLanTo(RouterDevice other, out int localPort, out int otherPort)
        {
            localPort = otherPort = 0;
            if (other == null) return false;
            for (int i = 0; i < lanSockets.Length; i++)
            {
                var socket = lanSockets[i];
                if (socket == null || socket.plug == null || socket.plug.Cable == null ||
                    socket.plug.Cable.Kind != CableKind.LAN) continue;
                var end = socket.plug.Cable.End(1 - socket.plug.End);
                var remote = end != null ? end.Connected : null;
                if (remote == null || remote.owner != other.item || remote.kind != PortKind.RouterLan) continue;
                localPort = i + 1;
                otherPort = other.PortNumber(remote);
                return otherPort != 0;
            }
            return false;
        }

        public int LocalUpstreamLanPort(RouterDevice parent)
        {
            if (CableMode.Enabled) return PhysicalLanTo(parent, out var local, out _) ? local : 0;
            return upstreamId == parent.Id ? 1 : 0;
        }

        public int FirstFreeVirtualPort(InternetService service, PC.Component.Motherboard requesting = null)
        {
            for (int i = 1; i <= Mathf.Min(4, lanPorts); i++)
            {
                bool used = false;
                foreach (var board in FindObjectsOfType<PC.Component.Motherboard>())
                    if (board != null && board != requesting && board.LanRouterId == Id &&
                        board.LanPortIndex == i) { used = true; break; }
                foreach (var child in FindObjectsOfType<RouterDevice>())
                    if (child != null && child != this && child.UpstreamId == Id &&
                        !child.UpstreamWireless && child.UpstreamLanPortIndex == i)
                    { used = true; break; }
                if (!used) return i;
            }
            return 0;
        }

        public int Id { get { if (item == null) item = GetComponent<Item>(); return item != null ? item.Id : 0; } }
        public int UpstreamId => upstreamId;
        public bool UpstreamWireless => upstreamWireless;
        public int UpstreamLanPortIndex => upstreamLanPortIndex;
        public string Ssid => ssid;
        public string Password => password;
        public float WifiRange => wifiRange;
        public float PowerWatts => powerWatts;
        public float MaxLanLength => maxLanLength;
        public int LanPorts => lanPorts;
        // In the physical-cable mode an energized Power Cable is REQUIRED.
        // The cable-free branch retains virtual mains power by user choice.
        public bool IsPowered => isActiveAndEnabled && ElectricityGrid.PowerAvailable &&
            (!CableMode.Enabled || HasMainsCable);

        public bool SetSsid(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 32) return false;
            ssid = value.Trim(); return true;
        }
        public bool SetPassword(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 8 || value.Length > 63) return false;
            password = value; return true;
        }
        public bool CredentialsMatch(string name, string key) =>
            ssid == name && password == key;

        public float WifiSignal(Transform device, float deviceRange)
        {
            if (!IsPowered || device == null) return 0f;
            float range = Mathf.Min(wifiRange, deviceRange);
            if (range <= 0f) return 0f;
            float d = Vector3.Distance(transform.position, device.position);
            if (d >= range) return 0f;
            float normalized = d / range;
            return Mathf.Clamp01(1f - .90f * normalized * normalized);
        }

        public bool WithinLanRange(Transform device) =>
            IsPowered && device != null && Vector3.Distance(transform.position, device.position) <= maxLanLength;

        public int LanClients(InternetService service)
        {
            int used = 0;
            foreach (var board in FindObjectsOfType<PC.Component.Motherboard>())
                if (board != null && board.LanRouterId == Id) used++;
            foreach (var router in FindObjectsOfType<RouterDevice>())
                if (router != null && router != this && router.UpstreamId == Id && !router.UpstreamWireless) used++;
            return used;
        }

        public bool ConnectUpstream(int parentId, bool wifi, string parentPassword = "")
        {
            var service = InternetService.Instance;
            if (service != null) service.Gateway();
            var parent = service != null ? service.Router(parentId) : null;
            if (parent == null || parent == this || Id == service.GatewayId || !IsPowered || !parent.IsPowered)
                return false;
            // Reject cycles even when attempting to link a router that used to
            // be an extender; old corrupt save cycles are rejected at runtime.
            var seen = new HashSet<int> { Id };
            var step = parent;
            while (step != null)
            {
                if (!seen.Add(step.Id)) return false;
                step = service.Router(step.UpstreamId);
            }
            if (wifi)
            {
                if (parent.Password != parentPassword ||
                    parent.WifiSignal(transform, wifiRange) <= 0f) return false;
            }
            else if (CableMode.Enabled)
            {
                if (!PhysicalLanTo(parent, out var ownPort, out var upstreamPort)) return false;
                upstreamLanPortIndex = upstreamPort;
            }
            else
            {
                if (!parent.WithinLanRange(transform)) return false;
                upstreamLanPortIndex = upstreamId == parentId && !upstreamWireless
                    ? upstreamLanPortIndex : parent.FirstFreeVirtualPort(service);
                if (upstreamLanPortIndex == 0) return false;
            }
            upstreamId = parentId;
            upstreamWireless = wifi;
            if (wifi) upstreamLanPortIndex = 0;
            upstreamPassword = wifi ? parentPassword : "";
            return true;
        }

        public void DisconnectUpstream()
        {
            upstreamId = 0; upstreamPassword = ""; upstreamWireless = false;
            upstreamLanPortIndex = 0;
        }

        public float UpstreamQuality(InternetService service, HashSet<int> seen)
        {
            if (!IsPowered || Id == 0 || !seen.Add(Id)) return 0f;
            var gateway = service != null ? service.Gateway() : null;
            if (gateway == this) return 1f;
            var parent = service != null ? service.Router(upstreamId) : null;
            if (parent == null || !parent.IsPowered) return 0f;
            float hop;
            if (upstreamWireless)
            {
                if (parent.Password != upstreamPassword) return 0f;
                hop = parent.WifiSignal(transform, wifiRange) * .85f;
            }
            else hop = (CableMode.Enabled ? PhysicalLanTo(parent, out _, out _) :
                parent.WithinLanRange(transform)) ? .98f : 0f;
            if (hop <= 0f) return 0f;
            return hop * parent.UpstreamQuality(service, seen);
        }

        public void ToData(JObject data)
        {
            data["routerSsid"] = ssid;
            data["routerPassword"] = password;
            data["routerUpstream"] = upstreamId;
            data["routerUpstreamWifi"] = upstreamWireless;
            data["routerUpstreamLanPort"] = upstreamLanPortIndex;
            data["routerUpstreamPassword"] = upstreamPassword;
        }
        public void FromData(JObject data)
        {
            if (data == null) return;
            ssid = data.Value<string>("routerSsid") ?? ssid;
            password = data.Value<string>("routerPassword") ?? password;
            upstreamId = data.Value<int?>("routerUpstream") ?? 0;
            upstreamWireless = data.Value<bool?>("routerUpstreamWifi") ?? false;
            upstreamLanPortIndex = data.Value<int?>("routerUpstreamLanPort") ??
                (upstreamId != 0 && !upstreamWireless ? 1 : 0);
            upstreamPassword = data.Value<string>("routerUpstreamPassword") ?? "";
        }
    }
}
