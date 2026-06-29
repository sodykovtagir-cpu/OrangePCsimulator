using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SaveManagement;
using UnityEngine;
using UnityEngine.UI;

public class FileMenu : MonoBehaviour
{
	[Serializable]
	public class Load
	{
		public GameObject graphic;

		public DataLoader loader;
	}

	[SerializeField]
	private MenuManager menuManager;

	[SerializeField]
	private GameObject empty;

	[SerializeField]
	private Transform slotParent;

	[SerializeField]
	private Transform slotPrefab;

	[SerializeField]
	private MessageBox messageBox;

	private List<Load> loads;

	[SerializeField]
	private FileInformation fileInformation;

	private void Start()
	{
		// init
		loads = new List<Load>();
		string fpath = SaveUtility.GetFolderPath();
		string pattern = "*" + SaveUtility.extension;
		// get date list
		Dictionary<string, DateTime> nList = new Dictionary<string, DateTime>();
		foreach (var b in Directory.GetFiles(fpath, pattern))
		{
			DateTime f = File.GetLastWriteTime(b);
			nList.Add(b, f);
		}
		// sort
		var sort = nList.OrderByDescending(kvp => kvp.Value);
		// append
		foreach (var x in sort)
		{
			AddSlot(x.Key);
		}
		empty.SetActive(loads.Count == 0);
	}

	private bool AddSlot(string path)
	{
		try
		{
			// read
			DataLoader l = new DataLoader(path);
			l.LoadFromPath();
			// load
			var parent = slotParent;
			var pref = slotPrefab;
			var x = Instantiate(pref, parent, false);
			x.Find("Name").GetComponent<Text>().text = l.GameData.roomName;
			x.Find("Hardcore").gameObject.SetActive(l.GameData.hardcore == true);
			// add to load list
			Load v = new Load();
			v.graphic = x.gameObject;
			v.loader = l;
			loads.Add(v);
			// buttons
			x.Find("Edit").GetComponent<Button>().onClick.AddListener(() => { ShowFileInformation(v); });
			x.Find("Name").GetComponent<Button>().onClick.AddListener(() => { MainMenu.Instance.LoadFile(l); });
			empty.SetActive(loads.Count == 0);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private void ShowFileInformation(Load load)
	{
		menuManager.ShowMenu("FileInformation");
		fileInformation.Show(load);
	}

	public void RefreshLoadButton(Load load)
	{
		var x = load.graphic.transform;
		x.Find("Name").GetComponent<Text>().text = load.loader.GameData.roomName;
		x.Find("Hardcore").gameObject.SetActive(load.loader.GameData.hardcore == true);
	}

	public void DeleteLoadButton(Load load)
	{
		if (File.Exists(load.loader.Path))
			File.Delete(load.loader.Path);
		loads.Remove(load);
		if (load.graphic != null)
		{
			Destroy(load.graphic);
		}
		empty.SetActive(loads.Count == 0);
	}

public void Import()
{
        string[] exts = new string[] { "*/*" };

        bool CanReadFile(string path)
	{
		try
		{
			using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read))
				return true;
		}
		catch
		{
			return false;
		}
	}

	void pickFileCallback(string path)
	{
		if (string.IsNullOrEmpty(path))
			return;

		if (!CanReadFile(path))
		{
			messageBox.Show(Localization.GetText("No permission to open the file."));
			return;
		}

		try
		{
			string ext = System.IO.Path.GetExtension(path).ToLower();

			if (ext == ".pc")
			{
				// Загружаем старый PC файл
				DataLoader oldLoader = new DataLoader(path);
				oldLoader.LoadFromPath();

				// Создаем новый OPC файл
				string newPath = SaveUtility.GetNewPath(
					System.IO.Path.GetFileNameWithoutExtension(path)
				);

				DataLoader newLoader = new DataLoader(newPath, oldLoader.GameData);
				newLoader.Content = oldLoader.Content;
				newLoader.WriteToFile();
			}
			else if (ext == ".opc")
			{
				File.Copy(
					path,
					SaveUtility.GetNewPath(
						System.IO.Path.GetFileNameWithoutExtension(path)
					),
					true
				);
			}
			else
			{
				messageBox.Show("Unsupported file format.");
				return;
			}
		}
		catch
		{
			messageBox.Show(
				Localization.GetText(
					"Import failed! An error occured while loading the file, please make sure the file version is 1.7.0 and above."
				)
			);
			return;
		}

		// Обновляем список сохранений
		loads = new List<Load>();

		for (int i = 0; i < slotParent.childCount; i++)
			Destroy(slotParent.GetChild(i).gameObject);

		string fpath = SaveUtility.GetFolderPath();
		string pattern = "*" + SaveUtility.extension;

		Dictionary<string, DateTime> nList = new Dictionary<string, DateTime>();

		foreach (var b in Directory.GetFiles(fpath, pattern))
		{
			DateTime f = File.GetLastWriteTime(b);
			nList.Add(b, f);
		}

		var sort = nList.OrderByDescending(kvp => kvp.Value);

		foreach (var j in sort)
			AddSlot(j.Key);

		empty.SetActive(loads.Count == 0);
	}

	NativeFilePicker.PickFile(pickFileCallback, exts);
}
}