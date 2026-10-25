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

        public void Init(CableItem cable, int end) { Cable = cable; End = end; }

        public bool TryInsert(CablePort port, bool restoring = false)
        {
            if (Time.time < cooldown || port == null || !port.Accepts(this)) return false;
            var other = Cable.End(1 - End);
            if (other != null && other.Connected != null &&
                Vector3.Distance(other.Connected.transform.position, port.transform.position) > Cable.Length * .95f)
                return false;
            if (!restoring && Vector3.Distance(transform.position, port.transform.position) > .18f) return false;
            transform.position = port.transform.position;
            var rb = GetComponent<Rigidbody>();
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            pin = gameObject.AddComponent<FixedJoint>();
            pin.connectedBody = port.GetComponentInParent<Rigidbody>();
            pin.autoConfigureConnectedAnchor = false;
            pin.anchor = Vector3.zero;
            pin.connectedAnchor = pin.connectedBody != null
                ? pin.connectedBody.transform.InverseTransformPoint(port.transform.position)
                : port.transform.position;
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

        private void OnDestroy() { Unplug(); }
    }
}
