using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Linq;
using PC.Component.Software.Lua;

namespace PC.Component.Software
{
    public class FileManager : App
    {
        private struct FileBlock
        {
            public Image block;
            public Text text;
            public bool isFolder;
        }

        private float lastClickTime;
        private int lastClickedIndex = -1;
        private const float doubleClickDelay = 0.3f;

        [SerializeField]
        private Button createFolderButton;

        [SerializeField]
        private Button storagePrefab;

        [SerializeField]
        private Button filePrefab;

        [SerializeField]
        private Button folderPrefab;

        [SerializeField]
        private Transform storageParent;

        [SerializeField]
        private Transform fileParent;

        [SerializeField]
        private InputField fileNameInput;

        [SerializeField]
        private Button cutButton;

        [SerializeField]
        private Button copyButton;

        [SerializeField]
        private Button pasteButton;

        [SerializeField]
        private Button renameButton;

        [SerializeField]
        private Button deleteButton;

        [SerializeField]
        private Button backButton;

        [SerializeField]
        private Text currentPathText;

        [SerializeField]
        private Toggle hiddenToggle;

        [SerializeField]
        private Color selectedColor;

        [SerializeField]
        private Color folderColor = new Color(1f, 0.9f, 0.6f);

        private string startFolderPath;

        private int selectedFile = -1;

        private Storage selectedStorage;

        private List<FileBlock> fileBlocks = new List<FileBlock>();

        private string extension;

        private string currentFolder = "";

        public string CurrentFolder => currentFolder ?? "";

        private readonly string[] systemFolders = new string[] { "System" };

        protected override void Start()
        {
            if (createFolderButton != null)
                createFolderButton.onClick.AddListener(CreateFolder);
            base.Start();

            var os = system;
            if (os == null || os.AllStorage == null) return;

            os.TakeResource();

            var storages = os.AllStorage;

            for (int i = 0; i < storages.Count; i++)
            {
                var storage = storages[i];
                if (storage == null) continue;

                if (i != 0 && !string.IsNullOrEmpty(storage.password)) continue;

                var button = Instantiate(storagePrefab, storageParent);
                int index = i;
                button.onClick.AddListener(() => SelectStorage(index));

                var text = button.GetComponentInChildren<Text>();
                if (text != null) text.text = i + ". " + storage.storageName;
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(GoBack);
                backButton.interactable = false;
            }

            SelectStorage(0);
            InitializeSystemFolders();
            EnsureExplorerPane();

            if (!string.IsNullOrEmpty(startFolderPath))
            {
                currentFolder = startFolderPath;
                UpdatePathText();
                RefreshItem();
            }
        }

        public void RefreshView()
        {
            RefreshItem();
            UpdatePasteButton();
        }

        private void EnsureExplorerPane()
        {
            if (fileParent == null) return;

            if (fileParent.GetComponent<Graphic>() == null)
            {
                var img = fileParent.gameObject.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0f);
                img.raycastTarget = true;
            }

            var pane = fileParent.GetComponent<ExplorerPane>();
            if (pane == null)
                pane = fileParent.gameObject.AddComponent<ExplorerPane>();
            pane.Init(this);
        }

        private void Update()
        {
            UpdatePasteButton();

            if (!Input.GetMouseButtonDown(1))
                return;

            if (IsPointerOverEmptyPane(Input.mousePosition))
            {
                var menu = DesktopContextMenu.For(this);
                if (menu != null) menu.ShowExplorerMenu(this, Input.mousePosition);
            }
        }

        private bool IsPointerOverEmptyPane(Vector2 screenPos)
        {
            if (fileParent == null || EventSystem.current == null)
                return false;

            var results = new List<RaycastResult>();
            var pointer = new PointerEventData(EventSystem.current);
            pointer.position = screenPos;
            EventSystem.current.RaycastAll(pointer, results);

            bool overParent = false;
            bool overChild = false;
            for (int i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                if (go == null) continue;
                var hitOs = go.GetComponentInParent<PC.Component.Software.OS.OperatingSystem>();
                if (hitOs != null && hitOs != system) continue;
                if (go.transform == fileParent)
                    overParent = true;
                else if (go.transform.IsChildOf(fileParent))
                    overChild = true;
            }

            return overParent && !overChild;
        }

