using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PC.Component.Cabling
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CablePlug : MonoBehaviour
    {
        public CableItem Cable { get; private set; }
        public int End { get; private set; }
        public CablePort Connected { get; private set; }
        public float ConnectedAt { get; private set; }
        private FixedJoint pin;
        private float cooldown;
        private CablePort yankedPort;
        // A passively broken LAN pin must not reconnect every time its loose
        // plug bounces back into the same socket. A deliberate hand grab rearms it.
        private CablePort manualReinsertPort;
        private Action<CablePlug> releaseDrag;
        [SerializeField, Min(10f)] private float pullOutForce = 200f;
        // Authored marker on the connector model where the jacket and first
        // physical joint meet. Socket snapping still uses the plug root/port.
        [SerializeField] private Transform cableExitPivot;
        // The pivot can be nested under the visual FBX (and that visual can be
        // scaled/rotated). Joint anchors are in the Rigidbody/plug ROOT space,
        // never in the pivot's immediate parent's local space.
        public Vector3 CableExitLocalPosition => cableExitPivot != null ?
            transform.InverseTransformPoint(cableExitPivot.position) : Vector3.zero;
        public Vector3 CableExitPosition => cableExitPivot != null ?
            cableExitPivot.position : transform.position;
        // Restored/interpolated Transforms can lag the saved Rigidbody pose.
        public Vector3 CableExitPhysicsPosition
        {
            get
            {
                var body = GetComponent<Rigidbody>();
                return body != null ? body.position + body.rotation * CableExitLocalPosition :
                    CableExitPosition;
            }
        }

        public void EnsureCableExitPivot(Vector3 fallbackPosition, Transform connectorModel = null)
        {
            if (cableExitPivot == null && connectorModel != null)
                cableExitPivot = connectorModel.Find("Cable Pivot");
            // Old saved prefabs can still have a marker directly under Plug A/B.
            if (cableExitPivot == null) cableExitPivot = transform.Find("Cable Pivot");
            if (cableExitPivot != null) return;
            // On older cable instances, add the marker on the actual connector
            // model when it exists; preserve its plug-root-space position.
            var marker = new GameObject("Cable Pivot");
            cableExitPivot = marker.transform;
            var parent = connectorModel != null && connectorModel.IsChildOf(transform)
                ? connectorModel : transform;
            cableExitPivot.SetParent(parent, false);
            cableExitPivot.position = transform.TransformPoint(fallbackPosition);
        }
        public float PullOutForce => pullOutForce >= 10f ? pullOutForce : 200f;
        // Current solver load is read only when a two-ended rope is nearly
        // taut. A 1 kg monitor supplies ~10 N: too little to break the normal
        // 200 N manual-pull pin, but enough to suspend it indefinitely.
        internal float PinLoad => pin != null ? pin.currentForce.magnitude : 0f;
        public bool IsHeld { get; private set; }

        // Physics.IgnoreCollision is PER PAIR. Only this plug and its nearest
        // rope capsule may pass through the owner of this particular socket
        // (and the enclosing case for installed GPU/PSU/board). Other cable
        // sections, hardware and world contacts remain physical.
        private struct Pair
        {
            public Collider plug;
            public Collider target;
        }
        private readonly List<Pair> devicePairs = new List<Pair>();
        private readonly List<Pair> casePairs = new List<Pair>();
        private readonly List<Pair> pendingRestore = new List<Pair>();
        private Coroutine restoreRoutine;
        private Item watchedOwner;
        private Case currentCase;
        private CablePort approachPort;
        // Unity can miss a trigger callback when a small socket and a dragged
        // rigidbody move past one another between physics steps.
        private readonly Collider[] nearbyPortColliders = new Collider[24];

        public void Init(CableItem cable, int end) { Cable = cable; End = end; }

        private bool NearPort(CablePort port, float extra = 0f)
        {
            if (port == null) return false;
            float reach = port.CaptureRadius + extra;
            foreach (var solid in GetComponentsInChildren<Collider>(true))
                if (solid.enabled && !solid.isTrigger && solid.gameObject.activeInHierarchy &&
                    (solid.ClosestPoint(port.TriggerCenter) - port.TriggerCenter).sqrMagnitude <=
                        reach * reach)
                    return true;
            return false;
        }
        // Re-arm an unplugged connector as soon as its collider has actually
        // cleared the old trigger. This must run even when no insertion is being
        // attempted: once the plug returns to the socket it is too late to
        // infer that it left in between.
        private void RefreshYankedPort()
        {
            // Stay disarmed until the plug has cleared the ENTIRE trigger,
            // not just its inner core. Otherwise a taut rope can immediately
            // reinsert the plug while it still brushes the socket's edge.
            if (yankedPort != null && !NearPort(yankedPort, .10f)) yankedPort = null;
        }

        public void SetHeld(bool held, Action<CablePlug> onAutoSnap = null)
        {
            IsHeld = held;
            if (held) manualReinsertPort = null;
            releaseDrag = held ? onAutoSnap : null;
        }
        public bool TryInsert(CablePort port, bool restoring = false)
        {
            // Do not let trigger events attach a saved plug to a nearby wrong
            // socket before SaveManager has restored its original port.
            if (!restoring && Cable != null && Cable.RestoringPhysics) return false;
            if ((!restoring && Time.time < cooldown) || port == null ||
                !port.Accepts(this, restoring)) return false;
            // Prevent immediate reinsertion inside the same trigger, but allow
            // insertion again once FixedUpdate observed the plug leaving it.
            RefreshYankedPort();
            if (!restoring && (port == yankedPort ||
                (port == manualReinsertPort && !IsHeld))) return false;
            var other = Cable.End(1 - End);
            if (other != null && other.Connected != null)
            {
                // All cables now measure the distance between their jacket
                // exits. Legacy plugs without pivots retain snap-to-snap reach.
                float span = CableExitLocalPosition == Vector3.zero &&
                    other.CableExitLocalPosition == Vector3.zero ?
                    Vector3.Distance(other.Connected.Snap.position, port.Snap.position) :
                    Vector3.Distance(other.CableExitPhysicsPosition,
                        port.Snap.position + port.Snap.rotation * CableExitLocalPosition);
                if (span > Cable.Length * (restoring ? 1f : .95f)) return false;
            }
            // Authored colliders can be offset from the Rigidbody origin.
            // A real overlap must not be rejected because the origin is outside
            // the sphere while the visible plug is already touching the socket.
            if (!restoring && !NearPort(port)) return false;
            if (!restoring && port.kind == PortKind.Outlet && port.owner != null)
            {
                float distance = Vector3.Distance(transform.position, port.Snap.position);
                foreach (var otherPort in port.owner.GetComponentsInChildren<CablePort>(true))
                    if (otherPort != port && otherPort.kind == PortKind.Outlet &&
                        otherPort.plug == null && otherPort.Powered &&
                        Vector3.Distance(transform.position, otherPort.Snap.position) < distance)
                        return false;
            }
            // Connect as soon as the plug reaches the trigger. End the player's
            // spring drag first, otherwise it would pull against the new joint.
            if (!restoring && IsHeld)
            {
                releaseDrag?.Invoke(this);
                if (IsHeld) return false; // only the active grabber may release it
            }
            // Drop temporary approach exclusions if this end connects to a
            // different port (for example the display on the other end).
            if (approachPort != null && approachPort != port) LeaveApproach(approachPort);
            // Exempt only this plug and its neighbouring capsule from the
            // target device before snapping to an inset authored pivot. This
            // includes monitor video/power, outlets, boards and loose PSUs.
            WatchPort(port);
            approachPort = null;
            var rb = GetComponent<Rigidbody>();
            Vector3 previousExit = CableExitPhysicsPosition;
            Vector3 snappedExit = port.Snap.position +
                port.Snap.rotation * CableExitLocalPosition;
            if (Cable != null && Cable.RestoringPhysics)
            {
                // Saved joints refer to the physics pose, not the interpolated
                // Transform. Only the frozen save-load chain needs this path.
                // The test fixture also calls TryInsert(..., true) but is NOT
                // restoring: its already-laid rope must retain normal snapping.
                rb.position = port.Snap.position;
                rb.rotation = port.Snap.rotation;
            }
            else
            {
                transform.SetPositionAndRotation(port.Snap.position, port.Snap.rotation);
                if (!restoring && Cable != null)
                    Cable.SettleEndAtSocket(End, snappedExit - previousExit);
            }
            // A saved plug is kinematic until CableItem.FinishRestore.
            // PhysX rejects writes to its velocity in that state. Ordinary
            // inserted (dynamic) plugs must still lose their throw velocity.
            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            pin = gameObject.AddComponent<FixedJoint>();
            pin.connectedBody = port.GetComponentInParent<Rigidbody>();
            pin.autoConfigureConnectedAnchor = false;
            pin.anchor = Vector3.zero;
            pin.connectedAnchor = pin.connectedBody != null
                ? pin.connectedBody.transform.InverseTransformPoint(port.Snap.position)
                : port.Snap.position;
            pin.enableCollision = false;
            // A loaded rope can apply an artificial impulse before its saved
            // rigidbodies have settled. Keep the pin during reconstruction,
            // then restore the SAME gameplay break threshold (not a permanent
            // strength buff) after the hosts are unfrozen.
            pin.breakForce = PullOutForce;
            pin.breakTorque = PullOutForce;
            if (restoring)
            {
                pin.breakForce = Mathf.Infinity;
                pin.breakTorque = Mathf.Infinity;
                StartCoroutine(ArmRestoredPin(pin));
            }
            Connected = port;
            ConnectedAt = Time.time;
            port.plug = this;
            // Saved plugs are restored as one batch. Recomputing power/video
            // after each end would see a half-wired PC and turn it off.
            if (!restoring) CablePower.RefreshAll();
            return true;
        }

        private IEnumerator ArmRestoredPin(FixedJoint restoredPin)
        {
            while (SaveManager.RestoringSave) yield return null;
            // One physics step with the restored hardware free to move.
            yield return new WaitForFixedUpdate();
            if (restoredPin != null && pin == restoredPin && Connected != null)
            {
                restoredPin.breakForce = PullOutForce;
                restoredPin.breakTorque = PullOutForce;
            }
        }

        private void WatchPort(CablePort port)
        {
            if (port == null || port.owner == null) return;
            if (watchedOwner != port.owner)
            {
                DeferRestore(devicePairs);
                DeferRestore(casePairs);
                currentCase = null;
                watchedOwner = port.owner;
            }
            AddPairs(devicePairs, watchedOwner.transform, watchedOwner);
            SyncCase();
        }

        // Give a held plug a passage through its own socket and case while it
        // approaches the trigger; restore everything if it leaves unconnected.
        public void PrepareApproach(CablePort port)
        {
            if (!IsHeld || Connected != null || port == null || !port.Accepts(this)) return;
            if (approachPort == port) return;
            if (approachPort != null) LeaveApproach(approachPort);
            approachPort = port;
            WatchPort(port);
        }

        public void LeaveApproach(CablePort port)
        {
            if (approachPort != port || Connected != null) return;
            approachPort = null;
            watchedOwner = null;
            currentCase = null;
            DeferRestore(devicePairs);
            DeferRestore(casePairs);
        }

        private void FixedUpdate()
        {
            RefreshYankedPort();
            // Trigger events alone are not reliable for a small moving socket.
            // While the player holds the plug, also look for a genuinely nearby
            // port. This does not move authored triggers or snap from a distance.
            if (IsHeld && Connected == null) TryNearbyPort();
            // GPU/PSU/board may be installed into a case after cabling. Reassert
            // only their scoped pairs after Unity toggles a device collider.
            if (Connected == null && approachPort != null &&
                Vector3.Distance(transform.position, approachPort.TriggerCenter) >
                approachPort.CaptureRadius + .25f)
                LeaveApproach(approachPort);
            if ((Connected == null && approachPort == null) || watchedOwner == null) return;
            SyncCase();
            Reinstate(devicePairs);
            Reinstate(casePairs);
        }

        private void TryNearbyPort()
        {
            float radius = .7f;
            bool hasSolid = false;
            foreach (var solid in GetComponentsInChildren<Collider>(true))
            {
                if (!solid.enabled || solid.isTrigger || !solid.gameObject.activeInHierarchy)
                    continue;
                hasSolid = true;
                radius = Mathf.Max(radius, Vector3.Distance(transform.position, solid.bounds.center) +
                    solid.bounds.extents.magnitude + .5f);
            }
            if (!hasSolid) return;
            // CablePort.Add places authored and fallback port triggers on layer 2.
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius,
                nearbyPortColliders, 1 << 2, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count && IsHeld && Connected == null; i++)
            {
                var port = nearbyPortColliders[i] != null
                    ? nearbyPortColliders[i].GetComponent<CablePort>() : null;
                if (port == null || !NearPort(port)) continue;
                PrepareApproach(port);
                TryInsert(port);
            }
        }

        // Called before the first physics step after a loaded cable becomes
        // solid again. Do not wait for FixedUpdate to reapply these scoped pairs.
        internal void RefreshCollisionExceptions()
        {
            Reinstate(devicePairs);
            Reinstate(casePairs);
        }

        private static void Reinstate(List<Pair> pairs)
        {
            foreach (var pair in pairs)
                if (pair.plug != null && pair.target != null && pair.plug.enabled &&
                    pair.target.enabled && pair.plug.gameObject.activeInHierarchy &&
                    pair.target.gameObject.activeInHierarchy &&
                    !Physics.GetIgnoreCollision(pair.plug, pair.target))
                    Physics.IgnoreCollision(pair.plug, pair.target, true);
        }

        private void SyncCase()
        {
            Case next = null;
            if (watchedOwner is GPU gpu) next = FindCase(gpu);
            else if (watchedOwner is Supply supply) next = FindCase(supply);
            else if (watchedOwner is Motherboard board) next = FindCase(board);
            if (next == currentCase) return;
            DeferRestore(casePairs);
            currentCase = next;
            if (currentCase == null) return;
            var caseOwner = currentCase.GetComponent<Item>();
            if (caseOwner != watchedOwner)
                AddPairs(casePairs, currentCase.transform, caseOwner);
        }

        private static Case FindCase(GPU gpu)
        {
            if (gpu == null) return null;
            var direct = gpu.GetComponentInParent<Case>();
            if (direct != null) return direct;
            var connector = gpu.GetComponent<Connector>();
            var slot = connector != null ? connector.slot as HardwareSlot : null;
            if (slot == null || !slot.Ready || slot.Hardware != gpu) return null;
            direct = slot.GetComponentInParent<Case>();
            if (direct != null) return direct;
            var board = slot.GetComponentInParent<Motherboard>();
            var boardConnector = board != null ? board.GetComponent<Connector>() : null;
            if (boardConnector == null || boardConnector.slot == null) return null;
            return boardConnector.slot.GetComponentInParent<Case>();
        }

        private static Case FindCase(Supply supply)
        {
            if (supply == null) return null;
            var direct = supply.GetComponentInParent<Case>();
            if (direct != null) return direct;
            var connector = supply.GetComponent<Connector>();
            var slot = connector != null ? connector.slot as HardwareSlot : null;
            if (slot == null || !slot.Ready || slot.Hardware != supply) return null;
            return slot.GetComponentInParent<Case>();
        }

        private static Case FindCase(Motherboard board)
        {
            if (board == null) return null;
            var direct = board.GetComponentInParent<Case>();
            if (direct != null) return direct;
            var connector = board.GetComponent<Connector>();
            var slot = connector != null ? connector.slot as HardwareSlot : null;
            if (slot == null || !slot.Ready || slot.Hardware != board) return null;
            return slot.GetComponentInParent<Case>();
        }

        private void AddPairs(List<Pair> list, UnityEngine.Component device, Item owner)
        {
            if (device == null || owner == null) return;
            var adjacent = Cable != null ? Cable.NearEndCollider(End) : null;
            var ourColliders = GetComponentsInChildren<Collider>(true);
            foreach (var target in device.GetComponentsInChildren<Collider>(true))
            {
                if (target == null || target.isTrigger || !target.enabled ||
                    !target.gameObject.activeInHierarchy || target.GetComponentInParent<Item>() != owner)
                    continue;
                foreach (var ours in ourColliders)
                    AddPair(list, ours, target);
                AddPair(list, adjacent, target);
            }
        }

        private void AddPair(List<Pair> list, Collider ours, Collider target)
        {
            if (ours == null || ours.isTrigger || !ours.enabled || !ours.gameObject.activeInHierarchy ||
                ours == target || target == null || HasPair(list, ours, target)) return;
            RemovePair(pendingRestore, ours, target);
            Physics.IgnoreCollision(ours, target, true);
            list.Add(new Pair { plug = ours, target = target });
        }

        private static bool HasPair(List<Pair> list, Collider ours, Collider target)
        {
            foreach (var pair in list)
                if (pair.plug == ours && pair.target == target) return true;
            return false;
        }

        private static void RemovePair(List<Pair> list, Collider ours, Collider target)
        {
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].plug == ours && list[i].target == target) list.RemoveAt(i);
        }

        private void DeferRestore(List<Pair> list)
        {
            foreach (var pair in list)
                if (!HasPair(pendingRestore, pair.plug, pair.target)) pendingRestore.Add(pair);
            list.Clear();
            if (pendingRestore.Count > 0 && restoreRoutine == null && isActiveAndEnabled)
                restoreRoutine = StartCoroutine(RestoreWhenClear());
        }

        private IEnumerator RestoreWhenClear()
        {
            // Re-enabling contacts while the connector is still embedded would
            // fling it away. Wait until each particular solid collider is clear.
            while (pendingRestore.Count > 0)
            {
                yield return new WaitForFixedUpdate();
                for (int i = pendingRestore.Count - 1; i >= 0; i--)
                {
                    var pair = pendingRestore[i];
                    if (pair.plug == null || pair.target == null ||
                        !pair.plug.enabled || !pair.target.enabled ||
                        !pair.plug.gameObject.activeInHierarchy || !pair.target.gameObject.activeInHierarchy)
                    {
                        pendingRestore.RemoveAt(i);
                        continue;
                    }
                    if (pair.plug.bounds.Intersects(pair.target.bounds)) continue;
                    Physics.IgnoreCollision(pair.plug, pair.target, false);
                    pendingRestore.RemoveAt(i);
                }
            }
            restoreRoutine = null;
        }

        public void Unplug() { Unplug(false); }

        public void Unplug(bool blockAutomaticReinsert)
        {
            if (Connected == null) return;
            var old = Connected;
            yankedPort = old;
            if (blockAutomaticReinsert && Cable != null && Cable.Kind == CableKind.LAN)
                manualReinsertPort = old;
            Connected = null;
            if (old != null && old.plug == this) old.plug = null;
            if (pin != null) Destroy(pin);
            pin = null;
            watchedOwner = null;
            currentCase = null;
            DeferRestore(devicePairs);
            DeferRestore(casePairs);
            cooldown = Time.time + .5f;
            CablePower.RefreshAll();
        }

        private void OnJointBreak(float force)
        {
            // The rope's end CharacterJoint has no break limit; the pin is the
            // breakable connection. Clear both ends of the logical link too.
#if UNITY_EDITOR
            if (Connected != null && CableStressMode.Recording)
                Debug.LogWarning($"Cable stress: PIN BREAK cable={Cable?.spawnId} id={Cable?.Id} " +
                    $"end={End} owner={Connected.owner?.name} id={Connected.owner?.Id} " +
                    $"port={Connected.name} force={force:0.00}");
            else if (Connected != null && Cable != null &&
                (Cable.Kind == CableKind.HDMI || Cable.Kind == CableKind.LAN))
                Debug.LogWarning($"Cable {Cable.Kind}: PIN BREAK end={End} " +
                    $"owner={Connected.owner?.name} port={Connected.name} " +
                    $"force={force:0.00} held={IsHeld} " +
                    $"sincePlugged={Time.time - ConnectedAt:0.00}s");
#endif
            if (Connected != null) Unplug(!IsHeld);
        }

        private void OnEnable()
        {
            if (pendingRestore.Count > 0 && restoreRoutine == null)
                restoreRoutine = StartCoroutine(RestoreWhenClear());
        }

        private void OnDisable()
        {
            SetHeld(false);
            if (restoreRoutine != null)
            {
                StopCoroutine(restoreRoutine);
                restoreRoutine = null;
            }
        }
        private void OnDestroy() { Unplug(); }
    }
}
