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
        bool readOnly = false;
        if (string.IsNullOrEmpty(Loader.Path)) readOnly = true;
        if (!string.IsNullOrEmpty(Loader.GameData.sign)) readOnly = true;

        Main.Instance.example = readOnly;
        if (Main.Instance.example) saveButton.SetActive(false);

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

        if (cdat.scene != null) {
            foreach (var sce in sceneObjects)
            {
                sce.FromData(cdat.scene);
            }
        }

        var scObj = new List<Tuple<ISave[], JObject>>();

        if (cdat.itemData != null && cdat.itemData.Length > 0)
        {
            foreach (var it in cdat.itemData)
            {
                var path   = $"Components/{it.spawnId}";
                var exists = Resources.Load(path);

                if (exists) {
                    var prefab = Instantiate((GameObject)exists);
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
                    Debug.LogWarning($"Prefab of {it.spawnId} not found!");
                    failCount++;
                }
            }
        }

        foreach (var (savers, data) in scObj)
        {
            if (savers == null) continue;
            foreach (var saver in savers)
            {
                try {
                if (saver == null) throw new NullReferenceException(nameof(ISave));
                if (data != null) saver.FromData(data);} catch
                {
                    failCount++;
                }
            }
        }
        return failCount;
    }

	private void OnDestroy()
	{
		Physics.gravity = new Vector3(0f, -9.81f, 0f);
		AirConditioner.temperature = AirConditioner.NormalTemperature;
	}
}