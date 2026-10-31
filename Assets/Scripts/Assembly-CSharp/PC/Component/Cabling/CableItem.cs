using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PC.Component.Cabling
{
    // A self-collision-free rigidbody chain. Plugs and jacket sections are
    // selectable; one continuous LineRenderer hides joint settling. The physical
    // capsules still collide with the environment.
    public sealed class CableItem : Item
    {
        [SerializeField] private CableKind kind;
        [SerializeField, Min(.5f)] private float length = 2f;
        [SerializeField] private int segments = 10;
        [SerializeField, Range(.08f, .25f)] private float cableDiameter = .14f;
        [SerializeField] private CablePlug plugA;
        [SerializeField] private CablePlug plugB;
        [SerializeField] private LineRenderer jacket;
        [SerializeField, Min(.1f)] private float maxPullMass = 5f;
        [SerializeField, Min(10f)] private float tensionStiffness = 1000f;
        private CablePlug[] plugs;
        private Rigidbody[] bodies;
        private Rigidbody heldSegment;
        private int heldSegmentIndex = -1;
        private readonly int[] restoredOwners = new int[2];
        private readonly string[] restoredPorts = new string[2];
        private readonly Vector3[] restoredPositions = new Vector3[2];
        private bool restorePositions;
        private bool restoringPhysics;
        private readonly bool[] restorePlugContacts = new bool[2];
        private bool[] restoreBodyContacts;
        private int overstretchSteps;
        private float strainCooldownUntil;

        // The root is an invisible spawn anchor: dragging the physical cable
        // moves its child rigidbodies, not this transform. SaveManager must use
        // the visible cable instead of the portal's unchanged root position.
        public Vector3 SavePosition => plugs != null && plugs[0] != null && plugs[1] != null
            ? (plugs[0].transform.position + plugs[1].transform.position) * .5f
            : transform.position;
        public CableKind Kind => kind;
        public bool RestoringPhysics => restoringPhysics;
        public float Length => length;
        public CablePlug End(int i) => plugs != null && i >= 0 && i < 2 ? plugs[i] : null;
        // The first physical capsule meets the panel at an inset socket.
        // Exempt only this short adjacent section from that panel's colliders.
        public Collider NearEndCollider(int end) => bodies != null && bodies.Length > 0
            ? bodies[end == 0 ? 0 : bodies.Length - 1].GetComponent<Collider>() : null;

        public void HoldSection(Rigidbody section)
        {
            heldSegment = null;
            heldSegmentIndex = -1;
            if (section == null || bodies == null) return;
            for (int i = 0; i < bodies.Length; i++)
                if (bodies[i] == section)
                {
                    heldSegment = section;
                    heldSegmentIndex = i;
                    break;
                }
        }

        private void Awake()
        {
            int count = Mathf.Clamp(segments, 7, 26);
            float step = length / count;
            // Spawn a loose coil instead of a 12 m straight rod through the
            // furniture. Chord length is approximately the segment length.
            int turns = count >= 24 && length > 12f ? 3 : count >= 11 ? 2 : 1;
            float radius = step / (2f * Mathf.Sin(Mathf.PI * turns / count));
            // A fixed half-metre lateral spread can make a 1 m cable longer
            // than its nominal length before it is even touched.
            float spread = Mathf.Min(.5f, length * .15f);
            var points = new Vector3[count + 1];
            for (int i = 0; i <= count; i++)
            {
                float angle = 2f * Mathf.PI * turns * i / count;
                points[i] = transform.position + new Vector3(
                    radius * Mathf.Cos(angle) + spread * (i / (float)count - .5f),
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
                body.layer = 0; // any physical section can be picked up by the player
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
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private static void Limit(CharacterJoint joint)
        {
            // The solver alone can separate a long chain when a plugged-in
            // device is heavy. Project only failed linear constraints; do not
            // straighten or lock the bends of the cable.
            joint.enableProjection = true;
            joint.projectionDistance = .02f;
            joint.projectionAngle = 180f;
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

        private void FixedUpdate()
        {
            if (plugs == null || bodies == null || restoringPhysics) return;
            // A falling PSU/monitor can stretch the chain with neither plug
            // held by the player. Release a connection before it can oscillate.
            if (ReleaseOverstretchedEnd()) return;
            if (Time.time < strainCooldownUntil) return;
            // Physics joints alone can stretch when a rope of many segments is
            // pulled quickly. Pull a light device when the span is nearly taut.
            for (int end = 0; end < 2; end++)
            {
                var attached = plugs[end];
                if (attached == null || attached.Connected == null || attached.IsHeld) continue;
                var other = plugs[1 - end];
                bool sectionHeld = heldSegment != null && heldSegmentIndex >= 0;
                if (!sectionHeld && (other == null || (!other.IsHeld && other.Connected == null))) continue;
                Vector3 target = sectionHeld ? heldSegment.position : other.GetComponent<Rigidbody>().position;
                float restLength = sectionHeld
                    ? length * (end == 0 ? heldSegmentIndex + .5f : bodies.Length - heldSegmentIndex - .5f)
                        / bodies.Length : length;
                float distance = Vector3.Distance(attached.Connected.Snap.position, target);
                float tension = Mathf.Max(0f, distance - restLength * .85f) *
                    (tensionStiffness >= 10f ? tensionStiffness : 1000f);
                if (tension <= 0f) continue;
                var rb = attached.Connected.GetComponentInParent<Rigidbody>();
                if (CanPull(rb, 0))
                {
                    Vector3 direction = (target - attached.Connected.Snap.position).normalized;
                    Vector3 targetVelocity = sectionHeld ? heldSegment.velocity :
                        other.GetComponent<Rigidbody>().velocity;
                    float closingSpeed = Vector3.Dot(
                        rb.GetPointVelocity(attached.Connected.Snap.position) - targetVelocity, direction);
                    // Stop pulling as the item catches up, rather than
                    // repeatedly overshooting and shaking at full force.
                    float drive = Mathf.Max(0f, tension - Mathf.Max(0f, closingSpeed) * rb.mass * 8f);
                    float force = Mathf.Min(drive,
                        Mathf.Min(attached.PullOutForce * .4f, rb.mass * 20f));
                    rb.AddForceAtPosition(direction * force, attached.Connected.Snap.position);
                }
                // Holding any section must never call Unplug. Pulling hard
                // enough breaks the plug's own FixedJoint (pullOutForce), not
                // a geometry threshold that can already be met on pickup.
            }
        }

        private bool ReleaseOverstretchedEnd()
        {
            if ((plugs[0].Connected == null && plugs[1].Connected == null) ||
                bodies.Length == 0 || Time.time < strainCooldownUntil)
                return false;
            // A click/drag is not a request to unplug. The actual FixedJoint
            // with pullOutForce handles deliberate pulling; this emergency
            // length guard is ONLY for passive loads between two devices.
            // A single free plug settling under gravity must not release the
            // other end either.
            var a = plugs[0];
            var b = plugs[1];
            if (a.Connected == null || b.Connected == null ||
                a.IsHeld || b.IsHeld || heldSegment != null)
            {
                overstretchSteps = 0;
                return false;
            }
            // Snapping a plug into its authored pivot or restoring a saved
            // pose takes a few physics steps. Don't misidentify that transient
            // as a long cable under load.
            if ((plugs[0].Connected != null && Time.time - plugs[0].ConnectedAt < .25f) ||
                (plugs[1].Connected != null && Time.time - plugs[1].ConnectedAt < .25f))
            {
                overstretchSteps = 0;
                return false;
            }

            // Use physics poses consistently; interpolated Transform poses are
            // meant for rendering and can falsely appear to stretch the path.
            Vector3 start = plugs[0].GetComponent<Rigidbody>().position;
            Vector3 end = plugs[1].GetComponent<Rigidbody>().position;
            float span = Vector3.Distance(start, end);
            float path = 0f;
            Vector3 previous = start;
            foreach (var body in bodies)
            {
                path += Vector3.Distance(previous, body.position);
                previous = body.position;
            }
            path += Vector3.Distance(previous, end);
            // Centre-line length catches a cable bent around furniture even
            // when its two plugs are close together. The small allowance
            // covers the chord error of the initial coil and solver tolerance.
            bool tooLong = path > length + Mathf.Max(.06f, length * .02f) ||
                           span > length + .02f;
            overstretchSteps = tooLong ? overstretchSteps + 1 : 0;
            bool hardLimit = overstretchSteps >= 2;
            Vector3 relativeVelocity = plugs[1].GetComponent<Rigidbody>().velocity -
                                       plugs[0].GetComponent<Rigidbody>().velocity;
            bool movingApart = span > length * .97f && span > .001f &&
                Vector3.Dot(relativeVelocity, (end - start) / span) > .35f;
            if (!hardLimit && !movingApart) return false;
            var release = StrainedEnd();
            if (release == null) return false;
            // If either connected device is light and movable, give the
            // damped pull a chance before treating the span as unmovable.
            if (!hardLimit &&
                ((plugs[0].Connected != null && CanPull(plugs[0].Connected.GetComponentInParent<Rigidbody>(), 0)) ||
                 (plugs[1].Connected != null && CanPull(plugs[1].Connected.GetComponentInParent<Rigidbody>(), 0))))
                return false;

#if UNITY_EDITOR
            if (CableStressMode.Recording)
                Debug.LogWarning($"Cable stress: OVERSTRETCH cable={spawnId} id={Id} end={release.End} " +
                    $"port={release.Connected?.name} owner={release.Connected?.owner?.Id} " +
                    $"span={span:0.000} path={path:0.000} length={length:0.000} " +
                    $"hardLimit={hardLimit} movingApart={movingApart}");
#endif
            release.Unplug(); // break the device connection before the chain can stretch
            overstretchSteps = 0;
            strainCooldownUntil = Time.time + .35f; // let the loose cable settle
            return true;
        }

        private CablePlug StrainedEnd()
        {
            var a = plugs[0].Connected != null ? plugs[0] : null;
            var b = plugs[1].Connected != null ? plugs[1] : null;
            if (a == null) return b;
            if (b == null) return a;
            // Passive heavy load: keep mains outlets mounted and prefer the
            // display input over an installed GPU for video cables.
            if (a.Connected.kind == PortKind.Outlet) return b;
            if (b.Connected.kind == PortKind.Outlet) return a;
            if (a.Connected.kind == PortKind.VgaInput || a.Connected.kind == PortKind.HdmiInput) return a;
            if (b.Connected.kind == PortKind.VgaInput || b.Connected.kind == PortKind.HdmiInput) return b;
            bool canA = CanPull(a.Connected.GetComponentInParent<Rigidbody>(), 0);
            bool canB = CanPull(b.Connected.GetComponentInParent<Rigidbody>(), 0);
            if (!canA && canB) return a;
            if (!canB && canA) return b;
            return b;
        }

        private bool CanPull(Rigidbody rb, int depth)
        {
            if (rb == null || rb.isKinematic || rb.mass >
                (maxPullMass > 0f ? maxPullMass : 5f) || depth > 3) return false;
            foreach (var joint in rb.GetComponents<FixedJoint>())
            {
                if (joint == null) continue;
                if (joint.connectedBody == null) return false; // bolted to the world
                if (joint.connectedBody != rb && !CanPull(joint.connectedBody, depth + 1))
                    return false;
            }
            return true;
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
                jacket.SetPosition(i + 1, bodies[i].transform.position);
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
                var rb = plug.GetComponent<Rigidbody>();
                var p = rb.position;
                var q = rb.rotation;
                data["cablePos" + i] = new JObject { ["x"] = p.x,
                    ["y"] = p.y, ["z"] = p.z };
                data["cableRot" + i] = new JArray(q.x, q.y, q.z, q.w);
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
            // The physics chain is NOT defined by body poses alone. Awake
            // constructs joints with anchors in an initial delivery coil; if
            // those anchors survive after restoring a different rope pose,
            // PhysX projects the whole saved chain towards the old coil in a
            // single step and breaks GPU/PSU slot joints. Save local anchors
            // with each section and the final joint on Plug B.
            var anchors = new JArray();
            if (bodies != null) foreach (var rb in bodies)
                anchors.Add(SaveJoint(rb.GetComponent<CharacterJoint>()));
            if (plugs != null && plugs[1] != null)
                anchors.Add(SaveJoint(plugs[1].GetComponent<CharacterJoint>()));
            data["cableJoints"] = anchors;
        }

        private static JArray SaveJoint(CharacterJoint joint)
        {
            var a = joint != null ? joint.anchor : Vector3.zero;
            var b = joint != null ? joint.connectedAnchor : Vector3.zero;
            return new JArray(a.x, a.y, a.z, b.x, b.y, b.z);
        }

        private static bool RestoreJoint(CharacterJoint joint, JToken token)
        {
            var values = token as JArray;
            if (joint == null || values == null || values.Count != 6) return false;
            joint.anchor = new Vector3(values[0].Value<float>(), values[1].Value<float>(),
                values[2].Value<float>());
            joint.connectedAnchor = new Vector3(values[3].Value<float>(),
                values[4].Value<float>(), values[5].Value<float>());
            return true;
        }

        private void ReanchorChainFromLoadedPose()
        {
            if (bodies == null || plugs == null) return;
            Rigidbody previous = plugs[0].GetComponent<Rigidbody>();
            for (int i = 0; i < bodies.Length; i++)
            {
                var current = bodies[i];
                var joint = current.GetComponent<CharacterJoint>();
                if (joint == null) { previous = current; continue; }
                // Adjacent saved segment centres straddle their old boundary.
                // This approximates older saves which had poses but no anchors;
                // after the next save the exact local anchors are preserved.
                Vector3 boundary = i == 0 ? previous.position :
                    (previous.position + current.position) * .5f;
                joint.anchor = current.transform.InverseTransformPoint(boundary);
                joint.connectedAnchor = previous.transform.InverseTransformPoint(boundary);
                previous = current;
            }
            var last = plugs[1].GetComponent<CharacterJoint>();
            if (last != null)
            {
                var end = plugs[1].GetComponent<Rigidbody>();
                Vector3 boundary = end.position;
                last.anchor = end.transform.InverseTransformPoint(boundary);
                last.connectedAnchor = previous.transform.InverseTransformPoint(boundary);
            }
        }

        public override void FromData(JObject data)
        {
            base.FromData(data);
            var havePosition = new bool[2];
            for (int i = 0; i < 2; i++)
            {
                restoredOwners[i] = data.Value<int?>("cableOwner" + i) ?? 0;
                restoredPorts[i] = data.Value<string>("cablePort" + i);
                var pos = data["cablePos" + i] as JObject;
                if (pos == null) continue; // saves predating physical cable poses
                restoredPositions[i] = new Vector3(pos.Value<float>("x"),
                    pos.Value<float>("y"), pos.Value<float>("z"));
                havePosition[i] = true;
            }
            restorePositions = havePosition[0] && havePosition[1];
            if (plugs == null || bodies == null) return;

            // Even pre-pose saves contain live cable rigidbodies. Freeze the
            // whole chain before the first physics tick, not just newer saves:
            // a loose spawn coil otherwise can hit/break a saved monitor while
            // ports and slotted hardware are still being restored.
            restoringPhysics = true;
            restoreBodyContacts = new bool[bodies.Length];
            for (int i = 0; i < 2; i++)
            {
                var rb = plugs[i].GetComponent<Rigidbody>();
                restorePlugContacts[i] = rb.detectCollisions;
                rb.detectCollisions = false;
                rb.isKinematic = true;
                if (havePosition[i]) rb.position = restoredPositions[i];
                var rot = data["cableRot" + i] as JArray;
                if (havePosition[i] && rot != null && rot.Count == 4)
                    rb.rotation = new Quaternion(rot[0].Value<float>(), rot[1].Value<float>(),
                        rot[2].Value<float>(), rot[3].Value<float>());
            }
            var poses = data["cableBodies"] as JArray;
            for (int i = 0; i < bodies.Length; i++)
            {
                var rb = bodies[i];
                restoreBodyContacts[i] = rb.detectCollisions;
                rb.detectCollisions = false;
                rb.isKinematic = true;
                if (poses != null && poses.Count == bodies.Length)
                {
                    var pose = poses[i] as JArray;
                    if (pose == null || pose.Count < 7) continue;
                    rb.position = new Vector3(pose[0].Value<float>(), pose[1].Value<float>(),
                        pose[2].Value<float>());
                    rb.rotation = new Quaternion(pose[3].Value<float>(), pose[4].Value<float>(),
                        pose[5].Value<float>(), pose[6].Value<float>());
                }
                else if (restorePositions)
                {
                    // Older saved endpoints without segment poses.
                    var axis = restoredPositions[1] - restoredPositions[0];
                    var direction = axis.sqrMagnitude > .0001f ? axis.normalized : Vector3.right;
                    rb.position = Vector3.Lerp(restoredPositions[0], restoredPositions[1],
                        (i + .5f) / bodies.Length) + Vector3.up * .16f;
                    rb.rotation = Quaternion.FromToRotation(Vector3.forward, direction);
                }
            }
            Physics.SyncTransforms();
            var savedJoints = data["cableJoints"] as JArray;
            bool exact = savedJoints != null && savedJoints.Count == bodies.Length + 1;
            if (exact)
            {
                for (int i = 0; i < bodies.Length; i++)
                    if (!RestoreJoint(bodies[i].GetComponent<CharacterJoint>(), savedJoints[i]))
                        exact = false;
                if (!RestoreJoint(plugs[1].GetComponent<CharacterJoint>(),
                    savedJoints[bodies.Length])) exact = false;
            }
            if (!exact && (poses != null || restorePositions))
                ReanchorChainFromLoadedPose();
            UpdateJacket();
        }

#if UNITY_EDITOR
        private static Vector3 StressArcPoint(Vector3 a, Vector3 b, float height, Vector3 bend, float t)
        {
            float wave = Mathf.Sin(Mathf.PI * t);
            return Vector3.Lerp(a, b, t) + (Vector3.up * height + bend) * wave;
        }

        private static float StressArcLength(Vector3 a, Vector3 b, float height, Vector3 bend)
        {
            Vector3 previous = a;
            float total = 0f;
            for (int i = 1; i <= 64; i++)
            {
                Vector3 point = StressArcPoint(a, b, height, bend, i / 64f);
                total += Vector3.Distance(previous, point);
                previous = point;
            }
            return total;
        }

        private static Vector3 StressArcSample(Vector3[] points, float[] distances, float distance)
        {
            int lo = 0, hi = distances.Length - 1;
            distance = Mathf.Clamp(distance, 0f, distances[hi]);
            while (lo + 1 < hi)
            {
                int middle = (lo + hi) / 2;
                if (distances[middle] < distance) lo = middle;
                else hi = middle;
            }
            float width = distances[hi] - distances[lo];
            return Vector3.Lerp(points[lo], points[hi],
                width > .00001f ? (distance - distances[lo]) / width : 0f);
        }

        // Editor F8 ONLY: a prefab delivery coil starts at the invisible root.
        // Collapsing its 22 capsules into a one-metre line (the old fixture)
        // made ten overlapping capsules hit the PSU/case in one physics step.
        // Space centres along ~82% of the REAL cable length, arching into free
        // space. Bend candidates point away from case/monitor colliders and
        // are preflight-tested before any physics step. Preserve normal radius,
        // segment masses and pin break force.
        // Nothing about gameplay cable placement or collision rules changes.
        public float PrepareStressTestSpan(Vector3 bend)
        {
            if (plugs == null || bodies == null || bodies.Length == 0) return 0f;
            Vector3 a = plugs[0].transform.position;
            Vector3 b = plugs[1].transform.position;
            float span = Vector3.Distance(a, b);
            if (span < .001f) return 0f;
            float targetLength = Mathf.Max(span, length * .82f);
            float low = 0f, high = length * .52f;
            for (int i = 0; i < 16; i++)
            {
                float middle = (low + high) * .5f;
                if (StressArcLength(a, b, middle, bend) < targetLength) low = middle;
                else high = middle;
            }
            var points = new Vector3[129];
            var distances = new float[129];
            points[0] = a;
            for (int i = 1; i < points.Length; i++)
            {
                points[i] = StressArcPoint(a, b, high, bend, i / 128f);
                distances[i] = distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }
            float total = distances[128];
            Rigidbody previous = plugs[0].GetComponent<Rigidbody>();
            for (int i = 0; i < bodies.Length; i++)
            {
                var rb = bodies[i];
                float centre = total * (i + .5f) / bodies.Length;
                rb.position = StressArcSample(points, distances, centre);
                var before = StressArcSample(points, distances, Mathf.Max(0f, centre - .07f));
                var after = StressArcSample(points, distances, Mathf.Min(total, centre + .07f));
                rb.rotation = Quaternion.LookRotation(after - before);
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                var link = rb.GetComponent<CharacterJoint>();
                Vector3 boundary = StressArcSample(points, distances,
                    total * i / bodies.Length);
                link.anchor = rb.transform.InverseTransformPoint(boundary);
                link.connectedAnchor = previous.transform.InverseTransformPoint(boundary);
                previous = rb;
            }
            var last = plugs[1].GetComponent<CharacterJoint>();
            last.anchor = Vector3.zero;
            last.connectedAnchor = previous.transform.InverseTransformPoint(b);
            Physics.SyncTransforms();
            UpdateJacket();
            return total;
        }

        // Diagnostic is TEST-ONLY and called once before enabling a new rope.
        // Exclude this rope and the exact plug/capsule collision exemptions;
        // never silently start a fixture with a capsule inside case/floor.
        public string StressInitialOverlaps()
        {
            if (bodies == null) return "no cable sections";
            int count = 0;
            var details = new System.Text.StringBuilder();
            foreach (var body in bodies)
            {
                var own = body.GetComponent<Collider>();
                if (own == null) continue;
                foreach (var other in Physics.OverlapBox(own.bounds.center,
                    own.bounds.extents, Quaternion.identity, ~0,
                    QueryTriggerInteraction.Ignore))
                {
                    if (other == null || other == own || other.isTrigger ||
                        other.GetComponentInParent<CableItem>() == this ||
                        Physics.GetIgnoreCollision(own, other)) continue;
                    Vector3 direction;
                    float depth;
                    if (!Physics.ComputePenetration(own, own.transform.position,
                        own.transform.rotation, other, other.transform.position,
                        other.transform.rotation, out direction, out depth) ||
                        depth < .02f) continue;
                    count++;
                    if (count <= 5)
                        details.Append(" [").Append(body.name).Append(" vs ")
                            .Append(other.name).Append(" depth=")
                            .Append(depth.ToString("0.000")).Append("m]");
                }
            }
            return count == 0 ? string.Empty : count + " overlaps:" + details;
        }
#endif

        public void FinishRestore()
        {
            if (!restoringPhysics) return;
            if (restorePositions)
            {
                Vector3 a = plugs[0].transform.position - restoredPositions[0];
                Vector3 b = plugs[1].transform.position - restoredPositions[1];
                if (a.sqrMagnitude > .0001f || b.sqrMagnitude > .0001f)
                    for (int i = 0; i < bodies.Length; i++)
                        bodies[i].position += Vector3.Lerp(a, b, (i + 1f) / (bodies.Length + 1f));
            }
            else if (plugs[0].Connected != null || plugs[1].Connected != null)
            {
                // Legacy cable without saved poses: its initial coil is at
                // the portal, not at the connected devices. Lay it between
                // its actual plugs before releasing physical contacts.
                for (int i = 0; i < bodies.Length; i++)
                    bodies[i].position = Vector3.Lerp(plugs[0].transform.position,
                        plugs[1].transform.position, (i + .5f) / bodies.Length) + Vector3.up * .16f;
            }
            foreach (var plug in plugs)
            {
                if (plug == null) continue;
                var rb = plug.GetComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            foreach (var rb in bodies)
            {
                rb.isKinematic = false;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
            // All pins have now been placed at their actual author-made pivots.
            // Enable contacts only after their per-device exceptions are in
            // place, so the first solver step cannot eject a monitor or case.
            for (int i = 0; i < plugs.Length; i++)
            {
                if (plugs[i] == null) continue;
                plugs[i].GetComponent<Rigidbody>().detectCollisions = restorePlugContacts[i];
            }
            for (int i = 0; i < bodies.Length; i++)
                bodies[i].detectCollisions = restoreBodyContacts[i];
            foreach (var plug in plugs)
                if (plug != null) plug.RefreshCollisionExceptions();
            restoreBodyContacts = null;
            restoringPhysics = false;
            overstretchSteps = 0;
            strainCooldownUntil = Time.time + .35f;
        }

        public void RestorePorts()
        {
            var main = Main.Instance;
            if (main == null || plugs == null) return;
            for (int i = 0; i < 2; i++)
            {
                if (restoredOwners[i] == 0 || string.IsNullOrEmpty(restoredPorts[i])) continue;
                var item = main.GetItemById(restoredOwners[i]);
                if (item == null)
                {
                    Debug.LogWarning($"Cable restore: {name} end={i} ownerId={restoredOwners[i]} " +
                        $"port={restoredPorts[i]}: owner not found");
                    continue;
                }
                bool found = false, connected = false;
                foreach (var port in item.GetComponentsInChildren<CablePort>(true))
                {
                    // Installed GPU and PSU ports may live below a case/board.
                    // Never attach to a same-named socket belonging to a
                    // different nested Item.
                    if (port.owner != item || port.name != restoredPorts[i]) continue;
                    found = true;
                    if (plugs[i].TryInsert(port, true)) { connected = true; break; }
                    var other = plugs[1 - i].Connected;
                    float span = other == null ? 0f :
                        Vector3.Distance(other.Snap.position, port.Snap.position);
                    Debug.LogWarning($"Cable restore: {name} id={Id} end={i} " +
                        $"owner={item.name}({item.Id}) port={port.name} kind={port.kind} " +
                        $"occupied={(port.plug != null ? port.plug.Cable?.Id.ToString() : "no")} " +
                        $"span={span:0.000}m length={Length:0.000}m " +
                        $"reason={(span > Length ? "too far" : "incompatible/busy/disabled")}");
                }
                if (!found || !connected)
                    Debug.LogWarning($"Cable restore: {name} id={Id} end={i} " +
                        $"ownerId={restoredOwners[i]} port={restoredPorts[i]} " +
                        $"result={(found ? "insertion failed" : "port missing on owner")}");
            }
        }
    }
}
