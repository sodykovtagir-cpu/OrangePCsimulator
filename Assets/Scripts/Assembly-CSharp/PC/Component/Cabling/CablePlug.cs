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
        public bool IsHeld { get; private set; }

        public void Init(CableItem cable, int end) { Cable = cable; End = end; }
        public void SetHeld(bool held) { IsHeld = held; }

        public bool TryInsert(CablePort port, bool restoring = false)
        {
            // Wait until the player's spring drag has been detached. Otherwise
            // the plug is snapped and simultaneously pulled away from its port.
            if ((!restoring && IsHeld) || Time.time < cooldown || port == null || !port.Accepts(this)) return false;
            var other = Cable.End(1 - End);
            if (other != null && other.Connected != null &&
                Vector3.Distance(other.Connected.Snap.position, port.Snap.position) > Cable.Length * .95f)
                return false;
            if (!restoring && Vector3.Distance(transform.position, port.TriggerCenter) > port.CaptureRadius) return false;
            // Enlarged outlet triggers overlap. Pick the nearest free socket
            // rather than whichever OnTriggerStay happens to run first.
            if (!restoring && port.kind == PortKind.Outlet && port.owner != null)
            {
                float distance = Vector3.Distance(transform.position, port.Snap.position);
                foreach (var otherPort in port.owner.GetComponentsInChildren<CablePort>(true))
                    if (otherPort != port && otherPort.kind == PortKind.Outlet &&
                        otherPort.plug == null && otherPort.Powered &&
                        Vector3.Distance(transform.position, otherPort.Snap.position) < distance)
                        return false;
            }
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
            Connected = port;
            port.plug = this;
            CablePower.RefreshAll();
            return true;
        }

        public void Unplug()
        {
            if (Connected == null) return;
            var old = Connected;
            Connected = null;
            if (old != null && old.plug == this) old.plug = null;
            if (pin != null) Destroy(pin);
            pin = null;
            cooldown = Time.time + .5f;
            CablePower.RefreshAll();
        }

        private void OnDisable() { IsHeld = false; }
        private void OnDestroy() { Unplug(); }
    }
}