        private void UpdatePasteButton()
        {
            if (pasteButton != null)
                pasteButton.interactable = system != null && system.HasClipboard;
        }

        private void CreateFolder()
        {
            if (system == null) return;
            system.CreateFolderAt(currentFolder, "New Folder");
        }

        private void InitializeSystemFolders()
        {
            var storage = selectedStorage;
            if (storage == null || storage.files == null) return;

            foreach (var folder in systemFolders)
            {
                bool exists = storage.files.Any(f => f != null && f.path == folder && f.isFolder);
                if (!exists)
                {
                    var folderFile = new File(folder, "", false, 0);
                    folderFile.isFolder = true;
                    storage.AddFile(folderFile);
                }
            }

            RefreshItem();
        }

        private void SelectStorage(int index)
        {
            var os = system;
            if (os == null || os.AllStorage == null) return;
            if (index < 0 || index >= os.AllStorage.Count) return;

            selectedStorage = os.AllStorage[index];
            currentFolder = "";
            UpdatePathText();
            RefreshItem();
        }

        private void RefreshItem()
        {
            if (fileParent == null) return;

            foreach (Transform child in fileParent)
                Destroy(child.gameObject);

            if (fileBlocks != null) fileBlocks.Clear();

            var storage = selectedStorage;
            if (storage == null || storage.files == null)
            {
                SelectFile(-1);
                return;
            }

            var files = GetFilesInCurrentFolder();

            for (int index = 0; index < files.Count; index++)
            {
                var file = files[index];
                if (file == null) continue;

                var prefab = file.isFolder ? folderPrefab : filePrefab;
                if (prefab == null) prefab = filePrefab;

                var button = Instantiate(prefab, fileParent);
                var itemHook = button.gameObject.GetComponent<ExplorerFileItem>();
                if (itemHook == null)
                    itemHook = button.gameObject.AddComponent<ExplorerFileItem>();
                itemHook.Init(file, this);
                int capturedIndex = index;

                if (file.isFolder)
                {
                    button.onClick.AddListener(() => OnItemClicked(capturedIndex));
                }
                else
                {
                    button.onClick.AddListener(() => SelectFile(capturedIndex));
                }

                var text = button.GetComponentInChildren<Text>();
                var displayName = GetDisplayName(file.path);

                if (text != null)
                {
                    text.text = file.isFolder ? "📁 " + displayName : displayName;
                }

                if (text != null)
                {
                    if (file.isFolder)
                    {
                        text.color = folderColor;
                    }
                    else
                    {
                        float v = file.hidden ? 0.5f : 0f;
                        text.color = new Color(v, v, v, 1f);
                    }
                }

                if (fileBlocks != null)
                {
                    var image = button.GetComponent<Image>();
                    fileBlocks.Add(new FileBlock { block = image, text = text, isFolder = file.isFolder });
                }
            }

            SelectFile(-1);
            UpdateBackButton();
            UpdatePasteButton();
        }

        private void OnItemClicked(int index)
        {
            if (lastClickedIndex == index && Time.time - lastClickTime < doubleClickDelay)
            {
                var file = GetFilesInCurrentFolder()[index];
                if (file != null && file.isFolder)
                {
                    OpenFolder(index);
                    lastClickedIndex = -1;
                    return;
                }
            }

            SelectFile(index);

            lastClickedIndex = index;
            lastClickTime = Time.time;
        }

        private List<File> GetFilesInCurrentFolder()
        {
            var storage = selectedStorage;
            if (storage == null || storage.files == null) return new List<File>();

            var result = new List<File>();

            foreach (var file in storage.files)
            {
                if (file == null) continue;

                string fileFolder = GetFolderPath(file.path);
                if (fileFolder == currentFolder)
                    result.Add(file);
            }

            return result.OrderByDescending(f => f.isFolder).ThenBy(f => f.path).ToList();
        }

        private string GetFolderPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";

            int lastSlash = path.LastIndexOf('/');
            if (lastSlash == -1) return "";

