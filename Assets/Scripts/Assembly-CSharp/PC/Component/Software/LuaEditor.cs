using System;
using System.Collections;
using System.Collections.Generic;
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
		[SerializeField] private GameObject compilePanel;
		[SerializeField] private InputField compileName;
		[SerializeField] private RawImage compileIconPreview;
		[SerializeField] private Image compileIconImage;
		[SerializeField] private Text docsLanguageHint;

		[Header("Icon picker")]
		[SerializeField] private GameObject iconPickerPanel;
		[SerializeField] private Transform iconGrid;
		[Tooltip("Кнопка-ячейка с Image.")]
		[SerializeField] private Button iconCellPrefab;
		[SerializeField] private Sprite[] extraIcons;

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
			var cp = Live(compilePanel);
			if (cp != null) cp.SetActive(false);
			var ip = Live(iconPickerPanel);
			if (ip != null) ip.SetActive(false);
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
			var panel = Live(docsPanel);
			if (panel == null) { ShowDocs(); return; }
			bool on = !panel.activeSelf;
			panel.SetActive(on);
			if (on) ShowDocs();
		}

		public void Docs() { ShowDocs(); }
		public void OpenDocs() { ShowDocs(); }

		public void ShowDocs()
		{
			var live = SceneSelf();
			if (live != null && live != this) { live.ShowDocs(); return; }
			var text = LuaDocs.Text();
			var dt = Live(docsText);
			if (dt != null) dt.text = text;
			var panel = Live(docsPanel);
			if (panel != null) panel.SetActive(true);
			var hint = Live(docsLanguageHint);
			if (hint != null)
				hint.text = Localization.GetLanguage() ?? "EN";
		}

		public void Run()
		{
			if (code == null) return;
			AppendOut("---");
			var nameField = Live(compileName);
			var name = nameField != null && !string.IsNullOrEmpty(nameField.text) ? nameField.text : "Preview";
			var pack = new LuaAppPackage
			{
				name = name,
				script = code.text ?? "",
				icon = compileIconB64
			};
			if (system != null)
			{
				if (system.RunLua(pack.ToJson(), AppendOut))
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

		public void Compile()
		{
			var live = SceneSelf();
			if (live != null && live != this) { live.Compile(); return; }
			OpenCompilePanel();
		}

		public void OpenCompilePanel()
		{
			var live = SceneSelf();
			if (live != null && live != this) { live.OpenCompilePanel(); return; }
			var picker = Live(iconPickerPanel);
			if (picker != null) picker.SetActive(false);
			var nameField = Live(compileName);
			if (nameField != null && string.IsNullOrEmpty(nameField.text))
			{
				var n = File.NameWithoutExtension(string.IsNullOrEmpty(filePath) ? defaultFileName : filePath);
				if (string.IsNullOrEmpty(n) || n == "Untitled") n = "LuaApp";
				nameField.text = n;
			}
			var panel = Live(compilePanel);
			if (panel != null) panel.SetActive(true);
		}

		public void CancelCompile()
		{
			var picker = Live(iconPickerPanel);
			if (picker != null) picker.SetActive(false);
			var panel = Live(compilePanel);
			if (panel != null) panel.SetActive(false);
		}

		public void ConfirmCompile()
		{
			WriteCompiledExe();
			CancelCompile();
		}

		public void OpenIconPicker()
		{
			var live = SceneSelf();
			if (live != null && live != this) { live.OpenIconPicker(); return; }
			var picker = Live(iconPickerPanel);
			if (picker == null)
			{
				AppendOut("Assign Icon Picker Panel: child of this window, not a Project prefab.");
				return;
			}
			FillIconGrid();
			picker.SetActive(true);
		}

		public void CancelIconPicker()
		{
			var picker = Live(iconPickerPanel);
			if (picker != null) picker.SetActive(false);
		}

		public void PickIcon()
		{
			OpenIconPicker();
		}

		public void PickIconFromDevice()
		{
			NativeGallery.GetImageFromGallery(path =>
			{
				if (string.IsNullOrEmpty(path)) return;
				try
				{
					var bytes = System.IO.File.ReadAllBytes(path);
					ApplyIconBytes(bytes);
					CancelIconPicker();
				}
				catch (Exception ex)
				{
					AppendOut("icon: " + ex.Message);
				}
			}, "Icon", "image/*");
		}

		void WriteCompiledExe()
		{
			if (code == null || system == null || system.FileManager == null) return;
			var nameField = Live(compileName);
			var name = nameField != null ? nameField.text : "";
			if (string.IsNullOrEmpty(name))
				name = File.NameWithoutExtension(string.IsNullOrEmpty(filePath) ? defaultFileName : filePath);
			if (string.IsNullOrEmpty(name) || name == "Untitled") name = "LuaApp";
			foreach (var c in System.IO.Path.GetInvalidFileNameChars())
				name = name.Replace(c, '_');
			if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
				name = name.Substring(0, name.Length - 4);
			var pack = new LuaAppPackage
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

		void FillIconGrid()
		{
			if (!InScene(gameObject)) return;
			var picker = Live(iconPickerPanel);
			Transform grid = null;
			if (picker != null)
			{
				if (iconGrid != null)
					grid = FindDeep(picker.transform, iconGrid.name);
				if (grid == null)
					grid = FindDeep(picker.transform, "Content");
			}
			if (grid == null) grid = Live(iconGrid);
			if (grid == null || !InScene(grid.gameObject) || iconCellPrefab == null)
			{
				AppendOut("Icon Grid must be Content inside the picker panel of this window.");
				return;
			}

			for (int i = grid.childCount - 1; i >= 0; i--)
			{
				var ch = grid.GetChild(i);
				if (ch == null || !InScene(ch.gameObject)) continue;
				Destroy(ch.gameObject);
			}

			var seen = new HashSet<Sprite>();
			void Add(Sprite sp)
			{
				if (sp == null || seen.Contains(sp)) return;
				seen.Add(sp);
				var btn = Instantiate(iconCellPrefab);
				if (!InScene(grid.gameObject)) return;
				btn.transform.SetParent(grid, false);
				btn.gameObject.SetActive(true);
				var img = btn.GetComponent<Image>();
				if (img == null) img = btn.GetComponentInChildren<Image>();
				if (img != null)
				{
					img.sprite = sp;
					img.preserveAspect = true;
				}
				var captured = sp;
				btn.onClick.RemoveAllListeners();
				btn.onClick.AddListener(() => SelectGridIcon(captured));
			}

			if (extraIcons != null)
			{
				for (int i = 0; i < extraIcons.Length; i++) Add(extraIcons[i]);
			}
			var apps = Resources.LoadAll<App>("apps");
			if (apps != null)
			{
				for (int i = 0; i < apps.Length; i++)
				{
					if (apps[i] == null) continue;
					Add(apps[i].Icon);
					Add(apps[i].FileIcon);
				}
			}
		}

		static bool InScene(GameObject go)
		{
			return go != null && go.scene.IsValid() && go.scene.isLoaded;
		}

		LuaEditor SceneSelf()
		{
			if (InScene(gameObject)) return this;
			var all = FindObjectsOfType<LuaEditor>();
			if (all == null) return null;
			for (int i = 0; i < all.Length; i++)
			{
				var e = all[i];
				if (e != null && e != this && InScene(e.gameObject)) return e;
			}
			return null;
		}

		T Live<T>(T assigned) where T : UnityEngine.Component
		{
			if (assigned == null) return null;
			if (InScene(assigned.gameObject)) return assigned;
			var found = FindDeep(transform, assigned.name);
			if (found == null) return null;
			return found.GetComponent<T>() ?? found.GetComponentInChildren<T>(true);
		}

		GameObject Live(GameObject assigned)
		{
			if (assigned == null) return null;
			if (InScene(assigned)) return assigned;
			var found = FindDeep(transform, assigned.name);
			return found != null ? found.gameObject : null;
		}

		static Transform FindDeep(Transform root, string name)
		{
			if (root == null || string.IsNullOrEmpty(name)) return null;
			if (root.name == name) return root;
			for (int i = 0; i < root.childCount; i++)
			{
				var f = FindDeep(root.GetChild(i), name);
				if (f != null) return f;
			}
			return null;
		}

		void SelectGridIcon(Sprite sprite)
		{
			if (sprite == null) return;
			var bytes = SpriteToPng(sprite);
			if (bytes != null && bytes.Length > 0)
				ApplyIconBytes(bytes);
			else
				ShowSpritePreview(sprite);
			CancelIconPicker();
		}

		void ApplyIconBytes(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0) return;
			compileIconB64 = Convert.ToBase64String(bytes);
			var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			tex.filterMode = FilterMode.Point;
			if (!tex.LoadImage(bytes)) return;
			var raw = Live(compileIconPreview);
			if (raw != null) raw.texture = tex;
			var img = Live(compileIconImage);
			if (img != null)
				img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
		}

		void ShowSpritePreview(Sprite sprite)
		{
			if (compileIconImage != null) compileIconImage.sprite = sprite;
			if (compileIconPreview != null && sprite != null && sprite.texture != null)
				compileIconPreview.texture = sprite.texture;
		}

		static byte[] SpriteToPng(Sprite sprite)
		{
			if (sprite == null || sprite.texture == null) return null;
			var src = sprite.texture;
			var tr = sprite.textureRect;
			int x = Mathf.Clamp(Mathf.RoundToInt(tr.x), 0, Mathf.Max(0, src.width - 1));
			int y = Mathf.Clamp(Mathf.RoundToInt(tr.y), 0, Mathf.Max(0, src.height - 1));
			int w = Mathf.Clamp(Mathf.RoundToInt(tr.width), 1, src.width - x);
			int h = Mathf.Clamp(Mathf.RoundToInt(tr.height), 1, src.height - y);

			try
			{
				if (src.isReadable)
				{
					var pix = src.GetPixels(x, y, w, h);
					var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
					copy.filterMode = FilterMode.Point;
					copy.SetPixels(pix);
					copy.Apply();
					return copy.EncodeToPNG();
				}
			}
			catch { }

			var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
			Graphics.Blit(src, rt);
			var prev = RenderTexture.active;
			RenderTexture.active = rt;
			var full = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
			full.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
			full.Apply();
			RenderTexture.active = prev;
			RenderTexture.ReleaseTemporary(rt);
			var crop = new Texture2D(w, h, TextureFormat.RGBA32, false);
			crop.filterMode = FilterMode.Point;
			crop.SetPixels(full.GetPixels(x, y, w, h));
			crop.Apply();
			UnityEngine.Object.Destroy(full);
			return crop.EncodeToPNG();
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
			if (string.IsNullOrEmpty(output.text)) output.text = line ?? "";
			else output.text = output.text + "\n" + (line ?? "");
			if (output.text.Length > 16000)
				output.text = output.text.Substring(output.text.Length - 12000);
		}

		static string DefaultSource()
		{
			return "print(\"Hello world!\")\n";
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
				new LuaSnippet { label = "ui.systemstyle", insert = "ui.systemstyle()" },
				new LuaSnippet { label = "isdraggable", insert = "isdraggable(true)" },
				new LuaSnippet { label = "ismaximable", insert = "ismaximable(true)" },
				new LuaSnippet { label = "enabledebugger", insert = "enabledebugger()" },
				new LuaSnippet { label = "ui.rect", insert = "ui.rect(10, 10, 80, 40, 0.2, 0.4, 0.8)" },
				new LuaSnippet { label = "gfx.rect", insert = "gfx.rect(10, 10, 40, 40, 1, 0, 0)" },
				new LuaSnippet { label = "gfx.text", insert = "gfx.text(10, 10, \"Hello\", 1, 1, 1)" },
				new LuaSnippet { label = "gfx.line", insert = "gfx.line(0, 0, 100, 100, 1, 1, 1)" },
				new LuaSnippet { label = "gfx.circle", insert = "gfx.circle(50, 50, 20, 0, 1, 0)" },
				new LuaSnippet { label = "gfx.getpixel", insert = "gfx.getpixel(10, 10)" },
				new LuaSnippet { label = "pairs(t)", insert = "pairs(|)" },
				new LuaSnippet { label = "ipairs(t)", insert = "ipairs(|)" },
				new LuaSnippet { label = "pcall", insert = "pcall(|, )" },
				new LuaSnippet { label = "string.format", insert = "string.format(\"|\", )" },
				new LuaSnippet { label = "string.reverse", insert = "string.reverse(\"|\")" },
				new LuaSnippet { label = "table.sort", insert = "table.sort(|)" },
				new LuaSnippet { label = "table.remove", insert = "table.remove(|)" },
				new LuaSnippet { label = "ui.image", insert = "ui.image(\"|\", 10, 10, 64, 64)" },
				new LuaSnippet { label = "ui.clear", insert = "ui.clear()" }
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
			return "PCOS Lua. Press Docs.";
		}
	}
}
