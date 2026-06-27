using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;

namespace PC.Component.Software.OS
{
    public class OperatingSystem : ComputerSystem
    {
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
                ud = JsonUtility.FromJson<User>(uf.content);
            else
                ud = new User { userName = "User" };

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
            Debug.Log("customBackgroundPath = " + userData.customBackgroundPath);
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
                        var name = f.NameWithoutExtension();
                        AddApp(name);
                    }
                }
            }

            RefreshDesktopIcon();
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

            iconInstance.Init(file, f =>
            {
                if (f.isFolder)
                {
                    if (IsAppInstalled("File Manager", out var prefab))
                    {
                        var app = Instantiate(prefab, appParent);
                        if (app == null) return;

                        app.Init(this);
                        RegisterRunningApp(app);
                        app.AppClosed += ResetAppState;
                        app.Open("");

                        var fm = app as PC.Component.Software.FileManager;
                        if (fm != null)
                            fm.OpenFolderFromPath(f.path);

                        FocusApp(true);
                    }
                    return;
                }

                var ext = f.Extension();

                foreach (var name in installedApps)
                {
                    if (!appPrefabs.TryGetValue(name, out var prefab) || prefab == null)
                        continue;

                    bool match = false;

                    if (ext == ".exe")
                    {
                        var noExt = f.NameWithoutExtension();
                        match = noExt == prefab.AppName;
                    }
                    else if (!string.IsNullOrEmpty(ext))
                    {
                        match = prefab.FileName == ext;
                    }

                    if (!match) continue;

                    var app = Instantiate(prefab, appParent);
                    if (app == null) return;

                    app.Init(this);
                    RegisterRunningApp(app);
                    app.AppClosed += ResetAppState;
                    app.Open(f.content);

                    FocusApp(true);
                    break;
                }
            });

            if (file.isFolder)
                iconInstance.Sprite = folderSprite;
            else
                iconInstance.Sprite = GetFileSprite(file.path);

            fileIcons.Add(key, iconInstance);
        }

        public Sprite GetFileSprite(string fileName)
        {
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

        public bool OpenFile(File file)
        {
            if (file == null || installedApps == null || appPrefabs == null) return false;

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
                else if (!string.IsNullOrEmpty(ext) && string.Equals(".exe", ext)) match = true;

                if (!match) continue;

                var instance = Instantiate(prefab, appParent);
                if (instance == null) return false;

                instance.Init(this);
                instance.AppClosed += ResetAppState;
                instance.Open(file.content);
                FocusApp(true);
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

        private IEnumerator WaitRefresh()
        {
            yield return new WaitForEndOfFrame();
            RefreshDesktopIcon();
        }

        public void RefreshDesktopIcon()
        {
            if (fileIcons != null) fileIcons.Clear();

            if (iconParent != null)
            {
                for (int i = iconParent.childCount - 1; i >= 0; i--)
                {
                    var c = iconParent.GetChild(i);
                    if (c != null) Destroy(c.gameObject);
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
            Debug.Log(JsonUtility.ToJson(userData));
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
        private float gameTime;

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

            gameTime += Time.deltaTime;

            if (clockText != null)
            {
                int h = Mathf.FloorToInt(gameTime / 3600);
                int m = Mathf.FloorToInt((gameTime % 3600) / 60);
                int s = Mathf.FloorToInt(gameTime % 60);

                clockText.text = $"{h:00}:{m:00}:{s:00}";
            }
        }

        private void ToggleStartMenu()
        {
            Debug.Log("StartMenu Animator = " + startMenuAnimator);
            Debug.Log("Controller = " +
                (startMenuAnimator != null
                    ? startMenuAnimator.runtimeAnimatorController
                    : null));
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
                    var app = Instantiate(prefab, appParent);
                    app.Init(this);
                    RegisterRunningApp(app);
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