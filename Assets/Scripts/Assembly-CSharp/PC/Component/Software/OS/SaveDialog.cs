using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.OS
{
    /// <summary>Shared PCOS Save As picker for user-created documents.</summary>
    public class SaveDialog : MonoBehaviour
    {
        [SerializeField] private InputField fileNameInput;
        [SerializeField] private OperatingSystem system;
        [SerializeField] private Text extensionText;

        private string content;
        private string[] extensions;
        private int index;
        private int currentDisk;
        private string currentFolder = "";
        private Action<File> saved;
        private bool administrator;
        [SerializeField] private ListView listView;
        [SerializeField] private Text locationText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text saveButtonText;
        private bool pickerInitialized;
        private readonly List<Entry> entries = new List<Entry>();
        private string pendingReplace;
        private int pendingDisk = -1;
        private const string LastLocationPath = "System/SaveDialog.location";

        [Serializable]
        private sealed class LastLocation
        {
            public int diskId;
            public string folder;
        }

        private sealed class Entry
        {
            public File file;
            public int disk;
            public bool back;
        }

        // All of the Save As layout is authored in PCOS.prefab. Only the
        // callbacks and selection tint are installed at runtime; never clone
        // the Open picker or override any RectTransform sizes here.
        private bool EnsurePicker()
        {
            if (listView == null || fileNameInput == null || locationText == null ||
                messageText == null || saveButtonText == null) return false;
            if (pickerInitialized) return true;
            listView.SetSelectionTint(new Color(.72f, .72f, .72f, 1f));
            listView.SelectedIndexChanged += SelectedIndexChanged;
            fileNameInput.onValueChanged.AddListener(_ => ClearConfirmation());
            pickerInitialized = true;
            return true;
        }

        private void LoadLastLocation()
        {
            File marker;
            var fm = system != null ? system.FileManager : null;
            var all = system != null ? system.AllStorage : null;
            if (fm == null || all == null ||
                !fm.TryGetFile(0, LastLocationPath, out marker) || marker == null) return;
            try
            {
                var saved = JsonUtility.FromJson<LastLocation>(marker.content ?? "");
                if (saved == null) return;
                for (int d = 0; d < all.Count; d++)
                {
                    if (all[d] == null || all[d].Id != saved.diskId) continue;
                    currentDisk = d;
                    currentFolder = saved.folder ?? "";
                    return;
                }
            }
            catch (ArgumentException) { /* Old/invalid marker: use the active disk. */ }
        }

        private void RememberLocation()
        {
            var all = system != null ? system.AllStorage : null;
            var fm = system != null ? system.FileManager : null;
            if (fm == null || all == null || currentDisk < 0 ||
                currentDisk >= all.Count || all[currentDisk] == null) return;
            var body = JsonUtility.ToJson(new LastLocation
            {
                diskId = all[currentDisk].Id,
                folder = currentFolder ?? ""
            });
            var file = fm.Write(0, LastLocationPath, body);
            if (file != null && file.content == body) file.hidden = true;
        }

        public void ShowDialog(string fileName, string body, string[] formats,
            string initialPath = null, int preferredDisk = -1, Action<File> onSaved = null,
            bool administrator = false)
        {
            if (formats == null || formats.Length == 0 || system == null ||
                system.FileManager == null || !EnsurePicker()) return;
            content = body ?? "";
            extensions = formats;
            index = 0;
            saved = onSaved;
            this.administrator = administrator;
            var all = system.AllStorage;
            currentDisk = preferredDisk >= 0 && all != null && preferredDisk < all.Count
                ? preferredDisk : system.ActiveStorage;
            currentFolder = "";
            // An opened document has its own location; new exports inherit the
            // last destination on this PC, even after a save/load cycle.
            if (string.IsNullOrEmpty(initialPath) && preferredDisk < 0)
                LoadLastLocation();
            if (!string.IsNullOrEmpty(initialPath))
            {
                int slash = initialPath.LastIndexOf('/');
                if (slash > 0) currentFolder = initialPath.Substring(0, slash);
            }
            // A deleted, moved or protected folder must not be reused.
            File folder;
            if (!string.IsNullOrEmpty(currentFolder) &&
                (!system.FileManager.TryGetFile(currentDisk, currentFolder, out folder) ||
                 folder == null || !folder.isFolder ||
                 (!administrator && OperatingSystem.IsSystemPath(currentFolder)))) currentFolder = "";
            var name = string.IsNullOrEmpty(initialPath) ? fileName :
                initialPath.Substring(initialPath.LastIndexOf('/') + 1);
            for (int i = 0; i < formats.Length; i++)
                if (!string.IsNullOrEmpty(formats[i]) && name != null &&
                    name.EndsWith(formats[i], StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    name = name.Substring(0, name.Length - formats[i].Length);
                    break;
                }
            if (extensionText != null) extensionText.text = formats[index];
            if (fileNameInput != null) fileNameInput.text = name ?? "";
            ClearConfirmation();
            Fill();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void Fill()
        {
            entries.Clear();
            listView.Clear();
            var all = system.AllStorage;
            if (all == null) return;
            if (currentDisk < 0)
            {
                locationText.text = "This PC  /  Choose a disk";
                for (int d = 0; d < all.Count; d++)
                    if (all[d] != null)
                    {
                        entries.Add(new Entry { disk = d });
                        listView.Add(new ListViewItem("[Drive] " + all[d].storageName, null));
                    }
                return;
            }
            if (currentDisk >= all.Count || all[currentDisk] == null) return;
            var storage = all[currentDisk];
            locationText.text = storage.storageName + " / " +
                (string.IsNullOrEmpty(currentFolder) ? "" : currentFolder + "/");
            entries.Add(new Entry { back = true });
            listView.Add(new ListViewItem("../  Back", null));
            var files = storage.files;
            if (files == null) return;
            for (int pass = 0; pass < 2; pass++)
                foreach (var file in files)
                {
                    if (file == null || file.hidden || string.IsNullOrEmpty(file.path) ||
                        (!administrator && OperatingSystem.IsSystemPath(file.path))) continue;
                    int slash = file.path.LastIndexOf('/');
                    string parent = slash < 0 ? "" : file.path.Substring(0, slash);
                    if (!string.Equals(parent, currentFolder, StringComparison.Ordinal)) continue;
                    if (file.isFolder != (pass == 0)) continue;
                    if (!file.isFolder && !MatchesExtension(file.Extension())) continue;
                    entries.Add(new Entry { file = file });
                    string label = slash < 0 ? file.path : file.path.Substring(slash + 1);
                    listView.Add(new ListViewItem(file.isFolder ? "[Folder] " + label : label,
                        file.isFolder ? null : system.GetFileSprite(file.path)));
                }
        }

        private bool MatchesExtension(string extension)
        {
            if (extensions == null) return false;
            foreach (var ext in extensions)
                if (string.Equals(extension, ext, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void SelectedIndexChanged(int selected)
        {
            if (selected < 0 || selected >= entries.Count) return;
            var entry = entries[selected];
            if (entry.file != null && !entry.file.isFolder)
            {
                string name = entry.file.path.Substring(entry.file.path.LastIndexOf('/') + 1);
                var ext = entry.file.Extension();
                for (int i = 0; i < extensions.Length; i++)
                    if (string.Equals(ext, extensions[i], StringComparison.OrdinalIgnoreCase))
                    { index = i; break; }
                if (extensionText != null) extensionText.text = extensions[index];
                fileNameInput.text = name.Substring(0, name.Length - ext.Length);
                return;
            }
            // Without a separate Open button, a single tap enters a folder,
            // goes back, or selects a drive. Files only fill the name field.
            Enter();
        }

        public void Enter()
        {
            int selected = listView != null ? listView.SelectedIndex : -1;
            if (selected < 0 || selected >= entries.Count) return;
            var entry = entries[selected];
            if (entry.back)
            {
                if (string.IsNullOrEmpty(currentFolder)) currentDisk = -1;
                else
                {
                    int slash = currentFolder.LastIndexOf('/');
                    currentFolder = slash < 0 ? "" : currentFolder.Substring(0, slash);
                }
            }
            else if (currentDisk < 0) { currentDisk = entry.disk; currentFolder = ""; }
            else if (entry.file != null && entry.file.isFolder) currentFolder = entry.file.path;
            else return;
            ClearConfirmation();
            Fill();
        }

        public void NextExtension()
        {
            if (extensions == null || extensions.Length == 0) return;
            index = (index + 1) % extensions.Length;
            if (extensionText != null) extensionText.text = extensions[index];
            ClearConfirmation();
            Fill();
        }

        private void ClearConfirmation()
        {
            pendingReplace = null;
            pendingDisk = -1;
            if (saveButtonText != null) saveButtonText.text = "Save";
            if (messageText != null) messageText.text = "";
        }

        public void Save()
        {
            if (system == null || extensions == null || index < 0 || index >= extensions.Length ||
                fileNameInput == null) return;
            if (currentDisk < 0) { messageText.text = "Choose a disk first."; return; }
            string name = (fileNameInput.text ?? "").Trim();
            // Never accept paths in the filename field; folders are selected
            // through the picker, and a filename cannot escape the disk root.
            if (name.Length == 0 || name == "." || name == ".." ||
                name.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0 ||
                name.IndexOf('\n') >= 0 || name.IndexOf('\r') >= 0)
            { messageText.text = "Enter a valid file name."; return; }
            // Honor an explicitly typed supported extension even if another
            // format is selected in the extension switcher.
            for (int i = 0; i < extensions.Length; i++)
                if (!string.IsNullOrEmpty(extensions[i]) &&
                    name.EndsWith(extensions[i], StringComparison.OrdinalIgnoreCase))
                { index = i; break; }
            string ext = extensions[index];
            if (string.IsNullOrEmpty(ext)) return;
            if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - ext.Length);
            if (name.Length == 0) { messageText.text = "Enter a valid file name."; return; }
            string path = (string.IsNullOrEmpty(currentFolder) ? "" : currentFolder + "/") + name + ext;
            if (!administrator && OperatingSystem.IsSystemPath(path))
            { messageText.text = "Administrator access is required."; return; }
            var fm = system.FileManager;
            File existing;
            if (fm.TryGetFile(currentDisk, path, out existing) && existing != null)
            {
                if (existing.isFolder || existing.hidden)
                { messageText.text = "Cannot replace this item."; return; }
                if (pendingDisk != currentDisk || pendingReplace != path)
                {
                    pendingDisk = currentDisk;
                    pendingReplace = path;
                    messageText.text = "File exists. Click Replace to overwrite it.";
                    if (saveButtonText != null) saveButtonText.text = "Replace";
                    return;
                }
            }
            File result;
            if (existing != null)
                result = fm.Write(currentDisk, path, content);
            else
            {
                result = new File(path, content);
                if (!fm.Create(currentDisk, result)) result = null;
            }
            if (result == null || result.path != path || result.content != content)
            {
                messageText.text = "Could not save the file. Check free disk space.";
                return;
            }
            RememberLocation();
            ClearConfirmation();
            gameObject.SetActive(false);
            var callback = saved;
            saved = null;
            if (callback != null) callback(result);
        }

        public void Cancel()
        {
            // Browsing elsewhere and cancelling still updates the next Save As
            // starting folder (but never persists an unchosen "This PC" root).
            RememberLocation();
            saved = null;
            ClearConfirmation();
            gameObject.SetActive(false);
        }
    }
}
