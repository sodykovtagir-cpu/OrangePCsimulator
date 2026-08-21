using System.IO;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_STANDALONE || UNITY_EDITOR
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
		coverJpg = null;
		RefreshWorkshopButtons();
		if (publishPanel != null) publishPanel.SetActive(false);
		FillPublishForm(null);
		EnsureWorkshopClient();
	}

	public void OpenPublishPanel()
	{
		if (publishPanel != null) publishPanel.SetActive(true);
		ShowPublishFields();
		FillPublishForm(null);
		SetWsStatus(IsPublished() ? "Loading listing..." : "New upload");
		if (!IsPublished()) return;
		EnsureWorkshopClient();
		int id = load.loader.GameData.workshopId;
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
		if (IsPublished())
		{
			WorkshopClient.Instance.UpdateSave(load.loader.GameData.workshopId, load.loader.GameData.workshopKey,
				load.loader.Path, title, author, desc, coverJpg, (id, key, err) =>
				{
					if (err != null) { SetWsStatus(err); return; }
					SetWsStatus("Updated");
					RefreshWorkshopButtons();
				});
		}
		else
		{
			WorkshopClient.Instance.Upload(load.loader.Path, title, author, desc, coverJpg, (id, key, err) =>
			{
				if (err != null) { SetWsStatus(err); return; }
				load.loader.GameData.workshopId = id;
				load.loader.GameData.workshopKey = key;
				load.loader.WriteToFile();
				SetWsStatus("Published #" + id);
				RefreshWorkshopButtons();
			});
		}
	}

	public void DeleteFromWorkshop()
	{
		if (!IsPublished()) return;
		EnsureWorkshopClient();
		SetWsStatus("Deleting...");
		WorkshopClient.Instance.DeleteSave(load.loader.GameData.workshopId, load.loader.GameData.workshopKey, err =>
		{
			if (err != null) { SetWsStatus(err); return; }
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

	private bool IsPublished()
	{
		return load != null && load.loader != null && load.loader.GameData != null
			&& load.loader.GameData.workshopId > 0
			&& !string.IsNullOrEmpty(load.loader.GameData.workshopKey);
	}

	private void RefreshWorkshopButtons()
	{
		bool pub = IsPublished();
		if (uploadButton != null) uploadButton.SetActive(!pub);
		if (updateButton != null) updateButton.SetActive(pub);
		if (deleteWorkshopButton != null) deleteWorkshopButton.SetActive(pub);
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
#if UNITY_STANDALONE || UNITY_EDITOR
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
