using System;
using System.Collections;
using PC.Component.Software.Lua;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PC.Component.Software
{
	[Serializable]
	public class LuaSnippet
	{
		public string label;
		[TextArea(1, 4)]
		public string insert;
	}

	public class LuaEditor : App
	{
		[Header("Code")]
		[SerializeField] private InputField code;
		[SerializeField] private Text output;
		[SerializeField] private string defaultFileName = "Untitled.lua";

		[Header("Docs")]
		[SerializeField] private GameObject docsPanel;
		[SerializeField] private Text docsText;
		[SerializeField] private bool fillDocsOnOpen = true;

		[Header("Snippets")]
		[Tooltip("Родитель, куда спавнятся кнопки из списка. Можно не трогать — вешай LuaSnippetButton на свои кнопки.")]
		[SerializeField] private Transform snippetParent;
		[Tooltip("Префаб кнопки подсказки (Button + Text). Если пусто — список не спавнится.")]
		[SerializeField] private Button snippetButtonPrefab;
		[SerializeField] private LuaSnippet[] snippets;

		[Header("Highlight (optional)")]
		[Tooltip("Code = InputField, Overlay = Text поверх (Rich Text). У Code цвет текста почти прозрачный.")]
		[SerializeField] private LuaSyntaxHighlight highlighter;

		[Header("Compile to .exe")]
		[SerializeField] private InputField compileName;
		[SerializeField] private RawImage compileIconPreview;
		[SerializeField] private Text docsLanguageHint;

		private string filePath;
		private string compileIconB64 = "";
		private bool snippetsBuilt;
		private int lastCaret;
		private int lastSelA;
		private int lastSelB;
		private LuaCaretTrack track;

		void EnsureTrack()
		{
			if (code == null) return;
			track = code.GetComponent<LuaCaretTrack>();
			if (track == null) track = code.gameObject.AddComponent<LuaCaretTrack>();
		}

		protected override void Start()
		{
			base.Start();
			EnsureTrack();
			BuildSnippetButtons();
			if (highlighter != null && code != null)
				highlighter.Bind(code);
		}

		[ContextMenu("Fill default snippets")]
		public void FillDefaultSnippets()
		{
			snippets = DefaultSnippets();
		}

		void Reset()
		{
			defaultFileName = "Untitled.lua";
			fillDocsOnOpen = true;
			snippets = DefaultSnippets();
		}

		public override void Open(string content)
		{
			base.Open(content);
			EnsureTrack();
			BuildSnippetButtons();
			if (fillDocsOnOpen && docsText != null && string.IsNullOrEmpty(docsText.text))
				docsText.text = LuaDocs.Text();
			if (code != null)
				code.text = string.IsNullOrEmpty(content) ? DefaultSource() : content;
			filePath = string.IsNullOrEmpty(content) ? defaultFileName : filePath;
			if (string.IsNullOrEmpty(filePath)) filePath = defaultFileName;
			AppendOut(Localization.GetText("Lua ready. Click a hint to insert."));
			if (highlighter != null) highlighter.Refresh();
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
				if (highlighter != null) highlighter.Refresh();
			});
		}

		public void Save()
		{
			if (system == null || system.SaveDialog == null || code == null) return;
			var name = File.NameWithoutExtension(string.IsNullOrEmpty(filePath) ? defaultFileName : filePath);
			system.SaveDialog.ShowDialog(name, code.text ?? "", new[] { ".lua", ".txt" });
		}

		public void ToggleDocs()
		{
			if (docsPanel == null) return;
			bool on = !docsPanel.activeSelf;
			docsPanel.SetActive(on);
			if (on) ShowDocs();
		}

		public void ShowDocs()
		{
			var text = LuaDocs.Text();
			if (docsText != null) docsText.text = text;
			if (docsPanel != null) docsPanel.SetActive(true);
			if (docsLanguageHint != null)
				docsLanguageHint.text = Localization.GetLanguage() ?? "EN";
		}

		public void Run()
		{
			if (code == null) return;
			if (output != null) output.text = "";
			var name = compileName != null && !string.IsNullOrEmpty(compileName.text) ? compileName.text : "Preview";
			var pack = new Lua.LuaAppPackage
			{
				name = name,
				script = code.text ?? "",
				icon = compileIconB64
			};
			if (system != null && system.LaunchLuaApp(pack.ToJson()))
			{
				AppendOut(Localization.GetText("Lua finished."));
				return;
			}
			var vm = new PcosLua();
			vm.Printer = AppendOut;
			PcosLuaHost.Bind(vm, system);
			try
			{
				vm.DoString(code.text ?? "");
				AppendOut(Localization.GetText("Lua finished."));
			}
			catch (Exception ex)
			{
				AppendOut("error: " + ex.Message);
				if (system != null) system.ShowMessageBox("Lua", ex.Message);
			}
		}

		public void PickIcon()
		{
			if (system == null) return;
			system.SelectFile("*", file =>
			{
				if (file == null || string.IsNullOrEmpty(file.content)) return;
				compileIconB64 = file.content;
				if (compileIconPreview == null) return;
				try
				{
					var data = Convert.FromBase64String(file.content);
					var tex = new Texture2D(2, 2);
					tex.filterMode = FilterMode.Point;
					if (tex.LoadImage(data))
						compileIconPreview.texture = tex;
				}
				catch { }
			});
		}

		public void Compile()
		{
			if (code == null || system == null || system.FileManager == null) return;
			var name = compileName != null ? compileName.text : "";
			if (string.IsNullOrEmpty(name))
				name = File.NameWithoutExtension(string.IsNullOrEmpty(filePath) ? defaultFileName : filePath);
			if (string.IsNullOrEmpty(name) || name == "Untitled") name = "LuaApp";
			foreach (var c in System.IO.Path.GetInvalidFileNameChars())
				name = name.Replace(c, '_');
			if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
				name = name.Substring(0, name.Length - 4);
			var pack = new Lua.LuaAppPackage
			{
				name = name,
				script = code.text ?? "",
				icon = compileIconB64 ?? ""
			};
			var json = pack.ToJson();
			var path = name + ".exe";
			system.FileManager.Write(0, path, json);
			system.RefreshDesktopIcon();
			filePath = path;
			AppendOut("compiled " + path);
			if (system != null) system.ShowMessageBox("Lua", path);
		}

		public void InsertSnippet(string snippet)
		{
			if (code == null || string.IsNullOrEmpty(snippet)) return;
			if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;

			EnsureTrack();
			if (track != null)
			{
				lastCaret = track.Caret;
				lastSelA = track.SelA;
				lastSelB = track.SelB;
			}

			int mark = snippet.IndexOf('|');
			string ins = snippet.Replace("|", "");
			string t = code.text ?? "";
			int caret = lastCaret;
			if (caret < 0 || caret > t.Length) caret = t.Length;

			int a = Mathf.Min(lastSelA, lastSelB);
			int b = Mathf.Max(lastSelA, lastSelB);
			if (a != b && a >= 0 && b <= t.Length)
			{
				t = t.Remove(a, b - a);
				caret = a;
			}

			code.text = t.Insert(caret, ins);
			int pos = caret + (mark >= 0 ? mark : ins.Length);
			lastCaret = lastSelA = lastSelB = pos;
			if (track != null) track.Set(pos, pos, pos);
			if (highlighter != null) highlighter.Refresh();
			StartCoroutine(PlaceCaret(pos));
		}

		IEnumerator PlaceCaret(int pos)
		{
			if (code == null) yield break;
			code.ActivateInputField();
			yield return null;
			if (code == null) yield break;
			code.caretPosition = pos;
			code.selectionAnchorPosition = pos;
			code.selectionFocusPosition = pos;
			code.ForceLabelUpdate();
		}

		void BuildSnippetButtons()
		{
			if (snippetsBuilt) return;
			if (snippetParent == null || snippetButtonPrefab == null) return;
			snippetsBuilt = true;
			var list = (snippets != null && snippets.Length > 0) ? snippets : DefaultSnippets();
			for (int i = 0; i < list.Length; i++)
			{
				var sn = list[i];
				if (sn == null || string.IsNullOrEmpty(sn.insert)) continue;
				var btn = Instantiate(snippetButtonPrefab, snippetParent);
				var label = string.IsNullOrEmpty(sn.label)
					? sn.insert.Replace("|", "").Replace("\n", " ")
					: sn.label;
				var txt = btn.GetComponentInChildren<Text>();
				if (txt != null) txt.text = label;
				var captured = sn.insert;
				btn.onClick.AddListener(() => InsertSnippet(captured));
			}
		}

		void AppendOut(string line)
		{
			if (output == null) return;
			if (string.IsNullOrEmpty(output.text)) output.text = line;
			else output.text = output.text + "\n" + line;
		}

		static string DefaultSource()
		{
			return "-- PCOS Lua\nui.title(\"Counter\")\nui.size(320, 180)\nlocal n = 0\nlocal lab = ui.label(\"0\", 20, 20, 200, 24)\nui.button(\"Click\", 20, 60, 100, 28, function()\n  n = n + 1\n  ui.set(lab, tostring(n))\nend)\n";
		}

		public static LuaSnippet[] DefaultSnippets()
		{
			return new[]
			{
				new LuaSnippet { label = "print(\"\")", insert = "print(\"|\")" },
				new LuaSnippet { label = "os.alert", insert = "os.alert(\"|\", \"\")" },
				new LuaSnippet { label = "os.open", insert = "os.open(\"|\")" },
				new LuaSnippet { label = "os.close", insert = "os.close(\"|\")" },
				new LuaSnippet { label = "os.apps()", insert = "os.apps()" },
				new LuaSnippet { label = "os.windows()", insert = "os.windows()" },
				new LuaSnippet { label = "os.username()", insert = "os.username()" },
				new LuaSnippet { label = "os.id()", insert = "os.id()" },
				new LuaSnippet { label = "os.installed", insert = "os.installed(\"|\")" },
				new LuaSnippet { label = "os.shutdown()", insert = "os.shutdown()" },
				new LuaSnippet { label = "fs.list()", insert = "fs.list()" },
				new LuaSnippet { label = "fs.read", insert = "fs.read(\"|\")" },
				new LuaSnippet { label = "fs.write", insert = "fs.write(\"|\", \"\")" },
				new LuaSnippet { label = "fs.exists", insert = "fs.exists(\"|\")" },
				new LuaSnippet { label = "fs.delete", insert = "fs.delete(\"|\")" },
				new LuaSnippet { label = "win.alert", insert = "win.alert(\"|\")" },
				new LuaSnippet { label = "win.open", insert = "win.open(\"|\")" },
				new LuaSnippet { label = "win.close", insert = "win.close(\"|\")" },
				new LuaSnippet { label = "if then end", insert = "if | then\n  \nend" },
				new LuaSnippet { label = "while do end", insert = "while | do\n  \nend" },
				new LuaSnippet { label = "for i = 1, n", insert = "for i = 1, | do\n  \nend" },
				new LuaSnippet { label = "function", insert = "function |()\n  \nend" },
				new LuaSnippet { label = "local", insert = "local | = " },
				new LuaSnippet { label = "tonumber", insert = "tonumber(\"|\")" },
				new LuaSnippet { label = "tostring", insert = "tostring(|)" },
				new LuaSnippet { label = "type", insert = "type(|)" },
				new LuaSnippet { label = "math.random", insert = "math.random(|)" },
				new LuaSnippet { label = "string.upper", insert = "string.upper(\"|\")" },
				new LuaSnippet { label = "table.insert", insert = "table.insert(|, )" },
				new LuaSnippet { label = "ui.title", insert = "ui.title(\"|\")" },
				new LuaSnippet { label = "ui.size", insert = "ui.size(400, 240)" },
				new LuaSnippet { label = "ui.label", insert = "ui.label(\"|\", 10, 10, 200, 24)" },
				new LuaSnippet { label = "ui.button", insert = "ui.button(\"OK\", 10, 40, 80, 24, function()\n  \nend)" },
				new LuaSnippet { label = "ui.input", insert = "ui.input(\"|\", 10, 70, 180, 24)" },
				new LuaSnippet { label = "ui.panel", insert = "ui.panel(8, 8, 200, 100)" },
				new LuaSnippet { label = "ui.slider", insert = "ui.slider(10, 100, 180, 20, 0, 1)" },
				new LuaSnippet { label = "ui.toggle", insert = "ui.toggle(\"On\", 10, 130, 120, 22, true)" },
				new LuaSnippet { label = "ui.get / set", insert = "ui.set(id, ui.get(id))" },
				new LuaSnippet { label = "ui.style", insert = "ui.style({ button = {0.9, 0.9, 0.9}, text = {0,0,0} })" },
				new LuaSnippet { label = "ui.systemstyle", insert = "ui.systemstyle()" }
			};
		}
	}

	public static class LuaDocs
	{
		public static string Text()
		{
			var lang = Localization.GetLanguage() ?? "EN";
			if (lang == "UA" || lang == "BRL") lang = "RU";
			var asset = Resources.Load<TextAsset>("LuaDocs_" + lang);
			if (asset == null) asset = Resources.Load<TextAsset>("LuaDocs_EN");
			if (asset == null) asset = Resources.Load<TextAsset>("LuaDocs");
			if (asset != null && !string.IsNullOrEmpty(asset.text)) return asset.text;
			return "PCOS Lua. Open Lua.txt on the desktop or press Docs.";
		}
	}
}
