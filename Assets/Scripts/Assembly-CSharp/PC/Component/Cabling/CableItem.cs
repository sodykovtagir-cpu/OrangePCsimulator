using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PC.Component.Cabling
{
    // A short, self-collision-free rigidbody chain. Only the plugs are selectable;
    // one continuous LineRenderer makes the jacket continuous even while joints
    // are settling. The physical capsules still collide with the environment.
    public sealed class CableItem : Item
    {
        [SerializeField] private CableKind kind;
        [SerializeField, Min(.5f)] private float length = 2f;
        [SerializeField] private int segments = 10;
        [SerializeField, Range(.08f, .25f)] private float cableDiameter = .14f;
        [SerializeField] private CablePlug plugA;
        [SerializeField] private CablePlug plugB;
        [SerializeField] private LineRenderer jacket;
        private CablePlug[] plugs;
        private Rigidbody[] bodies;
        private readonly int[] restoredOwners = new int[2];
        private readonly string[] restoredPorts = new string[2];
        private readonly Vector3[] restoredPositions = new Vector3[2];
        private bool restorePositions;

        public CableKind Kind => kind;
        public float Length => length;
        public CablePlug End(int i) => plugs != null && i >= 0 && i < 2 ? plugs[i] : null;
        // The first physical capsule meets the panel at an inset socket.
        // Exempt only this short adjacent section from that panel's colliders.
        public Collider NearEndCollider(int end) => bodies != null && bodies.Length > 0
            ? bodies[end == 0 ? 0 : bodies.Length - 1].GetComponent<Collider>() : null;

        private void Awake()
        {
            int count = Mathf.Clamp(segments, 7, 26);
            float step = length / count;
            // Spawn a loose coil instead of a 12 m straight rod through the
            // furniture. Chord length is approximately the segment length.
            int turns = count >= 15 ? 2 : 1;
            float radius = step / (2f * Mathf.Sin(Mathf.PI * turns / count));
            var points = new Vector3[count + 1];
            for (int i = 0; i <= count; i++)
            {
                float angle = 2f * Mathf.PI * turns * i / count;
                points[i] = transform.position + new Vector3(
                    radius * Mathf.Cos(angle) + .5f * (i / (float)count - .5f),
                    .45f, radius * Mathf.Sin(angle));
            }
            plugA = SetupPlug(plugA, "Plug A", 0, points[0]);
            plugB = SetupPlug(plugB, "Plug B", 1, points[count]);
            plugs = new[] { plugA, plugB };
            var physicalColliders = new Collider[count + 2];
            physicalColliders[0] = plugA.GetComponent<Collider>();
            physicalColliders[count + 1] = plugB.GetComponent<Collider>();
            bodies = new Rigidbody[count];
            Rigidbody previous = plugA.GetComponent<Rigidbody>();
            for (int i = 0; i < count; i++)
            {
                var body = new GameObject("Cable segment " + i);
                body.layer = 2; // ignored by player raycast, not by physics
                body.transform.SetParent(transform, true);
                body.transform.position = (points[i] + points[i + 1]) * .5f;
                body.transform.rotation = Quaternion.LookRotation(points[i + 1] - points[i]);
                var col = body.AddComponent<CapsuleCollider>();
                col.direction = 2;
                col.radius = cableDiameter * .5f;
                col.height = Vector3.Distance(points[i], points[i + 1]) + .04f;
                physicalColliders[i + 1] = col;
                var rb = body.AddComponent<Rigidbody>();
                ConfigureBody(rb, .025f);
                bodies[i] = rb;
                var joint = body.AddComponent<CharacterJoint>();
                joint.connectedBody = previous;
                joint.autoConfigureConnectedAnchor = false;
                joint.enableCollision = false;
                joint.enablePreprocessing = false;
                joint.anchor = body.transform.InverseTransformPoint(points[i]);
                joint.connectedAnchor = previous.transform.InverseTransformPoint(points[i]);
                Limit(joint);
                previous = rb;
            }
            var last = plugB.gameObject.AddComponent<CharacterJoint>();
            last.connectedBody = previous;
            last.autoConfigureConnectedAnchor = false;
            last.enableCollision = false;
            last.enablePreprocessing = false;
            last.anchor = Vector3.zero;
            last.connectedAnchor = previous.transform.InverseTransformPoint(points[count]);
            Limit(last);
            // All parts of one cable must ignore one another. Self-collision in
            // the original short chain was the source of the violent scattering.
            for (int i = 0; i < physicalColliders.Length; i++)
                for (int j = i + 1; j < physicalColliders.Length; j++)
                    if (physicalColliders[i] != null && physicalColliders[j] != null)
                        Physics.IgnoreCollision(physicalColliders[i], physicalColliders[j]);
            SetupJacket(count);
        }

        private static void ConfigureBody(Rigidbody rb, float mass)
        {
            rb.mass = mass;
            rb.drag = 1.25f;
            rb.angularDrag = 1.5f;
            rb.solverIterations = 12;
            rb.solverVelocityIterations = 8;
            rb.sleepThreshold = .03f;
            rb.maxDepenetrationVelocity = 1.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private static void Limit(CharacterJoint joint)
        {
            joint.lowTwistLimit = new SoftJointLimit { limit = -55f };
            joint.highTwistLimit = new SoftJointLimit { limit = 55f };
            joint.swing1Limit = new SoftJointLimit { limit = 85f };
            joint.swing2Limit = new SoftJointLimit { limit = 85f };
        }

        private CablePlug SetupPlug(CablePlug configured, string label, int end, Vector3 position)
        {
            var tr = configured != null ? configured.transform : transform.Find(label);
            GameObject go;
            if (tr == null)
            {
                go = new GameObject(label);
                go.transform.SetParent(transform, false);
            }
            else go = tr.gameObject;
            go.transform.position = position;
            var col = go.GetComponent<BoxCollider>();
            if (col == null) col = go.AddComponent<BoxCollider>();
            // The plug collider is on the root of the plug; custom model meshes
            // should be children, so they can be replaced without touching it.
            if (tr == null) col.size = new Vector3(.16f, .12f, .20f);
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            ConfigureBody(rb, .11f);
            var plug = go.GetComponent<CablePlug>();
            if (plug == null) plug = go.AddComponent<CablePlug>();
            plug.Init(this, end);
            if (go.GetComponentInChildren<Renderer>() == null)
            {
                var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.name = "Replace with plug model";
                model.transform.SetParent(go.transform, false);
                model.transform.localScale = new Vector3(.16f, .12f, .20f);
                Destroy(model.GetComponent<Collider>());
                var renderer = model.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial =
                    kind == CableKind.Power && end == 0 ? OutletPlugMaterial() : CableMaterial(kind);
            }
            return plug;
        }

        private void SetupJacket(int count)
        {
            if (jacket == null) jacket = GetComponent<LineRenderer>();
            if (jacket == null) jacket = gameObject.AddComponent<LineRenderer>();
            jacket.useWorldSpace = true;
            jacket.positionCount = count + 2;
            jacket.startWidth = cableDiameter;
            jacket.endWidth = cableDiameter;
            jacket.numCapVertices = 6;
            jacket.numCornerVertices = 4;
            if (jacket.sharedMaterial == null) jacket.sharedMaterial = CableMaterial(kind);
            UpdateJacket();
        }

        private void LateUpdate()
        {
            // Only a handful of positions: no per-frame primitives or meshes.
            if (jacket != null && bodies != null) UpdateJacket();
        }

        private void UpdateJacket()
        {
            jacket.SetPosition(0, plugA.transform.position);
            for (int i = 0; i < bodies.Length; i++)
                jacket.SetPosition(i + 1, bodies[i].position);
            jacket.SetPosition(bodies.Length + 1, plugB.transform.position);
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
            // Retain the settled rope on reload rather than spawning a straight
            // chain through furniture between two already-connected plugs.
            var poses = new JArray();
            if (bodies != null) foreach (var rb in bodies)
            {
                var p = rb.position;
                var q = rb.rotation;
                poses.Add(new JArray(p.x, p.y, p.z, q.x, q.y, q.z, q.w));
            }
            data["cableBodies"] = poses;
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
            if (!restorePositions || plugs == null) return;
            for (int i = 0; i < 2; i++) plugs[i].GetComponent<Rigidbody>().position = restoredPositions[i];
            var poses = data["cableBodies"] as JArray;
            if (bodies == null) return;
            if (poses != null && poses.Count == bodies.Length)
            {
                for (int i = 0; i < bodies.Length; i++)
                {
                    var a = poses[i] as JArray;
                    if (a == null || a.Count < 7) continue;
                    bodies[i].position = new Vector3(a[0].Value<float>(), a[1].Value<float>(), a[2].Value<float>());
                    bodies[i].rotation = new Quaternion(a[3].Value<float>(), a[4].Value<float>(),
                        a[5].Value<float>(), a[6].Value<float>());
                }
            }
            else
            {
                var axis = restoredPositions[1] - restoredPositions[0];
                var direction = axis.sqrMagnitude > .0001f ? axis.normalized : Vector3.right;
                var rotation = Quaternion.FromToRotation(Vector3.forward, direction);
                for (int i = 0; i < bodies.Length; i++)
                {
                    bodies[i].position = Vector3.Lerp(restoredPositions[0], restoredPositions[1],
                        (i + .5f) / bodies.Length) + Vector3.up * .16f;
                    bodies[i].rotation = rotation;
                }
            }
            UpdateJacket();
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
