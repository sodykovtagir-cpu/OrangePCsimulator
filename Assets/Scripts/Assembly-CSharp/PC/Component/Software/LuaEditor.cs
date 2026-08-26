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
		[SerializeField] private LuaSyntaxHighlight highlighter;

		private string filePath;
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

		public void Run()
		{
			if (code == null) return;
			if (output != null) output.text = "";
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

		public void ToggleDocs()
		{
			if (docsPanel == null) return;
			bool on = !docsPanel.activeSelf;
			docsPanel.SetActive(on);
			if (on && docsText != null && string.IsNullOrEmpty(docsText.text))
				docsText.text = LuaDocs.Text();
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
			return "-- PCOS Lua\nprint(\"Hello, PCOS!\")\n-- os.alert(\"Lua\", \"It works!\")\n-- os.open(\"Text Editor\")\n";
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
				new LuaSnippet { label = "table.insert", insert = "table.insert(|, )" }
			};
		}
	}

	public static class LuaDocs
	{
		public static string Text()
		{
			var lang = Localization.GetLanguage() ?? "";
			var ru = lang == "RU" || lang == "UA" || lang == "BRL";
			if (ru)
			{
				return
					"PCOS Lua — язык внутри системы.\n\n" +
					"Слева код, справа подсказки. | в шаблоне = куда встанет курсор.\n\n" +
					"print(x)\nos.alert(t, m)\nos.open(name)\nos.close(name)\n" +
					"os.apps() / os.windows()\nos.username() / os.id()\nos.installed(name)\nos.shutdown()\n" +
					"fs.list / read / write / exists / delete\nwin.alert / open / close\n\n" +
					"if / while / for i=1,n / function / local / tables {}\nmath / string / table\n\n" +
					"tg: t.me/orangePCsimu";
			}
			return
				"PCOS Lua — scripting language inside the OS.\n\n" +
				"Code left, hints right. | in a snippet is the caret target.\n\n" +
				"print(x)\nos.alert(t, m)\nos.open(name)\nos.close(name)\n" +
				"os.apps() / os.windows()\nos.username() / os.id()\nos.installed(name)\nos.shutdown()\n" +
				"fs.list / read / write / exists / delete\nwin.alert / open / close\n\n" +
				"if / while / for i=1,n / function / local / tables {}\nmath / string / table\n\n" +
				"tg: t.me/orangePCsimu";
		}
	}
}
