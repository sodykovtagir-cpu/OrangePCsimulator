using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaveManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yiming.Switch;

public class SaveManager : MonoBehaviour
{
	[SerializeField]
	private GameObject saveButton;

	[SerializeField]
	private Button earnButton;

	[SerializeField]
	[Header("Scene")]
	private SceneObject[] sceneObjects;

	[SerializeField]
	private GameObject preset;

	[SerializeField]
	private GameObject acTrigger;

	[SerializeField]
	private Collider acSwitch;

	[SerializeField]
	private Switch lamp;

	public static DataLoader Loader { get; set; }

	public static SaveManager Instance { get; private set; }
    // Collision impulses during the one-off save reconstruction are not
    // gameplay impacts; a saved monitor must not acquire Damaged on load.
    public static bool RestoringSave { get; private set; }
    public static float IgnoreLoadImpactsUntil { get; private set; }

	private void Awake()
	{
		Instance = this;
        RestoringSave = false;
        IgnoreLoadImpactsUntil = 0f;
	}

	private void Start()
	{
        int ld;
        try { ld = LoadData(); }
        catch { RestoringSave = false; throw; }
		string er = "";
		if (ld != 0)
		{
			er = string.Format("{0} items failed to load", ld);
			Debug.LogError(er);
			string fadeth = string.Concat("<color=red>Could not read file successfully!\n", er, "</color>");
			Main.Instance.FadeText(fadeth);
		}
	}

    public bool SaveData()
    {
        // Never write a half-restored cable whose saved port owners are still
        // frozen/unattached (for example when Save is clicked during loading).
        if (RestoringSave) return false;
        var loader = Loader;
        var game = loader.GameData;
        game.version = Application.version;
        var main = Main.Instance;
        game.coin = main.Money;
        game.playtime = main.playTime;
        game.light = lamp.IsOn;
        if (!main.hardcore)
        {
            var ac = AirConditioner.instance;
            game.temperature = ac.TargetTemperature;
            game.ac = ac.Power;
        }

        game.icon = CaptureScreenshot();

        var content = new ContentData();
        content.playerData = Player.Instance.SavePlayer();
        content.scene = new JObject();
        foreach (var s in sceneObjects) s.ToData(content.scene);

        var items = new List<object>();
        foreach (var item in FindObjectsOfType<Item>())
        {
            // A cable's root remains at the delivery portal while its plugs
            // and physical jacket move. Use the actual cable for save position
            // and the below-world filter; do not serialize the portal anchor.
            var savePos = item is PC.Component.Cabling.CableItem cable
                ? cable.SavePosition : item.transform.position;
            if (savePos.y < -20f) continue;

            // Распаянная начинка и прочие детали, вложенные в префаб
            // предмета в редакторе, — часть этого предмета, а не отдельная
            // вещь в комнате. Записать их отдельно значит получить при
            // загрузке вторую копию: корпус воссоздаётся вместе с вложенной
            // деталью, и рядом появляется та же деталь из своей записи.
            if (IsEmbedded(item)) continue;

            var data = new JObject();
            foreach (var save in item.GetComponents<ISave>()) save.ToData(data);

            // Transform/FixedJoint/Connector не сериализуются в .opc. Запоминаем
            // слот явно, иначе после загрузки остаётся только плата, а детали
            // не прикрепляются: OnTriggerEnter для созданного внутри слота
            // предмета Unity не гарантирует.
            int hostId = 0;
            string slotPath = null;
            var connector = item.GetComponent<Connector>();
            if (connector != null && connector.slot != null)
            {
                var slot = connector.slot;
                var host = slot.GetComponentInParent<Item>();
                if (host != null && host != item && host.Id != 0)
                {
                    hostId = host.Id;
                    slotPath = GetSlotPath(host.transform, slot.transform);
                }
            }

            items.Add(new
            {
                spawnId = item.spawnId,
                id = item.Id,
                hostId = hostId,
                slotPath = slotPath,
                pos = new { x = savePos.x, y = savePos.y, z = savePos.z },
                rot = new { x = item.transform.rotation.x, y = item.transform.rotation.y, z = item.transform.rotation.z, w = item.transform.rotation.w },
                data = data
            });
        }

        loader.Content = JsonConvert.SerializeObject(new
        {
            playerData = content.playerData,
            scene = content.scene,
            itemData = items.ToArray()
        });
        loader.WriteToFile();
        return true;
    }

