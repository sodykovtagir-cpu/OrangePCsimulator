using UnityEngine;

namespace PC.Component.Cabling
{
    // Place this marker over the *real* PSU cutout in a case prefab. A single
    // solid BoxCollider would otherwise block the cable even though the model
    // has a hole. At runtime replace that box with four colliders around the
    // opening; the rest of the case remains solid to plugs and cable capsules.
    public sealed class CableCaseOpening : MonoBehaviour
    {
        [SerializeField] private BoxCollider solidCase;
        [SerializeField] private Vector2 holeSize = new Vector2(.32f, .24f);

        private void Awake()
        {
            var pcCase = GetComponentInParent<Case>();
            if (pcCase == null) return;
            if (solidCase == null)
            {
                float largest = 0f;
                foreach (var box in pcCase.GetComponentsInChildren<BoxCollider>(true))
                {
                    if (box.isTrigger || !box.enabled || box.transform == transform) continue;
                    var p = box.transform.InverseTransformPoint(transform.position) - box.center;
                    var half = box.size * .5f;
                    if (Mathf.Abs(p.x) > half.x || Mathf.Abs(p.y) > half.y ||
                        Mathf.Abs(p.z) > half.z + .25f) continue;
                    float volume = box.size.x * box.size.y * box.size.z;
                    if (volume <= largest) continue;
                    largest = volume;
                    solidCase = box;
                }
            }
            if (solidCase == null || solidCase.isTrigger || !solidCase.enabled) return;
            var tr = solidCase.transform;
            var center = tr.InverseTransformPoint(transform.position);
            // Move/rotate the marker with the case's rear plane. Hole axes are
            // the box's local X and Y; its full depth is open for the PSU plug.
            float sx = holeSize.x * transform.lossyScale.x / Mathf.Max(.0001f, tr.lossyScale.x);
            float sy = holeSize.y * transform.lossyScale.y / Mathf.Max(.0001f, tr.lossyScale.y);
            var low = solidCase.center - solidCase.size * .5f;
            var high = solidCase.center + solidCase.size * .5f;
            float x0 = Mathf.Clamp(center.x - sx * .5f, low.x, high.x);
            float x1 = Mathf.Clamp(center.x + sx * .5f, low.x, high.x);
            float y0 = Mathf.Clamp(center.y - sy * .5f, low.y, high.y);
            float y1 = Mathf.Clamp(center.y + sy * .5f, low.y, high.y);
            if (x1 - x0 < .02f || y1 - y0 < .02f) return;
            // Children keep the original Rigidbody (if present) and material.
            Slab(low.x, x0, low.y, high.y, low.z, high.z, "Left");
            Slab(x1, high.x, low.y, high.y, low.z, high.z, "Right");
            Slab(x0, x1, low.y, y0, low.z, high.z, "Below");
            Slab(x0, x1, y1, high.y, low.z, high.z, "Above");
            solidCase.enabled = false;
        }

        private void Slab(float x0, float x1, float y0, float y1, float z0, float z1, string label)
        {
            if (x1 - x0 < .005f || y1 - y0 < .005f) return;
            var go = new GameObject("PSU opening " + label);
            go.layer = solidCase.gameObject.layer;
            go.transform.SetParent(solidCase.transform, false);
            var collider = go.AddComponent<BoxCollider>();
            collider.center = new Vector3((x0 + x1) * .5f, (y0 + y1) * .5f, (z0 + z1) * .5f);
            collider.size = new Vector3(x1 - x0, y1 - y0, z1 - z0);
            collider.sharedMaterial = solidCase.sharedMaterial;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(holeSize.x, holeSize.y, .45f));
        }
    }
}
