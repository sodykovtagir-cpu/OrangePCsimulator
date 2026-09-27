using System.Linq;
using UnityEditor;
using UnityEngine;
using PC.Component;

namespace PC.Tools
{
    /// <summary>
    /// Собрать мини-ПК x32box из модели x32box.fbx (Assets/Models).
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
    ///   x32box.prefab       — корпус с распаянной начинкой (SoC и память
    ///                         внутри, наружу не выведены), слотом M.2,
    ///                         слотом крышки, разъёмом USB и кнопкой
    ///                         питания. Крышка ему не вложена: он выдаёт её
    ///                         отдельным предметом (BundledParts);
    ///   x32box_Cover.prefab — снимаемая крышка отсека M.2.
    ///
    /// Запуск: меню Tools → x32box → Собрать префабы.
    /// </remarks>
    public static class BuildX32Box
    {
        private const string Fbx        = "Assets/Models/x32box.fbx";
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

            // Тег Cover и свой формат (match 2), иначе отсек её не примет.
            // Формат 2 не делят с крышками ATX: чужая крышка в мини-ПК
            // не встанет, а своя всегда найдётся.
            go.tag = "Cover";

            var col = go.AddComponent<BoxCollider>();
            if (mesh != null) { col.center = mesh.bounds.center; col.size = mesh.bounds.size; }

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.2f;

            var item = go.AddComponent<Item>();
            item.SpawnId = "x32box_Cover";
            SetPrivate(item, "info", "{x32box Cover}");
            // Формат 2 -- только у крышек x32box: чужая крышка в отсек не влезет.
            SetPrivate(item, "match", (byte)2);

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
            SetPrivate(item, "info", "{Mini PC}");

            // Плата с распаянной начинкой: процессор, память и питание внутри,
            // наружу выведены только отсек M.2 и разъём USB.
            var mb = go.AddComponent<Motherboard>();
            SetPrivate(mb, "integrated", true);
            SetPrivate(mb, "integratedWattage", 12f);
            SetPrivate(mb, "brandName", "x32");

            // --- распаянная начинка ----------------------------------------
            // Слотов под неё нет, но система ищет железо через GetHardwares,
            // поэтому это настоящие детали: просто без меша, коллайдера и
            // физики, чтобы снаружи их не было видно и нельзя было достать.
            var soc = Inner(go, "BuiltInSoC");
            var cpu = soc.AddComponent<CPU>();
            SetPrivate(cpu, "spawnId", "x32 SoC");
            SetPrivate(cpu, "info", "x32 SoC");
            SetPrivate(cpu, "score", 1500);
            SetPrivate(cpu, "wattage", 10f);
            SetPrivate(cpu, "defaultFrequency", 2f);
            SetPrivate(cpu, "frequency", 2f);
            // Пассивное охлаждение: у SoC нет кулера в слоте, поэтому нагрев
            // выставлен так, чтобы равновесие с воздухом комнаты держалось
            // около двадцати градусов. С обычным heat процессор сгорел бы.
            SetPrivate(cpu, "heat", 2.5f);
            SetPrivate(cpu, "burnTemp", 150f);

            var ram = Inner(go, "BuiltInRAM");
            var memory = ram.AddComponent<Hardware>();
            SetPrivate(memory, "spawnId", "x32 RAM");
            SetPrivate(memory, "info", "x32 RAM");
            SetPrivate(memory, "capacity", 8000);
            SetPrivate(memory, "score", 4000);
            SetPrivate(memory, "wattage", 2f);

            LinkBuiltIn(mb,
                new Motherboard.BuiltIn { type = HardwareType.CPU, hardware = cpu },
                new Motherboard.BuiltIn { type = HardwareType.RAM, hardware = memory });

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
            SetMatches(coverSlot, 2);   // тот же формат, что у крышки
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

            // --- комплектная крышка -----------------------------------------
            // Крышка едет в коробке вместе с мини-ПК: игрок покупает бокс и
            // получает закрытый отсек M.2. Вложить её в префаб корпуса
            // нельзя — тогда она вырастает заново при каждом появлении
            // корпуса, и снятая крышка размножается (одна из префаба, вторая
            // из сохранения). Корпус выдаёт её как отдельный предмет.
            var bundled = go.AddComponent<BundledParts>();
            var coverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OutCover);
            if (coverPrefab != null) LinkBundled(bundled, coverPrefab, "Cover");
            else Debug.LogWarning("Крышка не найдена, комплект пуст: " + OutCover);

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

        /// <summary>Пустой дочерний объект: деталь внутри корпуса, снаружи не видна.</summary>
        private static GameObject Inner(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        /// <summary>
        /// Зарегистрировать в плате распаянную начинку.
        /// </summary>
        /// <remarks>
        /// Плата ищет железо двумя путями: по слотам (external) и по своему
        /// списку builtIn. Впаянные детали слотов не имеют, поэтому без этой
        /// записи процессор и память для системы не существовали бы: в
        /// «Информации» было бы пусто, а тест производительности считал бы
        /// ноль.
        /// </remarks>
        private static void LinkBuiltIn(Object board, params Motherboard.BuiltIn[] entries)
        {
            var so = new SerializedObject(board);
            var arr = so.FindProperty("builtIn");
            if (arr == null) { Debug.LogWarning("В плате нет поля builtIn"); return; }

            arr.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("type").intValue = (int)entries[i].type;
                e.FindPropertyRelative("hardware").objectReferenceValue = entries[i].hardware;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Прописать комплектную деталь корпусу.</summary>
        private static void LinkBundled(BundledParts bundled, GameObject prefab, string slotTarget)
        {
            var so = new SerializedObject(bundled);
            var arr = so.FindProperty("parts");
            if (arr == null) { Debug.LogWarning("У комплекта нет поля parts"); return; }

            arr.arraySize = 1;
            var e = arr.GetArrayElementAtIndex(0);
            e.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            e.FindPropertyRelative("slotTarget").stringValue = slotTarget;
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