    public int LoadData()
    {
        // Read-only (как «пример») — ТОЛЬКО встроенные пресеты, которые грузятся
        // из Resources/стриминга без файла на диске (Path пуст). Скачанные из
        // мастерской сборки лежат локальным файлом (Path задан), поэтому в них
        // МОЖНО играть и сохраняться — даже если у чужой сборки заполнена
        // авторская подпись (sign). Раньше непустой sign ошибочно делал любой
        // воркшоп-сейв read-only, из-за чего сборку нельзя было пересохранить
        // и ломался механизм обновления.
        bool readOnly = string.IsNullOrEmpty(Loader.Path);

		Main.Instance.example = readOnly;
		if (Main.Instance.example && saveButton != null) saveButton.SetActive(false);

        Main.Instance.playTime = Loader.GameData.playtime;
        Main.Instance.SetMoney(Loader.GameData.coin, true);

        lamp.IsOn = Loader.GameData.light;
        if (!Loader.GameData.gravity) Physics.gravity = Vector3.zero;

        if (!Loader.GameData.hardcore)
        {
            AirConditioner.instance.TargetTemperature = Loader.GameData.temperature;
            AirConditioner.instance.Power = Loader.GameData.ac;
        }
        else
        {
            Main.Instance.hardcore = true;
            AirConditioner.temperature = Loader.GameData.temperature;
            earnButton.interactable = false;
            acTrigger.SetActive(false);
            acSwitch.enabled = false;
        }


        int failCount = 0;
        int duplicates = 0;

        if (string.IsNullOrEmpty(Loader.Content))
        {
            preset.SetActive(true);
            return 0;
        }

        var cdat = JsonConvert.DeserializeObject<ContentData>(Loader.Content);

        Player.Instance.LoadPlayer(cdat.playerData);

        if (cdat.scene != null)
        {
            foreach (var sce in sceneObjects)
            {
                sce.FromData(cdat.scene);
            }
        }

        var scObj = new List<Tuple<ISave[], JObject>>();
        var restored = new List<Tuple<ItemData, Item>>();
        var restoredById = new Dictionary<int, Item>();

        if (cdat.itemData != null && cdat.itemData.Length > 0)
        {
            RestoringSave = true;
            // Коробки в сохранении лежат как обычные предметы, и их содержимое
            // тоже записано отдельно. Пока восстанавливаем, запрещаем коробкам
            // выкладывать копию: иначе появится дубликат, который вклинится в
            // коллайдер оригинала и повиснет в воздухе.
            Box.Restoring = true;

            // ====== ФИКС: удаляем все стартовые предметы перед загрузкой ======
            var existingItems = FindObjectsOfType<Item>();
            foreach (var existing in existingItems)
            {
                if (existing != null && existing.gameObject != null)
                    Destroy(existing.gameObject);
            }
            // =================================================================

            // Сохранения, сделанные до фикса коробок, уже содержат дубликаты:
            // лишний предмет попал в файл и теперь воскресает при каждой
            // загрузке. Запрет спавна из коробки такие записи не лечит, их
            // нужно отсеять здесь -- иначе игрок навсегда остался бы с
            // призраком, который висит в воздухе внутри чужого коллайдера.
            var placed = new List<Tuple<string, Vector3>>();

            foreach (var it in cdat.itemData)
            {
                if (IsDuplicate(placed, it))
                {
                    duplicates++;
                    continue;
                }

                var exists = FindItemPrefab(it.spawnId);

                if (exists)
                {
                    placed.Add(Tuple.Create(it.spawnId, (Vector3)it.pos));

                    var prefab = Instantiate(exists);
                    prefab.transform.position = it.pos;
                    prefab.transform.rotation = it.rot;

                    var saves = prefab.GetComponents<ISave>();
                    var item = prefab.GetComponent<Item>();
                    if (item != null)
                    {
                        item.Id = it.id;
                        Main.Instance.AddItem(it.id, item);
                        restored.Add(Tuple.Create(it, item));
                        if (it.id != 0) restoredById[it.id] = item;
                    }
                    // JsonConvert already created the JObject in ItemData.
                    // Re-serializing every PC disk/cable pose a second time
                    // here is expensive on large saves and serves no purpose.
                    scObj.Add(Tuple.Create(saves, it.data));
                }
                else
                {
                    // Предмета нет в сборке: его удалили из игры или это
                    // сохранение от другой версии. Сообщаем и пропускаем --
                    // терять из-за одной пропажи всю комнату нельзя.
                    Debug.LogWarning($"Prefab of {it.spawnId} not found!");
                    failCount++;
                }
            }
        }

        // Все предметы созданы -- дальше коробки работают как обычно.
        Box.Restoring = false;

        if (duplicates > 0)
            Debug.Log($"Сохранение: отброшено дубликатов предметов: {duplicates}");

        foreach (var (savers, data) in scObj)
        {
            if (savers == null) continue;
            foreach (var saver in savers)
            {
                try
                {
                    if (saver == null) throw new NullReferenceException(nameof(ISave));
                    if (data != null) saver.FromData(data);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Save: failed to restore {saver}: {ex}");
                    failCount++;
                }
            }
        }

        // Сначала корпуса и платы, потом их дочерние детали (CPU/RAM/GPU).
        // Ни триггер, ни FixedJoint, ни Connector не переживают сериализацию.
        for (int pass = 0; pass < restored.Count; pass++)
        {
            bool changed = false;
            foreach (var pair in restored)
            {
                var saved = pair.Item1;
                var item = pair.Item2;
                if (item == null || item.GetComponent<Connector>() != null) continue;
                if (saved.hostId == 0 || string.IsNullOrEmpty(saved.slotPath)) continue;
                if (!restoredById.TryGetValue(saved.hostId, out var host) || host == null) continue;
                // Дочерний слот должен принадлежать уже прикреплённому хозяину.
                if (host == item || (HasSavedHost(restored, host) &&
                    host.GetComponent<Connector>() == null)) continue;

                var slot = FindSavedSlot(host, saved.slotPath);
                if (slot != null && slot.TryAttach(item)) changed = true;
            }
            if (!changed) break;
        }

        // Старые .opc не содержат hostId/slotPath. Только если деталь
        // практически совпадает с insertPos, восстанавливаем крепление.
        // Свободно лежащую рядом с корпусом вещь не притягиваем.
        foreach (var pair in restored)
        {
            if (pair.Item2 == null || pair.Item2.GetComponent<Connector>() != null) continue;
            if (pair.Item1.hostId != 0)
            {
                // Префаб мог измениться с момента записи: тогда старый путь
                // невалиден, но деталь ещё можно узнать по точной позиции.
                if (restoredById.TryGetValue(pair.Item1.hostId, out var host))
                    TryRestoreLegacySlot(pair.Item2, restored, host);
                if (pair.Item2.GetComponent<Connector>() == null)
                {
                    Debug.LogWarning($"Save: could not reconnect {pair.Item1.spawnId} " +
                        $"to host {pair.Item1.hostId}, slot {pair.Item1.slotPath}");
                    failCount++;
                }
            }
            else TryRestoreLegacySlot(pair.Item2, restored);
        }
        // Hold the entire slotted assembly still until saved cable joints and
        // collision exclusions are in place. Freezing only displays left the
        // GPU/PSU and their slot joints exposed to the first rope impulse.
        var frozenDisplays = new List<Tuple<Rigidbody, bool, bool>>();
        foreach (var pair in restored)
        {
            if (pair.Item2 == null || pair.Item2 is PC.Component.Cabling.CableItem) continue;
            var body = pair.Item2.GetComponent<Rigidbody>();
            if (body == null) continue;
            frozenDisplays.Add(Tuple.Create(body, body.isKinematic, body.detectCollisions));
            body.detectCollisions = false;
            body.isKinematic = true;
        }
        StartCoroutine(RestoreCableConnections(frozenDisplays));
        return failCount;
    }

