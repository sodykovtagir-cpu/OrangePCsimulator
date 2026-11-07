using UnityEngine;

namespace PC.Component.Software
{
    /// <summary>
    /// Чинит рендер принта на дакимакуре.
    ///
    /// Модель со Sketchfab приезжает с запечённой atlas-развёрткой UV под свою
    /// родную текстуру: на большой грани UV повёрнуты на 90° и занимают лишь
    /// кусок атласа. Когда TextureLoader подменяет mainTexture на картинку
    /// игрока, она ложится через эту кривую развёртку — боком и растянуто
    /// (ровно то, что было на скрине).
    ///
    /// Компонент заново проецирует UV0 плоскостью вдоль тонкой оси подушки:
    /// длинная сторона становится высотой картинки (v), короткая — шириной (u).
    /// Пропорции грани (~0.46) совпадают с пропорциями принта (32×70 / 1024×2240),
    /// поэтому изображение встаёт ровно и без искажений. Заодно нормализует
    /// размер до реальной дакимакуры и подгоняет BoxCollider, так что результат
    /// не зависит от масштаба импорта FBX.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [DisallowMultipleComponent]
    public sealed class DakimakuraPrintUV : MonoBehaviour
    {
        [Tooltip("Длина подушки в метрах (длинная сторона). 1.5 — стандартная дакимакура.")]
        [SerializeField] private float lengthMetres = 1.5f;

        [Tooltip("Отразить принт по горизонтали, если он оказался зеркальным.")]
        [SerializeField] private bool flipU = false;

        [Tooltip("Перевернуть принт вверх ногами.")]
        [SerializeField] private bool flipV = false;

        private Mesh meshInstance;

        private void Awake()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogError("DakimakuraPrintUV: нет MeshFilter с мешем.");
                return;
            }

            // .mesh создаёт копию для этого экземпляра — общий ассет не трогаем,
            // другие подушки и превью получают свои UV независимо.
            meshInstance = filter.mesh;
            ReprojectUV(meshInstance);
            NormalizeSize(meshInstance);
            FitCollider(meshInstance);
        }

        private void OnDestroy()
        {
            // Копия меша, созданная через .mesh, иначе утекает при уничтожении предмета.
            if (meshInstance != null)
                Destroy(meshInstance);
        }

        private void ReprojectUV(Mesh mesh)
        {
            var v = mesh.vertices;
            if (v == null || v.Length == 0)
                return;

            var b = mesh.bounds;
            Vector3 size = b.size;
            Vector3 min = b.min;

            // Тонкая ось (толщина подушки) — вдоль неё проецируем.
            int thin = 0;
            if (size.y < size[thin]) thin = 1;
            if (size.z < size[thin]) thin = 2;

            // Из двух оставшихся осей длинная — высота картинки (v), короткая — ширина (u).
            int a = (thin + 1) % 3;
            int c = (thin + 2) % 3;
            int height = a, width = c;
            if (size[c] > size[a]) { height = c; width = a; }

            float wSize = Mathf.Max(size[width], 1e-5f);
            float hSize = Mathf.Max(size[height], 1e-5f);

            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                float u = (v[i][width] - min[width]) / wSize;
                float vv = (v[i][height] - min[height]) / hSize;
                if (flipU) u = 1f - u;
                if (flipV) vv = 1f - vv;
                uv[i] = new Vector2(u, vv);
            }
            mesh.uv = uv;
        }

        private void NormalizeSize(Mesh mesh)
        {
            if (lengthMetres <= 0f)
                return;
            Vector3 size = mesh.bounds.size;
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (longest > 1e-4f)
            {
                float s = lengthMetres / longest;
                transform.localScale = new Vector3(s, s, s);
            }
        }

        private void FitCollider(Mesh mesh)
        {
            var box = GetComponent<BoxCollider>();
            if (box == null)
                return;
            // Коллайдер в локальных координатах; transform.localScale масштабирует
            // его вместе с мешем, поэтому берём именно локальные bounds меша.
            box.center = mesh.bounds.center;
            box.size = mesh.bounds.size;
        }
    }
}
