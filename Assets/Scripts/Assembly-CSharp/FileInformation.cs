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
		AccountPanel.EnsureOnScene();
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
		else if (load.loader.GameData.workshopId <= 0)
		{
			// Новый сейв на старом пути не наследует чужой листинг мастерской.
			WorkshopLocal.Remove(load.loader.Path);
		}
		coverJpg = null;
		RefreshWorkshopButtons();
		if (publishPanel != null) publishPanel.SetActive(false);
		FillPublishForm(null);
		EnsureWorkshopClient();
		EnsurePublishLayout();
		AccountPanel.RefreshAll();
	}

	public void OpenPublishPanel()
	{
		if (publishPanel != null) publishPanel.SetActive(true);
		ShowPublishFields();
		FillPublishForm(null);
		RefreshWorkshopButtons();
		EnsureWorkshopClient();
		if (!ServerAccounts.LoggedIn)
		{
			SetWsStatus("Login to publish");
			return;
		}

		int id = OwnerId();
		SetWsStatus(id > 0 ? "Checking listing..." : "New upload");
		WorkshopClient.Instance.ListSaves((list, err) =>
		{
			if (err != null) { SetWsStatus(err); return; }
			WorkshopItem found = null;
			if (id > 0 && list != null)
			{
				for (int i = 0; i < list.Count; i++)
					if (list[i] != null && list[i].id == id) { found = list[i]; break; }
			}
			if (id > 0 && found == null)
			{
				ForgetListing();
				FillPublishForm(null);
				SetWsStatus("Listing gone. Publish again as new.");
				RefreshWorkshopButtons();
				return;
			}
			FillPublishForm(found);
			SetWsStatus(found != null ? "Edit and press Update" : "New upload");
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

	private void ForgetListing()
	{
		if (load == null || load.loader == null || load.loader.GameData == null) return;
		WorkshopLocal.Remove(load.loader.Path);
		load.loader.GameData.workshopId = 0;
		load.loader.GameData.workshopKey = "";
		load.loader.WriteToFile();
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
		string author = ServerAccounts.LoggedIn ? ServerAccounts.Name : PlayerPrefs.GetString("WorkshopAuthor", "Player");
		string desc = "";
		if (!string.IsNullOrEmpty(g.workshopTitle)) title = g.workshopTitle;
		if (!string.IsNullOrEmpty(g.workshopDesc)) desc = g.workshopDesc;
		if (remote != null)
		{
			if (!string.IsNullOrEmpty(remote.title)) title = remote.title;
			if (remote.description != null) desc = remote.description;
		}
		if (wsTitle != null) wsTitle.text = title ?? "";
		if (wsAuthor != null)
		{
			wsAuthor.text = string.IsNullOrEmpty(author) ? "Player" : author;
			wsAuthor.interactable = !ServerAccounts.LoggedIn;
		}
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
		if (!ServerAccounts.LoggedIn)
		{
			SetWsStatus("Login to publish");
			return;
		}
		EnsureWorkshopClient();
		string title = wsTitle != null ? wsTitle.text : load.loader.GameData.roomName;
		string author = ServerAccounts.LoggedIn ? ServerAccounts.Name : (wsAuthor != null ? wsAuthor.text : "Player");
		string desc = wsDesc != null ? wsDesc.text : "";
		if (!ServerAccounts.LoggedIn && !string.IsNullOrEmpty(author))
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
		return WorkshopLocal.TryGetForSave(load.loader.Path, load.loader.GameData.workshopId, out rec);
	}

	private int OwnerId()
	{
		WorkshopLocalRec rec;
		if (load != null && load.loader != null && load.loader.GameData != null
			&& WorkshopLocal.TryGetForSave(load.loader.Path, load.loader.GameData.workshopId, out rec))
			return rec.id;
		return load != null && load.loader != null && load.loader.GameData != null
			? load.loader.GameData.workshopId : 0;
	}

	private void RefreshWorkshopButtons()
	{
		bool logged = ServerAccounts.LoggedIn;
		bool owner = IsOwner();
		// Кнопка панели публикации видна, только если:
		//  - вошли в аккаунт, И
		//  - сейв свой (локальный не-скачанный ИЛИ мы его владелец).
		bool canPublish = logged && CanPublish();

		// Кнопки открытия панели публикации (панель файлов).
		// Если Update не назначен — одна кнопка Upload меняет текст.
		if (updateButton == null)
		{
			if (uploadButton != null)
			{
				uploadButton.SetActive(canPublish);
				SetButtonLabel(uploadButton, owner ? "[Update]" : "[Upload]");
			}
		}
		else
		{
			if (uploadButton != null)
			{
				uploadButton.SetActive(canPublish && !owner);
				SetButtonLabel(uploadButton, "[Upload]");
			}
			updateButton.SetActive(canPublish && owner);
			SetButtonLabel(updateButton, "[Update]");
		}
		if (deleteWorkshopButton != null) deleteWorkshopButton.SetActive(canPublish && owner);

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

		ToggleNamed(publishPanel != null ? publishPanel.transform : transform, "Upload", canPublish && !owner);
		ToggleNamed(publishPanel != null ? publishPanel.transform : transform, "Update", canPublish && owner);
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
			string n = b.gameObject.name.ToLowerInvariant();
			var tx = b.GetComponent<Text>();
			if (tx == null) tx = b.GetComponentInChildren<Text>(true);
			string t = tx != null && tx.text != null ? tx.text.ToLowerInvariant() : "";
			bool isUpload = n.Contains("upload") || t.Contains("upload") || t.Contains("загруз");
			bool isUpdate = n.Contains("update") || t.Contains("update") || t.Contains("обнов");
			bool isDelete = (n.Contains("delete") && !n.Contains("file")) || t.Contains("удал") || t.Contains("[delete]");
			bool isClose = n.Contains("close") || t.Contains("закры") || t.Contains("[close]");
			if (isClose) { b.gameObject.SetActive(true); continue; }
			if (isDelete) { b.gameObject.SetActive(canPublish && owner); continue; }
			if (isUpdate && !isUpload) { b.gameObject.SetActive(canPublish && owner); continue; }
			if (isUpload && !isUpdate) { b.gameObject.SetActive(canPublish && !owner); continue; }
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

	private static void SetButtonLabel(GameObject go, string label)
	{
		if (go == null) return;
		var tx = go.GetComponent<Text>();
		if (tx == null) tx = go.GetComponentInChildren<Text>(true);
		if (tx != null) tx.text = label;
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