    private IEnumerator RestoreCableConnections(List<Tuple<Rigidbody, bool, bool>> frozenDisplays)
    {
        // Port owners and slot-attached hardware need their Start/slot pass
        // before restoring joints, otherwise saved endpoints attach to air.
        yield return null;
        yield return null;
        try
        {
            foreach (var cable in FindObjectsOfType<PC.Component.Cabling.CableItem>())
            {
                try { cable.RestorePorts(); }
                catch (Exception ex) { Debug.LogWarning($"Save: failed to restore cable ports: {ex}"); }
                finally
                {
                    try { cable.FinishRestore(); }
                    catch (Exception ex) { Debug.LogWarning($"Save: failed to release cable: {ex}"); }
                }
            }
            // Let the cable solver settle against stationary hardware first.
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }
        finally
        {
            foreach (var frozen in frozenDisplays)
            {
                if (frozen.Item1 == null) continue;
                frozen.Item1.isKinematic = frozen.Item2;
                frozen.Item1.detectCollisions = frozen.Item3;
            }
            Physics.SyncTransforms();
            IgnoreLoadImpactsUntil = Time.time + .75f;
            RestoringSave = false;
            // Every endpoint now exists: evaluate power and video only once,
            // never against a partially restored set of saved cables.
            try { PC.Component.Cabling.CablePower.RefreshAll(); }
            catch (Exception ex) { Debug.LogWarning("Save: cable power refresh: " + ex); }
        }
    }

