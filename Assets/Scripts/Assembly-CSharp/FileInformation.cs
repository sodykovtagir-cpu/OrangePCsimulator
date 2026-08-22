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

	private void Start()
	{
		menuManager = GetComponentInParent<MenuManager>();
		CreateAccountButton();
#if UNITY_ANDROID || UNITY_IOS
		if (!NativeFilePicker.CanExportFiles())
			exportButton.SetActive(false);
#else
		exportButton.SetActive(true);
#endif
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
		signInput.text = load.loader.GameData.sign;
		signNameText.text = string.IsNullOrEmpty(load.loader.GameData.sign) ? "No Sign" : load.loader.GameData.sign;
		playtimeText.text = Localization.GetText("Playing Time") + ":\n" + (load.loader.GameData.playtime / 60f).ToString("0.00") + " min";
		fileLocationText.text = Path.GetFileName(load.loader.Path);
		if (load.loader.GameData.workshopId > 0 && !string.IsNullOrEmpty(load.loader.GameData.workshopKey))
		{
			WorkshopLocal.Put(load.loader.Path, load.loader.GameData.workshopId, load.loader.GameData.workshopKey);
			load.loader.GameData.workshopKey = "";
			load.loader.WriteToFile();
		}
		coverJpg = null;
		RefreshWorkshopButtons();
		if (publishPanel != null) publishPanel.SetActive(false);
		FillPublishForm(null);
		EnsureWorkshopClient();
		EnsurePublishLayout();
		RefreshAccountButton();
	}

	public void OpenPublishPanel()
	{
		if (publishPanel != null) publishPanel.SetActive(true);
		ShowPublishFields();
		FillPublishForm(null);
		SetWsStatus(IsOwner() ? "Loading listing..." : "New upload");
		if (!IsOwner()) return;
		EnsureWorkshopClient();
		int id = OwnerId();
		WorkshopClient.Instance.ListSaves((list, err) =>
		{
			if (err != null) { SetWsStatus(err); return; }
			WorkshopItem found = null;
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
					if (list[i] != null && list[i].id == id) { found = list[i]; break; }
			}
			FillPublishForm(found);
			SetWsStatus("Edit and press Update");
			if (found != null && found.has_cover && wsCover != null)
			{
				WorkshopClient.Instance.DownloadCover(id, (tex, e) =>
				{
					if (tex != null && wsCover != null) wsCover.texture = tex;
				});
			}
		});
	}

	private void ShowPublishFields()
	{
		if (wsTitle != null) wsTitle.gameObject.SetActive(true);
		if (wsAuthor != null) wsAuthor.gameObject.SetActive(true);
		if (wsDesc != null) wsDesc.gameObject.SetActive(true);
		if (wsCover != null) wsCover.gameObject.SetActive(true);
	}

	private void FillPublishForm(WorkshopItem remote)
	{
		if (load == null || load.loader == null || load.loader.GameData == null) return;
		var g = load.loader.GameData;
		string title = g.roomName;
		string author = PlayerPrefs.GetString("WorkshopAuthor", "Player");
		string desc = "";
		if (!string.IsNullOrEmpty(g.workshopTitle)) title = g.workshopTitle;
		if (!string.IsNullOrEmpty(g.workshopAuthor)) author = g.workshopAuthor;
		if (!string.IsNullOrEmpty(g.workshopDesc)) desc = g.workshopDesc;
		if (remote != null)
		{
			if (!string.IsNullOrEmpty(remote.title)) title = remote.title;
			if (!string.IsNullOrEmpty(remote.author)) author = remote.author;
			if (remote.description != null) desc = remote.description;
		}
		if (wsTitle != null) wsTitle.text = title ?? "";
		if (wsAuthor != null) wsAuthor.text = string.IsNullOrEmpty(author) ? "Player" : author;
		if (wsDesc != null) wsDesc.text = desc ?? "";
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
		EnsureWorkshopClient();
		string title = wsTitle != null ? wsTitle.text : load.loader.GameData.roomName;
		string author = wsAuthor != null ? wsAuthor.text : "Player";
		string desc = wsDesc != null ? wsDesc.text : "";
		if (!string.IsNullOrEmpty(author))
		{
			PlayerPrefs.SetString("WorkshopAuthor", author);
			PlayerPrefs.Save();
		}
		load.loader.GameData.workshopTitle = title;
		load.loader.GameData.workshopAuthor = author;
		load.loader.GameData.workshopDesc = desc;
		SetWsStatus("Uploading...");
		if (IsOwner())
		{
			WorkshopLocalRec rec;
			WorkshopLocal.TryGet(load.loader.Path, out rec);
			int wid = rec != null ? rec.id : load.loader.GameData.workshopId;
			string wkey = rec != null ? rec.key : load.loader.GameData.workshopKey;
			WorkshopClient.Instance.UpdateSave(wid, wkey,
				load.loader.Path, title, author, desc, coverJpg, (id, key, err) =>
				{
					if (err != null) { SetWsStatus(err); return; }
					if (!string.IsNullOrEmpty(key))
						WorkshopLocal.Put(load.loader.Path, id > 0 ? id : wid, key);
					load.loader.GameData.workshopId = id > 0 ? id : wid;
					load.loader.GameData.workshopKey = "";
					load.loader.WriteToFile();
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
				load.loader.WriteToFile();
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
		WorkshopLocalRec rec;
		WorkshopLocal.TryGet(load.loader.Path, out rec);
		int wid = rec != null ? rec.id : load.loader.GameData.workshopId;
		string wkey = rec != null ? rec.key : load.loader.GameData.workshopKey;
		WorkshopClient.Instance.DeleteSave(wid, wkey, err =>
		{
			if (err != null) { SetWsStatus(err); return; }
			WorkshopLocal.Remove(load.loader.Path);
			load.loader.GameData.workshopId = 0;
			load.loader.GameData.workshopKey = "";
			load.loader.WriteToFile();
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
		if (load == null || load.loader == null) return false;
		WorkshopLocalRec rec;
		return WorkshopLocal.TryGet(load.loader.Path, out rec);
	}

	private int OwnerId()
	{
		WorkshopLocalRec rec;
		if (load != null && load.loader != null && WorkshopLocal.TryGet(load.loader.Path, out rec))
			return rec.id;
		return 0;
	}

	private void RefreshWorkshopButtons()
	{
		bool logged = AccountManager.IsLoggedIn();
		bool owner = IsOwner();
		// Кнопка панели публикации видна, только если:
		//  - вошли в аккаунт, И
		//  - сейв свой (локальный не-скачанный ИЛИ мы его владелец).
		bool canPublish = logged && CanPublish();

		// Кнопки открытия панели публикации (панель файлов):
		// Upload — для невыложенных, Update/Delete — для выложенных.
		if (uploadButton != null) uploadButton.SetActive(canPublish && !owner);
		if (updateButton != null) updateButton.SetActive(canPublish && owner);
		if (deleteWorkshopButton != null) deleteWorkshopButton.SetActive(canPublish && owner);

		// Кнопки внутри панели публикации.
		var action = FindPublishChild("PublishAction");
		var del = FindPublishChild("PublishDelete");
		var close = FindPublishChild("PublishClose");
		if (action != null)
		{
			// Одна кнопка = и Upload и Update: меняем подпись по статусу владельца.
			action.gameObject.SetActive(canPublish);
			var tx = action.GetComponentInChildren<Text>();
			if (tx != null) tx.text = owner ? "Update" : "Upload";
		}
		if (del != null) del.gameObject.SetActive(canPublish && owner);
		if (close != null) close.gameObject.SetActive(canPublish);

		// На всякий случай управляем и «каноническими» именами,
		// если в других сценах/префабах кнопки названы Upload/Update/Delete.
		ToggleNamed(publishPanel != null ? publishPanel.transform : transform, "Upload", canPublish && !owner);
		ToggleNamed(publishPanel != null ? publishPanel.transform : transform, "Update", canPublish && owner);
		ToggleNamed(publishPanel != null ? publishPanel.transform : transform, "Delete", canPublish && owner);
		ToggleNamed(publishPanel != null ? publishPanel.transform : transform, "DeleteWorkshop", canPublish && owner);
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

	/// <summary>
	/// Включает горизонтальную раскладку кнопок панели публикации,
	/// чтобы при скрытии одной кнопки остальные съезжались без пустот.
	/// </summary>
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

	#region Account UI (programmatic, no scene edits needed)

	private GameObject accountForm;
	private Text accountBtnText;

	private void CreateAccountButton()
	{
		if (transform.Find("AccountBtn") != null) return;

		var btn = new GameObject("AccountBtn", typeof(RectTransform), typeof(Image), typeof(Button));
		btn.transform.SetParent(transform, false);
		var rt = btn.GetComponent<RectTransform>();
		rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
		rt.pivot = new Vector2(1f, 1f);
		rt.anchoredPosition = new Vector2(-10f, -10f);
		rt.sizeDelta = new Vector2(150f, 30f);
		btn.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

		var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
		label.transform.SetParent(btn.transform, false);
		var tx = label.GetComponent<Text>();
		tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		tx.alignment = TextAnchor.MiddleCenter;
		tx.color = Color.white;
		tx.fontSize = 14;
		accountBtnText = tx;

		btn.GetComponent<Button>().onClick.AddListener(OnAccountButton);
		RefreshAccountButton();
	}

	private void RefreshAccountButton()
	{
		if (accountBtnText == null) return;
		accountBtnText.text = AccountManager.IsLoggedIn()
			? AccountManager.CurrentUser + "  ·  Logout"
			: "Login / Register";
	}

	private void OnAccountButton()
	{
		if (AccountManager.IsLoggedIn())
		{
			AccountManager.Logout();
			if (accountForm != null) accountForm.SetActive(false);
			RefreshAccountButton();
			RefreshWorkshopButtons();
			return;
		}

		if (accountForm == null) CreateAccountForm();
		accountForm.SetActive(!accountForm.activeSelf);
	}

	private void CreateAccountForm()
	{
		var form = new GameObject("AccountForm", typeof(RectTransform), typeof(Image));
		form.transform.SetParent(transform, false);
		var rt = form.GetComponent<RectTransform>();
		rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
		rt.pivot = new Vector2(1f, 1f);
		rt.anchoredPosition = new Vector2(-10f, -46f);
		rt.sizeDelta = new Vector2(230f, 128f);
		form.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.98f);
		form.SetActive(false);
		accountForm = form;

		var nick = CreateField(form.transform, "NickField", "nick", false, new Vector2(-90f, 48f));
		var pass = CreateField(form.transform, "PassField", "password", true, new Vector2(-90f, 16f));

		var loginBtn = CreateTextButton(form.transform, "LoginBtn", "Login", new Vector2(-52f, -24f));
		var regBtn = CreateTextButton(form.transform, "RegBtn", "Register", new Vector2(52f, -24f));

		loginBtn.onClick.AddListener(() =>
		{
			if (AccountManager.Login(nick.text, pass.text))
			{
				pass.text = "";
				form.SetActive(false);
				RefreshAccountButton();
				RefreshWorkshopButtons();
			}
			else
			{
				SetWsStatus("Bad login or account not found");
			}
		});

		regBtn.onClick.AddListener(() =>
		{
			if (AccountManager.Register(nick.text, pass.text))
			{
				pass.text = "";
				form.SetActive(false);
				RefreshAccountButton();
				RefreshWorkshopButtons();
			}
			else
			{
				SetWsStatus("Name taken or invalid (2-24 chars)");
			}
		});
	}

	private static InputField CreateField(Transform parent, string name, string placeholder, bool password, Vector2 pos)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
		go.transform.SetParent(parent, false);
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.anchoredPosition = pos;
		rt.sizeDelta = new Vector2(200f, 26f);
		go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.9f);

		var input = go.GetComponent<InputField>();
		var ph = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
		ph.transform.SetParent(go.transform, false);
		var phTx = ph.GetComponent<Text>();
		phTx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		phTx.fontSize = 13;
		phTx.color = new Color(0.3f, 0.3f, 0.3f);
		phTx.text = placeholder;

		var txt = new GameObject("Text", typeof(RectTransform), typeof(Text));
		txt.transform.SetParent(go.transform, false);
		var tx = txt.GetComponent<Text>();
		tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		tx.fontSize = 13;
		tx.color = Color.black;

		input.textComponent = tx;
		input.placeholder = phTx;
		if (password) input.contentType = InputField.ContentType.Password;
		return input;
	}

	private static Button CreateTextButton(Transform parent, string name, string label, Vector2 pos)
	{
		var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, false);
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.anchoredPosition = pos;
		rt.sizeDelta = new Vector2(96f, 28f);
		go.GetComponent<Image>().color = new Color(1f, 0.53f, 0f);

		var txt = new GameObject("Label", typeof(RectTransform), typeof(Text));
		txt.transform.SetParent(go.transform, false);
		var tx = txt.GetComponent<Text>();
		tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		tx.alignment = TextAnchor.MiddleCenter;
		tx.fontSize = 13;
		tx.color = Color.black;
		tx.text = label;

		return go.GetComponent<Button>();
	}

	#endregion

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
		// EditorUtility существует только в редакторе (UNITY_STANDALONE его не имеет).
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
