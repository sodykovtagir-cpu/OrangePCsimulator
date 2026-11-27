using System;
using System.Collections;
using System.Text;
using PC.Component.Software.Sites;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    public partial class Browser
    {
        bool localNavigationStarted, siteNavigationClosed;
        int localGeneration;
        Coroutine pendingLocalNavigation;
        SiteDocument localDocument;

        public override bool CanOpenExtension(string ext) { return string.Equals(ext, ".osite", StringComparison.OrdinalIgnoreCase); }
        public bool OpenLocalSite(File file, int diskIndex)
        {
            if (siteNavigationClosed || file == null || file.isFolder || system == null || !system.Ready || system.AllStorage == null || diskIndex < 0 || diskIndex >= system.AllStorage.Count) return false;
            var disk = system.AllStorage[diskIndex];
            if (disk == null || disk.files == null || !disk.files.Contains(file) || !SiteLocalAddress.SafePath(file.path)) return false;
            localNavigationStarted = true;
            try { RenderLocalDocument(SiteDocument.Parse(file.content), disk.Id, file.path); return true; }
            catch (Exception ex) { ShowError("Site: " + ex.Message); return false; }
        }
        /// <summary>
        /// Open a publication that is hosted from this save (local hosting). It is
        /// resolved on the local disk only, so it works without any internet.
        /// </summary>
        public bool OpenLocalHosted(string domain)
        {
            if (siteNavigationClosed || system == null || !system.Ready || string.IsNullOrEmpty(domain)) return false;
            var service = Sites.SiteHostingService.Instance;
            if (service == null || system.AllStorage == null || system.FileManager == null) return false;
            var site = service.Local(domain);
            if (site == null) return false;
            for (int index = 0; index < system.AllStorage.Count; index++)
            {
                var disk = system.AllStorage[index];
                if (disk == null || disk.Id != site.disk) continue;
                File file;
                if (!system.FileManager.TryGetFile(index, site.diskPath, out file) || file == null || file.isFolder) continue;
                try
                {
                    var doc = SiteDocument.Parse(file.content);
                    RenderLocalDocument(doc, disk.Id, file.path);
                    // Serving time is what local hosting is billed for.
                    servedDomain = site.domain; servedBeat = 0f;
                    return true;
                }
                catch (Exception ex) { ShowError("Site: " + ex.Message); return false; }
            }
            ShowError("Сайт " + domain + " не найден на диске этого сохранения");
            return false;
        }
        bool OpenLocalAddress(string url)
        {
            if (siteNavigationClosed || system == null || !system.Ready) return false;
            localNavigationStarted = true;
            string route="/";int pageHash=url.IndexOf("#page/",StringComparison.Ordinal);
            if(pageHash>=0){route="/"+url.Substring(pageHash+6);url=url.Substring(0,pageHash);if(!SitePages.SafeRoute(route)){ShowError("Invalid page path");return false;}}
            int id; string path, asset;
            if (!SiteLocalAddress.TryParse(url, out id, out path, out asset) || system == null || system.AllStorage == null || system.FileManager == null)
            { ShowError("Неверный локальный путь сайта"); return false; }
            for (int index = 0; index < system.AllStorage.Count; index++)
            {
                var disk = system.AllStorage[index]; File file;
                if (disk == null || disk.Id != id || !system.FileManager.TryGetFile(index, path, out file) || file == null || file.isFolder) continue;
                try
                {
                    var doc = SiteDocument.Parse(file.content);
                    if (string.IsNullOrEmpty(asset)) RenderLocalDocument(doc, id, path,route);
                    else
                    {
                        var found = doc.Asset(asset); if (found == null) throw new ArgumentException("File not found in site root");
                        ShowBundledFile(doc, id, path, found);
                    }
                    return true;
                }
                catch (Exception ex) { ShowError("Site: " + ex.Message); return false; }
            }
            ShowError("Файл сайта не найден на диске PCOS"); return false;
        }
        void RenderLocalDocument(SiteDocument document, int diskId, string path,string route="/")
        {
            ClearPage(); localNavigationStarted = true; oggleHome.SetActive(false);
            var root=document;document=SitePages.At(root,route);localDocument = document;
            string url = SiteLocalAddress.Build(diskId, path)+(route=="/"?"":"#page"+route); addressBar.SetTextWithoutNotify(url);
            var canvas = SiteCanvas.Create(pageContainer); networkPage = canvas.gameObject;
            canvas.URL = url;
            canvas.Message = message => system?.ShowMessageBox("Website", message);
            canvas.Redirect = destination => DeferLocalNavigation(() => { if(destination.StartsWith("/",StringComparison.Ordinal))RenderLocalDocument(root,diskId,path,destination);else{addressBar.text = destination; Search();} });
            canvas.OpenFile = asset => DeferLocalNavigation(() => ShowBundledFile(document, diskId, path, asset));
            canvas.Download = DownloadSiteAsset;
            canvas.Render(document, false);
        }
        void ShowBundledFile(SiteDocument document, int diskId, string path, SiteAsset asset,string remoteBase="")
        {
            // Keep a root-scoped package reference; never resolve file arguments
            // through the visitor's FileManager or execute an imported program.
            bool image = asset.media.StartsWith("image/", StringComparison.Ordinal);
            string extension = System.IO.Path.GetExtension(asset.name).ToLowerInvariant();
            if (!image && Array.IndexOf(new[] { ".txt", ".md", ".json", ".csv", ".lua", ".html", ".css", ".log" }, extension) < 0)
            { system?.ShowMessageBox("Website", "OpenFile открывает текст и изображения из сайта. Для других файлов используй StartDownload."); return; }
            ClearPage(); localNavigationStarted = true; oggleHome.SetActive(false);
            localDocument = document;
            string url = remoteBase==""?SiteLocalAddress.Build(diskId, path, asset.name):remoteBase+"#site/"+Uri.EscapeDataString(asset.name); addressBar.SetTextWithoutNotify(url);
            if (image)
            {
                var picture = new SiteDocument(); picture.assets.Add(asset);
                picture.elements.Add(new SiteElement { id = "image", kind = "image", image = asset.name, fill = "#FFFFFF", x = 0, y = 0, pivotX = 0, pivotY = 0, width = picture.width, height = picture.height });
                var canvas = SiteCanvas.Create(pageContainer); networkPage = canvas.gameObject; canvas.URL = url; canvas.Render(picture, false);
            }
            else
            {
                string text = new UTF8Encoding(false, true).GetString(asset.Bytes());
                if (text.Length > 32768) text = text.Substring(0, 32768) + "\n[Preview truncated; use StartDownload for the full file]";
                var panel = SiteWidgets.Panel("Site file", pageContainer, Color.white); networkPage = panel;
                var input = SiteWidgets.Input(panel.transform, "", value => { }, true); input.readOnly = true; input.characterLimit = 0; input.SetTextWithoutNotify(text);
                SiteWidgets.Fill((RectTransform)input.transform, 8, 8, 8, 8);
            }
        }
        void DownloadSiteAsset(SiteAsset asset)
        {
            if (siteNavigationClosed || system == null || !system.Ready || system.SaveDialog == null || localDocument == null || localDocument.Asset(asset.name) != asset) return;
            if(downloadHost!=null){StartCoroutine(DownloadHostedAsset(asset,downloadHost,localGeneration));return;}
            SaveSiteAsset(asset);
        }
        void SaveSiteAsset(SiteAsset asset)
        {
            if(siteNavigationClosed||localDocument==null||localDocument.Asset(asset.name)!=asset)return;
            try
            {
                // PCOS stores pictures as .pic (base64 of the image bytes); Viewer
                // only decodes that format, so never hand back .jpg/.png names.
                bool image = asset.media.StartsWith("image/", StringComparison.Ordinal);
                string content = image ? asset.data : new UTF8Encoding(false, true).GetString(asset.Bytes());
                string extension = image ? ".pic" : System.IO.Path.GetExtension(asset.name);
                if (string.IsNullOrEmpty(extension)) extension = ".txt";
                string title = System.IO.Path.GetFileNameWithoutExtension(asset.name);
                // SaveDialog requires a user-confirmed destination. Always normal
                // privileges, even if the browser itself was launched as admin.
                system.SaveDialog.ShowDialog(title, content, new[] { extension }, null, -1,
                    file => system?.ShowMessageBox("Website", "Сохранено: " + file.path), false);
            }
            catch (Exception ex) { system?.ShowMessageBox("Website", ex.Message); }
        }
        void DeferLocalNavigation(Action action)
        {
            if (siteNavigationClosed || localDocument == null || system == null || !system.Ready) return;
            if (pendingLocalNavigation != null) StopCoroutine(pendingLocalNavigation);
            pendingLocalNavigation = StartCoroutine(FinishLocalNavigation(localGeneration, action));
        }
        IEnumerator FinishLocalNavigation(int generation, Action action)
        {
            // Do not destroy the canvas while its button/Lua stack is running.
            yield return null; pendingLocalNavigation = null;
            if (!siteNavigationClosed && generation == localGeneration && isActiveAndEnabled && system != null && system.Ready)
            {
                try { action(); } catch (Exception ex) { ShowError("Site: " + ex.Message); }
            }
        }
        void ClearLocalContext()
        {
            servedDomain="";servedBeat=0f;
            ClearCustomContext();localGeneration++;
            if (pendingLocalNavigation != null) { StopCoroutine(pendingLocalNavigation); pendingLocalNavigation = null; }
            localDocument = null;
        }
        public override void OnSystemStop() { siteNavigationClosed = true; ClearPage(); base.OnSystemStop(); }
        public override void Close() { siteNavigationClosed = true; ClearPage(); base.Close(); }
    }
}
