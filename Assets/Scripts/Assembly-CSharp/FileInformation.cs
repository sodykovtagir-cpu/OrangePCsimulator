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
    [SerializeField] private InputField signInput;   // ✅ Новый input

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

        // ✅ Если в старом сохранении sign null — создаём пустую строку
        if (load.loader.GameData.sign == null)
            load.loader.GameData.sign = "";

        // Показываем значения
        nameInput.text = load.loader.GameData.roomName;
        signInput.text = load.loader.GameData.sign;
        signNameText.text = string.IsNullOrEmpty(load.loader.GameData.sign)
            ? "No Sign"
            : load.loader.GameData.sign;

        playtimeText.text =
            Localization.GetText("Playing Time") + ":\n" +
            (load.loader.GameData.playtime / 60f).ToString("0.00") + " min";

        fileLocationText.text = Path.GetFileName(load.loader.Path);
        coverJpg = null;
        RefreshWorkshopButtons();
        if (publishPanel != null) publishPanel.SetActive(false);
        if (wsTitle != null) wsTitle.text = load.loader.GameData.roomName;
        if (wsAuthor != null) wsAuthor.text = string.IsNullOrEmpty(load.loader.GameData.sign) ? "Player" : load.loader.GameData.sign;
        if (wsDesc != null) wsDesc.text = "";
        EnsureWorkshopClient();
    }

    public void OpenPublishPanel()
    {
        if (publishPanel != null) publishPanel.SetActive(true);
        SetWsStatus(IsPublished() ? "Update listing" : "Upload to workshop");
    }

    public void ClosePublishPanel()
    {
        if (publishPanel != null) publishPanel.SetActive(false);
    }

    public void PickCover()
    {
        NativeFilePicker.PickFile(path =>
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var tex = new Texture2D(2, 2);
                tex.LoadImage(File.ReadAllBytes(path));
                if (wsCover != null) wsCover.texture = tex;
                coverJpg = tex.EncodeToJPG(70);
                if (coverJpg != null && coverJpg.Length > 300000)
                    coverJpg = tex.EncodeToJPG(40);
            }
            catch
            {
                SetWsStatus("Bad image");
            }
        }, new[] { "image/*" });
    }

    public void ConfirmPublish()
    {
        if (load == null || load.loader == null) return;
        EnsureWorkshopClient();
        string title = wsTitle != null ? wsTitle.text : load.loader.GameData.roomName;
        string author = wsAuthor != null ? wsAuthor.text : "Player";
        string desc = wsDesc != null ? wsDesc.text : "";
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

    // ✅ Сохранение изменений
    public void ApplyEdit()
    {
        if (load == null || load.loader == null)
            return;

        load.loader.GameData.roomName = nameInput.text;
        load.loader.GameData.sign = signInput.text;  // ✅ Сохраняем sign

        load.loader.WriteToFile();

        fileMenu.RefreshLoadButton(load);
        menuManager.Back();
    }

    // ✅ Обновление текста таблички в реальном времени
    public void OnSignValueChanged(string value)
    {
        if (string.IsNullOrEmpty(value))
            signNameText.text = "No Sign";
        else
            signNameText.text = value;
    }

    // ✅ EXPORT
    public void Export()
    {
        if (load == null || load.loader == null)
            return;

#if UNITY_STANDALONE || UNITY_EDITOR

        string savePath = EditorUtility.SaveFilePanel(
            "Export Save File",
            "",
            Path.GetFileName(load.loader.Path),
            "sav");

        if (!string.IsNullOrEmpty(savePath))
        {
            File.Copy(load.loader.Path, savePath, true);
            Debug.Log("File exported to: " + savePath);
        }

#else

        NativeFilePicker.ExportFile(load.loader.Path, (success) =>
        {
            if (!success)
                messageBox?.Show("No permission to export the file.");
        });

#endif
    }

    public void AskDeleteMessage()
    {
        source?.PlayOneShot(warningSound);

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