using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class FileInformation : MonoBehaviour
{
	[Header("Room Name")]
	[SerializeField] private InputField nameInput;

	[Header("Sign")]
	[SerializeField] private GameObject sign;
	[SerializeField] private Text signNameText;
	[SerializeField] private InputField signInput;

	[SerializeField] private Button applyButton;
	[SerializeField] private AudioSource source;
	[SerializeField] private AudioClip warningSound;
	[SerializeField] private Text playtimeText;
	[SerializeField] private Text fileLocationText;

	[SerializeField] private MessageBox messageBox;
	[SerializeField] private FileMenu fileMenu;
	[SerializeField] private GameObject exportButton;
	[SerializeField] private ConfirmationDialog deleteConfirmationDialog;

	[Header("Workshop")]
	[SerializeField] private GameObject uploadButton;
	[SerializeField] private GameObject updateButton;
	[SerializeField] private GameObject deleteWorkshopButton;
	[SerializeField] private GameObject publishPanel;
	[SerializeField] private InputField wsTitle;
	[SerializeField] private InputField wsAuthor;
	[SerializeField] private InputField wsDesc;
	[SerializeField] private RawImage wsCover;
	[SerializeField] private Text wsStatus;

	private byte[] coverJpg;
	private FileMenu.Load load;
	private MenuManager menuManager;
	private static Texture2D coverPlaceholder;

	private void Awake()
	{
		HideStandaloneUpdate();
	}

	private void Start()
	{
		menuManager = GetComponentInParent<MenuManager>();
		AccountPanel.EnsureOnScene();
		HideStandaloneUpdate();
#if UNITY_ANDROID || UNITY_IOS
		if (!NativeFilePicker.CanExportFiles())
			exportButton.SetActive(false);
#else
		exportButton.SetActive(true);
#endif
	}

	private void OnEnable()
	{
		AccountManager.AccountChanged += OnAccountChanged;
		ServerAccounts.StateChanged += OnAccountChanged;
		HideStandaloneUpdate();
		if (load != null) RefreshWorkshopButtons();
	}

	private void OnDisable()
	{
		AccountManager.AccountChanged -= OnAccountChanged;
		ServerAccounts.StateChanged -= OnAccountChanged;
	}

	private void OnAccountChanged()
	{
		RefreshWorkshopButtons();
	}

	public void Show(FileMenu.Load load)
	{
		this.load = load;
		if (load == null || load.loader == null)
			return;

		sign.SetActive(true);
		if (load.loader.GameData.sign == null)
			load.loader.GameData.sign = "";

		nameInput.text = load.loader.GameData.roomName;
		string localDesc = FirstNonEmpty(load.loader.GameData.sign, load.loader.GameData.workshopDesc);
		if (load.loader.GameData.sign == null) load.loader.GameData.sign = "";
		signInput.text = localDesc;
		signNameText.text = string.IsNullOrEmpty(localDesc) ? "No Sign" : localDesc;
		playtimeText.text = Localization.GetText("Playing Time") + ":\n" + (load.loader.GameData.playtime / 60f).ToString("0.00") + " min";
		fileLocationText.text = Path.GetFileName(load.loader.Path);
		if (load.loader.GameData.workshopId > 0 && !string.IsNullOrEmpty(load.loader.GameData.workshopKey))
		{
			WorkshopLocal.Put(load.loader.Path, load.loader.GameData.workshopId, load.loader.GameData.workshopKey);
			load.loader.GameData.workshopKey = "";
			load.loader.WriteToFile();
		}
		else if (load.loader.GameData.workshopId <= 0)
		{
			WorkshopLocal.Remove(load.loader.Path);
		}
		coverJpg = null;
		ResetCover();
		if (publishPanel != null) publishPanel.SetActive(false);
		HideAuthorField();
		FillPublishForm(null);
		EnsureWorkshopClient();
		EnsurePublishLayout();
		AccountPanel.RefreshAll();
		RefreshWorkshopButtons();
		if (ServerAccounts.LoggedIn)
			EnsureMeThen(() => { FillPublishForm(null); RefreshWorkshopButtons(); });
	}

	public void OpenPublishPanel()
	{
		if (publishPanel != null) publishPanel.SetActive(true);
		ShowPublishFields();
		ResetCover();
		FillPublishForm(null);
		RefreshWorkshopButtons();
		EnsureWorkshopClient();
		if (!ServerAccounts.LoggedIn)
		{
			SetWsStatus("Login to publish");
			return;
		}

		SetWsStatus("Checking listing...");
		EnsureMeThen(LookupListing);
	}

	private void LookupListing()
	{
		int id = ListingId();
		WorkshopClient.Instance.ListSaves((list, err) =>
		{
			if (err != null) { SetWsStatus(err); return; }
			WorkshopItem found = FindItem(list, id);
			if (id > 0 && found == null && WasPublishedHere())
			{
				ForgetListing();
				FillPublishForm(null);
				SetWsStatus("Listing gone. Publish again as new.");
				RefreshWorkshopButtons();
				return;
			}
			FillPublishForm(found);
			SetWsStatus(IsOwner() ? "Edit and press [Update]" : "New upload");
			RefreshWorkshopButtons();
			if (found != null && found.has_cover && wsCover != null)
			{
				WorkshopClient.Instance.DownloadCover(found.id, (tex, e) =>
				{
					if (tex != null && wsCover != null) wsCover.texture = tex;
				});
			}
		});
	}

	private static WorkshopItem FindItem(List<WorkshopItem> list, int id)
	{
		if (id <= 0 || list == null) return null;
		for (int i = 0; i < list.Count; i++)
			if (list[i] != null && list[i].id == id) return list[i];
		return null;
	}

	private void EnsureMeThen(System.Action next)
	{
		EnsureWorkshopClient();
		if (!ServerAccounts.LoggedIn || WorkshopClient.Instance == null)
		{
			if (next != null) next();
			return;
		}
		WorkshopClient.Instance.AccountMe(ServerAccounts.Token, (r, err) =>
		{
			if (r != null && r.ok)
			{
				ServerAccounts.SetSession(ServerAccounts.Token, r.name, r.email);
				var list = r.saves != null
					? new List<AccountSaveItem>(r.saves)
					: new List<AccountSaveItem>();
				ServerAccounts.SetSaves(list);
			}
			if (next != null) next();
		});
	}

	private void ForgetListing()
	{
		if (load == null || load.loader == null || load.loader.GameData == null) return;
		WorkshopLocal.Remove(load.loader.Path);
		var g = load.loader.GameData;
		g.workshopId = 0;
		g.workshopKey = "";
		g.workshopSourceId = 0;
		g.workshopTitle = "";
		g.workshopDesc = "";
		g.workshopAuthor = "";
		load.loader.WriteToFile();
		if (fileMenu != null) fileMenu.RefreshLoadButton(load);
	}

	/// <summary>
	/// Публикация пишет имя в сейв и описание в Sign (это поле описания на странице файла).
	/// </summary>
	private void ApplyPublishedMeta(string title, string desc, string author, bool write)
	{
		if (load == null || load.loader == null || load.loader.GameData == null) return;
		var g = load.loader.GameData;
		if (!string.IsNullOrWhiteSpace(title))
		{
			g.roomName = title.Trim();
			g.workshopTitle = g.roomName;
			if (nameInput != null) nameInput.text = g.roomName;
		}
		g.workshopDesc = desc ?? "";
		g.sign = g.workshopDesc;
		if (signInput != null) signInput.text = g.sign;
		if (signNameText != null)
			signNameText.text = string.IsNullOrEmpty(g.sign) ? "No Sign" : g.sign;
		if (!string.IsNullOrEmpty(author)) g.workshopAuthor = author;
		if (write)
		{
			load.loader.WriteToFile();
			if (fileMenu != null) fileMenu.RefreshLoadButton(load);
		}
	}

	private static string FirstNonEmpty(string a, string b)
	{
		if (!string.IsNullOrEmpty(a)) return a;
		return b ?? "";
	}

	private void HideStandaloneUpdate()
	{
		if (updateButton != null) updateButton.SetActive(false);
		var buttons = GetComponentsInChildren<Button>(true);
		for (int i = 0; i < buttons.Length; i++)
		{
			var b = buttons[i];
			if (b == null) continue;
			if (uploadButton != null && b.gameObject == uploadButton) continue;
			if (publishPanel != null && b.transform.IsChildOf(publishPanel.transform)
				&& b.gameObject.name.IndexOf("PublishAction", System.StringComparison.OrdinalIgnoreCase) >= 0)
				continue;
			string n = b.gameObject.name.ToLowerInvariant();
			var tx = b.GetComponent<Text>();
			if (tx == null) tx = b.GetComponentInChildren<Text>(true);
			string t = tx != null && tx.text != null ? tx.text.ToLowerInvariant() : "";
			bool isUpdate = n.Contains("update") || t.Contains("update") || t.Contains("обнов");
			bool isUpload = n.Contains("upload") || t.Contains("upload") || t.Contains("загруз") || n.Contains("publishaction");
			if (isUpdate && !isUpload) b.gameObject.SetActive(false);
		}
	}

	private void ShowPublishFields()
	{
		if (wsTitle != null) wsTitle.gameObject.SetActive(true);
		if (wsDesc != null) wsDesc.gameObject.SetActive(true);
		if (wsCover != null) wsCover.gameObject.SetActive(true);
		HideAuthorField();
	}

	private void HideAuthorField()
	{
		if (wsAuthor == null) return;
		wsAuthor.gameObject.SetActive(false);
		var p = wsAuthor.transform.parent;
		if (p == null) return;
		// Строка автора — отдельный контейнер Name (2), не общий корень панели.
		if (p == transform || (publishPanel != null && p == publishPanel.transform)) return;
		if (p.GetComponent<InputField>() != null) return;
		bool hasOther = false;
		if (wsTitle != null && wsTitle.transform.IsChildOf(p) && wsTitle.transform != wsAuthor.transform)
			hasOther = true;
		if (wsDesc != null && wsDesc.transform.IsChildOf(p) && wsDesc.transform != wsAuthor.transform)
			hasOther = true;
		if (!hasOther) p.gameObject.SetActive(false);
	}

	private void FillPublishForm(WorkshopItem remote)
	{
		if (wsTitle != null) wsTitle.text = "";
		if (wsDesc != null) wsDesc.text = "";
		HideAuthorField();
		if (load == null || load.loader == null || load.loader.GameData == null) return;
		var g = load.loader.GameData;
		string title = "";
		string desc = "";
		if (remote != null)
		{
			title = remote.title ?? "";
			desc = remote.description ?? "";
		}
		else
		{
			var mine = ServerAccounts.FindSave(ListingId());
			if (mine != null)
			{
				title = mine.title ?? "";
				desc = mine.description ?? "";
			}
		}
		if (wsTitle != null) wsTitle.text = title;
		if (wsDesc != null) wsDesc.text = desc;
	}

	public void ClosePublishPanel()
	{
		if (publishPanel != null) publishPanel.SetActive(false);
	}

	public void PickCover()
	{
#if UNITY_EDITOR
		string path = EditorUtility.OpenFilePanel("Cover", "", "png,jpg,jpeg,webp");
		ApplyCoverPath(path);
#else
		NativeFilePicker.PickFile(ApplyCoverPath, new[] { "image/*", ".png", ".jpg", ".jpeg" });
#endif
	}

	private void ApplyCoverPath(string path)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
		try
		{
			var tex = new Texture2D(2, 2);
			if (!tex.LoadImage(File.ReadAllBytes(path)))
			{
				SetWsStatus("Bad image");
				return;
			}
			if (wsCover != null)
			{
				wsCover.texture = tex;
				wsCover.gameObject.SetActive(true);
			}
			coverJpg = tex.EncodeToJPG(70);
			if (coverJpg != null && coverJpg.Length > 300000)
				coverJpg = tex.EncodeToJPG(40);
			SetWsStatus("Cover ready (" + (coverJpg != null ? coverJpg.Length / 1024 : 0) + " KB)");
		}
		catch
		{
			SetWsStatus("Bad image");
		}
	}

	public void ConfirmPublish()
	{
		if (load == null || load.loader == null) return;
		if (!ServerAccounts.LoggedIn)
		{
			SetWsStatus("Login to publish");
			return;
		}
		EnsureWorkshopClient();
		string title = wsTitle != null ? wsTitle.text : "";
		if (string.IsNullOrWhiteSpace(title)) title = load.loader.GameData.roomName;
		string author = ServerAccounts.LoggedIn ? ServerAccounts.Name : "Player";
		string desc = wsDesc != null ? wsDesc.text : "";
		ApplyPublishedMeta(title, desc, author, write: false);
		SetWsStatus("Uploading...");
		if (IsOwner())
		{
			int wid = OwnerId();
			string wkey = OwnerKey();
			WorkshopClient.Instance.UpdateSave(wid, wkey,
				load.loader.Path, title, author, desc, coverJpg, (id, key, err) =>
				{
					if (err != null)
					{
						if (IsGoneError(err))
						{
							ForgetListing();
							SetWsStatus("Listing gone. Publishing as new...");
							ConfirmPublish();
							return;
						}
						SetWsStatus(err);
						return;
					}
					if (!string.IsNullOrEmpty(key))
						WorkshopLocal.Put(load.loader.Path, id > 0 ? id : wid, key);
					load.loader.GameData.workshopId = id > 0 ? id : wid;
					load.loader.GameData.workshopKey = "";
					ApplyPublishedMeta(title, desc, author, write: true);
					SetWsStatus("Updated");
					RefreshWorkshopButtons();
				});
		}
		else
		{
			WorkshopClient.Instance.Upload(load.loader.Path, title, author, desc, coverJpg, (id, key, err) =>
			{
				if (err != null) { SetWsStatus(err); return; }
				WorkshopLocal.Put(load.loader.Path, id, key);
				load.loader.GameData.workshopId = id;
				load.loader.GameData.workshopKey = "";
				ApplyPublishedMeta(title, desc, author, write: true);
				SetWsStatus("Published #" + id);
				RefreshWorkshopButtons();
			});
		}
	}

	public void DeleteFromWorkshop()
	{
		if (!IsOwner()) return;
		EnsureWorkshopClient();
		SetWsStatus("Deleting...");
		int wid = OwnerId();
		string wkey = OwnerKey();
		WorkshopClient.Instance.DeleteSave(wid, wkey, err =>
		{
			if (err != null && !IsGoneError(err)) { SetWsStatus(err); return; }
			ForgetListing();
			SetWsStatus("Removed from workshop");
			RefreshWorkshopButtons();
		});
	}

	private void EnsureWorkshopClient()
	{
		if (WorkshopClient.Instance == null)
		{
			var go = new GameObject("WorkshopClient");
			go.AddComponent<WorkshopClient>();
		}
	}

	private bool IsOwner()
	{
		if (load == null || load.loader == null || load.loader.GameData == null) return false;
		WorkshopLocalRec rec;
		if (WorkshopLocal.TryGetForSave(load.loader.Path, load.loader.GameData.workshopId, out rec))
			return true;
		return ServerAccounts.OwnsListing(ListingId());
	}

	private int ListingId()
	{
		if (load == null || load.loader == null || load.loader.GameData == null) return 0;
		var g = load.loader.GameData;
		if (g.workshopId > 0) return g.workshopId;
		if (g.workshopSourceId > 0) return g.workshopSourceId;
		return 0;
	}

	private int OwnerId()
	{
		WorkshopLocalRec rec;
		if (load != null && load.loader != null && load.loader.GameData != null
			&& WorkshopLocal.TryGetForSave(load.loader.Path, load.loader.GameData.workshopId, out rec))
			return rec.id;
		return ListingId();
	}

	private string OwnerKey()
	{
		if (load == null || load.loader == null || load.loader.GameData == null) return "";
		WorkshopLocalRec rec;
		if (WorkshopLocal.TryGetForSave(load.loader.Path, load.loader.GameData.workshopId, out rec)
			&& rec != null && !string.IsNullOrEmpty(rec.key))
			return rec.key;
		if (!string.IsNullOrEmpty(load.loader.GameData.workshopKey))
			return load.loader.GameData.workshopKey;
		return ServerAccounts.OwnerKeyFor(OwnerId());
	}

	private bool WasPublishedHere()
	{
		return load != null && load.loader != null && load.loader.GameData != null
			&& load.loader.GameData.workshopId > 0;
	}

	private void RefreshWorkshopButtons()
	{
		bool logged = ServerAccounts.LoggedIn;
		bool owner = IsOwner();
		bool canPublish = logged && CanPublish();

		// Одна кнопка Upload: для своего листинга подпись [Update].
		// Отдельная кнопка Update в сцене ни к чему не привязана — прячем всегда.
		if (uploadButton != null)
		{
			uploadButton.SetActive(canPublish);
			SetButtonLabel(uploadButton, owner ? "[Update]" : "[Upload]");
		}
		if (updateButton != null) updateButton.SetActive(false);
		if (deleteWorkshopButton != null && deleteWorkshopButton.GetComponent<Button>() != null)
			deleteWorkshopButton.SetActive(canPublish && owner);

		var action = FindPublishChild("PublishAction");
		var del = FindPublishChild("PublishDelete");
		var close = FindPublishChild("PublishClose");
		if (action != null)
		{
			action.gameObject.SetActive(canPublish);
			SetButtonLabel(action.gameObject, owner ? "[Update]" : "[Upload]");
		}
		if (del != null) del.gameObject.SetActive(canPublish && owner);
		if (close != null) close.gameObject.SetActive(true);

		ToggleNamed(publishPanel != null ? publishPanel.transform : transform, "Upload", canPublish);
		ToggleNamed(publishPanel != null ? publishPanel.transform : transform, "Update", false);
		ToggleNamed(publishPanel != null ? publishPanel.transform : transform, "Delete", canPublish && owner);
		ToggleNamed(publishPanel != null ? publishPanel.transform : transform, "DeleteWorkshop", canPublish && owner);
		ApplyPanelButtonTexts(canPublish, owner);
	}

	private static bool IsGoneError(string err)
	{
		if (string.IsNullOrEmpty(err)) return false;
		err = err.ToLowerInvariant();
		return err.Contains("not found") || err.Contains("forbidden") || err.Contains("no listing");
	}

	private void ApplyPanelButtonTexts(bool canPublish, bool owner)
	{
		if (publishPanel == null) return;
		var buttons = publishPanel.GetComponentsInChildren<Button>(true);
		for (int i = 0; i < buttons.Length; i++)
		{
			var b = buttons[i];
			if (b == null) continue;
			if (uploadButton != null && b.gameObject == uploadButton) continue;
			if (updateButton != null && b.gameObject == updateButton)
			{
				b.gameObject.SetActive(false);
				continue;
			}
			string n = b.gameObject.name.ToLowerInvariant();
			var tx = b.GetComponent<Text>();
			if (tx == null) tx = b.GetComponentInChildren<Text>(true);
			string t = tx != null && tx.text != null ? tx.text.ToLowerInvariant() : "";
			bool isAction = n.Contains("publishaction");
			bool isUpload = n.Contains("upload") || t.Contains("upload") || t.Contains("загруз");
			bool isUpdate = n.Contains("update") || t.Contains("update") || t.Contains("обнов");
			bool isDelete = (n.Contains("delete") && !n.Contains("file")) || n.Contains("publishdelete") || t.Contains("удал") || t.Contains("[delete]");
			bool isClose = n.Contains("close") || n.Contains("publishclose") || t.Contains("закры") || t.Contains("[close]");
			if (isClose) { b.gameObject.SetActive(true); continue; }
			if (isDelete) { b.gameObject.SetActive(canPublish && owner); continue; }
			if (isAction)
			{
				b.gameObject.SetActive(canPublish);
				SetButtonLabel(b.gameObject, owner ? "[Update]" : "[Upload]");
				continue;
			}
			if (isUpdate && !isUpload) { b.gameObject.SetActive(false); continue; }
			if (isUpload)
			{
				b.gameObject.SetActive(canPublish);
				SetButtonLabel(b.gameObject, owner ? "[Update]" : "[Upload]");
			}
		}
	}

	/// <summary>
	/// Можно ли публиковать текущий сейв:
	/// владелец всегда может; обычный локальный сейв — да;
	/// скачанный чужой сейв (workshopSourceId > 0 без ключа владельца) — нет.
	/// </summary>
	private bool CanPublish()
	{
		if (load == null || load.loader == null || load.loader.GameData == null) return false;
		if (IsOwner()) return true;
		return load.loader.GameData.workshopSourceId <= 0;
	}

	private Transform FindPublishChild(string name)
	{
		if (publishPanel == null) return null;
		var panel = publishPanel.transform.Find("Panel");
		if (panel == null) return null;
		var inner = panel.Find("Panel");
		return inner != null ? inner.Find(name) : panel.Find(name);
	}

	private void EnsurePublishLayout()
	{
		if (publishPanel == null) return;
		var panel = publishPanel.transform.Find("Panel");
		var inner = panel != null ? panel.Find("Panel") : null;
		if (inner == null) return;

		if (inner.GetComponent<HorizontalLayoutGroup>() == null)
		{
			var h = inner.gameObject.AddComponent<HorizontalLayoutGroup>();
			h.spacing = 8f;
			h.childAlignment = TextAnchor.MiddleCenter;
			h.childControlWidth = false;
			h.childControlHeight = true;
			h.childForceExpandWidth = false;
			h.childForceExpandHeight = true;
		}
		if (inner.GetComponent<ContentSizeFitter>() == null)
		{
			var f = inner.gameObject.AddComponent<ContentSizeFitter>();
			f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		}
	}

	private static void SetButtonLabel(GameObject go, string label)
	{
		if (go == null) return;
		var tx = go.GetComponent<Text>();
		if (tx == null) tx = go.GetComponentInChildren<Text>(true);
		if (tx != null) tx.text = label;
		var loc = go.GetComponent<LocalizationText>();
		if (loc == null) loc = go.GetComponentInChildren<LocalizationText>(true);
		if (loc != null) loc.Bind(label);
		var anim = go.GetComponent<TextAnimation>();
		if (anim == null) anim = go.GetComponentInChildren<TextAnimation>(true);
		if (anim != null) anim.ResetText();
	}

	private static void ToggleNamed(Transform root, string name, bool on)
	{
		if (root == null) return;
		var all = root.GetComponentsInChildren<Transform>(true);
		for (int i = 0; i < all.Length; i++)
		{
			if (all[i] != null && all[i] != root && all[i].name == name)
				all[i].gameObject.SetActive(on);
		}
	}

	private void ResetCover()
	{
		coverJpg = null;
		if (wsCover == null) return;
		wsCover.texture = CoverPlaceholder();
		wsCover.color = Color.white;
		wsCover.uvRect = new Rect(0f, 0f, 1f, 1f);
		wsCover.gameObject.SetActive(true);
	}

	private static Texture2D CoverPlaceholder()
	{
		if (coverPlaceholder != null) return coverPlaceholder;
		const int s = 128;
		var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
		tex.wrapMode = TextureWrapMode.Clamp;
		tex.filterMode = FilterMode.Point;
		var px = new Color32[s * s];
		var bg = new Color32(22, 22, 22, 255);
		var frame = new Color32(255, 136, 0, 255);
		var icon = new Color32(90, 90, 90, 255);
		var sun = new Color32(255, 136, 0, 255);
		for (int i = 0; i < px.Length; i++) px[i] = bg;
		for (int i = 0; i < s; i++)
		{
			px[i] = frame;
			px[(s - 1) * s + i] = frame;
			px[i * s] = frame;
			px[i * s + (s - 1)] = frame;
			if (i < s - 2)
			{
				px[s + 1 + i] = frame;
				px[(s - 2) * s + 1 + i] = frame;
				px[(i + 1) * s + 1] = frame;
				px[(i + 1) * s + (s - 2)] = frame;
			}
		}
		FillCircle(px, s, 40, 88, 9, sun);
		FillTri(px, s, 18, 28, 64, 78, 110, 28, icon);
		FillTri(px, s, 50, 28, 92, 70, 118, 28, new Color32(60, 60, 60, 255));
		tex.SetPixels32(px);
		tex.Apply(false, false);
		coverPlaceholder = tex;
		return tex;
	}

	private static void FillCircle(Color32[] px, int s, int cx, int cy, int r, Color32 c)
	{
		int r2 = r * r;
		for (int y = cy - r; y <= cy + r; y++)
		{
			if (y < 0 || y >= s) continue;
			for (int x = cx - r; x <= cx + r; x++)
			{
				if (x < 0 || x >= s) continue;
				int dx = x - cx, dy = y - cy;
				if (dx * dx + dy * dy <= r2) px[y * s + x] = c;
			}
		}
	}

	private static void FillTri(Color32[] px, int s, int x1, int y1, int x2, int y2, int x3, int y3, Color32 c)
	{
		int minx = Mathf.Min(x1, Mathf.Min(x2, x3));
		int maxx = Mathf.Max(x1, Mathf.Max(x2, x3));
		int miny = Mathf.Min(y1, Mathf.Min(y2, y3));
		int maxy = Mathf.Max(y1, Mathf.Max(y2, y3));
		for (int y = miny; y <= maxy; y++)
		{
			if (y < 0 || y >= s) continue;
			for (int x = minx; x <= maxx; x++)
			{
				if (x < 0 || x >= s) continue;
				if (InsideTri(x, y, x1, y1, x2, y2, x3, y3)) px[y * s + x] = c;
			}
		}
	}

	private static bool InsideTri(int px, int py, int x1, int y1, int x2, int y2, int x3, int y3)
	{
		float d1 = Sign(px, py, x1, y1, x2, y2);
		float d2 = Sign(px, py, x2, y2, x3, y3);
		float d3 = Sign(px, py, x3, y3, x1, y1);
		bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
		bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
		return !(hasNeg && hasPos);
	}

	private static float Sign(int x1, int y1, int x2, int y2, int x3, int y3)
	{
		return (x1 - x3) * (y2 - y3) - (x2 - x3) * (y1 - y3);
	}

	private void SetWsStatus(string s)
	{
		if (wsStatus != null) wsStatus.text = s;
		Debug.Log("[Workshop] " + s);
	}

	public void ApplyEdit()
	{
		if (load == null || load.loader == null) return;
		load.loader.GameData.roomName = nameInput.text;
		load.loader.GameData.sign = signInput.text;
		load.loader.WriteToFile();
		fileMenu.RefreshLoadButton(load);
		menuManager.Back();
	}

	public void OnSignValueChanged(string value)
	{
		signNameText.text = string.IsNullOrEmpty(value) ? "No Sign" : value;
	}

	public void Export()
	{
		if (load == null || load.loader == null) return;
#if UNITY_EDITOR
		string savePath = EditorUtility.SaveFilePanel("Export Save File", "", Path.GetFileName(load.loader.Path), "sav");
		if (!string.IsNullOrEmpty(savePath))
			File.Copy(load.loader.Path, savePath, true);
#else
		NativeFilePicker.ExportFile(load.loader.Path, (success) =>
		{
			if (!success) messageBox?.Show("No permission to export the file.");
		});
#endif
	}

	public void AskDeleteMessage()
	{
		source?.PlayOneShot(warningSound);
		deleteConfirmationDialog.Show(Delete);
	}

	private void Delete()
	{
		fileMenu.DeleteLoadButton(load);
		menuManager.Back();
	}
}
