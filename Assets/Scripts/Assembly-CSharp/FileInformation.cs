using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using PC.Component.Software;
public class FileInformation : MonoBehaviour
{
	[SerializeField]
	private InputField nameInput;

	[SerializeField]
	private Button applyButton;

	[SerializeField]
	private AudioSource source;

	[SerializeField]
	private AudioClip warningSound;

	[SerializeField]
	private Text playtimeText;

	[SerializeField]
	private Text fileLocationText;

	[SerializeField]
	private GameObject sign;

	[SerializeField]
	private Text signNameText;

	[SerializeField]
	private MessageBox messageBox;

	[SerializeField]
	private FileMenu fileMenu;

	[SerializeField]
	private GameObject exportButton;

	[SerializeField]
	private ConfirmationDialog deleteConfirmationDialog;

	private FileMenu.Load load;

	private string oldName;

	private MenuManager menuManager;

	private void Start()
	{
		menuManager = GetComponentInParent<MenuManager>();
		if (!NativeFilePicker.CanExportFiles())
			exportButton.gameObject.SetActive(false); 
	}

	public void Show(FileMenu.Load load)
	{
		this.load = load;
		sign.SetActive(load.loader.GameData.sign != "");
		signNameText.text = load.loader.GameData.sign != "" ? load.loader.GameData.sign : "-";
		nameInput.text = load.loader.GameData.roomName;
		// playtime
		playtimeText.text = string.Format("{0}:\n{1}", Localization.GetText("Playing Time"), (load.loader.GameData.playtime / 60f).ToString("0.00") + " min");
		fileLocationText.text = Path.GetFileName(load.loader.Path);
	}

	public void Export()
	{
		var l = load;
		if (l == null || l.loader == null) return;

		NativeFilePicker.FilesExportedCallback cb = success =>
		{
			if (success) return;
			var mb = messageBox;
			if (mb != null) mb.Show("No permission to open the file.");
		};

		NativeFilePicker.ExportFile(l.loader.Path, cb);
	}

	public void ApplyEdit()
	{
		
		load.loader.GameData.roomName = nameInput.text;
		load.loader.WriteToFile();
		fileMenu.RefreshLoadButton(load);
		menuManager.Back();
	}

	public void OnValueChangedName(string name)
	{
		nameInput.text = SceneSettings.CheckName(name);
		applyButton.interactable = load.loader.GameData.roomName != name || !string.IsNullOrEmpty(name) ? true : false;
	}

	public void AskDeleteMessage()
	{
		source.PlayOneShot(warningSound);
		deleteConfirmationDialog.Show(() =>
		{
			Delete();
		});
	}

	private void Delete()
	{
		fileMenu.DeleteLoadButton(load);
		menuManager.Back();
	}
}
