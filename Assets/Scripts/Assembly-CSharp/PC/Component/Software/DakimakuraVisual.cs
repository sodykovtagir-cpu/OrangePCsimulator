using UnityEngine;

namespace PC.Component.Software
{
    // A separate physical Item. Instantiating the imported FBX (rather than
    // taking its bare mesh) preserves the author's hierarchy and transforms.
    public sealed class DakimakuraVisual : MonoBehaviour
    {
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private float lengthMetres = 1.5f;
        public bool Ready { get; private set; }

        private void Awake()
        {
            if (modelPrefab == null) { Debug.LogError("Dakimakura FBX is not assigned"); return; }
            var visual = Instantiate(modelPrefab, transform, false);
            visual.name = "Dakimakura Model";
            var mesh = visual.GetComponentInChildren<MeshFilter>(true);
            var renderer = visual.GetComponentInChildren<MeshRenderer>(true);
            if (mesh == null || mesh.sharedMesh == null || renderer == null)
            {
                Debug.LogError("Dakimakura FBX has no renderable mesh");
                return;
            }

            // Normalize model import units to a pillow-sized physical item.
            var initialSize = BoundsInItemSpace(mesh).size;
            float longest = Mathf.Max(initialSize.x, initialSize.y, initialSize.z);
            if (longest > 0.0001f && lengthMetres > 0f)
                visual.transform.localScale *= lengthMetres / longest;

            var collider = GetComponent<BoxCollider>();
            if (collider != null) FitCollider(mesh, collider);
            var loader = GetComponent<TextureLoader>();
            if (loader != null)
            {
                loader.BindRenderer(renderer);
                Ready = true;
            }
        }

        private void FitCollider(MeshFilter filter, BoxCollider collider)
        {
            var bounds = BoundsInItemSpace(filter);
            collider.center = bounds.center;
            collider.size = bounds.size;
        }

        private Bounds BoundsInItemSpace(MeshFilter filter)
        {
            var bounds = filter.sharedMesh.bounds;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        var local = transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                        min = Vector3.Min(min, local);
                        max = Vector3.Max(max, local);
                    }
            return new Bounds((min + max) * .5f, max - min);
        }
    }
}
