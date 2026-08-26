using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.Lua
{
	public sealed class LuaUi
	{
		public struct Style
		{
			public Color window;
			public Color panel;
			public Color button;
			public Color buttonText;
			public Color text;
			public Color input;
			public Color inputText;
			public int fontSize;

			public static Style System()
			{
				return new Style
				{
					window = new Color(0.63f, 0.83f, 0.82f, 1f),
					panel = new Color(0.55f, 0.75f, 0.74f, 1f),
					button = new Color(0.93f, 0.96f, 0.95f, 1f),
					buttonText = Color.black,
					text = Color.black,
					input = Color.white,
					inputText = new Color(0.12f, 0.12f, 0.12f, 1f),
					fontSize = 14
				};
			}
		}

		readonly RectTransform root;
		readonly Font font;
		readonly PcosLua vm;
		readonly Dictionary<string, Graphic> widgets = new Dictionary<string, Graphic>();
		readonly Dictionary<string, GameObject> widgetRoots = new Dictionary<string, GameObject>();
		readonly Dictionary<string, InputField> inputs = new Dictionary<string, InputField>();
		readonly Dictionary<string, Text> labels = new Dictionary<string, Text>();
		readonly Dictionary<string, Slider> sliders = new Dictionary<string, Slider>();
		readonly Dictionary<string, Toggle> toggles = new Dictionary<string, Toggle>();
		Style style = Style.System();
		int nextId = 1;
		Image windowBg;

		public Action<string> OnTitle;
		public Action<float, float> OnSize;

		public LuaUi(RectTransform root, Font font, PcosLua vm)
		{
			this.root = root;
			this.font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
			this.vm = vm;
			if (root != null)
			{
				windowBg = root.GetComponent<Image>();
				if (windowBg == null) windowBg = root.gameObject.AddComponent<Image>();
				windowBg.color = style.window;
				windowBg.raycastTarget = true;
			}
		}

		public void Bind()
		{
			if (vm == null) return;
			var ui = new LuaTable();
			ui.Set(LuaValue.String("label"), Native(a => LuaValue.String(Label(Str(a, 0), Num(a, 1), Num(a, 2), Num(a, 3, 120), Num(a, 4, 22)))));
			ui.Set(LuaValue.String("button"), Native(a =>
			{
				LuaFunction fn = null;
				if (a.Length > 5 && a[5].Type == LuaType.Function) fn = a[5].Fn;
				return LuaValue.String(Button(Str(a, 0), Num(a, 1), Num(a, 2), Num(a, 3, 80), Num(a, 4, 24), fn));
			}));
			ui.Set(LuaValue.String("input"), Native(a => LuaValue.String(Input(Str(a, 0), Num(a, 1), Num(a, 2), Num(a, 3, 160), Num(a, 4, 24)))));
			ui.Set(LuaValue.String("panel"), Native(a => LuaValue.String(Panel(Num(a, 0), Num(a, 1), Num(a, 2, 100), Num(a, 3, 100)))));
			ui.Set(LuaValue.String("slider"), Native(a => LuaValue.String(MakeSlider(Num(a, 0), Num(a, 1), Num(a, 2, 160), Num(a, 3, 20), Num(a, 4, 0), Num(a, 5, 1)))));
			ui.Set(LuaValue.String("toggle"), Native(a => LuaValue.String(MakeToggle(Str(a, 0), Num(a, 1), Num(a, 2), Num(a, 3, 120), Num(a, 4, 22), a.Length > 5 && a[5].IsTruthy()))));
			ui.Set(LuaValue.String("get"), Native(a => LuaValue.String(Get(Str(a, 0)))));
			ui.Set(LuaValue.String("set"), Native(a => { Set(Str(a, 0), Str(a, 1)); return LuaValue.Nil; }));
			ui.Set(LuaValue.String("value"), Native(a => LuaValue.Number(Value(Str(a, 0)))));
			ui.Set(LuaValue.String("checked"), Native(a => LuaValue.Bool(Checked(Str(a, 0)))));
			ui.Set(LuaValue.String("remove"), Native(a => { Remove(Str(a, 0)); return LuaValue.Nil; }));
			ui.Set(LuaValue.String("clear"), Native(a => { Clear(); return LuaValue.Nil; }));
			ui.Set(LuaValue.String("style"), Native(ApplyStyle));
			ui.Set(LuaValue.String("systemstyle"), Native(a => { style = Style.System(); ApplyWindow(); return LuaValue.Nil; }));
			ui.Set(LuaValue.String("title"), Native(a => { if (OnTitle != null) OnTitle(Str(a, 0)); return LuaValue.Nil; }));
			ui.Set(LuaValue.String("size"), Native(a => { if (OnSize != null) OnSize(Num(a, 0, 420), Num(a, 1, 280)); return LuaValue.Nil; }));
			ui.Set(LuaValue.String("color"), Native(a => { ColorWidget(Str(a, 0), ColorArg(a, 1)); return LuaValue.Nil; }));
			ui.Set(LuaValue.String("rect"), Native(a => LuaValue.String(Rect(Num(a, 0), Num(a, 1), Num(a, 2, 40), Num(a, 3, 40), ColorArg(a, 4)))));
			ui.Set(LuaValue.String("image"), Native(a => LuaValue.String(Image(Str(a, 0), Num(a, 1), Num(a, 2), Num(a, 3, 64), Num(a, 4, 64)))));
			ui.Set(LuaValue.String("isdraggable"), Native(DragFn));
			ui.Set(LuaValue.String("draggable"), Native(DragFn));
			ui.Set(LuaValue.String("ismaximable"), Native(MaxFn));
			ui.Set(LuaValue.String("ismaximizable"), Native(MaxFn));
			ui.Set(LuaValue.String("maximizable"), Native(MaxFn));
			vm.SetGlobal("ui", LuaValue.FromTable(ui));
		}

		LuaValue DragFn(LuaValue[] a)
		{
			var app = Host();
			if (a.Length == 0) return LuaValue.Bool(app != null && app.IsDraggable);
			if (app != null) app.SetDraggable(a[0].IsTruthy());
			return LuaValue.Nil;
		}

		LuaValue MaxFn(LuaValue[] a)
		{
			var app = Host();
			if (a.Length == 0) return LuaValue.Bool(app != null && app.IsMaximizable);
			if (app != null) app.SetMaximizable(a[0].IsTruthy());
			return LuaValue.Nil;
		}

		LuaApp Host()
		{
			return root != null ? root.GetComponentInParent<LuaApp>() : null;
		}

		string NewId() { return "w" + (nextId++); }

		string Label(string text, float x, float y, float w, float h)
		{
			var id = NewId();
			var t = MakeText(text, style.text);
			Place(t.rectTransform, x, y, w, h);
			labels[id] = t;
			widgets[id] = t;
			widgetRoots[id] = t.gameObject;
			return id;
		}

		string Button(string text, float x, float y, float w, float h, LuaFunction click)
		{
			var id = NewId();
			var go = new GameObject(id, typeof(RectTransform), typeof(Image), typeof(Button));
			go.transform.SetParent(root, false);
			var img = go.GetComponent<Image>();
			img.color = style.button;
			img.raycastTarget = true;
			var btn = go.GetComponent<Button>();
			btn.targetGraphic = img;
			var captured = click;
			btn.onClick.AddListener(() =>
			{
				if (captured == null || vm == null) return;
				try { vm.Call(LuaValue.FromFn(captured), new List<LuaValue>(), 0); }
				catch (Exception ex)
				{
					if (vm.Printer != null) vm.Printer("error: " + ex.Message);
				}
				var host = Host();
				if (host != null) host.FlipGfx();
			});
			var tx = MakeText(text, style.buttonText);
			tx.transform.SetParent(go.transform, false);
			Stretch(tx.rectTransform);
			Place(go.GetComponent<RectTransform>(), x, y, w, h);
			widgets[id] = img;
			widgetRoots[id] = go;
			return id;
		}

		string Input(string text, float x, float y, float w, float h)
		{
			var id = NewId();
			var go = new GameObject(id, typeof(RectTransform), typeof(Image), typeof(InputField));
			go.transform.SetParent(root, false);
			go.GetComponent<Image>().color = style.input;
			var tx = MakeText(text ?? "", style.inputText);
			tx.transform.SetParent(go.transform, false);
			Stretch(tx.rectTransform, 4, 2, -4, -2);
			tx.supportRichText = false;
			var field = go.GetComponent<InputField>();
			field.textComponent = tx;
			field.text = text ?? "";
			field.lineType = InputField.LineType.SingleLine;
			Place(go.GetComponent<RectTransform>(), x, y, w, h);
			inputs[id] = field;
			widgets[id] = go.GetComponent<Image>();
			widgetRoots[id] = go;
			return id;
		}

		string Panel(float x, float y, float w, float h)
		{
			var id = NewId();
			var go = new GameObject(id, typeof(RectTransform), typeof(Image));
			go.transform.SetParent(root, false);
			var img = go.GetComponent<Image>();
			img.color = style.panel;
			Place(go.GetComponent<RectTransform>(), x, y, w, h);
			widgets[id] = img;
			widgetRoots[id] = go;
			return id;
		}

		string MakeSlider(float x, float y, float w, float h, float min, float max)
		{
			var id = NewId();
			var go = new GameObject(id, typeof(RectTransform), typeof(Slider));
			go.transform.SetParent(root, false);
			var bg = new GameObject("bg", typeof(RectTransform), typeof(Image));
			bg.transform.SetParent(go.transform, false);
			bg.GetComponent<Image>().color = style.panel;
			Stretch(bg.GetComponent<RectTransform>());
			var fillArea = new GameObject("Fill Area", typeof(RectTransform));
			fillArea.transform.SetParent(go.transform, false);
			Stretch(fillArea.GetComponent<RectTransform>(), 4, 4, -4, -4);
			var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
			fill.transform.SetParent(fillArea.transform, false);
			fill.GetComponent<Image>().color = style.button;
			Stretch(fill.GetComponent<RectTransform>());
			var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
			handleArea.transform.SetParent(go.transform, false);
			Stretch(handleArea.GetComponent<RectTransform>());
			var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
			handle.transform.SetParent(handleArea.transform, false);
			handle.GetComponent<Image>().color = style.buttonText;
			var hs = handle.GetComponent<RectTransform>();
			hs.sizeDelta = new Vector2(10, 0);
			var sl = go.GetComponent<Slider>();
			sl.fillRect = fill.GetComponent<RectTransform>();
			sl.handleRect = hs;
			sl.targetGraphic = handle.GetComponent<Image>();
			sl.minValue = min;
			sl.maxValue = max < min ? min + 1 : max;
			sl.value = min;
			Place(go.GetComponent<RectTransform>(), x, y, w, h);
			sliders[id] = sl;
			widgets[id] = bg.GetComponent<Image>();
			widgetRoots[id] = go;
			return id;
		}

		string MakeToggle(string text, float x, float y, float w, float h, bool on)
		{
			var id = NewId();
			var go = new GameObject(id, typeof(RectTransform), typeof(Toggle));
			go.transform.SetParent(root, false);
			var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
			box.transform.SetParent(go.transform, false);
			var boxRt = box.GetComponent<RectTransform>();
			boxRt.anchorMin = new Vector2(0, 0.5f);
			boxRt.anchorMax = new Vector2(0, 0.5f);
			boxRt.sizeDelta = new Vector2(18, 18);
			boxRt.anchoredPosition = new Vector2(10, 0);
			box.GetComponent<Image>().color = style.input;
			var mark = new GameObject("Mark", typeof(RectTransform), typeof(Image));
			mark.transform.SetParent(box.transform, false);
			Stretch(mark.GetComponent<RectTransform>(), 3, 3, -3, -3);
			mark.GetComponent<Image>().color = style.text;
			var tx = MakeText(text, style.text);
			tx.transform.SetParent(go.transform, false);
			var tr = tx.rectTransform;
			tr.anchorMin = Vector2.zero;
			tr.anchorMax = Vector2.one;
			tr.offsetMin = new Vector2(28, 0);
			tr.offsetMax = Vector2.zero;
			var tg = go.GetComponent<Toggle>();
			tg.graphic = mark.GetComponent<Image>();
			tg.targetGraphic = box.GetComponent<Image>();
			tg.isOn = on;
			Place(go.GetComponent<RectTransform>(), x, y, w, h);
			toggles[id] = tg;
			widgets[id] = box.GetComponent<Image>();
			widgetRoots[id] = go;
			return id;
		}

		string Get(string id)
		{
			InputField f;
			if (inputs.TryGetValue(id, out f) && f != null) return f.text ?? "";
			Text t;
			if (labels.TryGetValue(id, out t) && t != null) return t.text ?? "";
			return "";
		}

		void Set(string id, string value)
		{
			InputField f;
			if (inputs.TryGetValue(id, out f) && f != null) { f.text = value ?? ""; return; }
			Text t;
			if (labels.TryGetValue(id, out t) && t != null) t.text = value ?? "";
		}

		float Value(string id)
		{
			Slider s;
			if (sliders.TryGetValue(id, out s) && s != null) return s.value;
			return 0f;
		}

		bool Checked(string id)
		{
			Toggle t;
			if (toggles.TryGetValue(id, out t) && t != null) return t.isOn;
			return false;
		}

		void Remove(string id)
		{
			GameObject go;
			if (widgetRoots.TryGetValue(id, out go) && go != null)
				UnityEngine.Object.Destroy(go);
			else
			{
				Graphic g;
				if (widgets.TryGetValue(id, out g) && g != null)
					UnityEngine.Object.Destroy(g.gameObject);
			}
			widgetRoots.Remove(id);
			widgets.Remove(id);
			inputs.Remove(id);
			labels.Remove(id);
			sliders.Remove(id);
			toggles.Remove(id);
		}

		public void Clear()
		{
			if (root == null) return;
			for (int i = root.childCount - 1; i >= 0; i--)
			{
				var ch = root.GetChild(i);
				if (ch == null) continue;
				if (ch.name != null && ch.name.StartsWith("__")) continue;
				UnityEngine.Object.Destroy(ch.gameObject);
			}
			widgets.Clear();
			widgetRoots.Clear();
			inputs.Clear();
			labels.Clear();
			sliders.Clear();
			toggles.Clear();
		}

		string Rect(float x, float y, float w, float h, Color color)
		{
			var id = Panel(x, y, w, h);
			Graphic g;
			if (widgets.TryGetValue(id, out g) && g != null) g.color = color;
			return id;
		}

		string Image(string path, float x, float y, float w, float h)
		{
			var id = NewId();
			var go = new GameObject(id, typeof(RectTransform), typeof(RawImage));
			go.transform.SetParent(root, false);
			var raw = go.GetComponent<RawImage>();
			raw.color = Color.white;
			raw.raycastTarget = false;
			var host = Host();
			Texture2D tex = host != null ? host.LoadUserTexture(path) : null;
			if (tex != null) raw.texture = tex;
			Place(go.GetComponent<RectTransform>(), x, y, w, h);
			widgets[id] = raw;
			widgetRoots[id] = go;
			return id;
		}

		void ColorWidget(string id, Color color)
		{
			Graphic g;
			if (widgets.TryGetValue(id, out g) && g != null) g.color = color;
		}

		Color ColorArg(LuaValue[] a, int i)
		{
			if (i >= a.Length) return style.panel;
			if (a[i].Type == LuaType.Table) return ColorOf(a[i].Table, style.panel);
			if (a[i].Type == LuaType.Number)
			{
				float r = (float)a[i].N;
				float g = i + 1 < a.Length && a[i + 1].Type == LuaType.Number ? (float)a[i + 1].N : r;
				float b = i + 2 < a.Length && a[i + 2].Type == LuaType.Number ? (float)a[i + 2].N : r;
				float al = i + 3 < a.Length && a[i + 3].Type == LuaType.Number ? (float)a[i + 3].N : 1f;
				return new Color(r, g, b, al);
			}
			return style.panel;
		}

		static Color ColorOf(LuaTable t, Color fallback)
		{
			if (t == null) return fallback;
			float R(int i)
			{
				var x = t.Get(LuaValue.Number(i));
				return x.Type == LuaType.Number ? (float)x.N : 0f;
			}
			var a = t.Get(LuaValue.Number(4));
			return new Color(R(1), R(2), R(3), a.Type == LuaType.Number ? (float)a.N : 1f);
		}

		LuaValue ApplyStyle(LuaValue[] a)
		{
			if (a.Length == 0 || a[0].Type != LuaType.Table) { style = Style.System(); ApplyWindow(); return LuaValue.Nil; }
			var t = a[0].Table;
			style.window = ColorOf(t, "window", style.window);
			style.panel = ColorOf(t, "panel", style.panel);
			style.button = ColorOf(t, "button", style.button);
			style.buttonText = ColorOf(t, "buttonText", style.buttonText);
			style.text = ColorOf(t, "text", style.text);
			style.input = ColorOf(t, "input", style.input);
			style.inputText = ColorOf(t, "inputText", style.inputText);
			var fs = t.Get(LuaValue.String("fontSize"));
			if (fs.Type == LuaType.Number) style.fontSize = Mathf.Clamp((int)fs.N, 8, 48);
			ApplyWindow();
			return LuaValue.Nil;
		}

		void ApplyWindow()
		{
			if (windowBg != null) windowBg.color = style.window;
			if (root != null)
			{
				var img = root.GetComponentInParent<LuaApp>();
				if (img != null) img.ApplyChrome(style.window);
			}
		}

		static Color ColorOf(LuaTable t, string key, Color fallback)
		{
			var v = t.Get(LuaValue.String(key));
			if (v.Type != LuaType.Table) return fallback;
			return ColorOf(v.Table, fallback);
		}

		Text MakeText(string text, Color color)
		{
			var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
			go.transform.SetParent(root, false);
			var t = go.GetComponent<Text>();
			t.font = font;
			t.fontSize = style.fontSize;
			t.color = color;
			t.text = text ?? "";
			t.alignment = TextAnchor.MiddleLeft;
			t.horizontalOverflow = HorizontalWrapMode.Overflow;
			t.verticalOverflow = VerticalWrapMode.Overflow;
			t.raycastTarget = false;
			t.supportRichText = false;
			return t;
		}

		void Place(RectTransform rt, float x, float y, float w, float h)
		{
			rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
			rt.pivot = new Vector2(0f, 1f);
			rt.anchoredPosition = new Vector2(x, -y);
			rt.sizeDelta = new Vector2(Mathf.Max(8f, w), Mathf.Max(8f, h));
		}

		static void Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
		{
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = new Vector2(l, b);
			rt.offsetMax = new Vector2(r, t);
		}

		static string Str(LuaValue[] a, int i) { return i < a.Length ? a[i].AsString() : ""; }
		static float Num(LuaValue[] a, int i, float d = 0f)
		{
			if (i >= a.Length) return d;
			if (a[i].Type == LuaType.Number) return (float)a[i].N;
			return d;
		}
		static LuaValue Native(Func<LuaValue[], LuaValue> fn) { return LuaValue.FromFn(new LuaFunction { Native = fn }); }
	}
}
