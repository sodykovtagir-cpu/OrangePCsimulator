using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PC.Component;
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
            currentLayoutContext = ResolveIconLayoutContext();
            LoadIconPositions();
            LoadCurrentSortMode();
            TrackIconParentSize(true);

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

            // Orange custom wallpaper path
            public string customBackgroundPath;
            // 1.8.3 custom wallpaper path (same file; both written on save)
            public string bgPath;
            public int wallpaperMode;
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
        private Coroutine startMenuRoutine;
        private bool running;
        private int storageScore;
        private CoverImage background;
        private Dictionary<string, App> appPrefabs = new Dictionary<string, App>();
        private List<string> installedApps = new List<string>();
        private Dictionary<string, FileIcon> fileIcons = new Dictionary<string, FileIcon>();
        private Dictionary<string, Vector2> iconPositions = new Dictionary<string, Vector2>();
        private Canvas desktopCanvas;
        private string currentLayoutContext;
        private Vector2 lastKnownIconParentSize;
        private bool iconParentSizeInitialized;
        private bool layoutSwitchPending;
        private const string DefaultIconLayoutContext = "system";
        private const string MonitorIconLayoutContext = "monitor";
        private const string DefaultSortMode = "Name";
        private const float IconGridPadding = 20f;
        private const float IconGridCellWidth = 70f;
        private const float IconGridCellHeight = 70f;
        private const float IconGridSpacingX = 20f;
        private const float IconGridSpacingY = 20f;
        private const float IconGridBottomPadding = 60f;
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
            if (NormalizeWallpaperFields(userData))
                SaveUserData();

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
            if (userData == null) return;

            ApplyWallpaper();

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
            userData.customBackgroundPath = path ?? "";
            userData.bgPath = userData.customBackgroundPath;
            userData.background = -1;
            SaveUserData();
            ApplyWallpaper();
        }

        public int WallpaperMode
        {
            get { return userData != null ? userData.wallpaperMode : 0; }
            set
            {
                if (userData == null) return;
                userData.wallpaperMode = value;
                SaveUserData();
                ApplyWallpaper();
            }
        }

        private static bool NormalizeWallpaperFields(User u)
        {
            if (u == null) return false;
            bool changed = false;
            if (string.IsNullOrEmpty(u.customBackgroundPath) && !string.IsNullOrEmpty(u.bgPath))
            {
                u.customBackgroundPath = u.bgPath;
                changed = true;
            }
            else if (string.IsNullOrEmpty(u.bgPath) && !string.IsNullOrEmpty(u.customBackgroundPath))
            {
                u.bgPath = u.customBackgroundPath;
                changed = true;
            }
            return changed;
        }

        private void ApplyWallpaper()
        {
            if (background == null || userData == null) return;

            int mode = userData.wallpaperMode;
            if (mode < 0 || mode > 4) mode = 0;
            background.FitMode = (CoverImage.WallpaperFit)mode;

            string customPath = userData.customBackgroundPath;
            if (string.IsNullOrEmpty(customPath))
                customPath = userData.bgPath;

            if (!string.IsNullOrEmpty(customPath))
            {
                Sprite customSprite = LoadSpriteFromInGameFile(customPath);
                if (customSprite != null)
                {
                    background.Sprite = customSprite;
                    return;
                }
            }

            if (texDesktop == null) return;
            int index = userData.background;
            if (index >= 0 && index < texDesktop.Length)
                background.Sprite = texDesktop[index];
        }

        // Вспомогательный метод для превращения внутриигрового файла в Sprite
        private Sprite LoadSpriteFromInGameFile(string path)
        {
            if (FileManager == null || string.IsNullOrEmpty(path)) return null;
            if (!FileManager.TryGetFile(0, path, out var file) || file == null) return null;
            if (string.IsNullOrEmpty(file.content)) return null;

            try
            {
                var tex = FormatConverter.StringToTexture(file.content);
                if (tex == null) return null;
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            catch
            {
                return null;
            }
        }

        public void UpdateBackground(int index)
        {
            if (userData == null) return;

            if (index >= 0)
            {
                userData.background = index;
                userData.customBackgroundPath = "";
                userData.bgPath = "";
            }
            else
            {
                userData.background = -1;
                NormalizeWallpaperFields(userData);
            }

            SaveUserData();
            ApplyWallpaper();
        }

        public void UpdateBackground(int index, string path)
        {
            if (userData == null) return;
            userData.bgPath = path ?? "";
            userData.customBackgroundPath = userData.bgPath;
            userData.background = index;
            SaveUserData();
            ApplyWallpaper();
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

        public void SelectFile(string[] extensions, Action<File> callback)
        {
            var dlg = fileDialog;
            if (dlg != null) dlg.SelectFile(extensions, callback);
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
            EnsureViewer();
            // EnsureLuaEditor(); // Отключено — Lua Editor не устанавливается по умолчанию
        }

        private void EnsureViewer()
        {
            if (appPrefabs == null || !appPrefabs.TryGetValue("Viewer", out var prefab) || prefab == null)
                return;
            if (FileManager == null) return;

            if (!FileManager.Exists(0, "Viewer.exe"))
            {
                var file = new File("Viewer.exe", "", true, prefab.size > 0 ? prefab.size : 128);
                FileManager.Create(0, file);
            }
            else
            {
                File existing;
                if (FileManager.TryGetFile(0, "Viewer.exe", out existing) && existing != null)
                    existing.hidden = true;
            }

            if (!IsAppInstalled("Viewer"))
                AddApp("Viewer");
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
                    OpenFolder(f);
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
            Vector2 finalPos;
            if (iconPositions != null && iconPositions.ContainsKey(key))
            {
                finalPos = iconPositions[key];
                Debug.Log($"[AddFileIcon] '{key}' => RESTORED position: {finalPos}");
            }
            else
            {
                finalPos = FindFreeSpawnPosition(key);
                Debug.Log($"[AddFileIcon] '{key}' => NEW position: {finalPos}");
            }

            if (iconPositions == null) iconPositions = new Dictionary<string, Vector2>();
            iconPositions[key] = finalPos;
            iconInstance.SetPosition(finalPos);
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
            if (appPrefabs == null) return unknownFileSprite;

            if (ext == ".exe")
            {
                var name = File.NameWithoutExtension(fileName);
                if (!string.IsNullOrEmpty(name) && appPrefabs.TryGetValue(name, out var exeApp) && exeApp != null)
                    return exeApp.Icon;
            }

            var associated = FindAppForExtension(ext);
            if (associated != null && associated.FileIcon != null)
                return associated.FileIcon;

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
            if (file == null) return false;

            if (file.isFolder)
                return OpenFolder(file);

            if (LuaAppPackage.IsPackage(file.content))
                return LaunchLuaApp(file.content);

            var ext = file.Extension();
            App prefab = null;

            if (ext == ".exe")
            {
                var name = file.NameWithoutExtension();
                if (IsAppInstalled(name, out prefab) && prefab != null)
                {
                    LaunchApp(prefab, file.content ?? "");
                    return true;
                }

                ShowRequires(string.IsNullOrEmpty(name) ? "приложение" : name);
                return false;
            }

            prefab = FindInstalledAppForExtension(ext);
            if (prefab != null)
            {
                LaunchApp(prefab, file.content ?? "");
                return true;
            }

            var needed = FindAppForExtension(ext);
            ShowRequires(needed != null ? needed.AppName : "приложение");
            return false;
        }

        public bool OpenFileWith(File file, App app)
        {
            if (file == null || app == null) return false;
            if (file.isFolder)
                return OpenFolder(file);

            if (!IsAppInstalled(app.AppName))
            {
                ShowRequires(app.AppName);
                return false;
            }

            LaunchApp(app, file.content ?? "");
            return true;
        }

        public void ShowRequires(string appName)
        {
            if (string.IsNullOrEmpty(appName))
                appName = "приложение";
            ShowMessageBox("Ошибка", "Требует " + appName);
        }

        private bool OpenFolder(File folder)
        {
            if (folder == null || !folder.isFolder) return false;

            App prefab;
            if (!IsAppInstalled("File Manager", out prefab) || prefab == null)
            {
                ShowRequires("File Manager");
                return false;
            }

            LaunchApp(prefab, "");

            var existing = GetRunningApp(prefab.AppName);
            var fm = existing as PC.Component.Software.FileManager;
            if (fm != null)
                fm.OpenFolderFromPath(folder.path);

            FocusApp(true);
            return true;
        }

        public List<App> GetOpenWithApps(File file)
        {
            var result = new List<App>();
            if (file == null || file.isFolder || appPrefabs == null)
                return result;

            var ext = file.Extension();
            if (string.IsNullOrEmpty(ext))
                return result;

            void Add(string name)
            {
                if (string.IsNullOrEmpty(name) || !appPrefabs.TryGetValue(name, out var app) || app == null)
                    return;
                if (!result.Contains(app))
                    result.Add(app);
            }

            foreach (var kvp in appPrefabs)
            {
                var app = kvp.Value;
                if (app == null) continue;
                bool match = !string.IsNullOrEmpty(app.FileName)
                    && string.Equals(app.FileName, ext, StringComparison.OrdinalIgnoreCase);
                if (!match) match = app.CanOpenExtension(ext);
                if (match) Add(app.AppName);
            }

            if (ext.Equals(".pic", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".mov", StringComparison.OrdinalIgnoreCase))
                Add("Viewer");

            if (IsTextLikeExtension(ext))
            {
                Add("Text Editor");
                Add("Lua Editor");
            }

            return result;
        }

        private static bool IsTextLikeExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            return ext.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".lua", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".tmn", StringComparison.OrdinalIgnoreCase);
        }

        private App FindInstalledAppForExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext) || appPrefabs == null || installedApps == null) return null;

            for (int i = 0; i < installedApps.Count; i++)
            {
                if (!appPrefabs.TryGetValue(installedApps[i], out var installed) || installed == null)
                    continue;
                if (string.Equals(installed.FileName, ext, StringComparison.OrdinalIgnoreCase))
                    return installed;
            }

            return null;
        }

        private App FindAppForExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext) || appPrefabs == null) return null;

            var installed = FindInstalledAppForExtension(ext);
            if (installed != null) return installed;

            // Ищем приложение, которое заявляет поддержку расширения
            // через CanOpenExtension (например, Viewer для .pic/.mov).
            if (installedApps != null && appPrefabs != null)
            {
                for (int i = 0; i < installedApps.Count; i++)
                {
                    App cand;
                    if (!appPrefabs.TryGetValue(installedApps[i], out cand) || cand == null)
                        continue;
                    if (cand.CanOpenExtension(ext))
                        return cand;
                }
            }

            foreach (var kvp in appPrefabs)
            {
                var app = kvp.Value;
                if (app == null) continue;
                if (string.Equals(app.FileName, ext, StringComparison.OrdinalIgnoreCase))
                    return app;
            }

            return null;
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


        private struct IconGridMetrics
        {
            public float originX;
            public float originY;
            public float maxX;
            public float maxY;
            public float stepX;
            public float stepY;
            public int maxCols;
            public int maxRows;
        }

        private string NormalizeLayoutContext(string layoutContext)
        {
            return string.IsNullOrEmpty(layoutContext) ? DefaultIconLayoutContext : layoutContext;
        }

        private Canvas GetDesktopCanvas()
        {
            if (desktopCanvas != null) return desktopCanvas;
            if (iconParent != null) desktopCanvas = iconParent.GetComponentInParent<Canvas>();
            if (desktopCanvas == null && desktop != null) desktopCanvas = desktop.GetComponentInParent<Canvas>();
            return desktopCanvas;
        }

        private string ResolveIconLayoutContext()
        {
            var canvas = GetDesktopCanvas();
            if (canvas == null) return DefaultIconLayoutContext;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return DefaultIconLayoutContext;

            var board = Board;
            var monitor = board != null ? board.monitor : null;
            if (monitor != null && monitor.Id != 0)
                return MonitorIconLayoutContext + "_" + monitor.Id.ToString("X8");

            return MonitorIconLayoutContext;
        }

        private bool IsMonitorLayoutContext(string layoutContext)
        {
            return NormalizeLayoutContext(layoutContext).StartsWith(MonitorIconLayoutContext, StringComparison.Ordinal);
        }

        private string GetLegacyIconPositionsKey()
        {
            return "icon_positions_" + SystemId.ToString("X8");
        }

        private string GetLegacyMonitorIconPositionsKey()
        {
            return "icon_positions_" + SystemId.ToString("X8") + "_" + MonitorIconLayoutContext;
        }

        private string GetIconPositionsKey(string layoutContext = null)
        {
            return "icon_positions_" + SystemId.ToString("X8") + "_" + NormalizeLayoutContext(layoutContext ?? currentLayoutContext);
        }

        private string GetLegacyMonitorSortModeKey()
        {
            return "icon_sort_mode_" + SystemId.ToString("X8") + "_" + MonitorIconLayoutContext;
        }

        private string GetIconSortModeKey(string layoutContext = null)
        {
            return "icon_sort_mode_" + SystemId.ToString("X8") + "_" + NormalizeLayoutContext(layoutContext ?? currentLayoutContext);
        }

        private void LoadCurrentSortMode()
        {
            var layoutContext = NormalizeLayoutContext(currentLayoutContext ?? ResolveIconLayoutContext());
            currentSortMode = PlayerPrefs.GetString(GetIconSortModeKey(layoutContext), "");

            if (string.IsNullOrEmpty(currentSortMode) && IsMonitorLayoutContext(layoutContext))
                currentSortMode = PlayerPrefs.GetString(GetLegacyMonitorSortModeKey(), "");

            if (string.IsNullOrEmpty(currentSortMode))
                currentSortMode = DefaultSortMode;
        }

        private void SaveCurrentSortMode()
        {
            if (string.IsNullOrEmpty(currentSortMode))
                currentSortMode = DefaultSortMode;
            PlayerPrefs.SetString(GetIconSortModeKey(), currentSortMode);
        }

        private void PersistIconPositions()
        {
            if (iconPositions == null)
                iconPositions = new Dictionary<string, Vector2>();

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

        private void ParseIconPositions(string data, Dictionary<string, Vector2> target)
        {
            if (target == null || string.IsNullOrEmpty(data)) return;

            var entries = data.Split(';');
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry)) continue;
                var parts = entry.Split(',');
                if (parts.Length != 3) continue;

                float x, y;
                if (float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) &&
                    float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y))
                {
                    target[parts[0]] = new Vector2(x, y);
                }
            }
        }

        public void SaveIconPosition(string key, Vector2 position)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (iconPositions == null) iconPositions = new Dictionary<string, Vector2>();
            iconPositions[key] = position;
            PersistIconPositions();
        }

        private void LoadIconPositions()
        {
            if (iconPositions == null) iconPositions = new Dictionary<string, Vector2>();
            else iconPositions.Clear();

            currentLayoutContext = NormalizeLayoutContext(currentLayoutContext ?? ResolveIconLayoutContext());

            var data = PlayerPrefs.GetString(GetIconPositionsKey(), "");
            bool migratedLegacy = false;
            if (string.IsNullOrEmpty(data) && IsMonitorLayoutContext(currentLayoutContext))
            {
                data = PlayerPrefs.GetString(GetLegacyMonitorIconPositionsKey(), "");
                migratedLegacy = !string.IsNullOrEmpty(data);
            }
            if (string.IsNullOrEmpty(data))
            {
                data = PlayerPrefs.GetString(GetLegacyIconPositionsKey(), "");
                migratedLegacy = !string.IsNullOrEmpty(data);
            }

            ParseIconPositions(data, iconPositions);

            if (migratedLegacy && iconPositions.Count > 0)
                PersistIconPositions();
        }

        private Dictionary<string, Vector2> CaptureVisibleIconPositions()
        {
            var result = new Dictionary<string, Vector2>();
            if (fileIcons == null) return result;

            foreach (var kvp in fileIcons)
            {
                if (kvp.Value != null)
                    result[kvp.Key] = kvp.Value.GetPosition();
            }

            return result;
        }

        private void SaveCapturedPositionsToCurrentLayout(Dictionary<string, Vector2> capturedPositions)
        {
            if (capturedPositions == null) return;
            if (iconPositions == null) iconPositions = new Dictionary<string, Vector2>();

            foreach (var kvp in capturedPositions)
                iconPositions[kvp.Key] = kvp.Value;

            PersistIconPositions();
        }

        private Vector2 GetIconParentSize()
        {
            if (iconParent != null)
            {
                var parentRT = iconParent.GetComponent<RectTransform>();
                if (parentRT != null && parentRT.rect.width >= 100f && parentRT.rect.height >= 100f)
                    return parentRT.rect.size;
            }

            return new Vector2(Screen.width, Screen.height);
        }

        private void TrackIconParentSize(bool force = false)
        {
            var size = GetIconParentSize();
            if (size.x <= 0f || size.y <= 0f) return;

            if (force || !iconParentSizeInitialized || size != lastKnownIconParentSize)
            {
                lastKnownIconParentSize = size;
                iconParentSizeInitialized = true;
            }
        }

        private IconGridMetrics BuildIconGridMetrics(Vector2 size)
        {
            float pw = size.x;
            float ph = size.y;

            if (pw < 100f || ph < 100f)
            {
                pw = Screen.width;
                ph = Screen.height;
            }

            var metrics = new IconGridMetrics();
            metrics.stepX = IconGridCellWidth + IconGridSpacingX;
            metrics.stepY = IconGridCellHeight + IconGridSpacingY;
            metrics.originX = -pw / 2f + IconGridPadding + IconGridCellWidth / 2f;
            metrics.originY = ph / 2f - IconGridPadding - IconGridCellHeight / 2f;
            metrics.maxX = pw / 2f - IconGridPadding - IconGridCellWidth / 2f;
            metrics.maxY = -ph / 2f + IconGridBottomPadding + IconGridCellHeight / 2f;
            metrics.maxCols = Mathf.Max(1, Mathf.FloorToInt((metrics.maxX - metrics.originX) / metrics.stepX) + 1);
            metrics.maxRows = Mathf.Max(1, Mathf.FloorToInt((metrics.originY - metrics.maxY) / metrics.stepY) + 1);
            return metrics;
        }

        private Vector2Int ClampGridCell(Vector2Int cell, IconGridMetrics metrics)
        {
            return new Vector2Int(
                Mathf.Clamp(cell.x, 0, metrics.maxCols - 1),
                Mathf.Clamp(cell.y, 0, metrics.maxRows - 1));
        }

        private Vector2Int GetGridCellFromPosition(Vector2 position, IconGridMetrics metrics)
        {
            int col = Mathf.RoundToInt((position.x - metrics.originX) / metrics.stepX);
            int row = Mathf.RoundToInt((metrics.originY - position.y) / metrics.stepY);
            return ClampGridCell(new Vector2Int(col, row), metrics);
        }

        private Vector2 GetPositionFromGridCell(Vector2Int cell, IconGridMetrics metrics)
        {
            var clampedCell = ClampGridCell(cell, metrics);
            float x = metrics.originX + clampedCell.x * metrics.stepX;
            float y = metrics.originY - clampedCell.y * metrics.stepY;
            return new Vector2(x, y);
        }

        private Vector2 FindFreeCellPosition(Vector2Int desiredCell, IconGridMetrics metrics, HashSet<Vector2Int> occupied)
        {
            var clampedDesired = ClampGridCell(desiredCell, metrics);
            if (occupied == null) occupied = new HashSet<Vector2Int>();

            if (!occupied.Contains(clampedDesired))
            {
                occupied.Add(clampedDesired);
                return GetPositionFromGridCell(clampedDesired, metrics);
            }

            int maxSearch = Mathf.Max(metrics.maxCols, metrics.maxRows) + 8;
            for (int radius = 1; radius <= maxSearch; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                            continue;

                        var cell = ClampGridCell(new Vector2Int(clampedDesired.x + dx, clampedDesired.y + dy), metrics);
                        if (occupied.Contains(cell))
                            continue;

                        occupied.Add(cell);
                        return GetPositionFromGridCell(cell, metrics);
                    }
                }
            }

            return GetPositionFromGridCell(clampedDesired, metrics);
        }

        private Vector2 AdaptPositionToCurrentLayout(Vector2 sourcePosition, Vector2 sourceSize, HashSet<Vector2Int> occupied)
        {
            var sourceMetrics = BuildIconGridMetrics(sourceSize);
            var targetMetrics = BuildIconGridMetrics(GetIconParentSize());
            var desiredCell = GetGridCellFromPosition(sourcePosition, sourceMetrics);
            return FindFreeCellPosition(desiredCell, targetMetrics, occupied);
        }

        private Vector2 FitPositionToCurrentLayout(Vector2 position, HashSet<Vector2Int> occupied)
        {
            var targetMetrics = BuildIconGridMetrics(GetIconParentSize());
            var desiredCell = GetGridCellFromPosition(position, targetMetrics);
            return FindFreeCellPosition(desiredCell, targetMetrics, occupied);
        }

        private IEnumerator ApplyLayoutContextChange(string newLayoutContext, Vector2 sourceSize, Dictionary<string, Vector2> sourcePositions)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            currentLayoutContext = NormalizeLayoutContext(newLayoutContext);
            LoadIconPositions();
            LoadCurrentSortMode();

            if (fileIcons != null && fileIcons.Count > 0)
            {
                var orderedKeys = new List<string>(fileIcons.Keys);
                orderedKeys.Sort(StringComparer.OrdinalIgnoreCase);

                var occupied = new HashSet<Vector2Int>();
                bool changed = false;

                for (int i = 0; i < orderedKeys.Count; i++)
                {
                    var key = orderedKeys[i];
                    if (!fileIcons.TryGetValue(key, out var icon) || icon == null)
                        continue;

                    Vector2 nextPos;
                    if (iconPositions.TryGetValue(key, out var savedPos))
                    {
                        nextPos = FitPositionToCurrentLayout(savedPos, occupied);
                        if (savedPos != nextPos)
                        {
                            iconPositions[key] = nextPos;
                            changed = true;
                        }
                    }
                    else if (sourcePositions != null && sourcePositions.TryGetValue(key, out var previousPos))
                    {
                        nextPos = AdaptPositionToCurrentLayout(previousPos, sourceSize, occupied);
                        iconPositions[key] = nextPos;
                        changed = true;
                    }
                    else
                    {
                        nextPos = FindFreeCellPosition(new Vector2Int(0, 0), BuildIconGridMetrics(GetIconParentSize()), occupied);
                        iconPositions[key] = nextPos;
                        changed = true;
                    }

                    icon.SetPosition(nextPos);
                }

                if (changed)
                    PersistIconPositions();
            }

            TrackIconParentSize(true);
            layoutSwitchPending = false;
        }

        private void CheckForLayoutContextChange()
        {
            if (!Ready || layoutSwitchPending)
                return;

            var layoutContext = ResolveIconLayoutContext();
            if (string.IsNullOrEmpty(currentLayoutContext))
            {
                currentLayoutContext = NormalizeLayoutContext(layoutContext);
                LoadIconPositions();
                LoadCurrentSortMode();
                return;
            }

            if (layoutContext == currentLayoutContext)
                return;

            var sourceSize = iconParentSizeInitialized ? lastKnownIconParentSize : GetIconParentSize();
            var sourcePositions = CaptureVisibleIconPositions();
            SaveCapturedPositionsToCurrentLayout(sourcePositions);
            SaveCurrentSortMode();

            layoutSwitchPending = true;
            StartCoroutine(ApplyLayoutContextChange(layoutContext, sourceSize, sourcePositions));
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

            var metrics = BuildIconGridMetrics(GetIconParentSize());
            var occupied = new HashSet<Vector2Int>();
            foreach (var kvp in fileIcons)
            {
                if (kvp.Value == null) continue;
                var rt = kvp.Value.GetComponent<RectTransform>();
                if (rt == null) continue;
                occupied.Add(GetGridCellFromPosition(rt.anchoredPosition, metrics));
            }

            Debug.Log($"[FindFreeSpawn] Grid: {metrics.maxCols} cols x {metrics.maxRows} rows, occupied: {occupied.Count} cells");

            for (int row = 0; row < metrics.maxRows; row++)
            {
                for (int col = 0; col < metrics.maxCols; col++)
                {
                    var cell = new Vector2Int(col, row);
                    if (occupied.Contains(cell))
                        continue;

                    return GetPositionFromGridCell(cell, metrics);
                }
            }

            return GetPositionFromGridCell(new Vector2Int(0, 0), metrics);
        }

        /// <summary>
        /// Reset all icon positions and re-arrange them in grid order (left-to-right, top-to-bottom).
        /// Clears saved positions from PlayerPrefs.
        /// </summary>
        public void AutoArrangeIcons()
        {
            if (iconPositions != null)
                iconPositions.Clear();
            
            PlayerPrefs.DeleteKey(GetIconPositionsKey());
            currentSortMode = DefaultSortMode;
            SaveCurrentSortMode();
            
            Debug.Log("[AutoArrangeIcons] Cleared current layout positions, re-arranging...");
            
            RefreshDesktopIcon(false);
        }

        /// <summary>
        /// Sort desktop icons by the specified mode and re-arrange them.
        /// Mode: "Name", "Size", "Type"
        /// </summary>
        public void SortDesktopIcons(string mode)
        {
            if (iconPositions != null)
                iconPositions.Clear();
            
            PlayerPrefs.DeleteKey(GetIconPositionsKey());
            currentSortMode = string.IsNullOrEmpty(mode) ? DefaultSortMode : mode;
            SaveCurrentSortMode();
            
            Debug.Log($"[SortDesktopIcons] Sorting current layout by {currentSortMode}...");
            
            RefreshDesktopIcon(false);
        }

        private string currentSortMode = DefaultSortMode;

        private class ClipboardEntry
        {
            public File file;
            public bool cut;
        }

        private ClipboardEntry clipboard;

        public bool HasClipboard
        {
            get { return clipboard != null && clipboard.file != null; }
        }

        public bool IsProtectedFile(File file)
        {
            if (file == null) return true;
            if (string.IsNullOrEmpty(file.path)) return true;
            if (file.path == "System" || file.path.StartsWith("System/")) return true;
            if (file.Extension() == ".exe" && !LuaAppPackage.IsPackage(file.content)) return true;
            return false;
        }

        public bool CanCopyFile(File file)
        {
            if (file == null || string.IsNullOrEmpty(file.path)) return false;
            if (file.path == "System" || file.path.StartsWith("System/")) return false;
            if (file.Extension() == ".exe")
                return LuaAppPackage.IsPackage(file.content);
            return true;
        }

        public void CopyToClipboard(File file)
        {
            if (!CanCopyFile(file)) return;
            clipboard = new ClipboardEntry { file = file, cut = false };
        }

        public void CutToClipboard(File file)
        {
            if (file == null || IsProtectedFile(file)) return;
            clipboard = new ClipboardEntry { file = file, cut = true };
        }

        public bool PasteClipboard(string targetFolder)
        {
            if (!HasClipboard || AllStorage == null || AllStorage.Count == 0 || AllStorage[0] == null)
                return false;

            var storage = AllStorage[0];
            var src = clipboard.file;
            if (src == null || storage.files == null) return false;
            if (!storage.files.Contains(src) && clipboard.cut)
            {
                clipboard = null;
                return false;
            }

            string srcFolder = GetFolderPath(src.path);
            string destFolder = targetFolder ?? "";
            if (clipboard.cut && srcFolder == destFolder)
            {
                clipboard = null;
                return true;
            }

            if (src.isFolder && !string.IsNullOrEmpty(destFolder) &&
                (destFolder == src.path || destFolder.StartsWith(src.path + "/")))
                return false;

            string destPath = UniquePath(CombinePath(destFolder, GetFileName(src.path)));

            if (clipboard.cut)
            {
                string oldPath = src.path;
                src.path = destPath;
                if (src.isFolder)
                    RelocateChildren(storage, oldPath, destPath);
                clipboard = null;
            }
            else
            {
                DuplicateTree(storage, src, destPath);
            }

            RefreshDesktopIcon();
            RefreshRunningFileManagers();
            return true;
        }

        public void HideUserFile(File file)
        {
            if (file == null || IsProtectedFile(file)) return;
            file.hidden = true;
            RefreshDesktopIcon();
            RefreshRunningFileManagers();
        }

        public void DeleteUserFile(File file)
        {
            if (file == null || IsProtectedFile(file)) return;
            if (AllStorage == null || AllStorage.Count == 0 || AllStorage[0] == null) return;

            var storage = AllStorage[0];
            if (storage.files == null) return;

            if (file.isFolder)
            {
                for (int i = storage.files.Count - 1; i >= 0; i--)
                {
                    var f = storage.files[i];
                    if (f != null && f.path != null && f.path.StartsWith(file.path + "/"))
                        storage.files.RemoveAt(i);
                }
            }

            storage.files.Remove(file);
            if (clipboard != null && clipboard.file == file)
                clipboard = null;

            RefreshDesktopIcon();
            RefreshRunningFileManagers();
        }

        public bool RenameUserFile(File file, string newName)
        {
            if (file == null || IsProtectedFile(file) || string.IsNullOrEmpty(newName)) return false;
            if (AllStorage == null || AllStorage.Count == 0 || AllStorage[0] == null) return false;

            newName = newName.Trim();
            if (string.IsNullOrEmpty(newName)) return false;
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                newName = newName.Replace(c, '_');

            if (!file.isFolder)
            {
                var oldExt = File.Extension(file.path);
                if (!string.IsNullOrEmpty(oldExt) && File.Extension(newName) == "")
                    newName += oldExt;
            }

            string dest = CombinePath(GetFolderPath(file.path), newName);
            if (dest != file.path && FileManager != null && FileManager.Exists(0, dest))
                dest = UniquePath(dest);
            if (dest == file.path) return true;

            string oldPath = file.path;
            file.path = dest;
            if (file.isFolder)
                RelocateChildren(AllStorage[0], oldPath, dest);

            RefreshDesktopIcon();
            RefreshRunningFileManagers();
            return true;
        }

        public void CreateFileAt(string folder, string name, string content)
        {
            if (FileManager == null || string.IsNullOrEmpty(name)) return;
            string path = UniquePath(CombinePath(folder, name));
            var body = content ?? "";
            var created = FileManager.Write(0, path, body);
            if (created == null)
            {
                ShowMessageBox(name, "Не удалось создать файл.");
                return;
            }

            created.hidden = false;
            created.isFolder = false;
            created.size = body.Length;
            RefreshDesktopIcon();
            RefreshRunningFileManagers();
        }

        public void CreateFolderAt(string folder, string name)
        {
            if (FileManager == null || string.IsNullOrEmpty(name)) return;
            string path = UniquePath(CombinePath(folder, name));
            if (!FileManager.Create(0, File.MakeFolder(path)))
            {
                ShowMessageBox(name, "Не удалось создать папку.");
                return;
            }

            RefreshDesktopIcon();
            RefreshRunningFileManagers();
        }

        public void CreateDesktopFile(string name, string content)
        {
            CreateFileAt("", name, content);
        }

        public void CreateDesktopFolder(string name)
        {
            CreateFolderAt("", name);
        }

        public string UniquePath(string path)
        {
            if (string.IsNullOrEmpty(path) || FileManager == null || !FileManager.Exists(0, path))
                return path;

            var ext = File.Extension(path);
            var folder = GetFolderPath(path);
            var baseName = File.NameWithoutExtension(GetFileName(path));
            if (string.IsNullOrEmpty(baseName))
                baseName = GetFileName(path);

            int counter = 1;
            string candidate;
            do
            {
                string name = string.IsNullOrEmpty(ext)
                    ? string.Format("{0} ({1})", baseName, counter)
                    : string.Format("{0} ({1}){2}", baseName, counter, ext);
                candidate = CombinePath(folder, name);
                counter++;
            } while (FileManager.Exists(0, candidate));

            return candidate;
        }

        private static string GetFolderPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            int slash = path.LastIndexOf('/');
            return slash <= 0 ? "" : path.Substring(0, slash);
        }

        private static string GetFileName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            int slash = path.LastIndexOf('/');
            return slash < 0 ? path : path.Substring(slash + 1);
        }

        private static string CombinePath(string folder, string name)
        {
            if (string.IsNullOrEmpty(folder)) return name ?? "";
            if (string.IsNullOrEmpty(name)) return folder;
            return folder + "/" + name;
        }

        private static void RelocateChildren(Storage storage, string oldPath, string newPath)
        {
            if (storage == null || storage.files == null || string.IsNullOrEmpty(oldPath)) return;
            string prefix = oldPath + "/";
            for (int i = 0; i < storage.files.Count; i++)
            {
                var f = storage.files[i];
                if (f == null || string.IsNullOrEmpty(f.path)) continue;
                if (f.path.StartsWith(prefix))
                    f.path = newPath + f.path.Substring(oldPath.Length);
            }
        }

        private static void DuplicateTree(Storage storage, File src, string destPath)
        {
            if (storage == null || storage.files == null || src == null) return;

            var clone = new File(destPath, src.content, src.hidden, src.size);
            clone.isFolder = src.isFolder;
            storage.AddFile(clone);

            if (!src.isFolder) return;

            string prefix = src.path + "/";
            var extras = new List<File>();
            for (int i = 0; i < storage.files.Count; i++)
            {
                var f = storage.files[i];
                if (f == null || f == src || string.IsNullOrEmpty(f.path)) continue;
                if (!f.path.StartsWith(prefix)) continue;
                extras.Add(f);
            }

            for (int i = 0; i < extras.Count; i++)
            {
                var f = extras[i];
                var childDest = destPath + f.path.Substring(src.path.Length);
                var childClone = new File(childDest, f.content, f.hidden, f.size);
                childClone.isFolder = f.isFolder;
                storage.AddFile(childClone);
            }
        }

        private void RefreshRunningFileManagers()
        {
            if (runningApps == null) return;
            for (int i = 0; i < runningApps.Count; i++)
            {
                var explorer = runningApps[i] as PC.Component.Software.FileManager;
                if (explorer != null) explorer.RefreshView();
            }

            if (startMenuOpened)
                RefreshStartMenu();
        }

        public bool BlocksDesktopMenu(GameObject go)
        {
            if (go == null) return false;
            if (go.GetComponentInParent<App>() != null) return true;
            if (startMenu != null && startMenu.activeInHierarchy &&
                (go.transform == startMenu.transform || go.transform.IsChildOf(startMenu.transform)))
                return true;
            if (taskbar != null && (go.transform == taskbar.transform || go.transform.IsChildOf(taskbar.transform)))
                return true;
            if (menuBar != null && go.transform.IsChildOf(menuBar.transform))
                return true;
            if (popup != null && go.transform.IsChildOf(popup.transform))
                return true;
            return false;
        }

        public bool IsDesktopContextTarget(GameObject go)
        {
            if (go == null || BlocksDesktopMenu(go)) return false;
            if (iconParent != null && (go.transform == iconParent || go.transform.IsChildOf(iconParent)))
                return true;
            if (desktop != null && (go == desktop.gameObject || go.transform.IsChildOf(desktop.transform)))
                return true;
            return false;
        }

        public void RefreshDesktopIcon(bool preserveCurrentPositions = true)
        {
            if (fileIcons == null)
                fileIcons = new Dictionary<string, FileIcon>();

            if (preserveCurrentPositions && iconPositions != null)
            {
                foreach (var kvp in fileIcons)
                {
                    if (kvp.Value != null)
                        iconPositions[kvp.Key] = kvp.Value.GetPosition();
                }
            }

            var desktopFiles = CollectDesktopFiles();

            if (!preserveCurrentPositions)
            {
                fileIcons.Clear();
                if (iconParent != null)
                {
                    for (int i = iconParent.childCount - 1; i >= 0; i--)
                    {
                        var c = iconParent.GetChild(i);
                        if (c != null) Destroy(c.gameObject);
                    }
                }
            }
            else
            {
                var wanted = new HashSet<string>();
                for (int i = 0; i < desktopFiles.Count; i++)
                {
                    var file = desktopFiles[i];
                    if (file != null && !string.IsNullOrEmpty(file.path))
                        wanted.Add(file.path);
                }

                var stale = new List<string>();
                foreach (var kvp in fileIcons)
                {
                    if (kvp.Value == null || !wanted.Contains(kvp.Key))
                        stale.Add(kvp.Key);
                }

                for (int i = 0; i < stale.Count; i++)
                {
                    FileIcon icon;
                    if (fileIcons.TryGetValue(stale[i], out icon) && icon != null)
                        Destroy(icon.gameObject);
                    fileIcons.Remove(stale[i]);
                }
            }

            for (int i = 0; i < desktopFiles.Count; i++)
                AddFileIcon(desktopFiles[i]);

            StartCoroutine(DelayedLayoutRebuild());
        }

        private List<File> CollectDesktopFiles()
        {
            var desktopFiles = new List<File>();
            var all = AllStorage;
            if (all == null || all.Count == 0) return desktopFiles;

            var storage = all[0];
            var files = storage != null ? storage.files : null;
            if (files == null) return desktopFiles;

            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                if (f == null) continue;
                if (f.hidden) continue;
                if (string.IsNullOrEmpty(f.path) || f.path.Contains("/")) continue;
                if (f.isFolder && f.path == "System") continue;
                desktopFiles.Add(f);
            }

            if (string.IsNullOrEmpty(currentSortMode))
                return desktopFiles;

            switch (currentSortMode)
            {
                case "Name":
                    desktopFiles.Sort((a, b) => {
                        if (a.isFolder && !b.isFolder) return -1;
                        if (!a.isFolder && b.isFolder) return 1;
                        return string.Compare(a.path, b.path, System.StringComparison.OrdinalIgnoreCase);
                    });
                    break;
                case "Size":
                    desktopFiles.Sort((a, b) => {
                        if (a.isFolder && !b.isFolder) return -1;
                        if (!a.isFolder && b.isFolder) return 1;
                        return b.size.CompareTo(a.size);
                    });
                    break;
                case "Type":
                    desktopFiles.Sort((a, b) => {
                        if (a.isFolder && !b.isFolder) return -1;
                        if (!a.isFolder && b.isFolder) return 1;
                        var extA = a.isFolder ? "" : File.Extension(a.path);
                        var extB = b.isFolder ? "" : File.Extension(b.path);
                        int cmp = string.Compare(extA, extB, System.StringComparison.OrdinalIgnoreCase);
                        if (cmp != 0) return cmp;
                        return string.Compare(a.path, b.path, System.StringComparison.OrdinalIgnoreCase);
                    });
                    break;
            }

            return desktopFiles;
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
            
            var occupied = new HashSet<Vector2Int>();
            foreach (var kvp in fileIcons)
            {
                if (kvp.Value == null || !iconPositions.ContainsKey(kvp.Key))
                    continue;

                var fitted = FitPositionToCurrentLayout(iconPositions[kvp.Key], occupied);
                iconPositions[kvp.Key] = fitted;
                kvp.Value.SetPosition(fitted);
            }

            PersistIconPositions();
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
            CheckForLayoutContextChange();
            TrackIconParentSize();

            if (!Ready) return;

            if (clockText != null)
            {
                var now = System.DateTime.Now;
                clockText.text = $"{now.Hour:00}:{now.Minute:00}:{now.Second:00}";
            }
        }

        private void ToggleStartMenu()
        {
            if (startMenu == null)
                return;

            startMenuOpened = !startMenuOpened;

            if (startMenuAnimator != null)
                startMenuAnimator.enabled = false;

            if (startMenuRoutine != null)
                StopCoroutine(startMenuRoutine);

            var menuRt = startMenu.transform as RectTransform;

            if (startMenuOpened)
            {
                startMenu.SetActive(true);
                startMenu.transform.SetAsLastSibling();
                RefreshStartMenu();
                startMenuRoutine = StartCoroutine(PC.Component.Software.WindowChrome.PlayStartMenu(menuRt, true));
            }
            else
            {
                startMenuRoutine = StartCoroutine(PC.Component.Software.WindowChrome.PlayStartMenu(menuRt, false));
            }
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

            var luaApps = ListLuaAppFiles();
            for (int i = 0; i < luaApps.Count; i++)
            {
                var file = luaApps[i];
                if (file == null || installedAppButtonPrefab == null) continue;

                var pack = LuaAppPackage.Parse(file.content);
                var btn = Instantiate(installedAppButtonPrefab, installedAppsContainer);
                if (btn == null) continue;

                Sprite icon = unknownFileSprite;
                App luaPrefab;
                if (appPrefabs != null && appPrefabs.TryGetValue("Lua App", out luaPrefab) && luaPrefab != null && luaPrefab.Icon != null)
                    icon = luaPrefab.Icon;
                if (pack != null)
                    icon = pack.MakeIcon(icon);

                if (btn.transform.childCount > 0)
                {
                    var img = btn.transform.GetChild(0).GetComponent<Image>();
                    if (img != null && icon != null)
                        img.sprite = icon;
                }

                if (btn.transform.childCount > 1)
                {
                    var txt = btn.transform.GetChild(1).GetComponent<Text>();
                    if (txt != null)
                        txt.text = pack != null && !string.IsNullOrEmpty(pack.name) ? pack.name : file.NameWithoutExtension();
                }

                var captured = file;
                btn.onClick.AddListener(() =>
                {
                    OpenFile(captured);
                    if (startMenuOpened)
                        ToggleStartMenu();
                });
            }
        }

        private List<File> ListLuaAppFiles()
        {
            var result = new List<File>();
            var all = AllStorage;
            if (all == null || all.Count == 0 || all[0] == null || all[0].files == null)
                return result;

            var files = all[0].files;
            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                if (f == null || f.hidden || f.isFolder) continue;
                if (string.IsNullOrEmpty(f.path) || f.path == "System" || f.path.StartsWith("System/")) continue;
                if (f.Extension() != ".exe") continue;
                if (!LuaAppPackage.IsPackage(f.content)) continue;
                result.Add(f);
            }

            return result;
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

                var captured = app;
                btn.onClick.AddListener(() =>
                {
                    if (captured == null) return;

                    if (captured.IsMinimized)
                    {
                        // Свернутое окно — разворачиваем и выводим на передний план.
                        captured.Restore();
                        FocusApp(true);
                    }
                    else
                    {
                        // Активное окно повторный клик по его кнопке сворачивает.
                        captured.Minimize();
                    }
                });
            }
        }

        public void OnAppMinimized(App app)
        {
            if (menuBar != null)
            {
                // Меню-бар (если показан) прячем при сворачивании активного окна.
                bool anyActive = false;
                for (int i = 0; i < runningApps.Count; i++)
                {
                    if (runningApps[i] != null && !runningApps[i].IsMinimized)
                    { anyActive = true; break; }
                }
                if (!anyActive) menuBar.gameObject.SetActive(false);
            }
            RefreshRunningAppsUI();
        }

        public void OnAppRestored(App app)
        {
            RefreshRunningAppsUI();
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
                    if (existing.IsMinimized)
                        existing.Restore();
                    existing.transform.SetAsLastSibling();
                    if (!string.IsNullOrEmpty(content)) existing.Open(content);
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
            if (FileManager == null || imageBytes == null || userData == null)
                return;

            string content = Convert.ToBase64String(imageBytes);

            FileManager.Write(0, "System/Wallpaper.pic", content);

            userData.customBackgroundPath = "System/Wallpaper.pic";
            userData.bgPath = "System/Wallpaper.pic";
            userData.background = -1;
            SaveUserData();
            ApplyWallpaper();
        }

        private void EnableTaskbar()
        {
            if (taskbar != null)
                taskbar.SetActive(true);

            InitializeTaskbar();
        }
    }
}
