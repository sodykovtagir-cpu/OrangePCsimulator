using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PC.Component.Cabling
{
    // The rope has a small fixed body budget. Physics (capsule colliders and
    // character joints) provides contact with walls/furniture; visual geometry
    // shares Unity's primitive mesh and no per-frame mesh regeneration occurs.
    public sealed class CableItem : Item
    {
        [SerializeField] private CableKind kind;
        [SerializeField, Min(.5f)] private float length = 2f;
        [SerializeField] private int segments = 10;
        private CablePlug[] plugs;
        private Rigidbody[] bodies;
        private int[] restoredOwners = new int[2];
        private string[] restoredPorts = new string[2];
        private Vector3[] restoredPositions = new Vector3[2];
        private bool restorePositions;

        public CableKind Kind => kind;
        public float Length => length;
        public CablePlug End(int i) => plugs != null && i >= 0 && i < 2 ? plugs[i] : null;

        private void Awake()
        {
            int count = Mathf.Clamp(segments, 5, 16);
            float step = length / count;
            Vector3 origin = transform.position + Vector3.up * .16f;
            var a = Plug("Plug A", 0, origin - Vector3.right * length * .5f);
            var b = Plug("Plug B", 1, origin + Vector3.right * length * .5f);
            plugs = new[] { a, b };

            Rigidbody previous = a.GetComponent<Rigidbody>();
            bodies = new Rigidbody[count];
            for (int i = 0; i < count; i++)
            {
                var body = new GameObject("Cable segment " + i);
                body.transform.SetParent(transform, true);
                body.layer = 2; // physical collision, but raycasts select only plugs
                body.transform.position = Vector3.Lerp(a.transform.position, b.transform.position, (i + .5f) / count);
                body.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                var col = body.AddComponent<CapsuleCollider>();
                col.direction = 2;
                col.radius = .033f;
                col.height = step + .02f;
                var rb = body.AddComponent<Rigidbody>();
                rb.mass = .025f;
                rb.drag = .8f;
                rb.angularDrag = 1f;
                rb.solverIterations = 5;
                rb.sleepThreshold = .015f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                bodies[i] = rb;
                var joint = body.AddComponent<CharacterJoint>();
                joint.connectedBody = previous;
                joint.autoConfigureConnectedAnchor = false;
                joint.enableCollision = false;
                joint.anchor = new Vector3(0f, 0f, -step * .5f);
                joint.connectedAnchor = i == 0 ? Vector3.zero : new Vector3(0f, 0f, step * .5f);
                joint.lowTwistLimit = new SoftJointLimit { limit = -25f };
                joint.highTwistLimit = new SoftJointLimit { limit = 25f };
                joint.swing1Limit = new SoftJointLimit { limit = 55f };
                joint.swing2Limit = new SoftJointLimit { limit = 55f };
                var mesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mesh.name = "Cable jacket";
                mesh.layer = 2;
                Destroy(mesh.GetComponent<Collider>());
                mesh.transform.SetParent(body.transform, false);
                mesh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                mesh.transform.localScale = new Vector3(.035f, step * .5f, .035f);
                var rend = mesh.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = CableMaterial(kind);
                previous = rb;
            }
            var endJoint = b.gameObject.AddComponent<CharacterJoint>();
            endJoint.connectedBody = previous;
            endJoint.autoConfigureConnectedAnchor = false;
            endJoint.enableCollision = false;
            endJoint.connectedAnchor = new Vector3(0f, 0f, step * .5f);
            endJoint.swing1Limit = new SoftJointLimit { limit = 55f };
            endJoint.swing2Limit = new SoftJointLimit { limit = 55f };
        }

        private CablePlug Plug(string name, int end, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform, true);
            go.transform.position = position;
            go.transform.localScale = new Vector3(.09f, .055f, .13f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = .08f;
            rb.drag = .7f;
            rb.angularDrag = 1.2f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var plug = go.AddComponent<CablePlug>();
            plug.Init(this, end);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial =
                kind == CableKind.Power && end == 0 ? OutletPlugMaterial() : CableMaterial(kind);
            return plug;
        }

        private static Material outletPlugMaterial;
        private static Material OutletPlugMaterial()
        {
            if (outletPlugMaterial == null)
            {
                outletPlugMaterial = new Material(Shader.Find("Standard"));
                outletPlugMaterial.color = new Color(.86f, .82f, .7f);
            }
            return outletPlugMaterial;
        }

        private static readonly Material[] materials = new Material[3];
        private static Material CableMaterial(CableKind kind)
        {
            int index = (int)kind;
            if (materials[index] != null) return materials[index];
            var shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
            var material = new Material(shader);
            material.color = kind == CableKind.VGA ? new Color(.18f, .22f, .52f) :
                kind == CableKind.HDMI ? new Color(.1f, .1f, .1f) : new Color(.32f, .32f, .32f);
            materials[index] = material;
            return material;
        }

        public override string GetInfo() => base.GetInfo() + "\n" + kind + " - " + length.ToString("0.#") + " m";

        public override void ToData(JObject data)
        {
            base.ToData(data);
            for (int i = 0; i < 2; i++)
            {
                var plug = End(i);
                if (plug == null) continue;
                data["cablePos" + i] = new JObject { ["x"] = plug.transform.position.x,
                    ["y"] = plug.transform.position.y, ["z"] = plug.transform.position.z };
                var port = plug.Connected;
                data["cableOwner" + i] = port != null && port.owner != null ? port.owner.Id : 0;
                data["cablePort" + i] = port != null ? port.name : "";
            }
        }

        public override void FromData(JObject data)
        {
            base.FromData(data);
            for (int i = 0; i < 2; i++)
            {
                restoredOwners[i] = data.Value<int?>("cableOwner" + i) ?? 0;
                restoredPorts[i] = data.Value<string>("cablePort" + i);
                var pos = data["cablePos" + i];
                if (pos == null) continue;
                restoredPositions[i] = new Vector3(pos.Value<float>("x"),
                    pos.Value<float>("y"), pos.Value<float>("z"));
                restorePositions = true;
            }
            if (restorePositions && plugs != null)
            {
                for (int i = 0; i < 2; i++)
                    plugs[i].transform.position = restoredPositions[i];
                if (bodies != null)
                {
                    var axis = restoredPositions[1] - restoredPositions[0];
                    var orientation = axis.sqrMagnitude > .0001f
                        ? Quaternion.FromToRotation(Vector3.forward, axis.normalized)
                        : Quaternion.Euler(0f, 90f, 0f);
                    for (int i = 0; i < bodies.Length; i++)
                    {
                        bodies[i].position = Vector3.Lerp(restoredPositions[0],
                            restoredPositions[1], (i + .5f) / bodies.Length);
                        bodies[i].rotation = orientation;
                    }
                }
            }
        }

        public void RestorePorts()
        {
            var main = Main.Instance;
            if (main == null || plugs == null) return;
            for (int i = 0; i < 2; i++)
            {
                if (restoredOwners[i] == 0 || string.IsNullOrEmpty(restoredPorts[i])) continue;
                var item = main.GetItemById(restoredOwners[i]);
                if (item == null) continue;
                var ports = item.GetComponentsInChildren<CablePort>(true);
                foreach (var port in ports)
                    if (port.name == restoredPorts[i] && plugs[i].TryInsert(port, true)) break;
            }
        }
    }
}
