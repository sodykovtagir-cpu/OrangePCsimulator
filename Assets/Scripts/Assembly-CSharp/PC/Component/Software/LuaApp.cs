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

		PcosLua vm;
		LuaUi ui;
		LuaGfx gfx;
		GameObject maximizeGo;
		WindowDrag dragBar;
		GameObject debuggerGo;
		Text debuggerText;

		public Action<string> Printer;

		public override bool SingleInstance => false;
		protected override bool ShowMenuBar => false;

		void Awake()
		{
			canMaximize = false;
			canDrag = true;
		}

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
			BindWindowCommands();
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

		void BindWindowCommands()
		{
			if (vm == null) return;
			LuaValue Drag(LuaValue[] a)
			{
				if (a.Length == 0) return LuaValue.Bool(IsDraggable);
				SetDraggable(a[0].IsTruthy());
				return LuaValue.Nil;
			}
			LuaValue Max(LuaValue[] a)
			{
				if (a.Length == 0) return LuaValue.Bool(IsMaximizable);
				SetMaximizable(a[0].IsTruthy());
				return LuaValue.Nil;
			}
			vm.SetNative("isdraggable", Drag);
			vm.SetNative("Isdraggable", Drag);
			vm.SetNative("IsDraggable", Drag);
			vm.SetNative("ismaximable", Max);
			vm.SetNative("ismaxible", Max);
			vm.SetNative("ismaximizable", Max);
			vm.SetNative("Ismaximable", Max);
			vm.SetNative("enabledebugger", a => { EnableDebugger(); return LuaValue.Nil; });
			vm.SetNative("EnableDebugger", a => { EnableDebugger(); return LuaValue.Nil; });
		}

		void HandlePrint(string line)
		{
			if (line == null) line = "";
			if (Printer != null) Printer(line);
			AppendDebugger(line);
		}

		void HideLeftoverEditor()
		{
			var leftover = transform.Find("InputFieldMod_Scroll");
			if (leftover != null) leftover.gameObject.SetActive(false);
			var log = transform.Find("LuaLog");
			if (log != null) log.gameObject.SetActive(false);
		}

		void EnsureContent()
		{
			if (contentRoot != null) return;
			var go = new GameObject("Content", typeof(RectTransform), typeof(Image));
			go.transform.SetParent(transform, false);
			contentRoot = go.GetComponent<RectTransform>();
			contentRoot.anchorMin = Vector2.zero;
			contentRoot.anchorMax = Vector2.one;
			contentRoot.offsetMin = new Vector2(6, 6);
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
			EnsureMaximizeButton();
			if (maximizeGo != null) maximizeGo.SetActive(canMaximize);
		}

		void EnsureMaximizeButton()
		{
			if (maximizeGo != null) return;
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
			var state = maximizeGo.GetComponent<Image>();
			if (state != null) SetWindowStateImage(state);
		}

		public void EnableDebugger()
		{
			if (debuggerGo != null)
			{
				debuggerGo.SetActive(true);
				debuggerGo.transform.SetAsLastSibling();
				return;
			}

			var parent = transform.parent != null ? transform.parent : transform;
			debuggerGo = new GameObject("LuaDebug", typeof(RectTransform), typeof(Image));
			debuggerGo.transform.SetParent(parent, false);
			var rt = debuggerGo.GetComponent<RectTransform>();
			rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
			rt.pivot = new Vector2(0.5f, 0.5f);
			rt.sizeDelta = new Vector2(380, 170);
			rt.anchoredPosition = new Vector2(160, -90);
			var bg = debuggerGo.GetComponent<Image>();
			bg.color = new Color(0.05f, 0.08f, 0.07f, 0.96f);
			bg.raycastTarget = true;

			var bar = new GameObject("Drag", typeof(RectTransform), typeof(Image), typeof(WindowDrag));
			bar.transform.SetParent(debuggerGo.transform, false);
			var br = bar.GetComponent<RectTransform>();
			br.anchorMin = new Vector2(0f, 1f);
			br.anchorMax = new Vector2(1f, 1f);
			br.pivot = new Vector2(0.5f, 1f);
			br.anchoredPosition = Vector2.zero;
			br.sizeDelta = new Vector2(0f, 28f);
			bar.GetComponent<Image>().color = new Color(0.12f, 0.18f, 0.16f, 1f);
			bar.GetComponent<Image>().raycastTarget = true;

			var title = new GameObject("Title", typeof(RectTransform), typeof(Text));
			title.transform.SetParent(bar.transform, false);
			var tr = title.GetComponent<RectTransform>();
			tr.anchorMin = Vector2.zero;
			tr.anchorMax = Vector2.one;
			tr.offsetMin = new Vector2(8, 0);
			tr.offsetMax = new Vector2(-8, 0);
			var tt = title.GetComponent<Text>();
			tt.font = titleText != null ? titleText.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
			tt.fontSize = 14;
			tt.color = new Color(0.7f, 1f, 0.7f, 1f);
			tt.alignment = TextAnchor.MiddleLeft;
			tt.raycastTarget = false;
			tt.text = "Debug";
			tt.horizontalOverflow = HorizontalWrapMode.Overflow;
			tt.verticalOverflow = VerticalWrapMode.Overflow;

			var body = new GameObject("Log", typeof(RectTransform), typeof(Text));
			body.transform.SetParent(debuggerGo.transform, false);
			var lr = body.GetComponent<RectTransform>();
			lr.anchorMin = Vector2.zero;
			lr.anchorMax = Vector2.one;
			lr.offsetMin = new Vector2(8, 8);
			lr.offsetMax = new Vector2(-8, -32);
			debuggerText = body.GetComponent<Text>();
			debuggerText.font = tt.font;
			debuggerText.fontSize = 13;
			debuggerText.color = new Color(0.75f, 1f, 0.75f, 1f);
			debuggerText.alignment = TextAnchor.UpperLeft;
			debuggerText.horizontalOverflow = HorizontalWrapMode.Wrap;
			debuggerText.verticalOverflow = VerticalWrapMode.Overflow;
			debuggerText.raycastTarget = false;
			debuggerText.supportRichText = false;
			debuggerText.text = "";
		}

		void AppendDebugger(string line)
		{
			if (debuggerGo == null || !debuggerGo.activeSelf || debuggerText == null) return;
			if (string.IsNullOrEmpty(debuggerText.text)) debuggerText.text = line;
			else debuggerText.text = debuggerText.text + "\n" + line;
			if (debuggerText.text.Length > 16000)
				debuggerText.text = debuggerText.text.Substring(debuggerText.text.Length - 12000);
		}

		public override void Maximize()
		{
			if (!canMaximize && !maximized) return;
			if (rect == null) rect = GetComponent<RectTransform>();
			if (rect == null) return;
			var was = maximized;
			maximized = !was;
			if (!was) FitToDesktop();
			else
			{
				var center = new Vector2(0.5f, 0.5f);
				rect.anchorMin = center;
				rect.anchorMax = center;
				rect.sizeDelta = defaultSize.sqrMagnitude > 1f ? defaultSize : defaultWindowSize;
				rect.anchoredPosition = Vector2.zero;
			}
		}

		public override void SetDraggable(bool on)
		{
			base.SetDraggable(on);
			if (dragBar != null) dragBar.enabled = on;
		}

		public override void SetMaximizable(bool on)
		{
			EnsureMaximizeButton();
			base.SetMaximizable(on);
			if (maximizeGo != null) maximizeGo.SetActive(on);
		}

		public override void Close()
		{
			if (ui != null) ui.Clear();
			if (gfx != null) gfx.Destroy();
			if (debuggerGo != null) Destroy(debuggerGo);
			debuggerGo = null;
			debuggerText = null;
			vm = null;
			ui = null;
			gfx = null;
			base.Close();
		}
	}
}
