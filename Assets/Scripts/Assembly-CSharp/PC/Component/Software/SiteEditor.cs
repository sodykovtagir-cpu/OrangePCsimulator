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
        SiteHistory project;
        SiteCanvas view;
        RectTransform inspector, palette;
        Text status;
        string selected = "", filePath = "";
        int fileDisk = -1;
        bool preview, confirmNew;
        float fieldY;
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
            if (view == null) BuildUI();
            Redraw();
        }
        void BuildUI()
        {
            var body = SiteWidgets.Panel("Site editor", transform, new Color(.94f, .94f, .94f));
            SiteWidgets.Fill((RectTransform)body.transform, 6, 6, 6, 42);
            var toolbar = SiteWidgets.Node("Toolbar", body.transform);
            var tr = (RectTransform)toolbar.transform; tr.anchorMin = new Vector2(0, 1); tr.anchorMax = Vector2.one;
            tr.pivot = new Vector2(0, 1); tr.sizeDelta = new Vector2(0, 34);
            var layout = toolbar.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 4; layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            Tool(toolbar.transform, "New", () =>
            {
                // Two-click confirmation avoids discarding a working project.
                if (!confirmNew && (project.Document.elements.Count > 0 || project.Document.assets.Count > 0))
                { confirmNew = true; Report("Сохрани проект или нажми New ещё раз, чтобы очистить его."); return; }
                confirmNew = false; project.Reset(new SiteDocument()); selected = ""; filePath = ""; fileDisk = -1; preview = false; Redraw();
            });
            Tool(toolbar.transform, "Open", OpenFile); Tool(toolbar.transform, "Save", Save);
            Tool(toolbar.transform, "Undo", () => { project.Undo(); Redraw(); });
            Tool(toolbar.transform, "Redo", () => { project.Redo(); Redraw(); });
            Tool(toolbar.transform, "Preview", () => { preview = !preview; Redraw(); });
            Tool(toolbar.transform, "Host", () =>
            {
                Save();
                Report("Сохрани .osite. Публикация и тариф будут подключены на следующем этапе; сейчас сайт не отправлен.");
            });
            var workspace = SiteWidgets.Node("Workspace", body.transform); SiteWidgets.Fill((RectTransform)workspace.transform, 0, 24, 0, 40);
            var left = SiteWidgets.Panel("Palette", workspace.transform, new Color(.74f, .74f, .74f));
            var lr = (RectTransform)left.transform; lr.anchorMax = new Vector2(.2f, 1); lr.offsetMax = new Vector2(-6, 0);
            palette = (RectTransform)left.transform;
            PaletteButton("Button", 12, () => Add("button")); PaletteButton("Text", 50, () => Add("text"));
            PaletteButton("Image", 88, () => Add("image")); PaletteButton("Site settings", 138, () => { selected = ""; BuildInspector(); });
            PaletteButton("Import file", 176, ImportDownload);
            var center = SiteWidgets.Panel("Canvas border", workspace.transform, new Color(.18f, .18f, .18f));
            var cr = (RectTransform)center.transform; cr.anchorMin = new Vector2(.2f, 0); cr.anchorMax = new Vector2(.72f, 1);
            cr.offsetMin = Vector2.zero; cr.offsetMax = new Vector2(-6, 0);
            view = SiteCanvas.Create(center.transform); SiteWidgets.Fill((RectTransform)view.transform, 2, 2, 2, 2);
            view.Selected = id => { if (selected != id) inspector.anchoredPosition = Vector2.zero; selected = id; BuildInspector(); };
            view.Moved = (id, x, y) => Change(d => { var e = d.Find(id); e.x = x; e.y = y; });
            view.TextEdited = (id, text) => Change(d => d.Find(id).text = text);
            view.Message = Report;
            // Preview never leaves the editor or downloads/executes anything.
            view.Redirect = url => Report("Preview redirect: " + url);
            view.Download = asset => Report("Preview download: " + asset.name + " (файл не записан)");
            var right = SiteWidgets.Panel("Inspector", workspace.transform, new Color(.74f, .74f, .74f));
            var rr = (RectTransform)right.transform; rr.anchorMin = new Vector2(.72f, 0); rr.offsetMin = Vector2.zero;
            inspector = SiteWidgets.Scroll(right.transform);
            status = SiteWidgets.Label(body.transform, "", 12); var sr = status.rectTransform;
            sr.anchorMin = Vector2.zero; sr.anchorMax = new Vector2(1, 0); sr.pivot = Vector2.zero;
            sr.sizeDelta = new Vector2(0, 22); sr.anchoredPosition = Vector2.zero;
        }
        void Tool(Transform parent, string title, Action action) { SiteWidgets.Button(parent, title, action); }
        void PaletteButton(string title, float y, Action action)
        {
            var button = SiteWidgets.Button(palette, title, action); StretchRow((RectTransform)button.transform, y, 30);
        }
        static void StretchRow(RectTransform r, float y, float h)
        {
            r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.pivot = new Vector2(0, 1);
            r.offsetMin = new Vector2(8, -y - h); r.offsetMax = new Vector2(-8, -y);
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
            if (inspector == null) return;
            SiteWidgets.Clear(inspector); fieldY = 8;
            var element = project.Document.Find(selected);
            if (preview) { Info("Preview — Lua сайта, без записи файлов и перехода из редактора."); EndInspector(); return; }
            if (element == null)
            {
                Field("Title", project.Document.title, s => Change(d => d.title = s));
                Field("Description", project.Document.description, s => Change(d => d.description = s), true);
                Field("Keywords", project.Document.keywords, s => Change(d => d.keywords = s));
                Number("Page width", project.Document.width, n => Change(d => d.width = n));
                Number("Page height", project.Document.height, n => Change(d => d.height = n));
                Field("Background #RRGGBB", project.Document.background, s => Change(d => d.background = s));
                ActionRow("Import site icon", () => ImportImage("", true));
                Info("Icon: " + project.Document.icon);
                Info("Site root: " + project.Document.assets.Count + " files");
                foreach (var asset in project.Document.assets)
                {
                    string name = asset.name;
                    Info(name + " (" + asset.Bytes().Length + " B)");
                    ActionRow("Remove " + name, () => Change(d =>
                    {
                        if (d.icon == name) d.icon = "";
                        foreach (var e in d.elements) if (e.image == name) e.image = "";
                        d.assets.Remove(d.Asset(name));
                    }));
                }
                Info("Обычный текст: удерживай для редактирования. Перетаскивай элементы по холсту.");
                EndInspector(); return;
            }
            string id = selected;
            Field("Name", element.name, s => Change(d => d.Find(id).name = s));
            Info("ID: " + id);
            Number("Position X", element.x, n => Change(d => d.Find(id).x = n));
            Number("Position Y", element.y, n => Change(d => d.Find(id).y = n));
            Number("Width", element.width, n => Change(d => d.Find(id).width = n));
            Number("Height", element.height, n => Change(d => d.Find(id).height = n));
            Number("Pivot X (0..1)", element.pivotX, n => Change(d => d.Find(id).pivotX = n));
            Number("Pivot Y (0..1)", element.pivotY, n => Change(d => d.Find(id).pivotY = n));
            ActionRow("Pivot: top left", () => Change(d => { d.Find(id).pivotX = 0; d.Find(id).pivotY = 0; }));
            ActionRow("Pivot: center", () => Change(d => { d.Find(id).pivotX = .5f; d.Find(id).pivotY = .5f; }));
            if (element.kind != "image")
            {
                Field("Text", element.text, s => Change(d => d.Find(id).text = s), true);
                Field("Text color #RRGGBB", element.textColor, s => Change(d => d.Find(id).textColor = s));
                Number("Text size", element.fontSize, n => Change(d => d.Find(id).fontSize = (int)n));
                foreach (string font in new[] { "default", "mono", "medium" })
                { string captured = font; ActionRow("Font: " + font + (element.font == font ? " *" : ""), () => Change(d => d.Find(id).font = captured)); }
            }
            if (element.kind != "text")
            {
                Field("Fill #RRGGBB / #RRGGBBAA", element.fill, s => Change(d => d.Find(id).fill = s));
                ActionRow("Import fill image", () => ImportImage(id, false));
                Info("Image: " + element.image);
                ActionRow("Clear image", () => Change(d => d.Find(id).image = ""));
            }
            if (element.kind == "button")
            {
                Field("OnClick — Lua", element.onClick, s => Change(d => d.Find(id).onClick = s), true, 150);
                Info("RedirectURL(\"www.modforge.com\")\nStartDownload(\"site/myapp.exe\")\nSetText(\"element_id\", \"Hello\")\nAlert(\"Hello!\")");
            }
            ActionRow("Delete element", () => Change(d => d.elements.Remove(d.Find(id))));
            EndInspector();
        }
        void Field(string title, string value, Action<string> changed, bool multi = false, float h = 52)
        {
            var label = SiteWidgets.Label(inspector, title, 12); StretchRow(label.rectTransform, fieldY, 22); fieldY += 22;
            var input = SiteWidgets.Input(inspector, value, changed, multi);
            StretchRow((RectTransform)input.transform, fieldY, multi ? h : 28); fieldY += (multi ? h : 28) + 9;
        }
        void Number(string title, float value, Action<float> changed)
        {
            Field(title, value.ToString("0.###", CultureInfo.InvariantCulture), s =>
            {
                float n;
                if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out n)) { Report("Введите число с точкой"); return; }
                changed(n);
            });
        }
        void ActionRow(string title, Action action)
        {
            var button = SiteWidgets.Button(inspector, title, action); StretchRow((RectTransform)button.transform, fieldY, 28); fieldY += 36;
        }
        void Info(string text)
        {
            var label = SiteWidgets.Label(inspector, text, 12); label.alignment = TextAnchor.UpperLeft;
            float height = Mathf.Max(42, (text.Length / 24 + 1) * 17); StretchRow(label.rectTransform, fieldY, height); fieldY += height + 7;
        }
        void EndInspector() { inspector.sizeDelta = new Vector2(0, fieldY + 12); }
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
        void ImportDownload()
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
