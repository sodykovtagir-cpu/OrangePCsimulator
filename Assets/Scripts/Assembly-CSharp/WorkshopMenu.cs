using System.Collections.Generic;
using System.IO;
using SaveManagement;
using UnityEngine;
using UnityEngine.UI;

public class WorkshopMenu : MonoBehaviour
{
	[SerializeField] private Transform listParent;
	[SerializeField] private GameObject rowPrefab;
	[SerializeField] private Text statusText;
	[SerializeField] private InputField titleField;
	[SerializeField] private InputField authorField;
	[SerializeField] private InputField descField;
	[SerializeField] private Dropdown localSavesDropdown;

	private readonly List<WorkshopItem> items = new List<WorkshopItem>();
	private readonly List<string> localPaths = new List<string>();

	private void Start()
	{
		if (WorkshopClient.Instance == null)
		{
			var go = new GameObject("WorkshopClient");
			go.AddComponent<WorkshopClient>();
		}
		RefreshLocalSaves();
		RefreshList();
	}

	public void RefreshList()
	{
		SetStatus("Loading workshop...");
		WorkshopClient.Instance.ListSaves((list, err) =>
		{
			if (err != null)
			{
				SetStatus("Error: " + err);
				return;
			}
			items.Clear();
			if (list != null) items.AddRange(list);
			RebuildRows();
			SetStatus(items.Count + " saves");
		});
	}

	public void RefreshLocalSaves()
	{
		localPaths.Clear();
		if (localSavesDropdown == null) return;
		localSavesDropdown.ClearOptions();
		var folder = SaveUtility.GetFolderPath();
		if (!Directory.Exists(folder)) return;
		var files = Directory.GetFiles(folder, "*" + SaveUtility.extension);
		var opts = new List<string>();
		foreach (var f in files)
		{
			localPaths.Add(f);
			opts.Add(Path.GetFileNameWithoutExtension(f));
		}
		localSavesDropdown.AddOptions(opts);
	}

	public void UploadSelected()
	{
		if (localPaths.Count == 0 || localSavesDropdown == null)
		{
			SetStatus("No local save");
			return;
		}
		int i = localSavesDropdown.value;
		if (i < 0 || i >= localPaths.Count) return;
		string title = titleField != null ? titleField.text : Path.GetFileNameWithoutExtension(localPaths[i]);
		string author = authorField != null ? authorField.text : "Player";
		string desc = descField != null ? descField.text : "";
		SetStatus("Uploading...");
		WorkshopClient.Instance.Upload(localPaths[i], title, author, desc, (id, err) =>
		{
			if (err != null) { SetStatus("Upload: " + err); return; }
			SetStatus("Uploaded #" + id);
			RefreshList();
		});
	}

	public void DownloadAndPlay(WorkshopItem item)
	{
		SetStatus("Downloading " + item.title + "...");
		WorkshopClient.Instance.Download(item, (path, err) =>
		{
			if (err != null) { SetStatus("Download: " + err); return; }
			try
			{
				var loader = new DataLoader(path);
				loader.LoadFromPath();
				SetStatus("Saved locally, loading...");
				MainMenu.Instance.LoadFile(loader);
			}
			catch (System.Exception e)
			{
				SetStatus("Bad save: " + e.Message);
			}
		});
	}

	public void DownloadOnly(WorkshopItem item)
	{
		SetStatus("Downloading...");
		WorkshopClient.Instance.Download(item, (path, err) =>
		{
			if (err != null) { SetStatus("Download: " + err); return; }
			SetStatus("Saved: " + Path.GetFileName(path));
		});
	}

	private void RebuildRows()
	{
		if (listParent == null) return;
		for (int i = listParent.childCount - 1; i >= 0; i--)
			Destroy(listParent.GetChild(i).gameObject);

		foreach (var it in items)
		{
			var row = CreateRow(it);
			row.transform.SetParent(listParent, false);
		}
	}

	private GameObject CreateRow(WorkshopItem it)
	{
		if (rowPrefab != null)
		{
			var go = Instantiate(rowPrefab);
			WireRow(go.transform, it);
			return go;
		}

		var row = new GameObject("row_" + it.id, typeof(RectTransform));
		var le = row.AddComponent<HorizontalLayoutGroup>();
		le.childForceExpandHeight = true;
		le.childForceExpandWidth = false;
		le.spacing = 8;

		var label = NewText(row.transform, it.title + "  —  " + it.author + "  (" + it.downloads + ")");
		var fitter = label.gameObject.AddComponent<LayoutElement>();
		fitter.flexibleWidth = 1;

		var play = NewButton(row.transform, "Play", () => DownloadAndPlay(it));
		var dl = NewButton(row.transform, "Save", () => DownloadOnly(it));
		return row;
	}

	private void WireRow(Transform t, WorkshopItem it)
	{
		var name = t.Find("Name");
		if (name != null)
		{
			var tx = name.GetComponent<Text>();
			if (tx != null) tx.text = it.title + " — " + it.author;
		}
		var play = t.Find("Play");
		if (play != null)
		{
			var b = play.GetComponent<Button>();
			if (b != null) b.onClick.AddListener(() => DownloadAndPlay(it));
		}
		var save = t.Find("Save");
		if (save != null)
		{
			var b = save.GetComponent<Button>();
			if (b != null) b.onClick.AddListener(() => DownloadOnly(it));
		}
	}

	private static Text NewText(Transform parent, string s)
	{
		var go = new GameObject("label", typeof(RectTransform));
		go.transform.SetParent(parent, false);
		var tx = go.AddComponent<Text>();
		tx.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		tx.fontSize = 18;
		tx.color = Color.white;
		tx.text = s;
		return tx;
	}

	private static Button NewButton(Transform parent, string s, UnityEngine.Events.UnityAction onClick)
	{
		var go = new GameObject(s, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, false);
		var img = go.GetComponent<Image>();
		img.color = new Color(1f, 0.53f, 0f, 0.85f);
		var le = go.AddComponent<LayoutElement>();
		le.preferredWidth = 80;
		le.preferredHeight = 32;
		var tx = NewText(go.transform, s);
		tx.alignment = TextAnchor.MiddleCenter;
		var b = go.GetComponent<Button>();
		b.onClick.AddListener(onClick);
		return b;
	}

	private void SetStatus(string s)
	{
		if (statusText != null) statusText.text = s;
		Debug.Log("[Workshop] " + s);
	}
}
