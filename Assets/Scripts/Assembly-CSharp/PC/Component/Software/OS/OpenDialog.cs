using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.OS
{
    /// <summary>Shared PCOS picker: folders are navigable in every app that uses SelectFile.</summary>
    public class OpenDialog : MonoBehaviour
    {
        [SerializeField] private OperatingSystem system;
        [SerializeField] private ListView listView;
        [SerializeField] private Button selectButton;

        // Null is the synthetic ".." parent entry; actual folders are File entries.
        private File[] files;
        private string[] extensions;
        private string currentFolder = "";
        private Action<File> fileSelected;
        private int lastClick = -1;
        private float lastClickTime;

        private void Awake()
        {
            if (listView != null)
            {
                listView.SetSelectionTint(new Color(.72f, .72f, .72f, 1f));
                listView.SelectedIndexChanged += SelectedIndexChanged;
            }
        }

        private void SelectedIndexChanged(int index)
        {
            if (selectButton != null)
                selectButton.interactable = files != null && index >= 0 && index < files.Length;
            // Double-click/double-tap enters folders; single-click and Select
            // still work on touch devices. Never send a folder to an app.
            if (files == null || index < 0 || index >= files.Length) return;
            if (index == lastClick && Time.unscaledTime - lastClickTime < .35f &&
                (files[index] == null || files[index].isFolder))
            {
                Enter(index);
                return;
            }
            lastClick = index;
            lastClickTime = Time.unscaledTime;
        }

        public void SelectFile(string extension, Action<File> selected)
        {
            SelectFile(new[] { extension }, selected);
        }

        public void SelectFile(string[] filters, Action<File> selected)
        {
            fileSelected = selected;
            extensions = filters;
            currentFolder = "";
            Fill();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void Fill()
        {
            var lv = listView;
            if (lv == null) return;
            files = null;
            lastClick = -1;
            lv.Clear();
            if (selectButton != null) selectButton.interactable = false;

            var sys = system;
            var all = sys != null ? sys.AllStorage : null;
            var storage = all != null && all.Count > 0 ? all[0] : null;
            var source = storage != null ? storage.files : null;
            if (source == null) return;

            var entries = new List<File>();
            if (!string.IsNullOrEmpty(currentFolder))
            {
                entries.Add(null);
                lv.Add(new ListViewItem("../  Back (" + currentFolder + ")", null));
            }
            bool any = extensions == null || extensions.Length == 0;
            if (!any)
                foreach (string filter in extensions)
                    if (filter == "*") { any = true; break; }

            // List only immediate children; folders stay visible even when the
            // requested file type is filtered out, so nested files are reachable.
            for (int pass = 0; pass < 2; pass++)
                foreach (var f in source)
                {
                    if (f == null || f.hidden || string.IsNullOrEmpty(f.path)) continue;
                    int slash = f.path.LastIndexOf('/');
                    string parent = slash < 0 ? "" : f.path.Substring(0, slash);
                    if (!string.Equals(parent, currentFolder, StringComparison.Ordinal)) continue;
                    if (f.isFolder != (pass == 0)) continue;
                    if (!f.isFolder && !any && !MatchesAny(f.Extension(), extensions)) continue;
                    entries.Add(f);
                    string name = slash < 0 ? f.path : f.path.Substring(slash + 1);
                    lv.Add(new ListViewItem(f.isFolder ? "[Folder] " + name : name,
                        sys.GetFileSprite(f.path)));
                }
            files = entries.ToArray();
        }

        private static bool MatchesAny(string ext, string[] filters)
        {
            if (filters == null) return false;
            foreach (string filter in filters)
                if (string.Equals(ext, filter, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private bool Enter(int index)
        {
            var entries = files;
            if (entries == null || index < 0 || index >= entries.Length) return false;
            var folder = entries[index];
            if (folder != null && !folder.isFolder) return false;
            if (folder != null) currentFolder = folder.path;
            else
            {
                int slash = currentFolder.LastIndexOf('/');
                currentFolder = slash < 0 ? "" : currentFolder.Substring(0, slash);
            }
            Fill();
            return true;
        }

        public void Select()
        {
            int index = listView != null ? listView.SelectedIndex : -1;
            if (Enter(index)) return;
            var entries = files;
            if (entries == null || index < 0 || index >= entries.Length) return;
            var picked = entries[index];
            if (picked == null || picked.isFolder) return;
            gameObject.SetActive(false);
            var callback = fileSelected;
            fileSelected = null;
            if (callback != null) callback(picked);
        }

        public void Cancel()
        {
            fileSelected = null;
            gameObject.SetActive(false);
        }
    }
}
