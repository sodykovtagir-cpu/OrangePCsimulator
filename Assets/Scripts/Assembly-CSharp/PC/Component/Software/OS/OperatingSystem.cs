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
		private class User
		{
			public string userPicturePath;

			public string userName;

			public int background;
		}

		[SerializeField]
		private UnityEngine.Animator animator;

		[SerializeField]
		private Sprite[] texDesktop;

		[SerializeField]
		private AudioClip shutdownSound;

		[SerializeField]
		private AudioClip errorSound;

		[SerializeField]
		private AudioClip alertSound;

		[SerializeField]
		private Texture2D defaultUserPicture;

		[Header("Startup")]
		[SerializeField]
		private AudioClip loginFailSound;

		[SerializeField]
		private GameObject startup;

		[SerializeField]
		private GameObject user;

		[SerializeField]
		private GameObject loading;

		[SerializeField]
		private GameObject password;

		[SerializeField]
		private RawImage userPicture;

		[SerializeField]
		private InputField passwordInput;

		[SerializeField]
		private Text userText;

		[SerializeField]
		private UnityEngine.Animator passwordAnimator;

		[Header("Desktop")]
		[SerializeField]
		private Sprite unknownFileSprite;

		[SerializeField]
		private CanvasGroup desktop;

		[SerializeField]
		private FileIcon fileIconPrefab;

		[SerializeField]
		private Transform iconParent;

		[SerializeField]
		private Transform appParent;

		[SerializeField]
		private ProgressBar progressBar;

		[SerializeField]
		private MessageBox messageBox;

		[SerializeField]
		private PrintService printServicePrefab;

		[SerializeField]
		private OpenDialog fileDialog;

		[SerializeField]
		private SaveDialog saveDialog;

		[SerializeField]
		private DevicePicker devicePicker;

		[SerializeField]
		private Transform popup;

		[SerializeField]
		[Header("Menu Bar")]
		private Button menuBarItem;

		[SerializeField]
		private Transform menuBar;

		private bool busy;

		private bool error;

		private bool running;

		private App runningApp;

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
			get
			{
				return userData.userPicturePath;
			}
			set
            {
				userData.userPicturePath = value;
				SaveUserData();
            }
		}

		public string UserName
		{
			get
			{
				return userData.userName;
			}
			set
            {
				userData.userName = value;
				SaveUserData();
            }
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
			if (userData == null || texDesktop == null) return;

			int index = (int)userData.background;
			if (index < 0 || index >= texDesktop.Length) return;

			if (background != null) background.Sprite = texDesktop[index];
			if (animator != null) animator.SetTrigger("Enter");
			if (desktop != null) desktop.blocksRaycasts = true;

			if (iconParent != null)
			{
				var rl = iconParent.GetComponent<ReorderableList>();
				if (rl != null) rl.IsDraggable = true;
			}

			Ready = true;
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

			var app = runningApp;
			if (app != null) app.Close();

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
			}
		}

		public void UninstallApp(string softwareName)
		{
			var path = softwareName + ".exe";
			if (FileManager != null) FileManager.Delete(0, path);
			if (installedApps != null) installedApps.Remove(softwareName);
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
			{
				UnityEngine.Debug.LogError(key + " has been added to the list!");
				return;
			}

			var icon = Instantiate(fileIconPrefab, iconParent);
			if (icon == null) return;

			icon.Init(file, f =>
				{
					var ext = f.Extension();
					if (installedApps == null || appPrefabs == null) return;

					foreach (var name in installedApps)
					{
						if (!appPrefabs.TryGetValue(name, out var prefab) || prefab == null) continue;

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

						if (runningApp == null)
						{
							var app = Instantiate(prefab, appParent);
							app.Init(this);
							app.AppClosed += ResetAppState;
							app.Open(f.content);
							FocusApp(true);
							runningApp = app;
						}
						break;
					}
				});
			var sprite = GetFileSprite(file.path);
			icon.Sprite = sprite;

			fileIcons.Add(key, icon);
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

				if (runningApp != null) return false;

				var instance = Instantiate(prefab, appParent);
				if (instance == null) return false;

				instance.Init(this);
				instance.AppClosed += ResetAppState;
				instance.Open(file.content);
				FocusApp(true);
				runningApp = instance;
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
			runningApp = null;
			FocusApp(false);
			var t = menuBar;
			if (t != null)
			{
				var go = t.gameObject;
				if (go != null) go.SetActive(false);
				RefreshDesktopIcon();
			}
		}

		private void FocusApp(bool focus)
		{
			if (iconParent == null) return;
			var go = iconParent.gameObject;
			if (go != null) go.SetActive(!focus);
			var rl = iconParent.GetComponent<ReorderableList>();
			if (rl != null) rl.IsDraggable = !focus;
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
				if (f != null) AddFileIcon(f);
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

		public void UpdateBackground(int index)
		{
			var user = userData;
			var sprites = texDesktop;
			var bg = background;
			if (user == null || sprites == null || bg == null) return;
			user.background = index;
			if (index < 0 || index >= sprites.Length) return;
			bg.Sprite = sprites[index];
			SaveUserData();
		}

		private void SaveUserData()
		{
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
	}
}
