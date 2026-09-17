using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PC.Component.Tools
{
    /// <summary>
    /// Пересобрать платы MSG на меши из нового FBX.
    /// </summary>
    /// <remarks>
    /// ПОЧЕМУ ЭТО EDITOR-СКРИПТ, А НЕ ПРАВКА ФАЙЛА ПРЕФАБА ТЕКСТОМ.
    ///
    /// Меш внутри FBX адресуется парой (guid файла, fileID подобъекта). Guid
    /// лежит в .meta и известен, а вот fileID Unity назначает САМА при импорте
    /// модели, и вычислить его снаружи нельзя: в .meta нового FBX таблица
    /// internalIDToNameTable пуста, потому что редактор его ещё не открывал.
    /// Попытка подставить fileID вручную даёт ссылку в никуда — объект
    /// останется без меша и просто исчезнет со сцены.
    ///
    /// Поэтому спрашиваем у самого редактора: AssetDatabase загружает все
    /// подобъекты FBX вместе с их настоящими идентификаторами, и мы
    /// присваиваем меши по именам.
    ///
    /// Запуск: меню Tools → Плата MSG → Назначить меши из FBX.
    /// </remarks>
    public static class SetupMsgBoardMeshes
    {
        /// <summary>Платы, которые собираются из этой модели.</summary>
        /// <remarks>
        /// Чёрная и белая различаются только материалом, геометрия у них общая,
        /// поэтому меши назначаются обеим за один проход.
        /// </remarks>
        private static readonly string[] PrefabPaths =
        {
            "Assets/Resources/components/MSG_ATX_Black.prefab",
            "Assets/Resources/components/MSG_ATX_White.prefab",
        };

        private const string FbxPath =
            "Assets/Resources/components/MsgMotherBoard_ATX_black.fbx";

        /// <summary>Материал для каждой платы: чёрная и белая свои.</summary>
        private static readonly string[] MaterialPaths =
        {
            "Assets/Material/Msg_black.mat",
            "Assets/Material/Msg_white.mat",
        };

        /// <summary>
        /// Имена объектов префаба, которые в FBX названы иначе.
        /// </summary>
        /// <remarks>
        /// Корень платы в префабе зовётся MSG_ATX_Black/White, а в модели EXATX.
        /// Blender заменяет скобки на подчёркивания, отсюда M_2 (1) → M_2__1_.
        /// </remarks>
        private static readonly Dictionary<string, string> NameMap =
            new Dictionary<string, string>
            {
                { "MSG_ATX_Black", "EXATX" },
                { "MSG_ATX_White", "EXATX" },
                { "EXATX1", "EXATX" },
                { "M_2 (1)", "M_2__1_" },
                { "Mark", "Mark" },
            };

        [MenuItem("Tools/Плата MSG/Назначить меши из FBX")]
        public static void Apply()
        {
            for (int i = 0; i < PrefabPaths.Length; i++)
                ApplyTo(PrefabPaths[i],
                        i < MaterialPaths.Length ? MaterialPaths[i] : null);
        }

        private static void ApplyTo(string prefabPath, string materialPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Не найден префаб {prefabPath}");
                return;
            }

            // Все меши FBX с их настоящими fileID — их знает только редактор.
            var meshes = AssetDatabase
                .LoadAllAssetsAtPath(FbxPath)
                .OfType<Mesh>()
                .ToList();

            if (meshes.Count == 0)
            {
                Debug.LogError(
                    $"В {FbxPath} нет мешей. Модель не импортирована?");
                return;
            }

            var byName = new Dictionary<string, Mesh>();
            foreach (var mesh in meshes) byName[mesh.name] = mesh;

            Debug.Log($"Мешей в FBX: {meshes.Count} — " +
                      string.Join(", ", byName.Keys));

            var material = string.IsNullOrEmpty(materialPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
                Debug.LogWarning($"Материал {materialPath} не найден, " +
                                 "материалы останутся прежними.");

            var root = PrefabUtility.LoadPrefabContents(prefabPath);

            int assigned = 0, missing = 0, skipped = 0;
            var notFound = new List<string>();

            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var goName = filter.gameObject.name;

                string meshName;
                if (!NameMap.TryGetValue(goName, out meshName)) meshName = goName;

                Mesh mesh;
                if (!byName.TryGetValue(meshName, out mesh))
                {
                    // Имя не совпало — меш в FBX может называться иначе.
                    // Молча пропускаем: рвать связь с рабочим мешем нельзя,
                    // иначе деталь исчезнет со сцены.
                    missing++;
                    notFound.Add($"{goName} (искали «{meshName}»)");
                    continue;
                }

                if (filter.sharedMesh == mesh)
                {
                    skipped++;
                }
                else
                {
                    filter.sharedMesh = mesh;
                    assigned++;
                }

                if (material == null) continue;

                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                // На каждом рендерере может быть несколько слотов материалов.
                var mats = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == material) continue;
                    mats[i] = material;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = mats;
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.Refresh();

            Debug.Log($"{System.IO.Path.GetFileName(prefabPath)}: " +
                      $"назначено мешей: {assigned}, " +
                      $"уже стояли: {skipped}, не найдено в FBX: {missing}.");

            if (notFound.Count > 0)
                Debug.LogWarning("Не нашлись в FBX (оставлены как были):\n  " +
                                 string.Join("\n  ", notFound));
        }

        /// <summary>
        /// Показать, что лежит в FBX, ничего не меняя.
        /// </summary>
        /// <remarks>
        /// Полезно перед запуском: видно настоящие имена мешей, в том числе
        /// добавленных радиаторов, и сразу понятно, каких деталей не хватает.
        /// </remarks>
        [MenuItem("Tools/Плата MSG/Показать меши FBX")]
        public static void ListMeshes()
        {
            var meshes = AssetDatabase
                .LoadAllAssetsAtPath(FbxPath)
                .OfType<Mesh>()
                .OrderBy(m => m.name)
                .ToList();

            if (meshes.Count == 0)
            {
                Debug.LogError($"В {FbxPath} мешей не найдено.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[0]);
            var used = new HashSet<string>();
            if (prefab != null)
            {
                foreach (var f in prefab.GetComponentsInChildren<MeshFilter>(true))
                    if (f.sharedMesh != null) used.Add(f.sharedMesh.name);
            }

            var report = meshes
                .Select(m => (used.Contains(m.name) ? "  [есть] " : "  [НЕТ]  ")
                             + m.name + "  вершин: " + m.vertexCount);

            Debug.Log($"Мешей в FBX: {meshes.Count}\n" +
                      string.Join("\n", report));
        }
    }
}
