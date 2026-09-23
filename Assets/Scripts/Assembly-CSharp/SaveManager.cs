using System;
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

	private void Awake()
	{
		Instance = this;
	}

	private void Start()
	{
		int ld = LoadData();
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
            if (item.transform.position.y < -20f) continue;
            var data = new JObject();
            foreach (var save in item.GetComponents<ISave>()) save.ToData(data);
            items.Add(new
            {
                spawnId = item.spawnId,
                id = item.Id,
                pos = new { x = item.transform.position.x, y = item.transform.position.y, z = item.transform.position.z },
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

        if (cdat.itemData != null && cdat.itemData.Length > 0)
        {
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

            foreach (var it in cdat.itemData)
            {
                var exists = FindItemPrefab(it.spawnId);

                if (exists)
                {
                    var prefab = Instantiate(exists);
                    prefab.transform.position = it.pos;
                    prefab.transform.rotation = it.rot;

                    var saves = prefab.GetComponents<ISave>();
                    var item = prefab.GetComponent<Item>();
                    if (item != null)
                    {
                        item.Id = it.id;
                        Main.Instance.AddItem(it.id, item);
                    }
                    JObject savePayload = null;
                    if (it.data != null)
                        savePayload = JObject.FromObject(it.data);

                    scObj.Add(Tuple.Create(saves, savePayload));
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
                catch
                {
                    failCount++;
                }
            }
        }
        return failCount;
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
            found = Resources.Load<GameObject>($"Components/{itemFolders[i]}{spawnId}");

        if (found == null)
        {
            // Полный перебор как последняя попытка: спасает предметы из
            // подпапок, о которых этот список ещё не знает.
            var all = Resources.LoadAll<GameObject>("Components");
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == spawnId)
                {
                    found = all[i];
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