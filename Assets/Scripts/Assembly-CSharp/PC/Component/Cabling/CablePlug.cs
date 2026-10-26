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
        private Action<CablePlug> releaseDrag;
        [SerializeField, Min(10f)] private float pullOutForce = 200f;
        public float PullOutForce => pullOutForce >= 10f ? pullOutForce : 200f;
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
            var solid = GetComponent<Collider>();
            if (port == null || solid == null || !solid.enabled) return false;
            float reach = port.CaptureRadius + extra;
            return (solid.ClosestPoint(port.TriggerCenter) - port.TriggerCenter).sqrMagnitude <=
                reach * reach;
        }
        // Re-arm an unplugged connector as soon as its collider has actually
        // cleared the old trigger. This must run even when no insertion is being
        // attempted: once the plug returns to the socket it is too late to
        // infer that it left in between.
        private void RefreshYankedPort()
        {
            if (yankedPort != null && !NearPort(yankedPort, -.10f)) yankedPort = null;
        }

        public void SetHeld(bool held, Action<CablePlug> onAutoSnap = null)
        {
            IsHeld = held;
            releaseDrag = held ? onAutoSnap : null;
        }
        public bool TryInsert(CablePort port, bool restoring = false)
        {
            // Do not let trigger events attach a saved plug to a nearby wrong
            // socket before SaveManager has restored its original port.
            if (!restoring && Cable != null && Cable.RestoringPhysics) return false;
            if (Time.time < cooldown || port == null || !port.Accepts(this)) return false;
            // Prevent immediate reinsertion inside the same trigger, but allow
            // insertion again once FixedUpdate observed the plug leaving it.
            RefreshYankedPort();
            if (port == yankedPort) return false;
            var other = Cable.End(1 - End);
            if (other != null && other.Connected != null &&
                Vector3.Distance(other.Connected.Snap.position, port.Snap.position) > Cable.Length * .95f)
                return false;
            // The model's BoxCollider is offset from its Rigidbody origin.
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
            transform.SetPositionAndRotation(port.Snap.position, port.Snap.rotation);
            var rb = GetComponent<Rigidbody>();
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            pin = gameObject.AddComponent<FixedJoint>();
            pin.connectedBody = port.GetComponentInParent<Rigidbody>();
            pin.autoConfigureConnectedAnchor = false;
            pin.anchor = Vector3.zero;
            pin.connectedAnchor = pin.connectedBody != null
                ? pin.connectedBody.transform.InverseTransformPoint(port.Snap.position)
                : port.Snap.position;
            pin.enableCollision = false;
            pin.breakForce = PullOutForce; // same default force as Slot.cs
            pin.breakTorque = PullOutForce;
            Connected = port;
            ConnectedAt = Time.time;
            port.plug = this;
            CablePower.RefreshAll();
            return true;
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
            var solid = GetComponent<Collider>();
            if (solid == null || !solid.enabled) return;
            float radius = Mathf.Max(.7f, solid.bounds.extents.magnitude + .5f);
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

        public void Unplug()
        {
            if (Connected == null) return;
            var old = Connected;
            yankedPort = old;
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
            if (Connected != null) Unplug();
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
