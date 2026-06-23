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
		string[] exts = new string[] {".pc"};
		bool CanReadFile(string path)
		{
			try{using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read))return true;}catch{return false;}
		}
		void pickFileCallback(string path)
		{
			// check perms
			if (!CanReadFile(path))
				messageBox.Show(Localization.GetText("No permission to open the file."));
			// try load
			try
			{
				DataLoader x = new DataLoader(path);
				x.LoadFromPath();
			}
			catch
			{
				messageBox.Show(Localization.GetText("Import failed! An error occured while loading the file, please make sure the file version is 1.7.0 and above."));
			}
			// move to saves
			File.Copy(path, SaveUtility.GetNewPath(Path.GetFileNameWithoutExtension(path)));
			// reimport
			loads = new List<Load>();
			for (int i = 0; i < slotParent.transform.childCount; i++)
				Destroy(slotParent.transform.GetChild(i).gameObject);
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
			foreach (var j in sort)
			{
				AddSlot(j.Key);
			}
			empty.SetActive(loads.Count == 0);
		}
		NativeFilePicker.PickFile(pickFileCallback, exts);
	}
}