            return path.Substring(0, lastSlash);
        }

        private string GetDisplayName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";

            int lastSlash = path.LastIndexOf('/');
            if (lastSlash == -1) return path;

            return path.Substring(lastSlash + 1);
        }

        private bool IsProtectedFile(File file)
        {
            if (file == null) return false;
            if (file.isFolder && systemFolders.Contains(GetDisplayName(file.path))) return true;

            var ext = File.Extension(file.path);
            if (ext == ".exe")
            {
                if (LuaAppPackage.IsPackage(file.content)) return false;
                return true;
            }
            return file.path == "System/boot.bin" || file.hidden;
        }

        private void OpenFolder(int index)
        {
            var files = GetFilesInCurrentFolder();
            if (index < 0 || index >= files.Count) return;

            var file = files[index];
            if (file == null || !file.isFolder) return;

            currentFolder = file.path;
            UpdatePathText();
            RefreshItem();
        }

        private void GoBack()
        {
            if (string.IsNullOrEmpty(currentFolder)) return;

            int lastSlash = currentFolder.LastIndexOf('/');
            currentFolder = lastSlash == -1 ? "" : currentFolder.Substring(0, lastSlash);

            UpdatePathText();
            RefreshItem();
        }

        private void UpdateBackButton()
        {
            if (backButton != null)
                backButton.interactable = !string.IsNullOrEmpty(currentFolder);
        }

        private void UpdatePathText()
        {
            if (currentPathText != null)
            {
                string storageName = selectedStorage != null ? selectedStorage.storageName : "Storage";
                string path = string.IsNullOrEmpty(currentFolder) ? "/" : "/" + currentFolder;
                currentPathText.text = storageName + path;
            }
        }

        private void SelectFile(int index)
        {
            if (fileBlocks == null) return;

            if (selectedFile == index) index = -1;
            selectedFile = index;

            for (int i = 0; i < fileBlocks.Count; i++)
            {
                var fb = fileBlocks[i];
                if (fb.block != null) fb.block.color = Color.white;
            }

            if (selectedFile == -1)
            {
                if (cutButton != null) cutButton.interactable = false;
                if (copyButton != null) copyButton.interactable = false;
                if (renameButton != null) renameButton.interactable = false;
                if (deleteButton != null) deleteButton.interactable = false;
                if (hiddenToggle != null) hiddenToggle.interactable = false;
                return;
            }

            if (selectedFile < 0 || selectedFile >= fileBlocks.Count) return;

            var selectedBlock = fileBlocks[selectedFile];
            if (selectedBlock.block != null) selectedBlock.block.color = selectedColor;

            var file = GetSelectedFile();
            if (file == null)
            {
                if (cutButton != null) cutButton.interactable = false;
                if (copyButton != null) copyButton.interactable = false;
                if (renameButton != null) renameButton.interactable = false;
                if (deleteButton != null) deleteButton.interactable = false;
                if (hiddenToggle != null) hiddenToggle.interactable = false;
                return;
            }

            bool protect = IsProtectedFile(file);

            if (cutButton != null) cutButton.interactable = !protect;
            if (copyButton != null) copyButton.interactable = system != null && system.CanCopyFile(file);
            if (renameButton != null) renameButton.interactable = !protect;
            if (deleteButton != null) deleteButton.interactable = !protect;

            if (hiddenToggle != null)
            {
                hiddenToggle.interactable = !file.isFolder;
                hiddenToggle.isOn = file.hidden;
            }
        }

        public void OnValueChangedHidden(bool hidden)
        {
            var file = GetSelectedFile();
            if (file == null || file.isFolder) return;

            file.hidden = hidden;
            if (system != null) system.RefreshDesktopIcon();

            if (fileBlocks == null || selectedFile < 0 || selectedFile >= fileBlocks.Count) return;

            var block = fileBlocks[selectedFile];
            if (block.text != null)
            {
                float v = hidden ? 0.5f : 0f;
                block.text.color = new Color(v, v, v, 1f);
            }
        }

        public void Cut()
        {
            var file = GetSelectedFile();
            if (file == null || system == null) return;
            system.CutToClipboard(file);
            UpdatePasteButton();
        }

        public void Copy()
        {
            var file = GetSelectedFile();
            if (file == null || system == null || !system.CanCopyFile(file)) return;
            system.CopyToClipboard(file);
            UpdatePasteButton();
        }

        public void Paste()
        {
            if (system == null) return;
            system.PasteClipboard(currentFolder);
        }

        public void Rename()
        {
            if (fileNameInput == null) return;

            var go = fileNameInput.gameObject;
            if (go == null) return;

            go.SetActive(true);

            var file = GetSelectedFile();
            if (file == null) return;

            var displayName = GetDisplayName(file.path);

            if (file.isFolder)
            {
                fileNameInput.text = displayName;
                extension = "";
            }
            else
            {
                fileNameInput.text = File.NameWithoutExtension(displayName);
                extension = File.Extension(displayName);
            }

            fileNameInput.ActivateInputField();
        }

        public void ApplyRename()
        {
            if (fileNameInput == null) return;

            var inputGo = fileNameInput.gameObject;
            if (inputGo == null) return;

            inputGo.SetActive(false);

            var file = GetSelectedFile();
            if (file == null || system == null) return;

            system.RenameUserFile(file, fileNameInput.text + extension);
        }

        public void Delete()
        {
            var file = GetSelectedFile();
            if (file == null || system == null) return;
            system.DeleteUserFile(file);
        }

        private File GetSelectedFile()
        {
            var files = GetFilesInCurrentFolder();
            if (selectedFile < 0 || selectedFile >= files.Count) return null;
            return files[selectedFile];
        }

        public void OpenFolderFromPath(string path)
        {
            startFolderPath = path ?? "";
            if (selectedStorage == null) return;

            currentFolder = startFolderPath;
            UpdatePathText();
            RefreshItem();
        }

        public override void Close()
        {
            var os = system;
            if (os != null)
            {
                os.ReleaseResource();
                base.Close();
            }
        }
    }

    public class ExplorerPane : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private FileManager explorer;
        private float pointerDownTime;
        private Vector2 pointerDownPos;
        private bool isPointerDown;

        public void Init(FileManager fileManager)
        {
            explorer = fileManager;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!PointerInput.IsPrimary(eventData))
                return;

            PointerInput.ConsumedClick = false;
            isPointerDown = true;
            pointerDownTime = Time.unscaledTime;
            pointerDownPos = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPointerDown = false;
        }

        private void Update()
        {
            if (!isPointerDown || explorer == null)
                return;

            if (Time.unscaledTime - pointerDownTime <= PointerInput.LongPress)
                return;
            if (Vector2.Distance(PointerInput.ScreenPosition(), pointerDownPos) >= PointerInput.Slop)
                return;

            isPointerDown = false;
            PointerInput.ConsumedClick = true;
            var menu = DesktopContextMenu.For(explorer != null ? (Component)explorer : this);
            if (menu != null)
                menu.ShowExplorerMenu(explorer, pointerDownPos);
        }
    }

    public class ExplorerFileItem : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        private File file;
        private float pointerDownTime;
        private Vector2 pointerDownPos;
        private bool isPointerDown;
        private bool openedMenu;

        public File File => file;

        public void Init(File target, FileManager unused)
        {
            file = target;
            openedMenu = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!PointerInput.IsPrimary(eventData))
                return;

            PointerInput.ConsumedClick = false;
            isPointerDown = true;
            openedMenu = false;
            pointerDownTime = Time.unscaledTime;
            pointerDownPos = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPointerDown = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || file == null) return;
            if (eventData.button != PointerEventData.InputButton.Right) return;
            openedMenu = true;
            var menu = DesktopContextMenu.For(this);
            if (menu != null)
                menu.ShowFileMenu(file, eventData.position);
        }

        private void Update()
        {
            if (!isPointerDown || openedMenu || file == null) return;
            if (Time.unscaledTime - pointerDownTime <= PointerInput.LongPress) return;
            if (Vector2.Distance(PointerInput.ScreenPosition(), pointerDownPos) >= PointerInput.Slop) return;

            openedMenu = true;
            isPointerDown = false;
            PointerInput.ConsumedClick = true;
            var menu = DesktopContextMenu.For(this);
            if (menu != null)
                menu.ShowFileMenu(file, pointerDownPos);
        }
    }
}
