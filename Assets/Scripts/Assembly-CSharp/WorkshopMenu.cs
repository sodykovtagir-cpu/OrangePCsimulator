using System.Collections.Generic;
using System.IO;
using SaveManagement;
using UnityEngine;
using UnityEngine.UI;

public class WorkshopMenu : MonoBehaviour
{
	[Header("List")]
	[SerializeField] private Transform listParent;
	[Tooltip("Optional. Children: Name (Text), Play (Button), Save (Button)")]
	[SerializeField] private GameObject rowPrefab;
	[SerializeField] private Text statusText;

	[Header("Upload")]
	[SerializeField] private InputField titleField;
	[SerializeField] private InputField authorField;
	[SerializeField] private InputField descField;
	[SerializeField] private Dropdown localSavesDropdown;

	[Header("Buttons (optional — or wire in inspector)")]
	[SerializeField] private Button refreshButton;
	[SerializeField] private Button uploadButton;

	private readonly List<WorkshopItem> items = new List<WorkshopItem>();
	private readonly List<string> localPaths = new List<string>();

	private void Awake()
	{
		if (WorkshopClient.Instance == null)
		{
			var go = new GameObject("WorkshopClient");
			go.AddComponent<WorkshopClient>();
		}
		if (refreshButton != null) refreshButton.onClick.AddListener(RefreshList);
		if (uploadButton != null) uploadButton.onClick.AddListener(UploadSelected);
	}

	private void OnEnable()
	{
		RefreshLocalSaves();
		RefreshList();
	}

	public void RefreshList()
	{
		if (WorkshopClient.Instance == null)
		{
			SetStatus("No client");
			return;
		}
		SetStatus("Loading...");
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
		if (localPaths.Count == 0)
		{
			SetStatus("No local .opc");
			return;
		}
		int i = localSavesDropdown != null ? localSavesDropdown.value : 0;
		if (i < 0 || i >= localPaths.Count) i = 0;
		string title = titleField != null && !string.IsNullOrEmpty(titleField.text)
			? titleField.text : Path.GetFileNameWithoutExtension(localPaths[i]);
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
		SetStatus("Downloading...");
		WorkshopClient.Instance.Download(item, (path, err) =>
		{
			if (err != null) { SetStatus("Download: " + err); return; }
			try
			{
				var loader = new DataLoader(path);
				loader.LoadFromPath();
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
		var h = row.AddComponent<HorizontalLayoutGroup>();
		h.spacing = 8;
		h.childForceExpandHeight = true;
		h.childForceExpandWidth = false;
		var labelGo = new GameObject("Name", typeof(RectTransform));
		labelGo.transform.SetParent(row.transform, false);
		var tx = labelGo.AddComponent<Text>();
		tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		tx.fontSize = 18;
		tx.color = Color.white;
		tx.text = it.title + " — " + it.author;
		var le = labelGo.AddComponent<LayoutElement>();
		le.flexibleWidth = 1;
		MakeRowButton(row.transform, "Play", () => DownloadAndPlay(it));
		MakeRowButton(row.transform, "Save", () => DownloadOnly(it));
		return row;
	}

	private void WireRow(Transform t, WorkshopItem it)
	{
		var name = t.Find("Name");
		if (name != null)
		{
			var tx = name.GetComponent<Text>();
			if (tx != null) tx.text = it.title + " — " + it.author;
			var nb = name.GetComponent<Button>();
			if (nb != null) nb.onClick.AddListener(() => DownloadAndPlay(it));
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

	private static void MakeRowButton(Transform parent, string s, UnityEngine.Events.UnityAction click)
	{
		var go = new GameObject(s, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, false);
		go.GetComponent<Image>().color = new Color(1f, 0.53f, 0f);
		var le = go.AddComponent<LayoutElement>();
		le.preferredWidth = 80;
		le.preferredHeight = 32;
		var tx = go.AddComponent<Text>();
		tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		tx.alignment = TextAnchor.MiddleCenter;
		tx.color = Color.black;
		tx.text = s;
		go.GetComponent<Button>().onClick.AddListener(click);
	}

	private void SetStatus(string s)
	{
		if (statusText != null) statusText.text = s;
		Debug.Log("[Workshop] " + s);
	}
}
