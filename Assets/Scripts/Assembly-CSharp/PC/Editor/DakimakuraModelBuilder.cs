#if UNITY_EDITOR
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PC.Component.Software.EditorTools
{
    /// <summary>
    /// One-click setup for the dakimakura model:
    ///   1. force-reimports the selected FBX (or Assets/Models/dakimakura.fbx),
    ///   2. assigns it as the item's modelPrefab (no Inspector work),
    ///   3. fits the item BoxCollider to the model (same math as DakimakuraVisual),
    ///   4. prints a verification report.
    /// Run Tools → Dakimakura → Застроить дакимакуру after swapping the model.
    /// </summary>
    public static class DakimakuraModelBuilder
    {
        public const string DefaultModelAssetPath = "Assets/Models/dakimakura.fbx";
        public const string ItemPrefabPath = "Assets/Resources/components/Dakimakura.prefab";
        public const string ShopPrefabPath = "Assets/GameObject/DakimakuraExpert.prefab";

        

        [MenuItem("Tools/Dakimakura/Застроить дакимакуру")]
        public static void Build()
        {
            string modelPath = ResolveModelAssetPath();
            if (string.IsNullOrEmpty(modelPath))
            {
                EditorUtility.DisplayDialog("Dakimakura",
                    "Выбери FBX-модель в окне Project (или укажи Assets/Models/dakimakura.fbx) и запусти снова.", "OK");
                return;
            }

            // Перекажем FBX в Unity: если файл только закинули, модель ещё не импортирована.
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh();

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null)
            {
                Debug.LogError($"[Dakimakura] FBX не найден в проекте: {modelPath}");
                return;
            }

            var item = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefabPath);
            if (item == null)
            {
                Debug.LogError($"[Dakimakura] Предметный префаб не найден: {ItemPrefabPath}");
                return;
            }
            var visual = item.GetComponent<DakimakuraVisual>();
            if (visual == null)
            {
                Debug.LogError("[Dakimakura] В предметном префабе нет DakimakuraVisual.");
                return;
            }

            // 1) Готовим модель в памяти: иерархия FBX сохраняется, как в игре.
            var model = UnityEngine.Object.Instantiate(source);
            model.name = "Dakimakura Model";
            model.transform.position = item.transform.position;
            model.transform.rotation = item.transform.rotation;
            model.transform.localScale = item.transform.localScale;

            float lengthMetres = ReadLengthMetres(visual);
            DakimakuraVisual.FitToLengthMetres(model, lengthMetres);

            // 2) Коллайдер предмета считаем под эту модель — та же формула, что FitCollider.
            var bounds = DakimakuraVisual.ModelBoundsInSpace(model, item.transform);
            var itemCollider = item.GetComponent<BoxCollider>();
            if (itemCollider != null)
            {
                itemCollider.center = bounds.center;
                itemCollider.size = bounds.size;
            }

            // 3) Проставляем modelPrefab в префаб — без ручной ручежни в Inspector.
            SetPrivateField(visual, "modelPrefab", source);

            UnityEngine.Object.DestroyImmediate(model);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Dakimakura] Модель подключена: {modelPath}");
            Check();
        }

        [MenuItem("Tools/Dakimakura/Проверить дакимакуру")]
        public static void Check()
        {
            var sb = new StringBuilder();
            int fails = 0;

            void Report(bool ok, string line)
            {
                sb.AppendLine((ok ? "[ OK ] " : "[FAIL] ") + line);
                if (!ok) fails++;
            }

            string modelPath = ResolveModelAssetPath();
            Report(modelPath != null, modelPath != null ? $"FBX модель: {modelPath}" : "FBX модель не найдена (выбери FBX в Project)");

            GameObject source = null;
            if (modelPath != null)
                source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Report(source != null, "FBX импортирован (GameObject в Project)");

            bool hasMesh = source != null &&
                           source.GetComponentInChildren<MeshFilter>(true) != null &&
                           source.GetComponentInChildren<Renderer>(true) != null;
            Report(hasMesh, "Модель содержит видимый меш (MeshFilter + Renderer)");

            // Честная проверка модели: камеры/свет в иерархии и трансформы
            // 1x1x1 (запечённый масштаб — не 0.01/25 из грязного FBX).
            UnityEngine.Object probe = source != null ? UnityEngine.Object.Instantiate(source) : null;
            if (probe != null) probe.name = "Dakimakura Probe";
            Report(probe != null && probe.GetComponentInChildren<Camera>(true) == null,
                "Модель: нет Camera в иерархии");
            Report(probe != null && probe.GetComponentInChildren<Light>(true) == null,
                "Модель: нет Light в иерархии");

            bool badScale = false;
            string badNode = "—";
            if (probe != null)
            {
                foreach (var tr in probe.GetComponentsInChildren<Transform>(true))
                {
                    if ((tr.localScale - Vector3.one).magnitude > 1e-4f)
                    {
                        badScale = true;
                        badNode = tr.name + " " + tr.localScale.ToString("F3");
                        break;
                    }
                }
            }
            Report(probe != null && !badScale,
                "Модель: все трансформы 1x1x1" + (badScale ? $" (узел {badNode})" : ""));
            UnityEngine.Object.DestroyImmediate(probe);

            var importer = modelPath != null ? AssetImporter.GetAtPath(modelPath) : null;
            if (importer is ModelImporter mi)
            {
                Report(!mi.importCameras && !mi.importLights,
                    $"Импортер: камеры/свет из FBX не завезены (importCameras={mi.importCameras}, importLights={mi.importLights})");
            }
            else
            {
                Report(true, "Импортер: не ModelImporter (ожидается FBX)");
            }

            var item = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefabPath);
            Report(item != null, $"Предметный префаб: {ItemPrefabPath}");
            var visual = item?.GetComponent<DakimakuraVisual>();
            Report(visual != null, "Предметный префаб: есть DakimakuraVisual");

            if (visual != null)
            {
                var modelField = typeof(DakimakuraVisual).GetField("modelPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
                var modelRef = modelField?.GetValue(visual) as GameObject;
                Report(modelRef != null, "DakimakuraVisual: модель назначена (modelPrefab)");

                var lenField = typeof(DakimakuraVisual).GetField("lengthMetres", BindingFlags.NonPublic | BindingFlags.Instance);
                float len = lenField?.GetValue(visual) is float f ? f : 0f;
                Report(len > 0f, $"DakimakuraVisual: длина {len} м");
            }

            var itemCollider = item?.GetComponent<BoxCollider>();
            string colliderText = itemCollider != null ? itemCollider.size.ToString("F2") : "—";
            Report(itemCollider != null && itemCollider.size.magnitude > 0.001f,
                $"Предмет: коллайдер под модель ({colliderText})");

            var shop = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath);
            var printExpert = shop?.GetComponent<PrintExpert>();
            Report(printExpert != null, $"Витрина: {ShopPrefabPath}");
            if (printExpert != null)
            {
                var bf = typeof(PrintExpert).GetField("bannerPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
                var bfVal = bf?.GetValue(printExpert) as TextureLoader;
                Report(bfVal != null && item != null && bfVal.gameObject == item, "Витрина: bannerPrefab → предметный префаб");
            }

            Debug.Log("[Dakimakura] Проверка:\n" + sb);
            if (fails > 0)
            {
                EditorUtility.DisplayDialog("Dakimakura: проверка",
                    $"Найдено проблем: {fails}. Запустите «Застроить дакимакуру».", "OK");
            }
        }

        [MenuItem("Tools/Dakimakura/Открыть предметный префаб")]
        public static void OpenItemPrefab()
        {
            var item = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefabPath);
            if (item == null)
            {
                Debug.LogError($"[Dakimakura] Предметный префаб не найден: {ItemPrefabPath}");
                return;
            }
            Selection.activeObject = item;
            EditorGUIUtility.PingObject(item);
        }

        [MenuItem("Tools/Dakimakura/Открыть FBX модель")]
        public static void OpenModel()
        {
            string modelPath = ResolveModelAssetPath();
            if (string.IsNullOrEmpty(modelPath))
            {
                Debug.LogError("[Dakimakura] FBX модель не найдена.");
                return;
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source != null)
            {
                Selection.activeObject = source;
                EditorGUIUtility.PingObject(source);
            }
        }

        private static string ResolveModelAssetPath()
        {
            var selected = Selection.activeObject as GameObject;
            if (selected != null)
            {
                var path = AssetDatabase.GetAssetPath(selected);
                if (!string.IsNullOrEmpty(path) &&
                    (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".obj", System.StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase)))
                {
                    return path;
                }
            }
            if (System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "Models", "dakimakura.fbx")))
                return DefaultModelAssetPath;
            return null;
        }

        private static float ReadLengthMetres(DakimakuraVisual visual)
        {
            var field = typeof(DakimakuraVisual).GetField("lengthMetres", BindingFlags.NonPublic | BindingFlags.Instance);
            float value = field != null && field.GetValue(visual) is float f ? f : DakimakuraVisual.DefaultLengthMetres;
            return value > 0f ? value : DakimakuraVisual.DefaultLengthMetres;
        }

        private static void SetPrivateField(UnityEngine.Object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError($"[Dakimakura] Поле {name} не найдено в {target.GetType().Name}.");
                return;
            }
            field.SetValue(target, value);
        }
    }
}
#endif
