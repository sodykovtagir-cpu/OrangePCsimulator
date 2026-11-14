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

	public static FileMenu Instance { get; private set; }

	private List<Load> loads;

    int listGeneration;
    public static System.Collections.IEnumerator PreloadHeaders()
    {
        SaveCatalog.Initialize(SaveUtility.GetFolderPath());
        while (SaveCatalog.IsScanning) yield return null;
    }

	[SerializeField]
	private FileInformation fileInformation;

	private void Awake()
	{
		Instance = this;
	}

	private void OnDestroy()
	{
		if (Instance == this) Instance = null;
	}

	private void OnEnable()
	{
		// Returning from a save creates a new scene; entering this menu again
		// must show new/renamed/deleted saves without an explicit Refresh click.
		RebuildList();
	}

	/// <summary>Refresh the list after picker / Android Files import.</summary>
	public void RefreshFromDisk() { RebuildList(); }

	private bool AddSlot(SaveCatalog.Entry entry)
	{
		try
		{
			// read
			DataLoader l = entry.Loader; // disk/HMAC/JSON work already completed on a worker
            if (l == null) return false;
			// load
			var parent = slotParent;
			var pref = slotPrefab;
			var x = Instantiate(pref, parent, false);
			x.Find("Name").GetComponent<Text>().text = l.GameData.roomName;
			x.Find("Hardcore").gameObject.SetActive(l.GameData.hardcore == true);
			ApplySaveIcon(x, l.GameData);
			ApplyPublishedMark(x, IsPublished(l.GameData));
			// add to load list
			Load v = new Load();
			v.graphic = x.gameObject;
			v.loader = l;
			loads.Add(v);
			// buttons
			x.Find("Edit").GetComponent<Button>().onClick.AddListener(() => { ShowFileInformation(v); });
            x.Find("Name").GetComponent<Button>().onClick.AddListener(() =>
            {
                MainMenu.Instance.LoadSavedFile(entry.Path);
            });
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

    private void OnDisable() { listGeneration++; }
    public void RebuildList()
    {
        SaveCatalog.Refresh();
        if (!isActiveAndEnabled) return;
        StartCoroutine(BuildList(++listGeneration));
    }
    private System.Collections.IEnumerator BuildList(int generation)
    {
        // Let the button click/menu transition render before doing UI work.
        yield return null;
        if (generation != listGeneration) yield break;
        if (loads == null) loads = new List<Load>();
        loads.Clear();
        if (slotParent != null)
            for (int i = slotParent.childCount - 1; i >= 0; i--)
            {
                if (generation != listGeneration) yield break;
                var old = slotParent.GetChild(i).gameObject; old.SetActive(false); Destroy(old);
                if (i % 32 == 0) yield return null;
            }
        if (empty != null) empty.SetActive(false);
        while (SaveCatalog.IsScanning) { if (generation != listGeneration) yield break; yield return null; }
        var rows = SaveCatalog.Snapshot();
        SaveCoverCache.Prune(rows);
        long started = System.Diagnostics.Stopwatch.GetTimestamp(); int batch = 0;
        foreach (var entry in rows)
        {
            if (generation != listGeneration) yield break;
            if (entry.Loader == null) { Debug.LogWarning("Cannot index save " + Path.GetFileName(entry.Path) + ": " + entry.Error); continue; }
            AddSlot(entry);
            if (++batch >= 8 || (System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency >= 3)
            { yield return null; batch = 0; started = System.Diagnostics.Stopwatch.GetTimestamp(); }
        }
        if (empty != null) empty.SetActive(loads.Count == 0);
        if (SaveCatalog.Error != null) Debug.LogWarning("Cannot list saves: " + SaveCatalog.Error);
    }

	public void RefreshLoadButton(Load load)
	{
		var x = load.graphic.transform;
		x.Find("Name").GetComponent<Text>().text = load.loader.GameData.roomName;
		x.Find("Hardcore").gameObject.SetActive(load.loader.GameData.hardcore == true);
		ApplySaveIcon(x, load.loader.GameData);
		ApplyPublishedMark(x, IsPublished(load.loader.GameData));
	}

	private static void ApplySaveIcon(Transform row, GameData g)
	{
		if (row == null) return;
		Texture tex = null;
		if (g != null && !string.IsNullOrEmpty(g.icon))
		{
			try { tex = SaveCoverCache.Get(g.icon); }
			catch { tex = null; }
		}

		var icon = row.Find("Icon");
		if (icon == null)
		{
			var go = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(FadedCoverImage));
			go.transform.SetParent(row, false);
			go.transform.SetAsFirstSibling();
			var rt = go.GetComponent<RectTransform>();
			rt.anchorMin = new Vector2(0f, 0f);
			rt.anchorMax = new Vector2(0f, 1f);
			rt.pivot = new Vector2(0f, 0.5f);
			rt.sizeDelta = new Vector2(220f, 0f);
			rt.anchoredPosition = new Vector2(0f, 0f);
			icon = go.transform;
		}

		var faded = icon.GetComponent<FadedCoverImage>();
		if (faded == null)
			faded = icon.gameObject.AddComponent<FadedCoverImage>();

		var raw = icon.GetComponent<RawImage>();
		if (raw != null) raw.enabled = false;
		var img = icon.GetComponent<Image>();
		if (img != null) img.enabled = false;

		faded.raycastTarget = false;
		faded.FadeStart = 0.4f;
		faded.color = Color.white;
		faded.Texture = tex;
		faded.enabled = tex != null;
	}

	private static bool IsPublished(GameData g)
	{
		return g != null && g.workshopId > 0;
	}

	private static Sprite publishedSprite;

	private static void ApplyPublishedMark(Transform row, bool on)
	{
		if (row == null) return;
		var mark = row.Find("Published");
		if (mark == null)
		{
			var go = new GameObject("Published", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			go.transform.SetParent(row, false);
			var rt = go.GetComponent<RectTransform>();
			rt.anchorMin = new Vector2(1f, 0.5f);
			rt.anchorMax = new Vector2(1f, 0.5f);
			rt.pivot = new Vector2(0.5f, 0.5f);
			rt.sizeDelta = new Vector2(28f, 28f);
			rt.anchoredPosition = new Vector2(-168f, 0f);
			var img = go.GetComponent<Image>();
			img.raycastTarget = false;
			img.preserveAspect = true;
			mark = go.transform;
		}
		var image = mark.GetComponent<Image>();
		if (image != null)
		{
			image.sprite = PublishedSprite();
			image.color = Color.white;
		}
		mark.gameObject.SetActive(on);
	}

	private static Sprite PublishedSprite()
	{
		if (publishedSprite != null) return publishedSprite;
		const int s = 32;
		var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
		tex.wrapMode = TextureWrapMode.Clamp;
		tex.filterMode = FilterMode.Bilinear;
		var px = new Color32[s * s];
		var clear = new Color32(0, 0, 0, 0);
		var orange = new Color32(255, 136, 0, 255);
		var dark = new Color32(40, 24, 0, 255);
		for (int i = 0; i < px.Length; i++) px[i] = clear;
		int cx = 16, cy = 16, r = 14;
		int r2 = r * r;
		for (int y = 0; y < s; y++)
			for (int x = 0; x < s; x++)
			{
				int dx = x - cx, dy = y - cy;
				if (dx * dx + dy * dy <= r2) px[y * s + x] = orange;
			}
		// стрелка вверх
		for (int y = 8; y <= 20; y++)
			for (int x = 14; x <= 17; x++)
				px[y * s + x] = dark;
		for (int i = 0; i <= 7; i++)
			for (int x = 16 - i; x <= 16 + i; x++)
			{
				int y = 20 - i;
				if (x >= 0 && x < s && y >= 0 && y < s) px[y * s + x] = dark;
			}
		tex.SetPixels32(px);
		tex.Apply(false, false);
		publishedSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 32f);
		publishedSprite.name = "PublishedMark";
		return publishedSprite;
	}

	public void DeleteLoadButton(Load load)
	{
		SaveStorage.Delete(load.loader.Path);
		loads.Remove(load);
		if (load.graphic != null)
		{
			Destroy(load.graphic);
		}
		empty.SetActive(loads.Count == 0);
	}

public void Import()
{
#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
        string[] exts = new string[] { ".pc", ".opc" };
#else
        // Mobile providers have no registered MIME type for .pc/.opc.
        // The callback below still validates these two extensions before importing.
        string[] exts = new string[] { "*/*" };
#endif

        NativeFilePicker.PickFile(PickFileCallback, exts);
}

    private static bool CanReadFile(string path)
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

	private void PickFileCallback(string path)
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
			SaveFileImport.Import(path, System.IO.Path.GetFileName(path));
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

		RefreshFromDisk();
	}

}