    private static bool HasSavedHost(List<Tuple<ItemData, Item>> restored, Item item)
    {
        foreach (var pair in restored)
            if (pair.Item2 == item) return pair.Item1.hostId != 0;
        return false;
    }

    // Путь по индексам дочерних Transform внутри префаба хозяина. В отличие
    // от target он различает несколько одинаковых RAM/GPU-слотов.
    private static string GetSlotPath(Transform host, Transform slot)
    {
        var indices = new List<string>();
        for (var t = slot; t != null && t != host; t = t.parent)
            indices.Add(t.GetSiblingIndex().ToString());
        if (slot != host && !slot.IsChildOf(host)) return null;
        indices.Reverse();
        return string.Join("/", indices.ToArray());
    }

    private static Slot FindSavedSlot(Item host, string path)
    {
        if (host == null || string.IsNullOrEmpty(path)) return null;
        var t = host.transform;
        foreach (var part in path.Split('/'))
        {
            if (!int.TryParse(part, out int index) || index < 0 || index >= t.childCount)
                return null;
            t = t.GetChild(index);
        }
        var slot = t.GetComponent<Slot>();
        // Не позволяем старому пути переназначить чужую деталь/дочерний слот.
        return slot != null && slot.GetComponentInParent<Item>() == host ? slot : null;
    }

    private static void TryRestoreLegacySlot(Item item, List<Tuple<ItemData, Item>> restored,
        Item expectedHost = null)
    {
        if (item == null) return;
        Slot best = null;
        float nearest = 0.02f * 0.02f;
        foreach (var pair in restored)
        {
            var host = pair.Item2;
            if (host == null || host == item ||
                (expectedHost != null && host != expectedHost)) continue;
            foreach (var slot in host.GetComponentsInChildren<Slot>(true))
            {
                if (slot == null || slot.IsUsing || slot.InsertPosition == null) continue;
                if (slot.GetComponentInParent<Item>() != host) continue;
                if (!item.CompareTag(slot.target) || !slot.IsMatch(item.Match)) continue;

                var pos = slot.InsertPosition.position + item.transform.TransformVector(item.SlotOffset);
                float d = (item.transform.position - pos).sqrMagnitude;
                if (d >= nearest) continue;
                nearest = d;
                best = slot;
            }
        }
        if (best != null) best.TryAttach(item);
    }

    /// <summary>
    /// Деталь, вложенная в префаб другого предмета.
    /// </summary>
    /// <remarks>
    /// Сама вложенность ничего не значит: любая деталь, которую игрок
    /// поставил в слот, тоже становится дочерней — слот переносит её под
    /// свой insertPos. Признак ручной установки — Connector, который слот
    /// добавляет детали при подключении. У распаянной начинки и у деталей,
    /// вложенных в префаб в редакторе, его нет, и хранить их отдельной
    /// записью нельзя: префаб и так приносит их с собой.
    /// </remarks>
    private static bool IsEmbedded(Item item)
    {
        if (item == null) return false;
        if (item.GetComponent<Connector>() != null) return false;

        var parent = item.transform.parent;
        while (parent != null)
        {
            if (parent.GetComponent<Item>() != null) return true;
            parent = parent.parent;
        }

        return false;
    }

    /// <summary>
    /// Отсеять всё, что не является игровым предметом.
    /// </summary>
    /// <remarks>
    /// КРИТИЧНО. Пути Resources НЕ учитывают регистр, а в папке Components
    /// рядом с префабами лежат исходные модели: PortableMonitor.prefab и
    /// portablemonitor.fbx, FlatMonitor.prefab и FlatMonitor.fbx,
    /// RX570.prefab и RX570.fbx. Поэтому Resources.Load("Components/
    /// PortableMonitor") мог вернуть FBX -- голую модель без скриптов,
    /// Rigidbody и коллайдеров.
    ///
    /// В игре это выглядело так: после перезахода монитор превращался в
    /// неподвижную модельку с именем в нижнем регистре, висящую в воздухе,
    /// -- её нельзя было ни взять, ни включить. Настоящий предмет всегда
    /// несёт компонент Item (или его наследника: Monitor, Hardware, GPU),
    /// у импортированной модели его нет. По нему и различаем.
    /// </remarks>
    private static GameObject AsItem(GameObject candidate)
    {
        if (candidate == null) return null;
        return candidate.GetComponent<Item>() != null ? candidate : null;
    }

