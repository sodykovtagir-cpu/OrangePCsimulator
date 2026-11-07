using UnityEngine;

namespace PC.Component.Software
{
    // A separate physical Item. Instantiating the imported FBX (rather than
    // taking its bare mesh) preserves the author's hierarchy and transforms.
    //
    // Zero-config: run Tools → Dakimakura → Застроить дакимакуру once — the
    // builder assigns modelPrefab and fits the item collider for the prefab,
    // so no Inspector work is needed. If modelPrefab is still unassigned at
    // runtime, the model is loaded automatically from Resources.
    public sealed class DakimakuraVisual : MonoBehaviour
    {
        // Путь в Resources, откуда можно подгрузить модель, если modelPrefab
        // не назначен (префаб Assets/Resources/components/DakimakuraModel.prefab).
        public const string FallbackModelResourcesPath = "components/DakimakuraModel";
        public const float DefaultLengthMetres = 1.5f;

        [SerializeField]
        [Tooltip("FBX-модель дакимакуры. Если пусто — подгрузится автоматически (Tools → Dakimakura → Застроить дакимакуру).")]
        private GameObject modelPrefab;

        [SerializeField]
        [Tooltip("Длина модели в метрах (длинная сторона). 1.5 — стандартный размер дакимакуры.")]
        private float lengthMetres = 1.5f;

        public bool Ready { get; private set; }

        private void Awake()
        {
            var visual = ResolveModelInstance();
            if (visual == null)
            {
                Debug.LogError("Dakimakura: модель не назначена. Запустите Tools → Dakimakura → Застроить дакимакуру (или вручную назначьте modelPrefab).");
                return;
            }
            visual.name = "Dakimakura Model";

            var mesh = visual.GetComponentInChildren<MeshFilter>(true);
            var renderer = visual.GetComponentInChildren<Renderer>(true);
            if (mesh == null || mesh.sharedMesh == null || renderer == null)
            {
                Debug.LogError("Dakimakura FBX has no renderable mesh");
                return;
            }

            // Normalize model import units to a pillow-sized physical item.
            var initialSize = ModelBoundsInSpace(visual, transform).size;
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

        private GameObject ResolveModelInstance()
        {
            if (modelPrefab != null)
                return Instantiate(modelPrefab, transform, false);

            // Автоподгрузка из Resources — пустой слот в Inspector не ломает игру.
            var fallback = Resources.Load<GameObject>(FallbackModelResourcesPath);
            return fallback != null ? Instantiate(fallback, transform, false) : null;
        }

        /// <summary>
        /// Объём модели в координатах space (обычно — префаба предмета).
        /// Делится с редакторским builder: префаб получается такого же
        /// размера и коллайдера, какой увидит игра.
        /// </summary>
        public static Bounds ModelBoundsInSpace(GameObject modelRoot, Transform space)
        {
            var mesh = modelRoot.GetComponentInChildren<MeshFilter>(true);
            if (mesh == null || mesh.sharedMesh == null)
                return new Bounds(modelRoot.transform.position, Vector3.zero);
            return BoundsInSpace(mesh, space);
        }

        /// <summary>
        /// Масштаб modelRoot так, чтобы длинная сторона была lengthMetres
        /// и коллайдер modelRoot подогнал под результат.
        /// </summary>
        public static void FitToLengthMetres(GameObject modelRoot, float lengthMetres)
        {
            var bounds = ModelBoundsInSpace(modelRoot, modelRoot.transform);
            float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (longest > 0.0001f && lengthMetres > 0f)
                modelRoot.transform.localScale *= lengthMetres / longest;

            var collider = modelRoot.GetComponent<BoxCollider>();
            if (collider != null)
            {
                var fitted = ModelBoundsInSpace(modelRoot, modelRoot.transform);
                collider.center = fitted.center;
                collider.size = fitted.size;
            }
        }

        private void FitCollider(MeshFilter filter, BoxCollider collider)
        {
            var bounds = ModelBoundsInSpace(filter.gameObject, transform);
            collider.center = bounds.center;
            collider.size = bounds.size;
        }

        private static Bounds BoundsInSpace(MeshFilter filter, Transform space)
        {
            var bounds = filter.sharedMesh.bounds;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        var local = space.InverseTransformPoint(filter.transform.TransformPoint(corner));
                        min = Vector3.Min(min, local);
                        max = Vector3.Max(max, local);
                    }
            return new Bounds((min + max) * .5f, max - min);
        }
    }
}
