using System.Linq;
using UnityEditor;
using UnityEngine;
using PC.Component;

namespace PC.Tools
{
    /// <summary>
    /// Собрать мини-ПК x32box из модели x32box.fbx.
    /// </summary>
    /// <remarks>
    /// ПОЧЕМУ EDITOR-СКРИПТ, А НЕ ФАЙЛ ПРЕФАБА ТЕКСТОМ.
    ///
    /// Меш внутри FBX адресуется парой (guid файла, fileID подобъекта).
    /// Guid лежит в .meta, а fileID Unity назначает сама при импорте —
    /// снаружи его не вычислить. Выдуманный номер даёт ссылку в никуда, и
    /// деталь просто исчезает со сцены (ровно так пропадал портативный
    /// монитор). Поэтому меши берём у самого редактора.
    ///
    /// Что получается:
    ///   x32box.prefab       — корпус с распаянной начинкой, слотом M.2,
    ///                         слотом крышки и кнопкой питания;
    ///   x32box_Cover.prefab — снимаемая крышка отсека M.2.
    ///
    /// Запуск: меню Tools → x32box → Собрать префабы.
    /// </remarks>
    public static class BuildX32Box
    {
        private const string Fbx        = "Assets/Resources/components/x32box.fbx";
        private const string OutBox     = "Assets/Resources/components/x32box.prefab";
        private const string OutCover   = "Assets/Resources/components/x32box_Cover.prefab";
        private const string BiosPrefab = "Assets/GameObject/BIOS.prefab";

        [MenuItem("Tools/x32box/Собрать префабы")]
        public static void Build()
        {
            var meshes = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Mesh>().ToList();
            if (meshes.Count == 0)
            {
                Debug.LogError($"{Fbx}: мешей не найдено. Модель импортирована?");
                return;
            }

            Debug.Log("Меши в модели: " + string.Join(", ", meshes.Select(m => m.name)));

            var body  = Find(meshes, "minipc");
            var cover = Find(meshes, "m2_cover");
            var slot  = Find(meshes, "m2slot");
            var btn   = Find(meshes, "PowerButton");

            if (body == null) { Debug.LogError("Не нашёлся меш minipc"); return; }

            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Motherboard.mat")
                   ?? AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Material>().FirstOrDefault();

            BuildCover(cover, mat);
            BuildBody(body, slot, btn, mat);

            // Карточки магазина уже созданы, но поле spawn в них пустое:
            // корневой fileID префаба заранее неизвестен. Проставляем здесь.
            LinkShopItem("Assets/MonoBehaviour/x32box.asset", OutBox);
            LinkShopItem("Assets/MonoBehaviour/x32box_Cover.asset", OutCover);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("x32box собран. Префабы в Assets/Resources/components/");
        }

        private static Mesh Find(System.Collections.Generic.List<Mesh> list, string name)
        {
            var m = list.FirstOrDefault(x => x.name == name)
                 ?? list.FirstOrDefault(x => string.Equals(x.name, name, System.StringComparison.OrdinalIgnoreCase));
            if (m == null) Debug.LogWarning("Меш не найден: " + name);
            return m;
        }

        /// <summary>Добавить меш и коллайдер, общая часть для всех деталей.</summary>
        private static void Shape(GameObject go, Mesh mesh, Material mat)
        {
            if (mesh == null) return;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            if (mat != null) r.sharedMaterial = mat;
        }

        // ---------------------------------------------------------------- крышка
        private static void BuildCover(Mesh mesh, Material mat)
        {
            var go = new GameObject("x32box_Cover");
            Shape(go, mesh, mat);

            // Тег Cover и match 1 — как у обычных крышек корпуса, иначе слот
            // её не примет.
            go.tag = "Cover";

            var col = go.AddComponent<BoxCollider>();
            if (mesh != null) { col.center = mesh.bounds.center; col.size = mesh.bounds.size; }

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.2f;

            var item = go.AddComponent<Item>();
            item.SpawnId = "x32box_Cover";
            SetPrivate(item, "info", "{x32box Cover}");
            SetPrivate(item, "match", (byte)1);

            SaveAs(go, OutCover);
        }

