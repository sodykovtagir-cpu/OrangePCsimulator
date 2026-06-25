using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

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

        private bool copy;

        private int sourceFileIndex;

        private Storage sourceStorage;

        private string startFolderPath;

        private string sourceFolder;

        private int selectedFile = -1;

        private Storage selectedStorage;

        private List<FileBlock> fileBlocks = new List<FileBlock>();

        private string extension;

        private string currentFolder = "";

        // Системные папки
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
            
            if (!string.IsNullOrEmpty(startFolderPath))
            {
                currentFolder = startFolderPath;
                UpdatePathText();
                RefreshItem();
            }
        }

        private void CreateFolder()
        {
            if (selectedStorage == null) return;

            string baseName = "New Folder";
            string folderName = baseName;
            int counter = 1;

            while (selectedStorage.files.Any(f => f != null &&
                   f.path == (string.IsNullOrEmpty(currentFolder)
                   ? folderName
                   : currentFolder + "/" + folderName)))
            {
                folderName = baseName + " (" + counter + ")";
                counter++;
            }

            string newPath = string.IsNullOrEmpty(currentFolder)
                ? folderName
                : currentFolder + "/" + folderName;

            var folder = new File(newPath, "", false, 0);
            folder.isFolder = true;

            selectedStorage.AddFile(folder);

            RefreshItem();
        }

        private void InitializeSystemFolders()
        {
            var storage = selectedStorage;
            if (storage == null || storage.files == null) return;

            // Создаём системные папки если их нет
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

                bool protect = IsProtectedFile(file);

                if (text != null)
                {
                    if (file.isFolder)
                    {
                        text.color = folderColor;
                    }
                    else
                    {
                        float v = protect ? 0.5f : 0f;
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

                string filePath = file.path;
                string fileFolder = GetFolderPath(filePath);

                if (fileFolder == currentFolder)
                {
                    result.Add(file);
                }
            }

            // Сортируем: сначала папки, потом файлы
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
            return ext == ".exe" || file.path == "System/boot.bin" || file.hidden;
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
            {
                backButton.interactable = !string.IsNullOrEmpty(currentFolder);
            }
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
            if (copyButton != null) copyButton.interactable = !protect;
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
            copy = false;
            sourceFileIndex = selectedFile;
            sourceStorage = selectedStorage;
            sourceFolder = currentFolder;
            if (pasteButton != null) pasteButton.interactable = true;
        }

        public void Copy()
        {
            copy = true;
            sourceFileIndex = selectedFile;
            sourceStorage = selectedStorage;
            sourceFolder = currentFolder;
            if (pasteButton != null) pasteButton.interactable = true;
        }

        public void Paste()
        {
            var srcStorage = sourceStorage;
            if (srcStorage == null || srcStorage.files == null) return;

            var sourceFiles = GetFilesInFolder(srcStorage, sourceFolder);
            if (sourceFileIndex < 0 || sourceFileIndex >= sourceFiles.Count) return;

            var srcFile = sourceFiles[sourceFileIndex];
            var target = selectedStorage;
            if (srcFile == null || target == null) return;

            string newPath = string.IsNullOrEmpty(currentFolder)
                ? GetDisplayName(srcFile.path)
                : currentFolder + "/" + GetDisplayName(srcFile.path);

            if (!copy)
            {
                // Перемещение
                var actualFile = srcStorage.files.FirstOrDefault(f => f != null && f.path == srcFile.path);
                if (actualFile != null)
                {
                    srcStorage.files.Remove(actualFile);
                    string oldPath = actualFile.path;
                    actualFile.path = newPath;

                    // Если папка — обновляем пути вложенных файлов
                    if (actualFile.isFolder)
                    {
                        foreach (var f in srcStorage.files)
                        {
                            if (f == null) continue;
                            if (f.path.StartsWith(oldPath + "/"))
                            {
                                f.path = newPath + f.path.Substring(oldPath.Length);
                            }
                        }
                    }
                    target.AddFile(actualFile);
                }

                if (pasteButton != null) pasteButton.interactable = false;
                RefreshItem();
                return;
            }

            // Копирование
            var clone = new File(newPath, srcFile.content, srcFile.hidden, srcFile.size);
            clone.isFolder = srcFile.isFolder;

            target.AddFile(clone);
            RefreshItem();
        }

        private List<File> GetFilesInFolder(Storage storage, string folder)
        {
            if (storage == null || storage.files == null) return new List<File>();

            var result = new List<File>();
            foreach (var file in storage.files)
            {
                if (file == null) continue;
                string fileFolder = GetFolderPath(file.path);
                if (fileFolder == folder)
                {
                    result.Add(file);
                }
            }

            return result.OrderByDescending(f => f.isFolder).ThenBy(f => f.path).ToList();
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
                var nameWithoutExt = File.NameWithoutExtension(displayName);
                fileNameInput.text = nameWithoutExt;
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
            if (file == null) return;

            Storage storage = selectedStorage;   // ✅ ОБЯЗАТЕЛЬНО
            if (storage == null || storage.files == null) return;

            var newName = fileNameInput.text + extension;

            string newPath = string.IsNullOrEmpty(currentFolder)
                ? newName
                : currentFolder + "/" + newName;

            var actualFile = storage.files.FirstOrDefault(f => f != null && f.path == file.path);
            if (actualFile == null) return;

            string oldPath = actualFile.path;
            actualFile.path = newPath;

            // ✅ если папка — обновляем вложенные пути
            if (actualFile.isFolder)
            {
                foreach (var f in storage.files)
                {
                    if (f == null) continue;

                    if (f.path.StartsWith(oldPath + "/"))
                    {
                        f.path = newPath + f.path.Substring(oldPath.Length);
                    }
                }
            }

            RefreshItem();
        }

        public void Delete()
        {
            var file = GetSelectedFile();
            if (file == null) return;

            var storage = selectedStorage;
            if (storage == null || storage.files == null) return;

            var actualFile = storage.files.FirstOrDefault(f => f != null && f.path == file.path);
            if (actualFile == null) return;

            // Если это папка, удаляем все файлы внутри
            if (actualFile.isFolder)
            {
                var filesToDelete = storage.files.Where(f =>
                    f != null && f.path.StartsWith(actualFile.path + "/")).ToList();

                foreach (var f in filesToDelete)
                {
                    storage.files.Remove(f);
                }
            }

            storage.files.Remove(actualFile);

            if (fileBlocks != null && selectedFile >= 0 && selectedFile < fileBlocks.Count)
            {
                var fb = fileBlocks[selectedFile];
                if (fb.block != null) Destroy(fb.block.gameObject);
            }

            RefreshItem();
        }

        private File GetSelectedFile()
        {
            var files = GetFilesInCurrentFolder();
            if (selectedFile < 0 || selectedFile >= files.Count) return null;
            return files[selectedFile];
        }

        public void OpenFolderFromPath(string path)
        {
            startFolderPath = path;
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
}