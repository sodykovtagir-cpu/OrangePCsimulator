using System;
using System.Collections;
using PC.Component.Software.Lua;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class LuaEditor : App
	{
		[SerializeField] private InputField input;

		private string filePath = "Untitled.lua";
		private bool uiReady;
		private InputField code;
		private Text output;
		private Text docsText;
		private GameObject docsPanel;
		private Transform snippetRoot;
		private Font font;

		static readonly string[] Snippets =
		{
			"print(\"|")",
			"os.alert(\"|", \"\")",
			"os.open(\"|")",
			"os.close(\"|")",
			"os.apps()",
			"os.windows()",
			"os.username()",
			"os.id()",
			"os.installed(\"|")",
			"os.shutdown()",
			"fs.list()",
			"fs.read(\"|")",
			"fs.write(\"|", \"\")",
			"fs.exists(\"|")",
			"fs.delete(\"|")",
			"win.alert(\"|")",
			"win.open(\"|")",
			"win.close(\"|")",
			"if | then\n  \nend",
			"while | do\n  \nend",
			"for i = 1, | do\n  \nend",
			"function |()\n  \nend",
			"local | = ",
			"tonumber(\"|")",
			"tostring(|)",
			"type(|)",
			"math.random(|)",
			"string.upper(\"|")",
			"table.insert(|, )"
		};

		public override void Open(string content)
		{
			base.Open(content);
			BuildUi();
			if (code != null)
				code.text = string.IsNullOrEmpty(content) ? DefaultSource() : content;
			if (string.IsNullOrEmpty(content)) filePath = "Untitled.lua";
			AppendOut(Localization.GetText("Lua ready. Click a hint to insert."));
		}

		protected override void Start()
		{
			base.Start();
			BuildUi();
			SetDefaultSize(new Vector2(760f, 460f));
		}

		public void OpenFile()
		{
			if (system == null) return;
			system.SelectFile("*", file =>
			{
				if (file == null || code == null) return;
				code.text = file.content ?? "";
				filePath = file.path;
				AppendOut("> " + filePath);
			});
		}

		public void Save()
		{
			if (system == null || system.SaveDialog == null || code == null) return;
			var name = File.NameWithoutExtension(filePath);
			system.SaveDialog.ShowDialog(name, code.text ?? "", new[] { ".lua", ".txt" });
		}

		public void Run()
		{
			if (code == null) return;
			if (output != null) output.text = "";
			var src = code.text ?? "";
			var vm = new PcosLua();
			vm.Printer = line => AppendOut(line);
			PcosLuaHost.Bind(vm, system);
			try
			{
				vm.DoString(src);
				AppendOut(Localization.GetText("Lua finished."));
			}
			catch (Exception ex)
			{
				AppendOut("error: " + ex.Message);
				if (system != null)
					system.ShowMessageBox("Lua", ex.Message);
			}
		}

		public void ToggleDocs()
		{
			if (docsPanel == null) return;
			docsPanel.SetActive(!docsPanel.activeSelf);
		}

		static string DefaultSource()
		{
			return "-- PCOS Lua\nprint(\"Hello, PCOS!\")\n-- os.alert(\"Lua\", \"It works!\")\n-- os.open(\"Text Editor\")\n";
		}

		void AppendOut(string line)
		{
			if (output == null) return;
			if (string.IsNullOrEmpty(output.text)) output.text = line;
			else output.text = output.text + "\n" + line;
		}

		void InsertSnippet(string snippet)
		{
			if (code == null) return;
			int mark = snippet.IndexOf('|');
			string ins = snippet.Replace("|", "");
			string t = code.text ?? "";
			int caret = code.caretPosition;
			if (caret < 0 || caret > t.Length) caret = t.Length;
			if (code.selectionAnchorPosition != code.selectionFocusPosition)
			{
				int a = Mathf.Min(code.selectionAnchorPosition, code.selectionFocusPosition);
				int b = Mathf.Max(code.selectionAnchorPosition, code.selectionFocusPosition);
				if (a >= 0 && b <= t.Length && b >= a)
				{
					t = t.Remove(a, b - a);
					caret = a;
				}
			}
			code.text = t.Insert(caret, ins);
			int pos = caret + (mark >= 0 ? mark : ins.Length);
			StartCoroutine(PlaceCaret(pos));
		}

		IEnumerator PlaceCaret(int pos)
		{
			code.ActivateInputField();
			yield return null;
			if (code == null) yield break;
			code.caretPosition = pos;
			code.selectionAnchorPosition = pos;
			code.selectionFocusPosition = pos;
			code.ForceLabelUpdate();
		}

		void BuildUi()
		{
			if (uiReady) return;
			uiReady = true;
			rect = GetComponent<RectTransform>();
			if (rect != null) rect.sizeDelta = new Vector2(760f, 460f);

			var title = transform.Find("Title");
			var titleText = title != null ? title.GetComponent<Text>() : null;
			if (titleText != null)
			{
				font = titleText.font;
				titleText.text = Localization.GetText("Lua Editor");
			}
			if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

			if (title != null)
			{
				var hit = title.GetComponent<Image>();
				if (hit == null) hit = title.gameObject.AddComponent<Image>();
				hit.color = new Color(0f, 0f, 0f, 0f);
				hit.raycastTarget = true;
				if (title.GetComponent<WindowDrag>() == null)
					title.gameObject.AddComponent<WindowDrag>();
			}

			if (input != null) input.gameObject.SetActive(false);
			var oldScroll = transform.Find("InputFieldMod_Scroll");
			if (oldScroll != null) oldScroll.gameObject.SetActive(false);

			var body = Panel("Body", transform, new Color(0.93f, 0.97f, 0.96f, 1f));
			Stretch(body, 4, 40, -4, -4);

			var bar = Panel("Bar", body, new Color(0.45f, 0.68f, 0.66f, 1f));
			SetRect(bar, 0, 1, 1, 1, 0, -28, 0, 0);

			MakeBtn(bar, Localization.GetText("Run"), 6, Run);
			MakeBtn(bar, Localization.GetText("Save"), 78, Save);
			MakeBtn(bar, Localization.GetText("Open"), 150, OpenFile);
			MakeBtn(bar, Localization.GetText("Docs"), 222, ToggleDocs);

			var outGo = Panel("Output", body, new Color(0.12f, 0.16f, 0.16f, 1f));
			SetRect(outGo, 0, 0, 1, 0, 0, 0, 0, 86);
			output = MakeText(outGo, "", 12, TextAnchor.UpperLeft, Color.white);
			var outRt = output.rectTransform;
			outRt.offsetMin = new Vector2(6, 4);
			outRt.offsetMax = new Vector2(-6, -4);
			output.horizontalOverflow = HorizontalWrapMode.Wrap;
			output.verticalOverflow = VerticalWrapMode.Overflow;

			var hints = Panel("Hints", body, new Color(0.82f, 0.91f, 0.89f, 1f));
			SetRect(hints, 1, 0, 1, 1, -210, 90, 0, -32);
			var hintTitle = MakeText(hints, Localization.GetText("Snippets"), 13, TextAnchor.MiddleLeft, Color.black);
			SetRect(hintTitle.rectTransform, 0, 1, 1, 1, 6, -22, -4, 0);

			var scroll = new GameObject("HintScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
			scroll.transform.SetParent(hints, false);
			var scrollImg = scroll.GetComponent<Image>();
			scrollImg.color = new Color(0.75f, 0.86f, 0.84f, 1f);
			SetRect(scroll.GetComponent<RectTransform>(), 0, 0, 1, 1, 4, 4, -4, -24);

			var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
			viewport.transform.SetParent(scroll.transform, false);
			viewport.GetComponent<Image>().color = Color.white;
			viewport.GetComponent<Mask>().showMaskGraphic = false;
			Stretch(viewport.GetComponent<RectTransform>(), 0, 0, 0, 0);

			var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
			content.transform.SetParent(viewport.transform, false);
			var vlg = content.GetComponent<VerticalLayoutGroup>();
			vlg.childForceExpandHeight = false;
			vlg.childForceExpandWidth = true;
			vlg.childControlHeight = true;
			vlg.childControlWidth = true;
			vlg.spacing = 2;
			vlg.padding = new RectOffset(3, 3, 3, 3);
			content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			var crt = content.GetComponent<RectTransform>();
			crt.anchorMin = new Vector2(0, 1);
			crt.anchorMax = new Vector2(1, 1);
			crt.pivot = new Vector2(0.5f, 1);
			crt.anchoredPosition = Vector2.zero;
			crt.sizeDelta = new Vector2(0, 0);

			var sr = scroll.GetComponent<ScrollRect>();
			sr.horizontal = false;
			sr.viewport = viewport.GetComponent<RectTransform>();
			sr.content = crt;
			sr.scrollSensitivity = 20f;
			snippetRoot = content.transform;

			for (int i = 0; i < Snippets.Length; i++)
			{
				var sn = Snippets[i];
				var label = sn.Replace("|", "").Replace("\n", " ");
				if (label.Length > 28) label = label.Substring(0, 28) + "…";
				var captured = sn;
				var b = MakeLayoutBtn(snippetRoot, label, () => InsertSnippet(captured));
				var le = b.gameObject.AddComponent<LayoutElement>();
				le.minHeight = 22;
				le.preferredHeight = 22;
			}

			var editor = Panel("Editor", body, Color.white);
			SetRect(editor, 0, 0, 1, 1, 4, 90, -214, -32);

			var codeGo = new GameObject("Code", typeof(RectTransform), typeof(InputField), typeof(Image));
			codeGo.transform.SetParent(editor, false);
			Stretch(codeGo.GetComponent<RectTransform>(), 0, 0, 0, 0);
			codeGo.GetComponent<Image>().color = new Color(0.98f, 0.99f, 0.98f, 1f);
			var codeText = MakeText(codeGo.transform, "", 14, TextAnchor.UpperLeft, new Color(0.1f, 0.12f, 0.12f, 1f));
			Stretch(codeText.rectTransform, 8, 8, -8, -8);
			codeText.supportRichText = false;
			codeText.horizontalOverflow = HorizontalWrapMode.Wrap;
			codeText.verticalOverflow = VerticalWrapMode.Overflow;
			code = codeGo.GetComponent<InputField>();
			code.textComponent = codeText;
			code.lineType = InputField.LineType.MultiLineNewline;
			code.customCaretColor = true;
			code.caretColor = Color.black;
			if (input != null) code.text = input.text;

			docsPanel = Panel("Docs", body, new Color(1f, 1f, 0.93f, 1f)).gameObject;
			SetRect(docsPanel.GetComponent<RectTransform>(), 0, 0, 1, 1, 4, 90, -214, -32);
			docsText = MakeText(docsPanel.transform, LuaDocs.Text(), 12, TextAnchor.UpperLeft, Color.black);
			Stretch(docsText.rectTransform, 8, 8, -8, -8);
			docsText.horizontalOverflow = HorizontalWrapMode.Wrap;
			docsText.verticalOverflow = VerticalWrapMode.Overflow;
			docsPanel.SetActive(false);
		}

		void MakeBtn(Transform parent, string label, float x, Action onClick)
		{
			var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
			go.transform.SetParent(parent, false);
			var rt = go.GetComponent<RectTransform>();
			rt.anchorMin = new Vector2(0, 0.5f);
			rt.anchorMax = new Vector2(0, 0.5f);
			rt.sizeDelta = new Vector2(68, 22);
			rt.anchoredPosition = new Vector2(x + 34, 0);
			go.GetComponent<Image>().color = new Color(0.95f, 0.97f, 0.96f, 1f);
			var tx = MakeText(go.transform, label, 12, TextAnchor.MiddleCenter, Color.black);
			Stretch(tx.rectTransform, 0, 0, 0, 0);
			go.GetComponent<Button>().onClick.AddListener(() => onClick());
		}

		Button MakeLayoutBtn(Transform parent, string label, Action onClick)
		{
			var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
			go.transform.SetParent(parent, false);
			go.GetComponent<Image>().color = new Color(0.93f, 0.97f, 0.95f, 1f);
			var tx = MakeText(go.transform, label, 11, TextAnchor.MiddleLeft, Color.black);
			Stretch(tx.rectTransform, 4, 0, -2, 0);
			var btn = go.GetComponent<Button>();
			btn.onClick.AddListener(() => onClick());
			return btn;
		}

		Text MakeText(Transform parent, string text, int size, TextAnchor align, Color color)
		{
			var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
			go.transform.SetParent(parent, false);
			var t = go.GetComponent<Text>();
			t.font = font;
			t.fontSize = size;
			t.alignment = align;
			t.color = color;
			t.text = text;
			t.raycastTarget = false;
			return t;
		}

		static RectTransform Panel(string name, Transform parent, Color color)
		{
			var go = new GameObject(name, typeof(RectTransform), typeof(Image));
			go.transform.SetParent(parent, false);
			go.GetComponent<Image>().color = color;
			return go.GetComponent<RectTransform>();
		}

		static void Stretch(RectTransform rt, float l, float b, float r, float t)
		{
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = new Vector2(l, b);
			rt.offsetMax = new Vector2(r, t);
		}

		static void SetRect(RectTransform rt, float xmin, float ymin, float xmax, float ymax, float l, float b, float r, float t)
		{
			rt.anchorMin = new Vector2(xmin, ymin);
			rt.anchorMax = new Vector2(xmax, ymax);
			rt.offsetMin = new Vector2(l, b);
			rt.offsetMax = new Vector2(r, t);
		}
	}

	public static class LuaDocs
	{
		public static string Text()
		{
			var ru = Localization.GetLanguage() == "RU" || Localization.GetLanguage() == "UA" || Localization.GetLanguage() == "BRL";
			if (ru)
			{
				return
					"PCOS Lua — язык внутри системы.\n\n" +
					"Слева пишешь код, справа подсказки. Нажми подсказку — она вставится, курсор встанет в скобки.\n\n" +
					"print(x)             вывод в консоль\n" +
					"os.alert(t, m)       окно сообщения\n" +
					"os.open(name)        открыть программу или файл\n" +
					"os.close(name)       закрыть окно\n" +
					"os.apps()            список установленных .exe\n" +
					"os.windows()         открытые окна\n" +
					"os.username() / os.id()\n" +
					"os.installed(name)   есть ли программа\n" +
					"os.shutdown()        выключить ПК\n" +
					"fs.list() / fs.read(p) / fs.write(p, s)\n" +
					"fs.exists(p) / fs.delete(p)\n" +
					"win.alert / win.open / win.close\n\n" +
					"if / then / else / end, while, for i=1,n do, function, local, tables {}.\n" +
					"math / string / table — стандартные куски Lua.\n\n" +
					"Пример:\n" +
					"print(\"hi\")\n" +
					"os.alert(\"Lua\", \"ok\")\n" +
					"local a = os.apps()\n" +
					"for i=1,#a do print(a[i]) end\n" +
					"os.open(\"Text Editor\")\n\n" +
					"tg: t.me/orangePCsimu";
			}
			return
				"PCOS Lua — scripting language inside the OS.\n\n" +
				"Code on the left, hints on the right. Click a hint to insert it; the caret jumps into the brackets.\n\n" +
				"print(x)             console output\n" +
				"os.alert(t, m)       message box\n" +
				"os.open(name)        open a program or file\n" +
				"os.close(name)       close a window\n" +
				"os.apps()            installed .exe list\n" +
				"os.windows()         open windows\n" +
				"os.username() / os.id()\n" +
				"os.installed(name)\n" +
				"os.shutdown()\n" +
				"fs.list() / fs.read(p) / fs.write(p, s)\n" +
				"fs.exists(p) / fs.delete(p)\n" +
				"win.alert / win.open / win.close\n\n" +
				"if / then / else / end, while, for i=1,n do, function, local, tables {}.\n" +
				"math / string / table helpers.\n\n" +
				"Example:\n" +
				"print(\"hi\")\n" +
				"os.alert(\"Lua\", \"ok\")\n" +
				"local a = os.apps()\n" +
				"for i=1,#a do print(a[i]) end\n" +
				"os.open(\"Text Editor\")\n\n" +
				"tg: t.me/orangePCsimu";
		}
	}
}
