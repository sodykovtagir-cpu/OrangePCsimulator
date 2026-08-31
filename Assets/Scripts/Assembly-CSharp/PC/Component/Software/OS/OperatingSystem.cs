using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PC.Component.Software.Lua;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;

namespace PC.Component.Software.OS
{
    public class OperatingSystem : ComputerSystem
    {

    void Start()
    {
        LoadIconPositions();
        // Disable ReorderableList layout on iconParent for free icon positioning
        if (iconParent != null)
        {
            var rl = iconParent.GetComponent<UnityEngine.UI.Extensions.ReorderableList>();
            if (rl != null)
            {
                rl.IsDraggable = false;
                rl.enabled = false;
            }
            
            var lg = iconParent.GetComponent<UnityEngine.UI.LayoutGroup>();
            if (lg != null)
                lg.enabled = false;
        }
    }
        [Serializable]
        private class User
        {
            public string userPicturePath;
            public string userName;
            public int background;

            // Сохранение пути к кастомным обоям
            public string customBackgroundPath;
        }

        [SerializeField] private UnityEngine.Animator animator;
        [SerializeField] private Sprite[] texDesktop;
        [SerializeField] private AudioClip shutdownSound;
        [SerializeField] private Sprite folderSprite;
        [SerializeField] private AudioClip errorSound;
        [SerializeField] private AudioClip alertSound;
        [SerializeField] private Texture2D defaultUserPicture;

        [Header("Startup")]
        [SerializeField] private AudioClip loginFailSound;
        [SerializeField] private GameObject startup;
        [SerializeField] private GameObject user;
        [SerializeField] private GameObject loading;
        [SerializeField] private GameObject password;
        [SerializeField] private RawImage userPicture;
        [SerializeField] private InputField passwordInput;
        [SerializeField] private Text userText;
        [SerializeField] private UnityEngine.Animator passwordAnimator;

        [Header("Desktop")]
        [SerializeField] private Sprite unknownFileSprite;
        [SerializeField] private CanvasGroup desktop;
        [SerializeField] private FileIcon fileIconPrefab;
        [SerializeField] private Transform iconParent;
        [SerializeField] private Transform appParent;
        [SerializeField] private ProgressBar progressBar;
        [SerializeField] private MessageBox messageBox;
        [SerializeField] private PrintService printServicePrefab;
        [SerializeField] private OpenDialog fileDialog;
        [SerializeField] private SaveDialog saveDialog;
        [SerializeField] private DevicePicker devicePicker;
        [SerializeField] private Transform popup;

        [SerializeField]
        [Header("Menu Bar")]
        private Button menuBarItem;
        [SerializeField] private Transform menuBar;

        private bool busy;
        private bool error;
        private bool startMenuOpened;
        private bool running;
        private int storageScore;
        private CoverImage background;
        private Dictionary<string, App> appPrefabs = new Dictionary<string, App>();
        private List<string> installedApps = new List<string>();
        private Dictionary<string, FileIcon> fileIcons = new Dictionary<string, FileIcon>();
        private Dictionary<string, Vector2> iconPositions = new Dictionary<string, Vector2>();
        private const string userFilePath = "System/user";
        private User userData;

        public ProgressBar ProgressBar => progressBar;
        public SaveDialog SaveDialog => saveDialog;
        public DevicePicker DevicePicker => devicePicker;
        public bool Ready { get; private set; }

        public string UserPicturePath
        {
            get { return userData.userPicturePath; }
            set { userData.userPicturePath = value; SaveUserData(); }
        }

        public string UserName
        {
            get { return userData.userName; }
            set { userData.userName = value; SaveUserData(); }
        }

        public int SystemId => Board.Id;

        protected override void BootSystem()
        {
            var apps = Resources.LoadAll<App>("apps");
            if (apps != null && appPrefabs != null)
            {
                for (int i = 0; i < apps.Length; i++)
                {
                    var app = apps[i];
                    if (app != null && !appPrefabs.ContainsKey(app.AppName)) appPrefabs.Add(app.AppName, app);
                }
            }

            if (desktop != null)
            {
                background = desktop.GetComponent<CoverImage>();
                StartCoroutine(Boot());
            }
        }

        private IEnumerator Boot()
        {
            busy = true;
            running = true;

            if (startup != null) startup.SetActive(true);
            if (user != null) user.SetActive(false);
            if (loading != null) loading.SetActive(true);

            // Wait for canvas/iconParent to be fully initialized before creating icons.
            // Without this, parentRT.rect may be (0,0) and all icons spawn at (0,0).
            yield return null;
            yield return null;
            yield return null;

            // Extra safety: wait until iconParent has a valid rect size
            if (iconParent != null)
            {
                var prt = iconParent.GetComponent<RectTransform>();
                int safetyCounter = 0;
                while (prt != null && (prt.rect.width < 50 || prt.rect.height < 50) && safetyCounter < 30)
                {
                    yield return null;
                    safetyCounter++;
                }
            }

            LoadFilesFromDisk();

            storageScore = 0;
            var all = AllStorage;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    var s = all[i];
                    if (s != null) storageScore += s.Score;
                }
            }

