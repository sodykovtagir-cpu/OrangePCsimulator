using UnityEngine;

namespace PC.Component.Software
{
    /// <summary>
    /// Чинит ТОЛЬКО рендер принта на дакимакуре (UV). Размер, масштаб и коллайдер
    /// не трогает — они берутся из префаба как есть.
    ///
    /// Модель со Sketchfab приезжает с запечённой atlas-развёрткой UV под свою
    /// родную текстуру: на большой грани UV повёрнуты на 90° и занимают лишь
    /// кусок атласа. Когда TextureLoader подменяет mainTexture на картинку
    /// игрока, она ложится через эту развёртку — боком и растянуто.
    ///
    /// Компонент заново проецирует UV0 плоскостью вдоль тонкой оси подушки:
    /// длинная сторона становится высотой картинки (v), короткая — шириной (u).
    /// Пропорции грани (~0.46) совпадают с пропорциями принта (32×70 / 1024×2240),
    /// поэтому изображение встаёт ровно и без искажений.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [DisallowMultipleComponent]
    public sealed class DakimakuraPrintUV : MonoBehaviour
    {
        [Tooltip("Отразить принт по горизонтали, если он оказался зеркальным.")]
        [SerializeField] private bool flipU = false;

        [Tooltip("Перевернуть принт вверх ногами.")]
        [SerializeField] private bool flipV = false;

        private Mesh meshInstance;
        private Mesh twoSidedMesh;
        private MeshFilter meshFilter;
        private bool twoSided;
        private int sideWidth = 1;

        // Single-image legacy pillows keep their original full-width UVs.
        // Two-image pillows use a horizontal atlas: one half per face.
        public void SetTwoSided(bool value, int width = 1)
        {
            twoSided = value;
            sideWidth = Mathf.Max(1, width);
            if (meshInstance == null) return;
            if (value && twoSidedMesh == null) twoSidedMesh = CreateTwoSidedMesh(meshInstance);
            var target = value ? twoSidedMesh : meshInstance;
            ReprojectUV(target);
            if (meshFilter != null) meshFilter.sharedMesh = target;
        }

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogError("DakimakuraPrintUV: нет MeshFilter с мешем.");
                return;
            }

            // .mesh создаёт копию для этого экземпляра — общий ассет не трогаем,
            // другие подушки и превью получают свои UV независимо.
            meshInstance = meshFilter.mesh;
            ReprojectUV(meshInstance);
        }

        private void OnDestroy()
        {
            // Копия меша, созданная через .mesh, иначе утекает при уничтожении предмета.
            if (meshInstance != null) Destroy(meshInstance);
            if (twoSidedMesh != null) Destroy(twoSidedMesh);
        }

        private static T[] Duplicate<T>(T[] values, int expected)
        {
            if (values == null || values.Length != expected) return null;
            var result = new T[expected * 2];
            System.Array.Copy(values, 0, result, 0, expected);
            System.Array.Copy(values, 0, result, expected, expected);
            return result;
        }

        // Split the seam by triangle, not by vertex. A side-wall triangle can
        // share vertices with both faces; interpolating across the atlas would
        // otherwise smear unrelated images over the edge of the pillow.
        private static Mesh CreateTwoSidedMesh(Mesh original)
        {
            var vertices = original.vertices;
            int count = vertices.Length;
            var size = original.bounds.size;
            int thin = 0;
            if (size.y < size[thin]) thin = 1;
            if (size.z < size[thin]) thin = 2;
            float middle = original.bounds.center[thin];
            var indices = new int[original.subMeshCount][];
            for (int sub = 0; sub < indices.Length; sub++)
            {
                indices[sub] = original.GetTriangles(sub);
                for (int i = 0; i + 2 < indices[sub].Length; i += 3)
                {
                    int a = indices[sub][i], b = indices[sub][i + 1], c = indices[sub][i + 2];
                    if ((vertices[a][thin] + vertices[b][thin] + vertices[c][thin]) / 3f < middle)
                    {
                        indices[sub][i] += count;
                        indices[sub][i + 1] += count;
                        indices[sub][i + 2] += count;
                    }
                }
            }
            var result = Instantiate(original);
            result.name = original.name + " (two-sided)";
            if (count * 2 > 65535) result.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var normals = Duplicate(original.normals, count);
            var tangents = Duplicate(original.tangents, count);
            var colors = Duplicate(original.colors, count);
            var uv2 = Duplicate(original.uv2, count);
            var uv3 = Duplicate(original.uv3, count);
            var uv4 = Duplicate(original.uv4, count);
            result.vertices = Duplicate(vertices, count);
            if (normals != null) result.normals = normals;
            if (tangents != null) result.tangents = tangents;
            if (colors != null) result.colors = colors;
            if (uv2 != null) result.uv2 = uv2;
            if (uv3 != null) result.uv3 = uv3;
            if (uv4 != null) result.uv4 = uv4;
            for (int sub = 0; sub < indices.Length; sub++) result.SetTriangles(indices[sub], sub);
            result.RecalculateBounds();
            return result;
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
                if (twoSided)
                {
                    // Each triangle uses one of the two duplicated vertex
                    // sets, so interpolation cannot cross into the other side.
                    bool back = i >= v.Length / 2;
                    // The opposite face is viewed from the opposite direction.
                    if (back) u = 1f - u;
                    // Sample pixel centres so an edge never bleeds into the
                    // other image (also works with repeat-wrapped old textures).
                    u = (u * (sideWidth - 1) + 0.5f) / (2f * sideWidth) + (back ? 0.5f : 0f);
                }
                uv[i] = new Vector2(u, vv);
            }
            mesh.uv = uv;
        }
    }
}