    /// <summary>
    /// Насколько близко должны стоять два одинаковых предмета, чтобы считать
    /// их одним и тем же.
    /// </summary>
    /// <remarks>
    /// Дубликат из коробки появляется ровно там же, где оригинал, поэтому
    /// расстояние копеечное. Порог намеренно маленький: два одинаковых
    /// монитора, честно поставленных игроком рядом, должны остаться оба.
    /// </remarks>
    // 5 см склеивали соседние DIMM/GPU (расстояние между слотами бывает меньше).
    // Настоящие старые дубликаты из коробки стоят практически в одной точке.
    private const float DuplicateRadius = 0.001f;

    /// <summary>
    /// Уже восстанавливали такой же предмет в этой же точке?
    /// </summary>
    private static bool IsDuplicate(List<Tuple<string, Vector3>> placed, ItemData it)
    {
        if (placed == null || it == null || string.IsNullOrEmpty(it.spawnId))
            return false;
        // A cable root is an invisible spawn anchor: two separately purchased
        // cables can share it while their plugs have different saved positions.
        if (it.spawnId.StartsWith("Power Cable ", StringComparison.Ordinal) ||
            it.spawnId.StartsWith("VGA Cable ", StringComparison.Ordinal) ||
            it.spawnId.StartsWith("HDMI Cable ", StringComparison.Ordinal)) return false;

        Vector3 pos = it.pos;

        for (int i = 0; i < placed.Count; i++)
        {
            if (placed[i].Item1 != it.spawnId) continue;
            if ((placed[i].Item2 - pos).sqrMagnitude > DuplicateRadius * DuplicateRadius)
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Кеш найденных префабов: путь ищется один раз на весь запуск.
    /// </summary>
    private static readonly Dictionary<string, GameObject> prefabCache =
        new Dictionary<string, GameObject>();

    /// <summary>Подпапки Resources/Components, где тоже лежат предметы.</summary>
    /// <remarks>
    /// Изначально загрузчик искал предмет строго как Components/<id>, и любой
    /// префаб из подпапки не находился: сохранение молча теряло ящики готовых
    /// сборок (их двадцать, они лежат в Components/ready). В логе это выглядит
    /// как «Prefab of ... not found!» и «N items failed to load».
    /// </remarks>
    private static readonly string[] itemFolders = { "", "ready/" };

    /// <summary>
    /// Найти префаб предмета по его spawnId.
    /// </summary>
    /// <remarks>
    /// Сначала пробуем прямой путь -- это самый частый случай и он самый
    /// быстрый. Если не нашли, проверяем известные подпапки. В последнюю
    /// очередь ищем по всей папке Components: это дорого, поэтому результат
    /// кешируется, зато предмет находится, куда бы его ни положили.
    /// </remarks>
    private static GameObject FindItemPrefab(string spawnId)
    {
        if (string.IsNullOrEmpty(spawnId)) return null;

        if (prefabCache.TryGetValue(spawnId, out var cached)) return cached;

        GameObject found = null;

        for (int i = 0; i < itemFolders.Length && found == null; i++)
            found = AsItem(Resources.Load<GameObject>(
                $"Components/{itemFolders[i]}{spawnId}"));

        if (found == null)
        {
            // Полный перебор как последняя попытка: спасает предметы из
            // подпапок, о которых этот список ещё не знает, и случаи, когда
            // прямой путь увёл к голой модели.
            var all = Resources.LoadAll<GameObject>("Components");

            // Сначала точное совпадение имени, затем без учёта регистра:
            // имя предмета в сохранении пишется так же, как называется его
            // префаб, но у модели рядом имя может отличаться регистром.
            for (int pass = 0; pass < 2 && found == null; pass++)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    var candidate = all[i];
                    if (candidate == null) continue;

                    bool match = pass == 0
                        ? candidate.name == spawnId
                        : string.Equals(candidate.name, spawnId,
                            System.StringComparison.OrdinalIgnoreCase);

                    if (!match) continue;
                    if (AsItem(candidate) == null) continue;

                    found = candidate;
                    break;
                }
            }
        }

        prefabCache[spawnId] = found;
        return found;
    }

    private string CaptureScreenshot()
    {
        var cam = Camera.main;
        if (cam == null) return null;

        RenderTexture rt = null;
        Texture2D tex = null;
        var prev = cam.targetTexture;
        try
        {
            rt = new RenderTexture(128, 128, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex = new Texture2D(128, 128, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
            tex.Apply();
            return Convert.ToBase64String(tex.EncodeToPNG());
        }
        catch
        {
            return null;
        }
        finally
        {
            cam.targetTexture = prev;
            RenderTexture.active = null;
            if (rt != null) Destroy(rt);
            if (tex != null) Destroy(tex);
        }
    }

    private void OnDestroy()
	{
		Physics.gravity = new Vector3(0f, -9.81f, 0f);
		AirConditioner.temperature = AirConditioner.NormalTemperature;
	}
}