            float wait = storageScore > 0 ? 10000f / storageScore : 0f;
            if (wait > 0f) yield return new UnityEngine.WaitForSeconds(wait);

            if (startup != null) startup.SetActive(false);
            if (user != null) user.SetActive(true);

            var fm = FileManager;
            File uf;
            User ud;

            if (fm != null && fm.TryGetFile(0, "System/user", out uf) && uf != null)
            {
                ud = JsonUtility.FromJson<User>(uf.content);

#if UNITY_EDITOR
                UnityEngine.Debug.Log("Содержимое System/user:");
                UnityEngine.Debug.Log(uf.content);
                UnityEngine.Debug.Log("После загрузки customBackgroundPath = " + ud.customBackgroundPath);
#endif
            }
            else
            {
                ud = new User { userName = "User" };
            }

            userData = ud;

            var tex = UserPicture();
            if (userPicture != null) userPicture.texture = tex;
            if (userData != null && userText != null) userText.text = userData.userName;

            bool hasPassword = false;
            if (all != null && all.Count > 0 && all[0] != null)
            {
                var pwd = all[0].password;
                hasPassword = !string.IsNullOrEmpty(pwd);
            }

            if (!hasPassword)
            {
                if (loading != null) loading.SetActive(true);
                if (password != null) password.SetActive(false);
                yield return new WaitForSeconds(1f);
                busy = false;
                Desktop();
                yield break;
            }