        // ---------------------------------------------------------------- корпус
        private static void BuildBody(Mesh body, Mesh slotMesh, Mesh btnMesh, Material mat)
        {
            var go = new GameObject("x32box");
            Shape(go, body, mat);

            var col = go.AddComponent<BoxCollider>();
            if (body != null) { col.center = body.bounds.center; col.size = body.bounds.size; }

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1.2f;

            go.AddComponent<AudioSource>().playOnAwake = false;

            var item = go.AddComponent<Item>();
            item.SpawnId = "x32box";
            SetPrivate(item, "info", "{x32box}");

            // Плата с распаянной начинкой: процессор, память и питание внутри,
            // наружу выведен только отсек M.2.
            var mb = go.AddComponent<Motherboard>();
            SetPrivate(mb, "integrated", true);
            SetPrivate(mb, "integratedWattage", 25f);
            SetPrivate(mb, "brandName", "x32");

            var bios = AssetDatabase.LoadAssetAtPath<GameObject>(BiosPrefab);
            if (bios != null)
            {
                var biosComp = bios.GetComponent<PC.Component.Software.OS.Bios>();
                if (biosComp != null) SetPrivate(mb, "biosPrefab", biosComp);
            }

            go.AddComponent<Breakable>();

            // --- отсек M.2 -------------------------------------------------
            var slotGo = new GameObject("m2slot");
            slotGo.transform.SetParent(go.transform, false);
            Shape(slotGo, slotMesh, mat);
            if (slotMesh != null) slotGo.transform.localPosition = Vector3.zero;

            var insert = new GameObject("InsertPos");
            insert.transform.SetParent(slotGo.transform, false);
            if (slotMesh != null) insert.transform.localPosition = slotMesh.bounds.center;

            var trigger = new GameObject("Trigger");
            trigger.transform.SetParent(slotGo.transform, false);
            var tc = trigger.AddComponent<BoxCollider>();
            tc.isTrigger = true;
            if (slotMesh != null) { tc.center = slotMesh.bounds.center; tc.size = slotMesh.bounds.size * 1.4f; }

            var drive = trigger.AddComponent<HardwareSlot>();
            drive.target = "Drive";                     // поле публичное
            SetMatches(drive, 1);                       // 1 — формат M.2
            SetPrivate(drive, "insertPos", insert.transform);
            SetPrivate(drive, "setParent", true);

            // --- слот крышки -----------------------------------------------
            var coverSlotGo = new GameObject("CoverSlot");
            coverSlotGo.transform.SetParent(go.transform, false);
            var cc = coverSlotGo.AddComponent<BoxCollider>();
            cc.isTrigger = true;
            if (body != null)
            {
                cc.center = new Vector3(body.bounds.center.x, body.bounds.min.y, body.bounds.center.z);
                cc.size = new Vector3(body.bounds.size.x * .9f, body.bounds.size.y * .3f, body.bounds.size.z * .9f);
            }

            var coverInsert = new GameObject("InsertPos");
            coverInsert.transform.SetParent(coverSlotGo.transform, false);

            var coverSlot = coverSlotGo.AddComponent<Slot>();
            coverSlot.target = "Cover";
            SetMatches(coverSlot, 1);
            SetPrivate(coverSlot, "insertPos", coverInsert.transform);
            SetPrivate(coverSlot, "setParent", true);

            // --- разъём USB -------------------------------------------------
            // Без него систему не установить: PCOS ставится с загрузочной
            // флешки, а вставлять её будет некуда.
            var usbGo = new GameObject("UsbSlot");
            usbGo.transform.SetParent(go.transform, false);
            var uc = usbGo.AddComponent<BoxCollider>();
            uc.isTrigger = true;
            if (body != null)
            {
                uc.center = new Vector3(body.bounds.min.x, body.bounds.center.y, body.bounds.center.z);
                uc.size = new Vector3(body.bounds.size.x * .25f, body.bounds.size.y * .5f, body.bounds.size.z * .5f);
            }

            var usbInsert = new GameObject("InsertPos");
            usbInsert.transform.SetParent(usbGo.transform, false);
            usbInsert.transform.localPosition = uc.center;

            var usb = usbGo.AddComponent<HardwareSlot>();
            usb.target = "USB";
            SetMatches(usb, 0);
            SetPrivate(usb, "insertPos", usbInsert.transform);
            SetPrivate(usb, "setParent", true);

            // --- регистрация слотов в плате ---------------------------------
            // КРИТИЧНО: плата ищет железо только в списках external. Слот,
            // не попавший туда, для неё не существует: BIOS не найдёт
            // накопитель, а система не увидит флешку.
            LinkSlots(mb, "external.drive", drive);
            LinkSlots(mb, "external.usb", usb);

            // --- кнопка питания --------------------------------------------
            var btnGo = new GameObject("PowerButton");
            btnGo.transform.SetParent(go.transform, false);
            Shape(btnGo, btnMesh, mat);

            var bc = btnGo.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            if (btnMesh != null) { bc.center = btnMesh.bounds.center; bc.size = btnMesh.bounds.size * 1.6f; }

            // Плата встроенная, поэтому кнопка дёргает её напрямую, без Case.
            var recv = btnGo.AddComponent<Receiver>();
            var ev = new UnityEngine.Events.UnityEvent();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(ev, mb.Switch);
            SetPrivate(recv, "OnClick", ev, isField: true);

            SaveAs(go, OutBox);
        }

