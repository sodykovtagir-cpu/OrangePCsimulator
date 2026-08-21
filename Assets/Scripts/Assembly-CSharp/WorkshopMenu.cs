using System.Collections.Generic;
using System.IO;
using SaveManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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
	private GameObject panel;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void AutoHook()
	{
		SceneManager.sceneLoaded -= OnScene;
		SceneManager.sceneLoaded += OnScene;
		TrySpawn();
	}

	private static void OnScene(Scene s, LoadSceneMode m)
	{
		TrySpawn();
	}

	private static void TrySpawn()
	{
		if (FindObjectOfType<WorkshopMenu>() != null) return;
		if (FindObjectOfType<MainMenu>() == null) return;
		var go = new GameObject("WorkshopMenu");
		go.AddComponent<WorkshopMenu>();
	}

	private void Start()
	{
		if (WorkshopClient.Instance == null)
		{
			var go = new GameObject("WorkshopClient");
			go.AddComponent<WorkshopClient>();
		}
		EnsureUi();
		RefreshLocalSaves();
	}

	public void Show()
	{
		EnsureUi();
		if (panel != null) panel.SetActive(true);
		RefreshLocalSaves();
		RefreshList();
	}

	public void Hide()
	{
		if (panel != null) panel.SetActive(false);
	}

	private void EnsureUi()
	{
		if (listParent != null) return;
		BuildOverlay();
	}

	private void BuildOverlay()
	{
		var font = UiFont();
		var canvasGo = new GameObject("WorkshopCanvas");
		DontDestroyOnLoad(canvasGo);
		var canvas = canvasGo.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 80;
		canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
		canvasGo.AddComponent<GraphicRaycaster>();
		if (FindObjectOfType<EventSystem>() == null)
		{
			var es = new GameObject("EventSystem");
			es.AddComponent<EventSystem>();
			es.AddComponent<StandaloneInputModule>();
		}

		var open = MakeButton(canvasGo.transform, "Workshop", new Vector2(160, 48), new Vector2(1, 1), new Vector2(-90, -40), font);
		open.onClick.AddListener(Show);

		panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
		panel.transform.SetParent(canvasGo.transform, false);
		var pr = panel.GetComponent<RectTransform>();
		pr.anchorMin = new Vector2(0.08f, 0.08f);
		pr.anchorMax = new Vector2(0.92f, 0.92f);
		pr.offsetMin = Vector2.zero;
		pr.offsetMax = Vector2.zero;
		panel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.94f);
		panel.SetActive(false);

		MakeText(panel.transform, "Workshop", 32, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -16), font);

		var close = MakeButton(panel.transform, "X", new Vector2(48, 48), new Vector2(1, 1), new Vector2(-32, -32), font);
		close.onClick.AddListener(Hide);

		statusText = MakeText(panel.transform, "", 18, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(24, -56), font);
		var stRt = statusText.rectTransform;
		stRt.sizeDelta = new Vector2(800, 28);
		stRt.pivot = new Vector2(0, 1);

		authorField = MakeInput(panel.transform, "Author", new Vector2(24, -96), font);
		titleField = MakeInput(panel.transform, "Title", new Vector2(280, -96), font);
		descField = MakeInput(panel.transform, "Description", new Vector2(536, -96), font);

		var ddGo = new GameObject("LocalSaves", typeof(RectTransform));
		ddGo.transform.SetParent(panel.transform, false);
		var ddRt = ddGo.GetComponent<RectTransform>();
		ddRt.anchorMin = ddRt.anchorMax = new Vector2(0, 1);
		ddRt.pivot = new Vector2(0, 1);
		ddRt.anchoredPosition = new Vector2(24, -150);
		ddRt.sizeDelta = new Vector2(400, 36);
		localSavesDropdown = ddGo.AddComponent<Dropdown>();
		var ddImg = ddGo.AddComponent<Image>();
		ddImg.color = new Color(0.15f, 0.15f, 0.15f, 1);
		localSavesDropdown.captionText = MakeText(ddGo.transform, "local save", 16, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, font);
		localSavesDropdown.captionText.rectTransform.anchorMin = Vector2.zero;
		localSavesDropdown.captionText.rectTransform.anchorMax = Vector2.one;
		localSavesDropdown.captionText.rectTransform.offsetMin = new Vector2(8, 0);
		localSavesDropdown.captionText.rectTransform.offsetMax = new Vector2(-8, 0);
		var template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
		template.transform.SetParent(ddGo.transform, false);
		template.SetActive(false);
		localSavesDropdown.template = template.GetComponent<RectTransform>();
		localSavesDropdown.itemText = localSavesDropdown.captionText;

		var up = MakeButton(panel.transform, "Upload", new Vector2(140, 40), new Vector2(0, 1), new Vector2(460, -152), font);
		up.onClick.AddListener(UploadSelected);
		var refb = MakeButton(panel.transform, "Refresh", new Vector2(140, 40), new Vector2(0, 1), new Vector2(620, -152), font);
		refb.onClick.AddListener(RefreshList);

		var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
		scrollGo.transform.SetParent(panel.transform, false);
		var srt = scrollGo.GetComponent<RectTransform>();
		srt.anchorMin = new Vector2(0.02f, 0.04f);
		srt.anchorMax = new Vector2(0.98f, 0.68f);
		srt.offsetMin = srt.offsetMax = Vector2.zero;
		scrollGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 1);

		var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
		content.transform.SetParent(scrollGo.transform, false);
		var crt = content.GetComponent<RectTransform>();
		crt.anchorMin = new Vector2(0, 1);
		crt.anchorMax = new Vector2(1, 1);
		crt.pivot = new Vector2(0.5f, 1);
		crt.anchoredPosition = Vector2.zero;
		crt.sizeDelta = new Vector2(0, 0);
		var vlg = content.GetComponent<VerticalLayoutGroup>();
		vlg.childForceExpandWidth = true;
		vlg.childForceExpandHeight = false;
		vlg.spacing = 6;
		vlg.padding = new RectOffset(8, 8, 8, 8);
		content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		scrollGo.GetComponent<ScrollRect>().content = crt;
		scrollGo.GetComponent<ScrollRect>().horizontal = false;
		listParent = content.transform;
	}

	private static Font UiFont()
	{
		var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 16);
		if (f == null) f = Font.CreateDynamicFontFromOSFont("Liberation Sans", 16);
		return f;
	}

	private static Text MakeText(Transform parent, string s, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Font font)
	{
		var go = new GameObject("txt", typeof(RectTransform));
		go.transform.SetParent(parent, false);
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
		rt.anchoredPosition = pos;
		rt.sizeDelta = new Vector2(600, 40);
		var tx = go.AddComponent<Text>();
		tx.font = font;
		tx.fontSize = size;
		tx.color = Color.white;
		tx.alignment = align;
		tx.text = s;
		return tx;
	}

	private static InputField MakeInput(Transform parent, string placeholder, Vector2 pos, Font font)
	{
		var go = new GameObject(placeholder, typeof(RectTransform), typeof(Image));
		go.transform.SetParent(parent, false);
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
		rt.pivot = new Vector2(0, 1);
		rt.anchoredPosition = pos;
		rt.sizeDelta = new Vector2(240, 36);
		go.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 1);
		var input = go.AddComponent<InputField>();
		var text = MakeText(go.transform, "", 16, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, font);
		text.rectTransform.anchorMin = Vector2.zero;
		text.rectTransform.anchorMax = Vector2.one;
		text.rectTransform.offsetMin = new Vector2(8, 0);
		text.rectTransform.offsetMax = new Vector2(-8, 0);
		input.textComponent = text;
		var ph = MakeText(go.transform, placeholder, 16, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, font);
		ph.color = new Color(1, 1, 1, 0.35f);
		ph.rectTransform.anchorMin = Vector2.zero;
		ph.rectTransform.anchorMax = Vector2.one;
		ph.rectTransform.offsetMin = new Vector2(8, 0);
		ph.rectTransform.offsetMax = new Vector2(-8, 0);
		input.placeholder = ph;
		return input;
	}

	private static Button MakeButton(Transform parent, string label, Vector2 size, Vector2 anchor, Vector2 pos, Font font)
	{
		var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, false);
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
		rt.anchoredPosition = pos;
		rt.sizeDelta = size;
		go.GetComponent<Image>().color = new Color(1f, 0.53f, 0f, 1f);
		var tx = MakeText(go.transform, label, 18, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, font);
		tx.rectTransform.anchorMin = Vector2.zero;
		tx.rectTransform.anchorMax = Vector2.one;
		tx.rectTransform.offsetMin = tx.rectTransform.offsetMax = Vector2.zero;
		tx.color = Color.black;
		return go.GetComponent<Button>();
	}

	public void RefreshList()
	{
		if (WorkshopClient.Instance == null) return;
		SetStatus("Loading workshop...");
		WorkshopClient.Instance.ListSaves((list, err) =>
		{
			if (err != null) { SetStatus("Error: " + err); return; }
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
			SetStatus("No local .opc save. Play once and save first.");
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
		SetStatus("Downloading " + item.title + "...");
		WorkshopClient.Instance.Download(item, (path, err) =>
		{
			if (err != null) { SetStatus("Download: " + err); return; }
			try
			{
				var loader = new DataLoader(path);
				loader.LoadFromPath();
				Hide();
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
			var le = row.GetComponent<LayoutElement>();
			if (le == null) le = row.AddComponent<LayoutElement>();
			le.preferredHeight = 40;
			le.minHeight = 40;
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
		h.childForceExpandHeight = true;
		h.childForceExpandWidth = false;
		h.spacing = 8;
		var label = NewText(row.transform, it.title + "  —  " + it.author + "  (" + it.downloads + ")");
		var fitter = label.gameObject.AddComponent<LayoutElement>();
		fitter.flexibleWidth = 1;
		NewButton(row.transform, "Play", () => DownloadAndPlay(it));
		NewButton(row.transform, "Save", () => DownloadOnly(it));
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
		tx.font = UiFont();
		tx.fontSize = 18;
		tx.color = Color.white;
		tx.text = s;
		return tx;
	}

	private static Button NewButton(Transform parent, string s, UnityEngine.Events.UnityAction onClick)
	{
		var go = new GameObject(s, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, false);
		go.GetComponent<Image>().color = new Color(1f, 0.53f, 0f, 0.85f);
		var le = go.AddComponent<LayoutElement>();
		le.preferredWidth = 80;
		le.preferredHeight = 32;
		var tx = NewText(go.transform, s);
		tx.alignment = TextAnchor.MiddleCenter;
		tx.color = Color.black;
		go.GetComponent<Button>().onClick.AddListener(onClick);
		return go.GetComponent<Button>();
	}

	private void SetStatus(string s)
	{
		if (statusText != null) statusText.text = s;
		Debug.Log("[Workshop] " + s);
	}
}
