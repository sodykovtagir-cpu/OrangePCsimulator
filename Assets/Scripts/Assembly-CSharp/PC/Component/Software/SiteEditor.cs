using System;
using System.Globalization;
using PC.Component.Software.Sites;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    /// <summary>Native PCOS visual site editor. Content is orange-site JSON, not HTML.</summary>
    public sealed class SiteEditor : App
    {
        [Serializable]
        public sealed class InspectorField
        {
            public string key;
            public InputField input;
        }
        [SerializeField] private SiteCanvas view;
        [SerializeField] private RectTransform inspector;
        [SerializeField] private GameObject sitePanel, elementPanel, previewPanel;
        [SerializeField] private Text status, iconInfo, elementId;
        [SerializeField] private InputField assetList, assetToRemove;
        [SerializeField] private Button undoButton, redoButton;
        [SerializeField] private InspectorField[] fields;
        SiteHistory project;
        string selected = "", filePath = "";
        int fileDisk = -1;
        bool preview, confirmNew, bound;
        Vector2 fittedHost;
        float fittedInset = -1;
        public override bool SingleInstance => true;
        protected override bool ShowMenuBar => false;
        public override bool CanOpenExtension(string ext) { return string.Equals(ext, ".osite", StringComparison.OrdinalIgnoreCase); }
        public override void Open(string content)
        {
            base.Open(content);
            if (project == null) project = new SiteHistory(new SiteDocument());
            if (!string.IsNullOrWhiteSpace(content))
            {
                try { project.Reset(SiteDocument.Parse(content)); selected = ""; filePath = ""; fileDisk = -1; }
                catch (Exception ex) { system?.ShowMessageBox("Site Editor", ex.Message); }
            }
            if (!BindUI()) return;
            FitWindow();
            Redraw();
        }
        bool BindUI()
        {
            if (bound) return true;
            if (view == null || inspector == null || sitePanel == null || elementPanel == null || previewPanel == null || fields == null)
            {
                system?.ShowMessageBox("Site Editor", "Неполный префаб SiteEditor: проверь ссылки интерфейса.");
                return false;
            }
            foreach (var field in fields)
            {
                if (field == null || field.input == null) continue;
                string key = field.key;
                field.input.onEndEdit.AddListener(value => SetField(key, value));
            }
            view.Selected = id => { if (selected != id) inspector.anchoredPosition = Vector2.zero; selected = id; BuildInspector(); };
            view.Moved = (id, x, y) => Change(d => { var e = d.Find(id); e.x = x; e.y = y; });
            view.TextEdited = (id, text) => Change(d => d.Find(id).text = text);
            view.Message = Report;
            view.Redirect = url => Report("Preview redirect: " + url);
            view.Download = asset => Report("Preview download: " + asset.name + " (файл не записан)");
            bound = true;
            return true;
        }
        // Window coordinates are LOCAL PCOS app-parent units, not screen pixels.
        // The prefab owns all internal layout; only the top-level window is fitted.
        void FitWindow()
        {
            var window = transform as RectTransform;
            var host = transform.parent as RectTransform;
            if (window == null || host == null || host.rect.width <= 0 || host.rect.height <= 0) return;
            float inset = system != null ? system.GetTaskbarInsetLocalHeight() : 48f;
            fittedHost = new Vector2(host.rect.width, host.rect.height); fittedInset = inset;
            if (maximized) return;
            window.anchorMin = window.anchorMax = new Vector2(.5f, .5f);
            window.pivot = new Vector2(.5f, .5f);
            window.sizeDelta = Sites.SiteWindowLayout.FittedSize(host.rect.width, host.rect.height, inset);
            window.anchoredPosition = new Vector2(0, SiteWindowLayout.BottomInset(host.rect.height, inset) * .5f);
        }
        void LateUpdate()
        {
            var host = transform.parent as RectTransform;
            if (host == null) return;
            float inset = system != null ? system.GetTaskbarInsetLocalHeight() : 48f;
            if (host.rect.width != fittedHost.x || host.rect.height != fittedHost.y || inset != fittedInset) FitWindow();
        }
        public override void Maximize() { base.Maximize(); if (!maximized) FitWindow(); }
        public void NewProject()
        {
            if (!confirmNew && (project.Document.elements.Count > 0 || project.Document.assets.Count > 0))
            { confirmNew = true; Report("Сохрани проект или нажми New ещё раз, чтобы очистить его."); return; }
            confirmNew = false; project.Reset(new SiteDocument()); selected = ""; filePath = ""; fileDisk = -1; preview = false; Redraw();
        }
        public void Undo() { project.Undo(); Redraw(); }
        public void Redo() { project.Redo(); Redraw(); }
        public void TogglePreview() { preview = !preview; Redraw(); }
        public void Host()
        {
            Report("Публикация на Byethost ещё не подключена; сейчас сайт не отправлен.");
        }
        public void AddButton() { Add("button"); }
        public void AddText() { Add("text"); }
        public void AddImage() { Add("image"); }
        public void SiteSettings() { selected = ""; inspector.anchoredPosition = Vector2.zero; BuildInspector(); }
        public void ImportIcon() { ImportImage("", true); }
        public void ImportFill() { if (!string.IsNullOrEmpty(selected)) ImportImage(selected, false); }
        public void ClearImage() { if (!string.IsNullOrEmpty(selected)) Change(d => d.Find(selected).image = ""); }
        public void DeleteElement() { if (!string.IsNullOrEmpty(selected)) Change(d => d.elements.Remove(d.Find(selected))); }
        public void PivotTopLeft() { SetPivot(0, 0); }
        public void PivotCenter() { SetPivot(.5f, .5f); }
        void SetPivot(float x, float y) { if (!string.IsNullOrEmpty(selected)) Change(d => { d.Find(selected).pivotX = x; d.Find(selected).pivotY = y; }); }
        public void FontDefault() { SetField("font", "default"); }
        public void FontMono() { SetField("font", "mono"); }
        public void FontMedium() { SetField("font", "medium"); }
        public void RemoveAsset()
        {
            string name = assetToRemove != null ? assetToRemove.text.Trim() : "";
            if (project.Document.Asset(name) == null) { Report("Укажи имя файла из корня сайта"); return; }
            Change(d => { if (d.icon == name) d.icon = ""; foreach (var e in d.elements) if (e.image == name) e.image = ""; d.assets.Remove(d.Asset(name)); });
        }
        void Add(string kind)
        {
            string id = "el_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            Change(d => d.elements.Add(new SiteElement { id = id, kind = kind, name = kind,
                text = kind == "button" ? "Cool button" : "Hello World!", x = d.width / 2, y = 150 + d.elements.Count * 12,
                height = kind == "image" ? 180 : 48, width = kind == "image" ? 240 : 300 }));
            selected = id; view.Select(id);
            if (kind == "image") ImportImage(id, false);
        }
        void Change(Action<SiteDocument> edit)
        {
            try { confirmNew = false; project.Change(edit); Redraw(); Report("Изменения не сохранены"); }
            catch (Exception ex) { Report(ex.Message); Redraw(); }
        }
        void Redraw()
        {
            if (view == null) return;
            if (project.Document.Find(selected) == null) selected = "";
            // Lua preview uses an isolated copy, never the authored project.
            try { view.Render(SiteDocument.Parse(project.Document.ToJson()), !preview); }
            catch (Exception ex) { Report(ex.Message); }
            if (!preview && !string.IsNullOrEmpty(selected)) view.Select(selected); else BuildInspector();
        }
        void BuildInspector()
        {
            if (!bound) return;
            var doc = project.Document; var element = doc.Find(selected);
            sitePanel.SetActive(!preview && element == null);
            elementPanel.SetActive(!preview && element != null);
            previewPanel.SetActive(preview);
            var active = (RectTransform)(preview ? previewPanel : element == null ? sitePanel : elementPanel).transform;
            inspector.sizeDelta = new Vector2(inspector.sizeDelta.x, active.rect.height);
            foreach (var field in fields)
            {
                if (field == null || field.input == null) continue;
                field.input.SetTextWithoutNotify(FieldValue(field.key, doc, element));
                bool text = field.key == "text" || field.key == "textColor" || field.key == "font" || field.key == "fontSize";
                bool image = field.key == "fill";
                field.input.interactable = element == null || ((!text || element.kind != "image") && (!image || element.kind != "text") && (field.key != "onClick" || element.kind == "button"));
            }
            if (iconInfo != null) iconInfo.text = "Icon: " + doc.icon;
            if (elementId != null) elementId.text = element == null ? "" : "ID: " + element.id + " | " + element.kind;
            if (assetList != null)
            {
                var list = new System.Text.StringBuilder();
                foreach (var asset in doc.assets) list.Append(asset.name).Append(" (").Append(asset.Bytes().Length).Append(" B)\n");
                assetList.SetTextWithoutNotify(list.ToString());
            }
            if (undoButton != null) undoButton.interactable = project.CanUndo;
            if (redoButton != null) redoButton.interactable = project.CanRedo;
        }
        static string Number(float value) { return value.ToString("0.###", CultureInfo.InvariantCulture); }
        static string FieldValue(string key, SiteDocument d, SiteElement e)
        {
            switch (key)
            {
                case "title": return d.title; case "description": return d.description; case "keywords": return d.keywords;
                case "pageWidth": return Number(d.width); case "pageHeight": return Number(d.height); case "background": return d.background;
            }
            if (e == null) return "";
            switch (key)
            {
                case "name": return e.name; case "x": return Number(e.x); case "y": return Number(e.y);
                case "width": return Number(e.width); case "height": return Number(e.height);
                case "pivotX": return Number(e.pivotX); case "pivotY": return Number(e.pivotY);
                case "text": return e.text; case "textColor": return e.textColor; case "fontSize": return Number(e.fontSize);
                case "font": return e.font; case "fill": return e.fill; case "onClick": return e.onClick;
                default: return "";
            }
        }
        void SetField(string key, string value)
        {
            Change(d =>
            {
                switch (key)
                {
                    case "title": d.title = value; return; case "description": d.description = value; return;
                    case "keywords": d.keywords = value; return; case "background": d.background = value; return;
                    case "pageWidth": d.width = ParseNumber(value); return; case "pageHeight": d.height = ParseNumber(value); return;
                }
                var e = d.Find(selected); if (e == null) return;
                switch (key)
                {
                    case "name": e.name = value; break; case "x": e.x = ParseNumber(value); break; case "y": e.y = ParseNumber(value); break;
                    case "width": e.width = ParseNumber(value); break; case "height": e.height = ParseNumber(value); break;
                    case "pivotX": e.pivotX = ParseNumber(value); break; case "pivotY": e.pivotY = ParseNumber(value); break;
                    case "text": e.text = value; break; case "textColor": e.textColor = value; break;
                    case "fontSize": e.fontSize = (int)ParseNumber(value); break; case "font": e.font = value; break;
                    case "fill": e.fill = value; break; case "onClick": e.onClick = value; break;
                }
            });
        }
        static float ParseNumber(string value)
        {
            float n;
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out n)) throw new ArgumentException("Введите число с точкой");
            return n;
        }
        void Report(string message) { if (status != null) status.text = message; }
        public void OpenFile()
        {
            if (system == null) return;
            system.SelectFile(".osite", file =>
            {
                if (file == null) return;
                try { project.Reset(SiteDocument.Parse(file.content)); selected = ""; filePath = file.path; fileDisk = DiskFor(file); preview = false; Redraw(); Report(filePath); }
                catch (Exception ex) { Report(ex.Message); }
            });
        }
        public void Save()
        {
            if (system == null || system.SaveDialog == null) return;
            try
            {
                system.SaveDialog.ShowDialog("MySite", project.Document.ToJson(), new[] { ".osite" },
                    string.IsNullOrEmpty(filePath) ? null : filePath, fileDisk, file => { filePath = file.path; fileDisk = DiskFor(file); Report("Сохранено: " + file.path); }, IsAdministrator);
            }
            catch (Exception ex) { Report(ex.Message); }
        }
        int DiskFor(File file)
        {
            var disks = system != null ? system.AllStorage : null;
            if (disks != null) for (int i = 0; i < disks.Count; i++) if (disks[i] != null && disks[i].files != null && disks[i].files.Contains(file)) return i;
            return -1;
        }
        void ImportImage(string id, bool icon)
        {
            if (system == null) return;
            system.SelectFile(new[] { ".png", ".jpg", ".jpeg", ".pic" }, file =>
            {
                if (file == null) return;
                try
                {
                    var asset = new SiteAsset { name = "image_" + Guid.NewGuid().ToString("N").Substring(0, 12) + ".png", data = file.content };
                    var bytes = asset.Bytes(); asset.media = bytes.Length > 2 && bytes[0] == 255 && bytes[1] == 216 ? "image/jpeg" : "image/png";
                    if (asset.media == "image/jpeg") asset.name = asset.name.Replace(".png", ".jpg");
                    SiteDocument.ValidateImage(bytes, asset.media);
                    var probe = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    try
                    {
                        if (!probe.LoadImage(bytes) || probe.width > 2048 || probe.height > 2048)
                            throw new ArgumentException("Не удалось прочитать изображение");
                    }
                    finally { Destroy(probe); }
                    Change(d => { d.assets.Add(asset); if (icon) d.icon = asset.name; else { d.Find(id).image = asset.name; d.Find(id).fill = "#FFFFFF"; } });
                }
                catch (Exception ex) { Report(ex.Message); }
            });
        }
        public void ImportDownload()
        {
            if (system == null) return;
            system.SelectFile("*", file =>
            {
                if (file == null || file.isFolder) return;
                string name = (file.path ?? "").Replace('\\', '/'); name = name.Substring(name.LastIndexOf('/') + 1);
                try
                {
                    var asset = new SiteAsset { name = name, media = "application/x-pcos-file", data = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(file.content ?? "")) };
                    Change(d => d.assets.Add(asset));
                }
                catch (Exception ex) { Report(ex.Message); }
            });
        }
    }
}