        // ------------------------------------------------------------ вспомогательное
        /// <summary>
        /// Записать значение в поле компонента, в том числе приватное.
        /// </summary>
        /// <remarks>
        /// Почти все настройки деталей объявлены как private [SerializeField]:
        /// из обычного кода их не видно. SerializedObject правит их так же,
        /// как это делает инспектор, — значение попадает в сам ассет.
        /// </remarks>
        /// <summary>
        /// Записать список допустимых форматов детали.
        /// </summary>
        /// <remarks>
        /// У слота это массив matches, а не одно число: один слот может
        /// принимать несколько типоразмеров. Для отсека M.2 достаточно
        /// единственного значения.
        /// </remarks>
        private static void SetMatches(Object target, params byte[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty("matches");
            if (prop == null) { Debug.LogWarning("Нет поля matches"); return; }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).intValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivate(Object target, string field, object value, bool isField = false)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"{target.GetType().Name}: нет поля {field}"); return; }

            switch (value)
            {
                case bool b:        prop.boolValue = b; break;
                case float f:       prop.floatValue = f; break;
                case byte by:       prop.intValue = by; break;
                case int i:         prop.intValue = i; break;
                case string s:      prop.stringValue = s; break;
                case Object o:      prop.objectReferenceValue = o; break;
                default:
                    if (isField) { so.ApplyModifiedPropertiesWithoutUndo(); return; }
                    Debug.LogWarning($"Тип не поддержан для {field}");
                    return;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Положить слоты в список, по которому плата ищет железо.
        /// </summary>
        /// <remarks>
        /// Motherboard.Awake вызывает AddExternal(external), и дальше всё
        /// железо ищется только там. Слот, не попавший в список, плата не
        /// видит вовсе — компьютер ведёт себя так, будто отсек пустой.
        /// </remarks>
        private static void LinkSlots(Object board, string path, params HardwareSlot[] slots)
        {
            var so = new SerializedObject(board);
            var arr = so.FindProperty(path);
            if (arr == null) { Debug.LogWarning("Нет поля " + path); return; }
            arr.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Связать карточку магазина с готовым префабом.</summary>
        private static void LinkShopItem(string assetPath, string prefabPath)
        {
            var card = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (card == null || prefab == null)
            {
                Debug.LogWarning($"Не связал карточку: {assetPath} / {prefabPath}");
                return;
            }
            var so = new SerializedObject(card);
            var p = so.FindProperty("spawn");
            if (p == null) { Debug.LogWarning("В карточке нет поля spawn"); return; }
            p.objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(card);
            Debug.Log("Карточка связана: " + assetPath);
        }

        private static void SaveAs(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log("Сохранён " + path);
        }
    }
}
