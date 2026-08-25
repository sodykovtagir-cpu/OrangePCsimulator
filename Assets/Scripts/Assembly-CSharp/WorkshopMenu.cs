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

	[Header("Search / Sort")]
	[Tooltip("Поиск по названию/автору/описанию.")]
	[SerializeField] private InputField searchField;
	[Tooltip("Циклическая кнопка сортировки: Сначала новые → По скачиваниям → По лайкам.")]
	[SerializeField] private Button sortButton;
	[Tooltip("Переключатель порядка: По убыванию / По возрастанию.")]
	[SerializeField] private Button orderButton;
	[Tooltip("Кнопка возврата к сортировке «сначала новые».")]
	[SerializeField] private Button resetButton;
	[SerializeField] private Text sortLabel;
	[SerializeField] private Text orderLabel;

	private enum SortMode { New = 0, Downloads = 1, Likes = 2 }
	private SortMode sortMode = SortMode.New;
	private bool ascending = false;
	private string searchText = "";

	private readonly List<WorkshopItem> items = new List<WorkshopItem>();
	private readonly List<WorkshopItem> visible = new List<WorkshopItem>();
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
		WireSearchSort();
	}

	private void WireSearchSort()
	{
		if (searchField != null) searchField.onValueChanged.AddListener(OnSearchChanged);
		if (sortButton != null) sortButton.onClick.AddListener(CycleSort);
		if (orderButton != null) orderButton.onClick.AddListener(ToggleOrder);
		if (resetButton != null) resetButton.onClick.AddListener(ResetSort);
		RefreshSortLabels();
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
			ApplyFilterSort();
			SetStatus(visible.Count + " saves" + (visible.Count != items.Count ? " (of " + items.Count + ")" : ""));
		});
	}

	// ================= Search / Sort =================

	public void OnSearchChanged(string value)
	{
		searchText = value ?? "";
		ApplyFilterSort();
	}

	public void CycleSort()
	{
		sortMode = (SortMode)(((int)sortMode + 1) % 3);
		ApplyFilterSort();
		RefreshSortLabels();
	}

	public void SetSort(int mode)
	{
		sortMode = (SortMode)Mathf.Clamp(mode, 0, 2);
		ApplyFilterSort();
		RefreshSortLabels();
	}

	public void ToggleOrder()
	{
		ascending = !ascending;
		ApplyFilterSort();
		RefreshSortLabels();
	}

	public void ResetSort()
	{
		sortMode = SortMode.New;
		ascending = false;
		ApplyFilterSort();
		RefreshSortLabels();
	}

	private void ApplyFilterSort()
	{
		visible.Clear();
		string q = (searchText ?? "").Trim().ToLowerInvariant();
		for (int i = 0; i < items.Count; i++)
		{
			var it = items[i];
			if (q.Length > 0)
			{
				bool match = Contains(it.title, q) || Contains(it.author, q) || Contains(it.description, q);
				if (!match) continue;
			}
			visible.Add(it);
		}
		visible.Sort(Compare);
		RebuildRows();
	}

	private bool Contains(string s, string q)
	{
		return !string.IsNullOrEmpty(s) && s.ToLowerInvariant().IndexOf(q, System.StringComparison.Ordinal) >= 0;
	}

	private int Compare(WorkshopItem a, WorkshopItem b)
	{
		int c = 0;
		switch (sortMode)
		{
			case SortMode.Downloads: c = a.downloads.CompareTo(b.downloads); break;
			case SortMode.Likes:     c = a.likes.CompareTo(b.likes); break;
			default:                 c = string.CompareOrdinal(b.created_at ?? "", a.created_at ?? ""); break;
		}
		return ascending ? c : -c;
	}

	private void RefreshSortLabels()
	{
		if (sortLabel != null) sortLabel.text = SortName();
		if (orderLabel != null) orderLabel.text = ascending ? "↑ asc" : "↓ desc";
	}

	private string SortName()
	{
		switch (sortMode)
		{
			case SortMode.Downloads: return "By downloads";
			case SortMode.Likes:     return "By likes";
			default:                 return "New first";
		}
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
		if (!ServerAccounts.LoggedIn)
		{
			SetStatus("Login to publish");
			return;
		}
		if (localPaths.Count == 0)
		{
			SetStatus("No local .opc");
			return;
		}
		int i = localSavesDropdown != null ? localSavesDropdown.value : 0;
		if (i < 0 || i >= localPaths.Count) i = 0;

		// Защита от копирования: скачанный из мастерской сейв перевыложить нельзя.
		try
		{
			var probe = new DataLoader(localPaths[i]);
			probe.LoadFromPath();
			if (probe.GameData != null && probe.GameData.workshopSourceId > 0)
			{
				SetStatus("Cannot republish a downloaded save");
				return;
			}
		}
		catch { }

		string title = titleField != null && !string.IsNullOrEmpty(titleField.text)
			? titleField.text : Path.GetFileNameWithoutExtension(localPaths[i]);
		string author = authorField != null && !string.IsNullOrEmpty(authorField.text)
			? authorField.text : ServerAccounts.Name;
		string desc = descField != null ? descField.text : "";
		SetStatus("Uploading...");
		WorkshopClient.Instance.Upload(localPaths[i], title, author, desc, null, (id, key, err) =>
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
			if (FileMenu.Instance != null) FileMenu.Instance.RebuildList();
			SetStatus("Saved: " + Path.GetFileName(path));
		});
	}

	private void RebuildRows()
	{
		if (listParent == null) return;
		for (int i = listParent.childCount - 1; i >= 0; i--)
			Destroy(listParent.GetChild(i).gameObject);
		foreach (var it in visible)
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
		SetChildText(t, "Name", it.title + " — " + it.author);
		SetChildText(t, "Downloads", it.downloads.ToString());
		SetChildText(t, "Likes", it.likes.ToString());
		SetChildText(t, "Description", it.description ?? "");

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
		var like = t.Find("Like");
		if (like != null)
		{
			var b = like.GetComponent<Button>();
			if (b != null)
			{
				int captured = it.id;
				b.onClick.AddListener(() =>
				{
					WorkshopClient.Instance.Like(captured, (n, err) =>
					{
						if (t == null) return;
						if (err != null) { SetStatus(err); return; }
						SetChildText(t, "Likes", n.ToString());
					});
				});
			}
		}
		var cover = t.Find("Cover");
		if (cover != null && it.has_cover)
		{
			var raw = cover.GetComponent<RawImage>();
			if (raw != null)
			{
				WorkshopClient.Instance.DownloadCover(it.id, (tex, err) =>
				{
					if (raw == null) return;
					if (tex != null) raw.texture = tex;
				});
			}
		}
	}

	private static void SetChildText(Transform t, string child, string value)
	{
		var c = t.Find(child);
		if (c == null) return;
		var tx = c.GetComponent<Text>();
		if (tx != null) tx.text = value;
	}

	private System.Collections.IEnumerator LoadCover(RawImage raw, int id)
	{
		using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(WorkshopClient.CoverUrl(id)))
		{
			yield return req.SendWebRequest();
			if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
				raw.texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
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
