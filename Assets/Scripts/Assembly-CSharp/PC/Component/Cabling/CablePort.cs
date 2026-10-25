using UnityEngine;

namespace PC.Component.Cabling
{
    public enum PortKind { Outlet, PcPower, MonitorPower, VgaInput, HdmiInput, BoardVideo, GpuVideo }
    public enum CableKind { Power, VGA, HDMI }

    // Ports are generated only once per hardware instance; the GameObject is a
    // trigger (not a solid obstruction) and is ignored by the player's raycast.
    public sealed class CablePort : MonoBehaviour
    {
        public PortKind kind;
        public Item owner;
        public CablePlug plug;
        private static readonly Material[] portMaterials = new Material[7];
        public bool Powered => kind == PortKind.Outlet &&
            owner != null && owner.GetComponent<OutletStrip>() is OutletStrip strip && strip.IsMounted;

        public bool Accepts(CablePlug candidate)
        {
            if (candidate == null || plug != null || candidate.Connected != null) return false;
            var cable = candidate.Cable;
            if (cable == null) return false;
            var other = cable.End(1 - candidate.End);
            var opposite = other != null ? other.Connected : null;
            if (cable.Kind == CableKind.Power)
            {
                if (kind == PortKind.Outlet)
                    return Powered && (opposite == null ||
                        opposite.kind == PortKind.PcPower || opposite.kind == PortKind.MonitorPower);
                if (kind == PortKind.PcPower || kind == PortKind.MonitorPower)
                    return opposite == null || opposite.kind == PortKind.Outlet;
                return false;
            }
            bool input = kind == PortKind.VgaInput && cable.Kind == CableKind.VGA ||
                kind == PortKind.HdmiInput && cable.Kind == CableKind.HDMI;
            bool output = kind == PortKind.BoardVideo || kind == PortKind.GpuVideo;
            if (input) return opposite == null ||
                opposite.kind == PortKind.BoardVideo || opposite.kind == PortKind.GpuVideo;
            if (output) return opposite == null ||
                (opposite.kind == PortKind.VgaInput && cable.Kind == CableKind.VGA) ||
                (opposite.kind == PortKind.HdmiInput && cable.Kind == CableKind.HDMI);
            return false;
        }

        public static CablePort Add(Item owner, PortKind kind, string label, Vector3 localPosition, float radius = .15f)
        {
            if (owner == null) return null;
            var existing = owner.transform.Find(label);
            var go = existing != null ? existing.gameObject : new GameObject(label);
            if (existing == null)
            {
                go.transform.SetParent(owner.transform, false);
                go.transform.localPosition = localPosition;
            }
            var ready = go.GetComponent<CablePort>();
            if (ready != null) return ready;
            go.layer = 2; // Ignore Raycast, still receives plug trigger contacts.
            var collider = go.GetComponent<SphereCollider>();
            if (collider == null) collider = go.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = radius;
            var port = go.AddComponent<CablePort>();
            port.kind = kind;
            port.owner = owner;
            // Small, color-coded rear-panel connector; no solid collider here
            // (the actual device keeps its own collision, the plug is solid).
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Port face";
            marker.transform.SetParent(go.transform, false);
            marker.transform.localScale = new Vector3(.075f, .052f, .018f);
            Object.Destroy(marker.GetComponent<Collider>());
            marker.layer = 2;
            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                int index = (int)kind;
                var mat = portMaterials[index];
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Standard"));
                    mat.color = kind == PortKind.GpuVideo ? new Color(.23f, .37f, .8f) :
                    kind == PortKind.BoardVideo ? new Color(.18f, .67f, .35f) :
                    kind == PortKind.VgaInput ? new Color(.25f, .32f, .78f) :
                    kind == PortKind.HdmiInput ? new Color(.18f, .18f, .2f) :
                    kind == PortKind.Outlet ? new Color(.85f, .85f, .82f) :
                    new Color(.25f, .25f, .25f);
                    portMaterials[index] = mat;
                }
                renderer.sharedMaterial = mat;
            }
            return port;
        }

        private void OnTriggerEnter(Collider other)
        {
            var candidate = other != null ? other.GetComponent<CablePlug>() : null;
            if (candidate != null) candidate.TryInsert(this);
        }

        private void OnTriggerStay(Collider other)
        {
            // A plug may already be in range when its host is restored.
            var candidate = other != null ? other.GetComponent<CablePlug>() : null;
            if (candidate != null && !candidate.Connected) candidate.TryInsert(this);
        }

        private void OnDestroy()
        {
            if (plug != null) plug.Unplug();
        }
    }
}
