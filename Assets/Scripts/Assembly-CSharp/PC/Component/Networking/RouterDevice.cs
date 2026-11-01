using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

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
        private string upstreamPassword = "";
        private Item item;

        public int Id { get { if (item == null) item = GetComponent<Item>(); return item != null ? item.Id : 0; } }
        public int UpstreamId => upstreamId;
        public bool UpstreamWireless => upstreamWireless;
        public string Ssid => ssid;
        public string Password => password;
        public float WifiRange => wifiRange;
        public float PowerWatts => powerWatts;
        public float MaxLanLength => maxLanLength;
        public int LanPorts => lanPorts;
        // Adapter power is modelled as mains for now: no power connector prefab.
        public bool IsPowered => isActiveAndEnabled && ElectricityGrid.PowerAvailable;

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
            else if (!parent.WithinLanRange(transform) ||
                ((upstreamId != parentId || upstreamWireless) && parent.LanClients(service) >= parent.LanPorts))
                return false;
            upstreamId = parentId;
            upstreamWireless = wifi;
            upstreamPassword = wifi ? parentPassword : "";
            return true;
        }

        public void DisconnectUpstream()
        {
            upstreamId = 0; upstreamPassword = ""; upstreamWireless = false;
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
            else hop = parent.WithinLanRange(transform) ? .98f : 0f;
            if (hop <= 0f) return 0f;
            return hop * parent.UpstreamQuality(service, seen);
        }

        public void ToData(JObject data)
        {
            data["routerSsid"] = ssid;
            data["routerPassword"] = password;
            data["routerUpstream"] = upstreamId;
            data["routerUpstreamWifi"] = upstreamWireless;
            data["routerUpstreamPassword"] = upstreamPassword;
        }
        public void FromData(JObject data)
        {
            if (data == null) return;
            ssid = data.Value<string>("routerSsid") ?? ssid;
            password = data.Value<string>("routerPassword") ?? password;
            upstreamId = data.Value<int?>("routerUpstream") ?? 0;
            upstreamWireless = data.Value<bool?>("routerUpstreamWifi") ?? false;
            upstreamPassword = data.Value<string>("routerUpstreamPassword") ?? "";
        }
    }
}
