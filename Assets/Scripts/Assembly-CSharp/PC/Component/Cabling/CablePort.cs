using UnityEngine;

namespace PC.Component.Cabling
{
    public enum PortKind { Outlet, PcPower, MonitorPower, VgaInput, HdmiInput, BoardVideo, GpuVideo }
    public enum CableKind { Power, VGA, HDMI }

    // Put a CablePort with a SphereCollider (Is Trigger) on the device prefab.
    // Move this trigger to the inlet and its Snap Pivot child to the desired
    // final plug position/rotation. CablePorts.Install uses authored ports in
    // preference to its legacy fallback, without moving their transforms.
    [RequireComponent(typeof(SphereCollider))]
    public sealed class CablePort : MonoBehaviour
    {
        public PortKind kind;
        public Item owner;
        [SerializeField] private Transform snapPivot;
        public CablePlug plug;
        private static readonly Material[] portMaterials = new Material[7];
        public Transform Snap => snapPivot != null ? snapPivot :
            transform.Find("Snap Pivot") != null ? transform.Find("Snap Pivot") : transform;
        public Vector3 TriggerCenter => GetComponent<SphereCollider>() is SphereCollider sphere
            ? transform.TransformPoint(sphere.center) : transform.position;
        public float CaptureRadius
        {
            get
            {
                var sphere = GetComponent<SphereCollider>();
                return sphere != null ? sphere.radius * Mathf.Max(transform.lossyScale.x,
                    transform.lossyScale.y, transform.lossyScale.z) + .12f : .45f;
            }
        }
        public void SetPivot(Transform pivot) { snapPivot = pivot; }
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
                // Plug A (end 0) is the wall plug; Plug B (end 1) is the
                // device connector. An unplugged opposite end must not make
                // these different physical connectors interchangeable.
                if (kind == PortKind.Outlet)
                    return candidate.End == 0 && Powered && (opposite == null ||
                        opposite.kind == PortKind.PcPower || opposite.kind == PortKind.MonitorPower);
                if (kind == PortKind.PcPower || kind == PortKind.MonitorPower)
                    return candidate.End == 1 && (opposite == null || opposite.kind == PortKind.Outlet);
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

        public static CablePort Add(Item owner, PortKind kind, string label, Vector3 localPosition, float radius = .42f)
        {
            if (owner == null) return null;
            // Nested prefab sockets may sit several transforms deep. Match by
            // stable name and nearest Item, never steal a port on installed GPU.
            CablePort ready = null;
            foreach (var candidate in owner.GetComponentsInChildren<CablePort>(true))
                if (candidate.name == label && candidate.GetComponentInParent<Item>() == owner)
                {
                    ready = candidate;
                    break;
                }
            var existing = ready != null ? ready.transform : owner.transform.Find(label);
            var go = existing != null ? existing.gameObject : new GameObject(label);
            bool generated = existing == null;
            if (generated)
            {
                go.transform.SetParent(owner.transform, false);
                go.transform.localPosition = localPosition;
            }
            go.layer = 2; // ignore selection raycast, retain trigger contacts
            var collider = go.GetComponent<SphereCollider>();
            if (collider == null) collider = go.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            if (generated) collider.radius = radius;
            if (ready == null) ready = go.GetComponent<CablePort>();
            if (ready == null) ready = go.AddComponent<CablePort>();
            ready.kind = kind;
            ready.owner = owner;
            if (ready.snapPivot == null)
            {
                var pivot = go.transform.Find("Snap Pivot");
                if (pivot == null && generated)
                {
                    var point = new GameObject("Snap Pivot");
                    point.transform.SetParent(go.transform, false);
                    pivot = point.transform;
                }
                ready.snapPivot = pivot != null ? pivot : go.transform;
            }
            // Only add temporary fallback graphics to autogenerated ports.
            // An authored port's mesh and socket location remain untouched.
            if (generated)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "Replace with socket model";
                marker.transform.SetParent(go.transform, false);
                marker.transform.localScale = new Vector3(.10f, .08f, .025f);
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
            }
            return ready;
        }

        private void OnTriggerEnter(Collider other)
        {
            var candidate = other != null ? other.GetComponentInParent<CablePlug>() : null;
            if (candidate != null)
            {
                candidate.PrepareApproach(this);
                candidate.TryInsert(this);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            var candidate = other != null ? other.GetComponentInParent<CablePlug>() : null;
            if (candidate != null && !candidate.Connected)
            {
                candidate.PrepareApproach(this);
                candidate.TryInsert(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var candidate = other != null ? other.GetComponentInParent<CablePlug>() : null;
            if (candidate != null && Vector3.Distance(candidate.transform.position, TriggerCenter) >
                CaptureRadius + .25f) candidate.LeaveApproach(this);
        }

        private void OnDrawGizmosSelected()
        {
            var sphere = GetComponent<SphereCollider>();
            if (sphere == null) return;
            Gizmos.color = new Color(.25f, .85f, .4f, .5f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(Snap.position, .025f);
            Gizmos.DrawRay(Snap.position, Snap.forward * .18f);
        }

        private void OnDestroy()
        {
            if (plug != null) plug.Unplug();
        }
    }
}
