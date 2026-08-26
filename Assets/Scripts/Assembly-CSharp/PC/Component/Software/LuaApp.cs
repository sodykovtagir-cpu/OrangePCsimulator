using System;
using PC.Component.Software.Lua;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class LuaApp : App
	{
		[SerializeField] private Text titleText;
		[SerializeField] private Image chrome;
		[SerializeField] private RectTransform contentRoot;
		[SerializeField] private Vector2 defaultWindowSize = new Vector2(420, 280);
		[SerializeField] private Text logText;

		PcosLua vm;
		LuaUi ui;
		LuaGfx gfx;
		GameObject maximizeGo;
		WindowDrag dragBar;

		public Action<string> Printer;

		public override bool SingleInstance => false;
		protected override bool ShowMenuBar => false;

		public void ApplyChrome(Color color)
		{
			if (chrome != null) chrome.color = color;
			var img = GetComponent<Image>();
			if (img != null) img.color = color;
		}

		public void SetTitle(string title)
		{
			if (titleText != null) titleText.text = title ?? "Lua";
		}

		public void SetWindowSize(float w, float h)
		{
			SetDefaultSize(new Vector2(Mathf.Clamp(w, 160, 1200), Mathf.Clamp(h, 120, 800)));
		}

		public void FlipGfx()
		{
			if (gfx != null) gfx.Apply();
		}

		public Texture2D LoadUserTexture(string path)
		{
			if (system == null || string.IsNullOrEmpty(path)) return null;
			string content;
			if (!system.TryReadFile(path, out content) || string.IsNullOrEmpty(content)) return null;
			try
			{
				var bytes = Convert.FromBase64String(content);
				var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
				tex.filterMode = FilterMode.Point;
				if (!tex.LoadImage(bytes)) return null;
				return tex;
			}
			catch
			{
				return null;
			}
		}

		public override void Open(string content)
		{
			base.Open(content);
			rect = GetComponent<RectTransform>();
			if (chrome == null) chrome = GetComponent<Image>();
			if (titleText == null)
			{
				var t = transform.Find("Title");
				if (t != null) titleText = t.GetComponent<Text>();
			}
			HideLeftoverEditor();
			EnsureContent();
			EnsureChrome();
			var pack = LuaAppPackage.Parse(content);
			if (pack == null) pack = new LuaAppPackage { name = "Lua", script = content ?? "" };
			SetTitle(pack.name);
			SetWindowSize(defaultWindowSize.x, defaultWindowSize.y);
			if (ui != null) ui.Clear();
			if (gfx != null) gfx.Destroy();
			vm = new PcosLua();
			vm.Printer = HandlePrint;
			PcosLuaHost.Bind(vm, system);
			var font = titleText != null ? titleText.font : null;
			ui = new LuaUi(contentRoot, font, vm);
			ui.OnTitle = SetTitle;
			ui.OnSize = SetWindowSize;
			ui.Bind();
			gfx = new LuaGfx(contentRoot, vm);
			gfx.Bind();
			try { vm.DoString(pack.script ?? ""); }
			catch (Exception ex)
			{
				HandlePrint("error: " + ex.Message);
				if (system != null) system.ShowMessageBox(pack.name ?? "Lua", ex.Message);
			}
			if (gfx != null) gfx.Apply();
		}

		void HandlePrint(string line)
		{
			if (line == null) line = "";
			AppendLog(line);
			if (Printer != null) Printer(line);
		}

		void AppendLog(string line)
		{
			EnsureLog();
			if (logText == null) return;
			if (string.IsNullOrEmpty(logText.text)) logText.text = line;
			else logText.text = logText.text + "\n" + line;
			if (logText.text.Length > 16000)
				logText.text = logText.text.Substring(logText.text.Length - 12000);
		}

		void HideLeftoverEditor()
		{
			var leftover = transform.Find("InputFieldMod_Scroll");
			if (leftover != null) leftover.gameObject.SetActive(false);
		}

		void EnsureContent()
		{
			if (contentRoot != null) return;
			var go = new GameObject("Content", typeof(RectTransform), typeof(Image));
			go.transform.SetParent(transform, false);
			contentRoot = go.GetComponent<RectTransform>();
			contentRoot.anchorMin = Vector2.zero;
			contentRoot.anchorMax = Vector2.one;
			contentRoot.offsetMin = new Vector2(6, 24);
			contentRoot.offsetMax = new Vector2(-6, -36);
			go.GetComponent<Image>().color = new Color(0.63f, 0.83f, 0.82f, 1f);
			go.GetComponent<Image>().raycastTarget = true;
		}

		void EnsureChrome()
		{
			if (dragBar == null)
			{
				var existing = transform.Find("DragBar");
				GameObject go;
				if (existing != null) go = existing.gameObject;
				else
				{
					go = new GameObject("DragBar", typeof(RectTransform), typeof(Image));
					go.transform.SetParent(transform, false);
					go.transform.SetAsFirstSibling();
					var rt = go.GetComponent<RectTransform>();
					rt.anchorMin = new Vector2(0f, 1f);
					rt.anchorMax = new Vector2(1f, 1f);
					rt.pivot = new Vector2(0.5f, 1f);
					rt.anchoredPosition = Vector2.zero;
					rt.sizeDelta = new Vector2(-70f, 36f);
					var img = go.GetComponent<Image>();
					img.color = new Color(0f, 0f, 0f, 0.01f);
					img.raycastTarget = true;
				}
				dragBar = go.GetComponent<WindowDrag>();
				if (dragBar == null) dragBar = go.AddComponent<WindowDrag>();
			}
			dragBar.enabled = canDrag;

			if (maximizeGo == null)
			{
				var existing = transform.Find("Maximize");
				if (existing != null) maximizeGo = existing.gameObject;
				else
				{
					maximizeGo = new GameObject("Maximize", typeof(RectTransform), typeof(Image), typeof(Button));
					maximizeGo.transform.SetParent(transform, false);
					var rt = maximizeGo.GetComponent<RectTransform>();
					rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
					rt.pivot = new Vector2(0.5f, 0.5f);
					rt.anchoredPosition = new Vector2(-52f, -20f);
					rt.sizeDelta = new Vector2(26f, 26f);
					var img = maximizeGo.GetComponent<Image>();
					img.color = new Color(0.93f, 0.96f, 0.95f, 1f);
					img.raycastTarget = true;
					var btn = maximizeGo.GetComponent<Button>();
					btn.targetGraphic = img;
					btn.onClick.AddListener(Maximize);
				}
			}
			var state = maximizeGo.GetComponent<Image>();
			if (state != null) SetWindowStateImage(state);
			maximizeGo.SetActive(canMaximize);
			EnsureLog();
		}

		void EnsureLog()
		{
			if (logText != null) return;
			var existing = transform.Find("LuaLog");
			GameObject go;
			if (existing != null) go = existing.gameObject;
			else
			{
				go = new GameObject("LuaLog", typeof(RectTransform), typeof(Text));
				go.transform.SetParent(transform, false);
				var rt = go.GetComponent<RectTransform>();
				rt.anchorMin = new Vector2(0f, 0f);
				rt.anchorMax = new Vector2(1f, 0f);
				rt.pivot = new Vector2(0.5f, 0f);
				rt.anchoredPosition = new Vector2(0f, 2f);
				rt.sizeDelta = new Vector2(-12f, 20f);
			}
			logText = go.GetComponent<Text>();
			if (logText == null) logText = go.AddComponent<Text>();
			if (logText.font == null)
				logText.font = titleText != null ? titleText.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
			logText.fontSize = 12;
			logText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
			logText.alignment = TextAnchor.MiddleLeft;
			logText.horizontalOverflow = HorizontalWrapMode.Overflow;
			logText.verticalOverflow = VerticalWrapMode.Truncate;
			logText.raycastTarget = false;
			logText.supportRichText = false;
		}

		public override void SetDraggable(bool on)
		{
			base.SetDraggable(on);
			if (dragBar != null) dragBar.enabled = on;
		}

		public override void SetMaximizable(bool on)
		{
			base.SetMaximizable(on);
			if (maximizeGo != null) maximizeGo.SetActive(on);
		}

		public override void Close()
		{
			if (ui != null) ui.Clear();
			if (gfx != null) gfx.Destroy();
			vm = null;
			ui = null;
			gfx = null;
			base.Close();
		}
	}
}