            busy = false;
            if (loading != null) loading.SetActive(false);
            if (password != null) password.SetActive(true);
            if (passwordInput != null) passwordInput.text = "";
        }

        private void Desktop()
        {
#if UNITY_EDITOR
            UnityEngine.Debug.Log("customBackgroundPath = " + userData.customBackgroundPath);
#endif
            if (userData == null || texDesktop == null) return;

            // Логика загрузки обоев (стандартные или кастомные)
            if (background != null)
            {
                bool customLoaded = false;

                if (!string.IsNullOrEmpty(userData.customBackgroundPath))
                {
                    Sprite customSprite = LoadSpriteFromInGameFile(userData.customBackgroundPath);
                    if (customSprite != null)
                    {
                        background.Sprite = customSprite;
                        customLoaded = true;
                    }
                }

                if (!customLoaded)
                {
                    int index = (int)userData.background;
                    if (index >= 0 && index < texDesktop.Length)
                    {
                        background.Sprite = texDesktop[index];
                    }
                }
            }

            if (animator != null) animator.SetTrigger("Enter");
            if (desktop != null) desktop.blocksRaycasts = true;

            Ready = true;

            if (taskbar != null)
                taskbar.SetActive(true);

            InitializeTaskbar();

            startMenuOpened = false;

            if (startMenu != null)
            {
                startMenu.SetActive(false);
            }

            if (startMenuAnimator != null)
            {
                startMenuAnimator.SetBool("Open", false);
            }
        }

        // Установка внутриигровой картинки как обои
        public void SetCustomBackgroundPath(string path)
        {
            if (userData == null) return;
            userData.customBackgroundPath = path;
            SaveUserData();
            Desktop();
        }

        // Вспомогательный метод для превращения внутриигрового файла в Sprite
        private Sprite LoadSpriteFromInGameFile(string path)
        {
            if (FileManager == null) return null;
            if (!FileManager.TryGetFile(0, path, out var file) || file == null) return null;

            try
            {
                byte[] data = Convert.FromBase64String(file.content);
                Texture2D tex = new Texture2D(2, 2);
                tex.filterMode = FilterMode.Bilinear;
                if (tex.LoadImage(data))
                {
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
            catch
            {
                return null;
            }
            return null;
        }

        public void UpdateBackground(int index)
        {
            var user = userData;
            var sprites = texDesktop;
            if (user == null || sprites == null) return;

            user.background = index;
            user.customBackgroundPath = ""; // Очищаем кастомные при выборе стандарта

            SaveUserData();
            Desktop();
        }

        public void Login()
        {
            var input = passwordInput;
            var all = AllStorage;
            if (input == null || all == null || all.Count == 0) return;

            var typed = input.text;
            var storage = all[0];
            if (storage == null) return;

            var correct = storage.password;
            if (string.Equals(typed, correct))
            {
                Desktop();
                return;
            }

            if (passwordAnimator != null) passwordAnimator.SetTrigger("Wrong");

            var board = Board;
            var src = board != null ? board.Source : null;
            if (src != null) src.PlayOneShot(loginFailSound);
        }

        public override void PowerClicked()
        {
            if (!error)
            {
                if (busy) return;
                StartCoroutine(ShutDown());
                return;
            }

            var board = Board;
            if (board != null) board.PowerOff(false);
        }

        private IEnumerator ShutDown()
        {
            busy = true;
            StopProcess();

            var src = Board != null ? Board.Source : null;
            if (src != null) src.PlayOneShot(shutdownSound);

            if (animator != null) animator.SetTrigger("Exit");

            float seconds = storageScore > 0 ? 10000f / storageScore : 2f;
            if (seconds > 5f) seconds = 5f;
            if (seconds < 2f) seconds = 2f;

            yield return new WaitForSeconds(seconds);

            var board = Board;
            if (board != null) board.PowerOff(false);
        }

        public override void Fault()
        {
            if (error) return;
            error = true;
            StopProcess();
            var src = Board != null ? Board.Source : null;
            if (src != null) src.PlayOneShot(errorSound);
            if (animator != null) animator.SetTrigger("Error");
            StopAllCoroutines();
        }

        public void StopProcess()
        {
            if (!running) return;
            running = false;
            Ready = false;

            if (desktop != null) desktop.blocksRaycasts = false;

            if (iconParent != null)
            {
                var rl = iconParent.GetComponent<ReorderableList>();
                if (rl != null) rl.IsDraggable = false;
            }
        }

        public void InstallApp(App app)
        {
            if (app == null || FileManager == null) return;

            var path = app.AppName + ".exe";
            var file = new File(path, "", false, app.size);

            if (FileManager.Create(0, file))
            {
                AddApp(app.AppName);
                RefreshDesktopIcon();
            }
        }

        public void UninstallApp(string softwareName)
        {
            var path = softwareName + ".exe";
            if (FileManager != null) FileManager.Delete(0, path);
            if (installedApps != null) installedApps.Remove(softwareName);

            RefreshDesktopIcon();
        }

        public bool IsAppInstalled(string name)
        {
            if (installedApps == null) return false;
            return installedApps.Contains(name);
        }

        public bool IsAppInstalled(string name, out App app)
        {
            app = null;
            if (installedApps == null || !installedApps.Contains(name) || appPrefabs == null) return false;
            if (appPrefabs.TryGetValue(name, out var a))
            {
                app = a;
                return true;
            }
            return false;
        }

        public void SelectFile(string extension, Action<File> callback)
        {
            var dlg = fileDialog;
            if (dlg != null) dlg.SelectFile(extension, callback);
        }

        private void LoadFilesFromDisk()
        {
            if (installedApps == null) return;
            installedApps.Clear();

            var all = AllStorage;
            if (all == null || all.Count == 0)
            {
                RefreshDesktopIcon();
                return;
            }

            var storage = all[0];
            var files = storage != null ? storage.files : null;
            if (files != null)
            {
                for (int i = 0; i < files.Count; i++)
                {
                    var f = files[i];
                    if (f == null) continue;
                    if (string.Equals(f.Extension(), ".exe"))
                    {
                        if (LuaAppPackage.IsPackage(f.content)) continue;
                        var name = f.NameWithoutExtension();
                        AddApp(name);
                    }
                }
            }

            RefreshDesktopIcon();
            // EnsureLuaEditor(); // Отключено — Lua Editor не устанавливается по умолчанию
        }

        private void EnsureLuaEditor()
        {
            if (appPrefabs == null || !appPrefabs.TryGetValue("Lua Editor", out var prefab) || prefab == null)
                return;
            if (FileManager != null && FileManager.Exists(0, "System/lua_v1"))
            {
                if (IsAppInstalled("Lua Editor")) EnsureLuaDocs();
                return;
            }
            if (!IsAppInstalled("Lua Editor"))
                InstallApp(prefab);
            if (FileManager != null)
            {
                var mark = FileManager.Write(0, "System/lua_v1", "1");
                if (mark != null) mark.hidden = true;
            }
            EnsureLuaDocs();
        }

        private void EnsureLuaDocs()
        {
            if (FileManager == null) return;
            if (FileManager.Exists(0, "Lua.txt"))
                FileManager.Delete(0, "Lua.txt");
            // hello.lua не создаётся — пользователь сам решит когда нужен
            RefreshDesktopIcon();
        }

        public IList<string> InstalledAppNames()
        {
            return installedApps ?? new List<string>();
        }

        public List<string> RunningAppNames()
        {
            var r = new List<string>();
            if (runningApps == null) return r;
            for (int i = 0; i < runningApps.Count; i++)
            {
                var a = runningApps[i];
                if (a != null) r.Add(a.AppName);
            }
            return r;
        }

        public bool TryLaunchApp(string name, string content = "")
        {
            if (string.IsNullOrEmpty(name) || appPrefabs == null) return false;
            App prefab;
            if (!appPrefabs.TryGetValue(name, out prefab) || prefab == null) return false;
            if (!IsAppInstalled(name)) return false;
            LaunchApp(prefab, content ?? "");
            return true;
        }

        public bool TryCloseApp(string name)
        {
            var app = GetRunningApp(name);
            if (app == null) return false;
            app.Close();
            return true;
        }

        public bool LuaOpen(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (TryLaunchApp(name)) return true;
            File file;
            if (FileManager != null && FileManager.TryGetFile(0, name, out file) && file != null)
                return OpenFile(file);
            return false;
        }

        public List<string> ListUserFiles()
        {
            var r = new List<string>();
            var all = AllStorage;
            if (all == null || all.Count == 0 || all[0] == null || all[0].files == null) return r;
            var files = all[0].files;
            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                if (f == null || f.hidden) continue;
                r.Add(f.path);
            }
            return r;
        }

        public bool TryReadFile(string path, out string content)
        {
            content = null;
            if (FileManager == null || string.IsNullOrEmpty(path)) return false;
            File file;
            if (!FileManager.TryGetFile(0, path, out file) || file == null) return false;
            content = file.content ?? "";
            return true;
        }

        public bool TryWriteFile(string path, string content)
        {
            if (FileManager == null || string.IsNullOrEmpty(path)) return false;
            var file = FileManager.Write(0, path, content ?? "");
            RefreshDesktopIcon();
            return file != null;
        }

        public bool FileExists(string path)
        {
            return FileManager != null && !string.IsNullOrEmpty(path) && FileManager.Exists(0, path);
        }

        public bool TryDeleteFile(string path)
        {
            if (FileManager == null || string.IsNullOrEmpty(path)) return false;
            if (!FileManager.Exists(0, path)) return false;
            FileManager.Delete(0, path);
            RefreshDesktopIcon();
            return true;
        }

        private void AddApp(string name)
        {
            if (appPrefabs == null || installedApps == null) return;
            if (!appPrefabs.ContainsKey(name))
            {
                UnityEngine.Debug.LogErrorFormat("App ({0}) not found!", name);
                return;
            }
            installedApps.Add(name);
        }

        private void AddFileIcon(File file)
        {
            if (file == null || fileIcons == null) return;

            var key = file.path;
            if (fileIcons.ContainsKey(key))
                return;

            var iconInstance = Instantiate(fileIconPrefab, iconParent);
            if (iconInstance == null) return;

            // Ensure DesktopIconDragger exists and is initialized
            var dragger = iconInstance.GetComponent<DesktopIconDragger>();
            if (dragger == null)
                dragger = iconInstance.gameObject.AddComponent<DesktopIconDragger>();
            // Reinit after a frame to ensure everything is ready
            StartCoroutine(ReinitDragger(dragger));

            iconInstance.Init(file, f =>
            {
                if (f.isFolder)
                {
                    if (IsAppInstalled("File Manager", out var prefab))
                    {
                        LaunchApp(prefab, "");

                        var existing = GetRunningApp(prefab.AppName);
                        var fm = existing as PC.Component.Software.FileManager;
                        if (fm != null)
                            fm.OpenFolderFromPath(f.path);

                        FocusApp(true);
                    }
                    return;
                }

                OpenFile(f);
            });

            if (file.isFolder)
                iconInstance.Sprite = folderSprite;
            else
                iconInstance.Sprite = GetFileSprite(file.path);

            // Add to fileIcons first so FindFreeSpawnPosition can see it
            fileIcons.Add(key, iconInstance);
            
            // Restore saved position or find free spawn position
            if (iconPositions != null && iconPositions.ContainsKey(key))
            {
                iconInstance.SetPosition(iconPositions[key]);
            }
            else
            {
                iconInstance.SetPosition(FindFreeSpawnPosition(key));
            }
        }

        public Sprite GetFileSprite(string fileName)
        {
            File packed;
            if (FileManager != null && FileManager.TryGetFile(0, fileName, out packed) && packed != null
                && LuaAppPackage.IsPackage(packed.content))
            {
                var pack = LuaAppPackage.Parse(packed.content);
                Sprite fallback = unknownFileSprite;
                App luaApp;
                if (appPrefabs != null && appPrefabs.TryGetValue("Lua App", out luaApp) && luaApp != null)
                    fallback = luaApp.Icon != null ? luaApp.Icon : unknownFileSprite;
                if (pack != null) return pack.MakeIcon(fallback);
            }

            var ext = File.Extension(fileName);
            if (installedApps == null || appPrefabs == null) return unknownFileSprite;

            for (int i = 0; i < installedApps.Count; i++)
            {
                var appName = installedApps[i];
                if (!appPrefabs.TryGetValue(appName, out var app) || app == null) continue;

                if (ext == ".exe")
                {
                    var name = File.NameWithoutExtension(fileName);
                    if (string.Equals(name, app.AppName)) return app.Icon;
                }

                if (!string.IsNullOrEmpty(ext) && string.Equals(app.FileName, ext)) return app.FileIcon;
            }

            return unknownFileSprite;
        }

        public bool LaunchLuaApp(string content)
        {
            return RunLua(content, null);
        }

        public bool RunLua(string content, Action<string> printer)
        {
            var pack = LuaAppPackage.Parse(content);
            var script = pack != null ? pack.script : content;
            if (LuaAppPackage.NeedsWindow(script))
                return SpawnLuaApp(content ?? "", printer) != null;
            RunLuaHeadless(script ?? "", printer);
            return true;
        }

        void RunLuaHeadless(string script, Action<string> printer)
        {
            var vm = new PcosLua();
            vm.Printer = printer;
            PcosLuaHost.Bind(vm, this);
            vm.SetNative("isdraggable", a => LuaValue.Bool(false));
            vm.SetNative("Isdraggable", a => LuaValue.Bool(false));
            vm.SetNative("ismaximable", a => LuaValue.Bool(false));
            vm.SetNative("ismaxible", a => LuaValue.Bool(false));
            vm.SetNative("ismaximizable", a => LuaValue.Bool(false));
            vm.SetNative("enabledebugger", a => LuaValue.Nil);
            vm.SetNative("onupdate", a => LuaValue.Nil);
            try { vm.DoString(script ?? ""); }
            catch (Exception ex)
            {
                if (printer != null) printer("error: " + ex.Message);
                ShowMessageBox("Lua", ex.Message);
            }
        }

        public LuaApp SpawnLuaApp(string content, Action<string> printer)
        {
            App prefab = null;
            if (appPrefabs != null) appPrefabs.TryGetValue("Lua App", out prefab);
            if (prefab == null) return null;

            var app = Instantiate(prefab, appParent);
            if (app == null) return null;

            app.Init(this);
            var lua = app as LuaApp;
            if (lua != null && printer != null) lua.Printer = printer;
            RegisterRunningApp(app);
            app.AppClosed += ResetAppState;
            app.Open(content ?? "");
            FocusApp(true);
            return lua;
        }

        public bool OpenFile(File file)
        {
            if (file == null || appPrefabs == null) return false;
            if (LuaAppPackage.IsPackage(file.content))
                return LaunchLuaApp(file.content);

            if (installedApps == null) return false;
            var ext = file.Extension();

            for (int i = 0; i < installedApps.Count; i++)
            {
                var appName = installedApps[i];
                if (!appPrefabs.TryGetValue(appName, out var prefab) || prefab == null) continue;

                var match = false;

                if (ext == ".exe")
                {
                    var name = file.NameWithoutExtension();
                    if (string.Equals(name, prefab.AppName)) match = true;
                }
                else if (!string.IsNullOrEmpty(ext))
                {
                    match = prefab.FileName == ext;
                }

                if (!match) continue;

                LaunchApp(prefab, file.content);
                return true;
            }

            return false;
        }

        public void ShowMenuBar(App app)
        {
            if (app == null || app.MenuBar == null || app.MenuBar.Length == 0 || menuBar == null) return;

            for (int i = menuBar.childCount - 1; i >= 0; i--)
            {
                var c = menuBar.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }

            var items = app.MenuBar;
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var btn = Instantiate(menuBarItem, menuBar);
                if (btn == null) continue;

                var captured = item;
                btn.onClick.AddListener(() => captured.onClick?.Invoke());

                var img = btn.GetComponent<Image>();
                if (img != null) img.sprite = captured.icon;
            }

            var go = menuBar.gameObject;
            if (go != null) go.SetActive(true);
        }

        private void ResetAppState()
        {
            FocusApp(false);

            // ИСПРАВЛЕНИЕ БАГА: Прячем панель меню при закрытии программы (Paint и др.)
            if (menuBar != null)
            {
                menuBar.gameObject.SetActive(false);
            }
        }

        private void FocusApp(bool focus)
        {
            if (!focus)
            {
                // При unfocus — скрываем меню-бар если все окна закрыты
                if (runningApps.Count == 0 && menuBar != null)
                    menuBar.gameObject.SetActive(false);
                return;
            }
            // При focus — поднимаем последнее приложение наверх
            if (runningApps.Count > 0)
            {
                var last = runningApps[runningApps.Count - 1];
                if (last != null && last.transform != null)
                    last.transform.SetAsLastSibling();
            }
        }

        public void OnFileIconDropped(ReorderableList.ReorderableListEventStruct reorderableListEventStruct)
        {
            var all = AllStorage;
            if (all == null || all.Count == 0) return;

            var storage = all[0];
            var files = storage != null ? storage.files : null;
            if (files == null) return;

            int from = reorderableListEventStruct.FromIndex;
            int to = reorderableListEventStruct.ToIndex;
            if (from < 0 || from >= files.Count) return;
            if (to < 0) to = 0;
            if (to > files.Count) to = files.Count;

            var f = files[from];
            files.RemoveAt(from);
            if (to > files.Count) to = files.Count;
            files.Insert(to, f);

            StartCoroutine(WaitRefresh());
        }


        private string GetIconPositionsKey()
        {
            return "icon_positions_" + SystemId.ToString("X8");
        }

        public void SaveIconPosition(string key, Vector2 position)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (iconPositions == null) iconPositions = new Dictionary<string, Vector2>();
            iconPositions[key] = position;

            var sb = new System.Text.StringBuilder();
            foreach (var kvp in iconPositions)
            {
                if (sb.Length > 0) sb.Append(";");
                sb.Append(kvp.Key).Append(",")
                  .Append(kvp.Value.x.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",")
                  .Append(kvp.Value.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            PlayerPrefs.SetString(GetIconPositionsKey(), sb.ToString());
        }

        private void LoadIconPositions()
        {
            if (iconPositions == null) iconPositions = new Dictionary<string, Vector2>();

            var data = PlayerPrefs.GetString(GetIconPositionsKey(), "");
            if (string.IsNullOrEmpty(data)) return;

            var entries = data.Split(';');
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry)) continue;
                var parts = entry.Split(',');
                if (parts.Length == 3)
                {
                    float x, y;
                    if (float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) &&
                        float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y))
                        iconPositions[parts[0]] = new Vector2(x, y);
                }
            }
        }

        private IEnumerator WaitRefresh()
        {
            yield return new WaitForEndOfFrame();
            RefreshDesktopIcon();
        }



        private System.Collections.IEnumerator ReinitDragger(DesktopIconDragger dragger)
        {
            yield return null; // Wait one frame
            if (dragger != null)
                dragger.Init();
        }

        private Vector2 FindFreeSpawnPosition(string fileName = null)
        {
            if (iconParent == null) return Vector2.zero;
            
            float padding = 20f;
            float cellWidth = 70f;
            float cellHeight = 70f;
            float spacingX = 20f;
            float spacingY = 20f;
            float bottomPadding = 60f;
            
            float gridStepX = cellWidth + spacingX;
            float gridStepY = cellHeight + spacingY;
            
            var parentRT = iconParent.GetComponent<RectTransform>();
            if (parentRT == null) return Vector2.zero;
            
            // Use parentRT (iconParent) rect size, NOT canvas.pixelRect.
            // Icons are positioned relative to iconParent, so we must use its dimensions.
            float pw = parentRT.rect.width;
            float ph = parentRT.rect.height;
            
            // If parent rect is not yet initialized, use screen size as fallback
            if (pw < 100 || ph < 100)
            {
                pw = Screen.width;
                ph = Screen.height;
                Debug.LogWarning($"[FindFreeSpawn] parentRT not ready ({parentRT.rect.width}x{parentRT.rect.height}), using Screen: {pw}x{ph}");
            }
            
            float originX = -pw / 2f + padding + cellWidth / 2f;
            float originY = ph / 2f - padding - cellHeight / 2f;
            float maxX = pw / 2f - padding - cellWidth / 2f;
            float maxY = -ph / 2f + bottomPadding + cellHeight / 2f;
            
            // Get occupied cells from existing icons
            var occupied = new HashSet<Vector2Int>();
            foreach (var kvp in fileIcons)
            {
                if (kvp.Value == null) continue;
                var rt = kvp.Value.GetComponent<RectTransform>();
                if (rt == null) continue;
                
                int col = Mathf.RoundToInt((rt.anchoredPosition.x - originX) / gridStepX);
                int row = Mathf.RoundToInt((originY - rt.anchoredPosition.y) / gridStepY);
                occupied.Add(new Vector2Int(col, row));
            }
            
            // Find first free cell in grid order (left-to-right in first row, then next row down)
            // This is the standard row-major layout
            int maxCols = Mathf.Max(1, Mathf.FloorToInt((maxX - originX) / gridStepX) + 1);
            int maxRows = Mathf.Max(1, Mathf.FloorToInt((originY - maxY) / gridStepY) + 1);
            
            Debug.Log($"[FindFreeSpawn] Grid: {maxCols} cols x {maxRows} rows, parentRT: {pw}x{ph}, occupied: {occupied.Count} cells");
            
            // Row-major order: fill first row left-to-right, then next row
            for (int row = 0; row < maxRows; row++)
            {
                for (int col = 0; col < maxCols; col++)
                {
                    if (!occupied.Contains(new Vector2Int(col, row)))
                    {
                        float x = originX + col * gridStepX;
                        float y = originY - row * gridStepY;
                        return new Vector2(x, y);
                    }
                }
            }
            
            // Fallback: return origin if all cells are occupied
            return new Vector2(originX, originY);
        }

        public void RefreshDesktopIcon()
        {
            // Collect positions before destroying icons
            if (fileIcons != null && iconPositions != null)
            {
                foreach (var kvp in fileIcons)
                {
                    if (kvp.Value != null)
                        iconPositions[kvp.Key] = kvp.Value.GetPosition();
                }
                fileIcons.Clear();
            }

            if (iconParent != null)
            {
                for (int i = iconParent.childCount - 1; i >= 0; i--)
                {
                    var c = iconParent.GetChild(i);
                    if (c != null) DestroyImmediate(c.gameObject);
                }
            }

            var all = AllStorage;
            if (all == null || all.Count == 0) return;

            var storage = all[0];
            var files = storage != null ? storage.files : null;
            if (files == null) return;

            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                if (f == null) continue;

                if (f.path.Contains("/")) continue;
                if (f.isFolder && f.path == "System") continue;

                AddFileIcon(f);
            }

            // Delay layout rebuild to ensure canvas is ready
            StartCoroutine(DelayedLayoutRebuild());
        }

        private System.Collections.IEnumerator DelayedLayoutRebuild()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            
            if (iconParent != null)
            {
                var rt = iconParent.GetComponent<RectTransform>();
                if (rt != null)
                    UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(rt);
            }
            
            // Re-apply positions after layout is ready
            ReapplyIconPositions();
        }

        private void ReapplyIconPositions()
        {
            if (fileIcons == null || iconPositions == null) return;
            
            foreach (var kvp in fileIcons)
            {
                if (kvp.Value != null && iconPositions.ContainsKey(kvp.Key))
                {
                    kvp.Value.SetPosition(iconPositions[kvp.Key]);
                }
            }
        }

        public List<DeviceDetail> ListInstalledDevices()
        {
            var list = new WirelessDeviceList(this);
            return list.ListAllDevices();
        }

        public List<DeviceDetail> DiscoverDevices()
        {
            var result = new List<DeviceDetail>();
            var board = Board;
            if (board == null) return result;

            var devices = board.FindWirelessDevices();
            if (devices == null) return result;

            for (int i = 0; i < devices.Count; i++)
            {
                var d = devices[i];
                if (d == null) continue;

                var detail = new DeviceDetail(d.DeviceName, d.DeviceType, d.Id);

                result.Add(detail);
            }

            return result;
        }

        public T ConnectDevice<T>(int id) where T : Device
        {
            var board = Board;
            if (board == null) return null;
            return board.ConnectDevice<T>(id);
        }

        public Texture2D UserPicture()
        {
            var user = userData;
            var fm = FileManager;
            if (user == null || fm == null) return defaultUserPicture;

            if (!fm.TryGetFile(0, user.userPicturePath, out var file) || file == null) return defaultUserPicture;

            var s = file.content;
            if (string.IsNullOrEmpty(s)) return defaultUserPicture;

            byte[] data;
            try { data = Convert.FromBase64String(s); } catch { return defaultUserPicture; }
            if (data == null || data.Length == 0) return defaultUserPicture;

            var tex = new Texture2D(2, 2);
            tex.filterMode = FilterMode.Point;
            ImageConversion.LoadImage(tex, data);
            tex.Apply();
            return tex;
        }

        private void SaveUserData()
        {
#if UNITY_EDITOR
            UnityEngine.Debug.Log(JsonUtility.ToJson(userData));
#endif
            var fm = FileManager;
            var content = JsonUtility.ToJson(userData);
            if (fm == null) return;
            var file = fm.Write(0, "System/user", content);
            if (file != null) file.hidden = true;
        }

        public void ShowMessageBox(string title, string message)
        {
            var box = messageBox;
            if (box != null) box.Show(title, message);
            var src = Board != null ? Board.Source : null;
            if (src != null) src.PlayOneShot(alertSound);
        }

        public void PrintPicture(Texture2D picture)
        {
            var svc = Instantiate(printServicePrefab, popup);
            if (svc == null) return;
            var tr = svc.transform;
            if (tr != null) tr.SetAsFirstSibling();
            svc.Show(this, picture);
        }

        // ================= TASKBAR & START MENU =================

        [Header("TASKBAR")]
        [SerializeField] private GameObject taskbar;
        [SerializeField] private Button startButton;
        [SerializeField] private Text clockText;
        [SerializeField] private Transform runningAppsContainer;
        [SerializeField] private Button runningAppButtonPrefab;

        [Header("START MENU")]
        [SerializeField] private GameObject startMenu;
        [SerializeField] private UnityEngine.Animator startMenuAnimator;
        [SerializeField] private RawImage startUserAvatar;
        [SerializeField] private Text startUserName;
        [SerializeField] private Transform installedAppsContainer;
        [SerializeField] private Button installedAppButtonPrefab;
        [SerializeField] private Button shutdownButton;

        private List<App> runningApps = new List<App>();

        private bool taskbarInitialized;

        private void InitializeTaskbar()
        {
            if (taskbarInitialized)
                return;

            taskbarInitialized = true;

            if (startButton != null)
                startButton.onClick.AddListener(ToggleStartMenu);

            if (shutdownButton != null)
                shutdownButton.onClick.AddListener(() => PowerClicked());
        }

        private void Update()
        {
            if (!Ready) return;

            if (clockText != null)
            {
                var now = System.DateTime.Now;
                clockText.text = $"{now.Hour:00}:{now.Minute:00}:{now.Second:00}";
            }
        }

        private void ToggleStartMenu()
        {
#if UNITY_EDITOR
            UnityEngine.Debug.Log("StartMenu Animator = " + startMenuAnimator);
            UnityEngine.Debug.Log("Controller = " +
                (startMenuAnimator != null
                    ? startMenuAnimator.runtimeAnimatorController
                    : null));
#endif
            if (startMenu == null)
                return;

            startMenuOpened = !startMenuOpened;

            if (startMenuOpened)
            {
                startMenu.SetActive(true);
                startMenu.transform.SetAsLastSibling();

                if (startMenuAnimator != null)
                    startMenuAnimator.SetBool("Open", true);

                RefreshStartMenu();
            }
            else
            {
                if (startMenuAnimator != null)
                    startMenuAnimator.SetBool("Open", false);

                StartCoroutine(HideStartAfterAnim());
            }
        }

        private IEnumerator HideStartAfterAnim()
        {
            yield return new WaitForSeconds(0.3f);

            if (!startMenuOpened && startMenu != null)
                startMenu.SetActive(false);
        }

        private void RefreshStartMenu()
        {
            if (startUserAvatar != null)
                startUserAvatar.texture = UserPicture();

            if (startUserName != null)
                startUserName.text = UserName;

            if (installedAppsContainer == null) return;

            for (int i = installedAppsContainer.childCount - 1; i >= 0; i--)
                Destroy(installedAppsContainer.GetChild(i).gameObject);

            foreach (var appName in installedApps)
            {
                if (!appPrefabs.TryGetValue(appName, out var prefab))
                    continue;

                var btn = Instantiate(installedAppButtonPrefab, installedAppsContainer);

                var img = btn.transform.GetChild(0).GetComponent<Image>();
                if (img != null)
                    img.sprite = prefab.Icon;

                var txt = btn.transform.GetChild(1).GetComponent<Text>();
                if (txt != null)
                    txt.text = appName;

                btn.onClick.AddListener(() =>
                {
                    LaunchApp(prefab);
                    ToggleStartMenu();
                });
            }
        }

        private void RegisterRunningApp(App app)
        {
            if (app == null) return;

            runningApps.Add(app);

            app.AppClosed += () =>
            {
                runningApps.Remove(app);
                RefreshRunningAppsUI();
            };

            RefreshRunningAppsUI();
        }

        private void RefreshRunningAppsUI()
        {
            if (runningAppsContainer == null) return;

            for (int i = runningAppsContainer.childCount - 1; i >= 0; i--)
                Destroy(runningAppsContainer.GetChild(i).gameObject);

            foreach (var app in runningApps)
            {
                var btn = Instantiate(runningAppButtonPrefab, runningAppsContainer);

                var img = btn.GetComponent<Image>();
                if (img != null)
                    img.sprite = app.Icon;

                btn.onClick.AddListener(() =>
                {
                    if (app != null)
                        app.transform.SetAsLastSibling();
                });
            }
        }

        private App GetRunningApp(string appName)
        {
            foreach (var app in runningApps)
            {
                if (app != null && app.AppName == appName)
                    return app;
            }
            return null;
        }

        private void LaunchApp(App prefab, string content = "")
        {
            if (prefab == null) return;

            if (prefab.SingleInstance)
            {
                var existing = GetRunningApp(prefab.AppName);
                if (existing != null)
                {
                    existing.transform.SetAsLastSibling();
                    if (!string.IsNullOrEmpty(content))
                        existing.Open(content);
                    FocusApp(true);
                    return;
                }
            }

            var app = Instantiate(prefab, appParent);
            if (app == null) return;

            app.Init(this);
            RegisterRunningApp(app);
            app.AppClosed += ResetAppState;
            app.Open(content);

            FocusApp(true);
        }

        public void ImportWallpaperFromDevice(byte[] imageBytes)
        {
            if (FileManager == null || imageBytes == null)
                return;

            string content = Convert.ToBase64String(imageBytes);

            FileManager.Write(0, "System/Wallpaper.pic", content);

            userData.customBackgroundPath = "System/Wallpaper.pic";
            SaveUserData();
            Desktop();
        }

        private void EnableTaskbar()
        {
            if (taskbar != null)
                taskbar.SetActive(true);

            InitializeTaskbar();
        }
    }
}