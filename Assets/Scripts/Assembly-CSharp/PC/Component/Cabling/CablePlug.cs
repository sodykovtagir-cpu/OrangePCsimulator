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
        private FixedJoint pin;
        private float cooldown;
        private CablePort yankedPort;
        private Action<CablePlug> releaseDrag;
        [SerializeField, Min(10f)] private float pullOutForce = 200f;
        public float PullOutForce => pullOutForce >= 10f ? pullOutForce : 200f;
        public bool IsHeld { get; private set; }

        // Physics.IgnoreCollision is PER PAIR. Only this plug and the nearest
        // rope capsule may pass through their own GPU and enclosing case; all
        // other world/PC/cable contacts remain physical.
        private struct Pair
        {
            public Collider plug;
            public Collider target;
        }
        private readonly List<Pair> gpuPairs = new List<Pair>();
        private readonly List<Pair> casePairs = new List<Pair>();
        private readonly List<Pair> pendingRestore = new List<Pair>();
        private Coroutine restoreRoutine;
        private GPU watchedGpu;
        private Case currentCase;
        private CablePort approachPort;

        public void Init(CableItem cable, int end) { Cable = cable; End = end; }
        public void SetHeld(bool held, Action<CablePlug> onAutoSnap = null)
        {
            IsHeld = held;
            releaseDrag = held ? onAutoSnap : null;
        }
        public void PullOutIfOverloaded(float tension)
        {
            if (Connected != null && tension >= PullOutForce) Unplug();
        }

        public bool TryInsert(CablePort port, bool restoring = false)
        {
            if (Time.time < cooldown || port == null || !port.Accepts(this)) return false;
            // A yanked connector cannot magically reinsert itself while still
            // overlapping the same large trigger. Move it clear first.
            if (yankedPort != null)
            {
                if (Vector3.Distance(transform.position, yankedPort.TriggerCenter) >
                    yankedPort.CaptureRadius + .25f) yankedPort = null;
                else if (port == yankedPort) return false;
            }
            var other = Cable.End(1 - End);
            if (other != null && other.Connected != null &&
                Vector3.Distance(other.Connected.Snap.position, port.Snap.position) > Cable.Length * .95f)
                return false;
            if (!restoring && Vector3.Distance(transform.position, port.TriggerCenter) > port.CaptureRadius) return false;
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
            // Ignore the socket's solid GPU/case panel *before* teleporting to
            // its inset pivot, otherwise the penetration solver ejects it.
            if (port.kind == PortKind.GpuVideo && port.owner is GPU gpu)
            {
                watchedGpu = gpu;
                AddPairs(gpuPairs, gpu, gpu);
                SyncCase();
            }
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
            port.plug = this;
            CablePower.RefreshAll();
            return true;
        }

        // Give a held plug a passage through its target while it approaches an
        // exterior trigger; insertion now releases the grab automatically.
        public void PrepareApproach(CablePort port)
        {
            if (!IsHeld || Connected != null || port == null ||
                port.kind != PortKind.GpuVideo || !(port.owner is GPU gpu) || !port.Accepts(this)) return;
            if (approachPort == port) return;
            if (approachPort != null) LeaveApproach(approachPort);
            approachPort = port;
            watchedGpu = gpu;
            AddPairs(gpuPairs, gpu, gpu);
            SyncCase();
        }

        public void LeaveApproach(CablePort port)
        {
            if (approachPort != port || Connected != null) return;
            approachPort = null;
            watchedGpu = null;
            currentCase = null;
            DeferRestore(gpuPairs);
            DeferRestore(casePairs);
        }

        private void FixedUpdate()
        {
            // A GPU may be wired while loose and then installed in a case.
            // Reassert pairs after Unity resets them when a collider is toggled.
            if (Connected == null && approachPort != null &&
                Vector3.Distance(transform.position, approachPort.TriggerCenter) >
                approachPort.CaptureRadius + .25f)
                LeaveApproach(approachPort);
            if ((Connected == null && approachPort == null) || watchedGpu == null) return;
            SyncCase();
            Reinstate(gpuPairs);
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
            var next = FindCase(watchedGpu);
            if (next == currentCase) return;
            DeferRestore(casePairs);
            currentCase = next;
            if (currentCase != null)
                AddPairs(casePairs, currentCase.transform, currentCase.GetComponent<Item>());
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
            watchedGpu = null;
            currentCase = null;
            DeferRestore(gpuPairs);
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
