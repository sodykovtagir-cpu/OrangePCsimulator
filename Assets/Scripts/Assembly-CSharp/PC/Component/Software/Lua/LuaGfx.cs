using System;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software.Lua
{
	public sealed class LuaGfx
	{
		readonly RectTransform root;
		readonly PcosLua vm;
		RawImage view;
		Texture2D tex;
		Color pen = Color.white;
		int tw = 320;
		int th = 180;
		bool dirty;

		public LuaGfx(RectTransform root, PcosLua vm)
		{
			this.root = root;
			this.vm = vm;
		}

		public void Bind()
		{
			if (vm == null) return;
			var g = new LuaTable();
			g.Set(LuaValue.String("size"), Native(a =>
			{
				int w = Mathf.Clamp((int)Num(a, 0, tw), 8, 640);
				int h = Mathf.Clamp((int)Num(a, 1, th), 8, 480);
				Resize(w, h);
				return LuaValue.Nil;
			}));
			g.Set(LuaValue.String("color"), Native(a =>
			{
				pen = Col(a, 0, pen);
				return LuaValue.Nil;
			}));
			g.Set(LuaValue.String("clear"), Native(a =>
			{
				Ensure();
				var c = a.Length == 0 ? new Color(0, 0, 0, 0) : Col(a, 0, Color.clear);
				Fill(c);
				return LuaValue.Nil;
			}));
			g.Set(LuaValue.String("pixel"), Native(a =>
			{
				Ensure();
				Plot((int)Num(a, 0), (int)Num(a, 1), Col(a, 2, pen));
				return LuaValue.Nil;
			}));
			g.Set(LuaValue.String("rect"), Native(a =>
			{
				Ensure();
				FillRect((int)Num(a, 0), (int)Num(a, 1), (int)Num(a, 2, 10), (int)Num(a, 3, 10), Col(a, 4, pen));
				return LuaValue.Nil;
			}));
			g.Set(LuaValue.String("box"), Native(a =>
			{
				Ensure();
				StrokeRect((int)Num(a, 0), (int)Num(a, 1), (int)Num(a, 2, 10), (int)Num(a, 3, 10), Col(a, 4, pen));
				return LuaValue.Nil;
			}));
			g.Set(LuaValue.String("line"), Native(a =>
			{
				Ensure();
				Line((int)Num(a, 0), (int)Num(a, 1), (int)Num(a, 2), (int)Num(a, 3), Col(a, 4, pen));
				return LuaValue.Nil;
			}));
			g.Set(LuaValue.String("circle"), Native(a =>
			{
				Ensure();
				Circle((int)Num(a, 0), (int)Num(a, 1), (int)Num(a, 2, 8), Col(a, 3, pen), true);
				return LuaValue.Nil;
			}));
			g.Set(LuaValue.String("ring"), Native(a =>
			{
				Ensure();
				Circle((int)Num(a, 0), (int)Num(a, 1), (int)Num(a, 2, 8), Col(a, 3, pen), false);
				return LuaValue.Nil;
			}));
			g.Set(LuaValue.String("text"), Native(a =>
			{
				Ensure();
				int tx = (int)Num(a, 0);
				int ty = (int)Num(a, 1);
				string text = a.Length > 2 ? a[2].AsString() : "";
				Color tc = Col(a, 3, pen);
				DrawText(tx, ty, text, tc);
				return LuaValue.Nil;
			}));
			g.Set(LuaValue.String("getpixel"), Native(a =>
			{
				if (tex == null) return LuaValue.Nil;
				int px = (int)Num(a, 0);
				int py = (int)Num(a, 1);
				if (px < 0 || py < 0 || px >= tw || py >= th) return LuaValue.Nil;
				var c = tex.GetPixel(px, th - 1 - py);
				var ct = new LuaTable();
				ct.Set(LuaValue.Number(1), LuaValue.Number(c.r));
				ct.Set(LuaValue.Number(2), LuaValue.Number(c.g));
				ct.Set(LuaValue.Number(3), LuaValue.Number(c.b));
				ct.Set(LuaValue.Number(4), LuaValue.Number(c.a));
				return LuaValue.FromTable(ct);
			}));
			g.Set(LuaValue.String("flip"), Native(a => { Apply(); return LuaValue.Nil; }));
			g.Set(LuaValue.String("apply"), Native(a => { Apply(); return LuaValue.Nil; }));
			vm.SetGlobal("gfx", LuaValue.FromTable(g));
		}

		public void Apply()
		{
			if (!dirty || tex == null) return;
			tex.Apply();
			dirty = false;
		}

		public void Destroy()
		{
			if (tex != null) UnityEngine.Object.Destroy(tex);
			tex = null;
			if (view != null) UnityEngine.Object.Destroy(view.gameObject);
			view = null;
		}

		void Ensure()
		{
			if (tex != null && view != null) return;
			Resize(tw, th);
		}

		void Resize(int w, int h)
		{
			tw = w;
			th = h;
			if (tex != null) UnityEngine.Object.Destroy(tex);
			tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
			tex.filterMode = FilterMode.Point;
			tex.wrapMode = TextureWrapMode.Clamp;
			Fill(new Color(0, 0, 0, 0));
			if (root == null) return;
			if (view == null)
			{
				var existing = root.Find("__gfx");
				GameObject go;
				if (existing != null) go = existing.gameObject;
				else
				{
					go = new GameObject("__gfx", typeof(RectTransform), typeof(RawImage));
					go.transform.SetParent(root, false);
					go.transform.SetAsFirstSibling();
				}
				view = go.GetComponent<RawImage>();
				if (view == null) view = go.AddComponent<RawImage>();
				view.raycastTarget = false;
				var rt = go.GetComponent<RectTransform>();
				rt.anchorMin = Vector2.zero;
				rt.anchorMax = Vector2.one;
				rt.offsetMin = Vector2.zero;
				rt.offsetMax = Vector2.zero;
			}
			view.texture = tex;
			view.uvRect = new Rect(0, 0, 1, 1);
		}

		void Fill(Color c)
		{
			if (tex == null) return;
			var pix = new Color32[tw * th];
			byte R = (byte)(Mathf.Clamp01(c.r) * 255);
			byte G = (byte)(Mathf.Clamp01(c.g) * 255);
			byte B = (byte)(Mathf.Clamp01(c.b) * 255);
			byte A = (byte)(Mathf.Clamp01(c.a) * 255);
			var fill = new Color32(R, G, B, A);
			for (int i = 0; i < pix.Length; i++) pix[i] = fill;
			tex.SetPixels32(pix);
			dirty = true;
		}

		void Plot(int x, int y, Color c)
		{
			if (tex == null || x < 0 || y < 0 || x >= tw || y >= th) return;
			tex.SetPixel(x, th - 1 - y, c);
			dirty = true;
		}

		void FillRect(int x, int y, int w, int h, Color c)
		{
			if (w < 0) { x += w; w = -w; }
			if (h < 0) { y += h; h = -h; }
			int x2 = Mathf.Min(x + w, tw);
			int y2 = Mathf.Min(y + h, th);
			int x1 = Mathf.Max(x, 0);
			int y1 = Mathf.Max(y, 0);
			for (int py = y1; py < y2; py++)
				for (int px = x1; px < x2; px++)
					tex.SetPixel(px, th - 1 - py, c);
			dirty = true;
		}

		void StrokeRect(int x, int y, int w, int h, Color c)
		{
			if (w <= 0 || h <= 0) return;
			FillRect(x, y, w, 1, c);
			FillRect(x, y + h - 1, w, 1, c);
			FillRect(x, y, 1, h, c);
			FillRect(x + w - 1, y, 1, h, c);
		}

		void Line(int x0, int y0, int x1, int y1, Color c)
		{
			int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
			int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
			int err = dx + dy;
			while (true)
			{
				Plot(x0, y0, c);
				if (x0 == x1 && y0 == y1) break;
				int e2 = 2 * err;
				if (e2 >= dy) { err += dy; x0 += sx; }
				if (e2 <= dx) { err += dx; y0 += sy; }
			}
		}

		void Circle(int cx, int cy, int r, Color c, bool fill)
		{
			if (r < 0) r = -r;
			if (r > 256) r = 256;
			if (fill)
			{
				for (int yy = -r; yy <= r; yy++)
				{
					int span = (int)Mathf.Sqrt(r * r - yy * yy);
					FillRect(cx - span, cy + yy, span * 2 + 1, 1, c);
				}
				return;
			}
			int x = r, y = 0, err = 0;
			while (x >= y)
			{
				Plot(cx + x, cy + y, c); Plot(cx + y, cy + x, c);
				Plot(cx - y, cy + x, c); Plot(cx - x, cy + y, c);
				Plot(cx - x, cy - y, c); Plot(cx - y, cy - x, c);
				Plot(cx + y, cy - x, c); Plot(cx + x, cy - y, c);
				y++;
				if (err <= 0) err += 2 * y + 1;
				if (err > 0) { x--; err -= 2 * x + 1; }
			}
		}

		void DrawText(int x, int y, string text, Color c)
		{
			if (tex == null || string.IsNullOrEmpty(text)) return;
			// Простой 5x7 bitmap шрифт для базовых ASCII символов
			foreach (char ch in text)
			{
				byte[] glyph = GetGlyph(ch);
				if (glyph != null)
				{
					for (int gy = 0; gy < 7; gy++)
						for (int gx = 0; gx < 5; gx++)
							if ((glyph[gy] & (1 << (4 - gx))) != 0)
								Plot(x + gx, y + gy, c);
				}
				x += 6; // 5px ширина + 1px интервал
			}
		}

		static byte[] GetGlyph(char ch)
		{
			// Минимальный 5x7 bitmap шрифт (каждая строка — 5 бит в старших битах байта)
			switch (ch)
			{
				case 'A': return new byte[]{0x0E,0x11,0x11,0x1F,0x11,0x11,0x11};
				case 'B': return new byte[]{0x1E,0x11,0x11,0x1E,0x11,0x11,0x1E};
				case 'C': return new byte[]{0x0E,0x11,0x10,0x10,0x10,0x11,0x0E};
				case 'D': return new byte[]{0x1E,0x11,0x11,0x11,0x11,0x11,0x1E};
				case 'E': return new byte[]{0x1F,0x10,0x10,0x1E,0x10,0x10,0x1F};
				case 'F': return new byte[]{0x1F,0x10,0x10,0x1E,0x10,0x10,0x10};
				case 'G': return new byte[]{0x0E,0x11,0x10,0x17,0x11,0x11,0x0E};
				case 'H': return new byte[]{0x11,0x11,0x11,0x1F,0x11,0x11,0x11};
				case 'I': return new byte[]{0x0E,0x04,0x04,0x04,0x04,0x04,0x0E};
				case 'J': return new byte[]{0x07,0x02,0x02,0x02,0x02,0x12,0x0C};
				case 'K': return new byte[]{0x11,0x12,0x14,0x18,0x14,0x12,0x11};
				case 'L': return new byte[]{0x10,0x10,0x10,0x10,0x10,0x10,0x1F};
				case 'M': return new byte[]{0x11,0x1B,0x15,0x15,0x11,0x11,0x11};
				case 'N': return new byte[]{0x11,0x11,0x19,0x15,0x13,0x11,0x11};
				case 'O': return new byte[]{0x0E,0x11,0x11,0x11,0x11,0x11,0x0E};
				case 'P': return new byte[]{0x1E,0x11,0x11,0x1E,0x10,0x10,0x10};
				case 'Q': return new byte[]{0x0E,0x11,0x11,0x11,0x15,0x12,0x0D};
				case 'R': return new byte[]{0x1E,0x11,0x11,0x1E,0x14,0x12,0x11};
				case 'S': return new byte[]{0x0E,0x11,0x10,0x0E,0x01,0x11,0x0E};
				case 'T': return new byte[]{0x1F,0x04,0x04,0x04,0x04,0x04,0x04};
				case 'U': return new byte[]{0x11,0x11,0x11,0x11,0x11,0x11,0x0E};
				case 'V': return new byte[]{0x11,0x11,0x11,0x11,0x0A,0x0A,0x04};
				case 'W': return new byte[]{0x11,0x11,0x11,0x15,0x15,0x1B,0x11};
				case 'X': return new byte[]{0x11,0x11,0x0A,0x04,0x0A,0x11,0x11};
				case 'Y': return new byte[]{0x11,0x11,0x0A,0x04,0x04,0x04,0x04};
				case 'Z': return new byte[]{0x1F,0x01,0x02,0x04,0x08,0x10,0x1F};
				case 'a': return new byte[]{0x00,0x00,0x0E,0x01,0x0F,0x11,0x0F};
				case 'b': return new byte[]{0x10,0x10,0x1E,0x11,0x11,0x11,0x1E};
				case 'c': return new byte[]{0x00,0x00,0x0E,0x11,0x10,0x11,0x0E};
				case 'd': return new byte[]{0x01,0x01,0x0F,0x11,0x11,0x11,0x0F};
				case 'e': return new byte[]{0x00,0x00,0x0E,0x11,0x1F,0x10,0x0E};
				case 'f': return new byte[]{0x06,0x09,0x08,0x1C,0x08,0x08,0x08};
				case 'g': return new byte[]{0x00,0x00,0x0F,0x11,0x0F,0x01,0x0E};
				case 'h': return new byte[]{0x10,0x10,0x1E,0x11,0x11,0x11,0x11};
				case 'i': return new byte[]{0x04,0x00,0x0C,0x04,0x04,0x04,0x0E};
				case 'j': return new byte[]{0x02,0x00,0x06,0x02,0x02,0x12,0x0C};
				case 'k': return new byte[]{0x10,0x10,0x12,0x14,0x18,0x14,0x12};
				case 'l': return new byte[]{0x0C,0x04,0x04,0x04,0x04,0x04,0x0E};
				case 'm': return new byte[]{0x00,0x00,0x1A,0x15,0x15,0x11,0x11};
				case 'n': return new byte[]{0x00,0x00,0x1E,0x11,0x11,0x11,0x11};
				case 'o': return new byte[]{0x00,0x00,0x0E,0x11,0x11,0x11,0x0E};
				case 'p': return new byte[]{0x00,0x00,0x1E,0x11,0x1E,0x10,0x10};
				case 'q': return new byte[]{0x00,0x00,0x0F,0x11,0x0F,0x01,0x01};
				case 'r': return new byte[]{0x00,0x00,0x16,0x19,0x10,0x10,0x10};
				case 's': return new byte[]{0x00,0x00,0x0F,0x10,0x0E,0x01,0x1E};
				case 't': return new byte[]{0x08,0x08,0x1C,0x08,0x08,0x09,0x06};
				case 'u': return new byte[]{0x00,0x00,0x11,0x11,0x11,0x11,0x0F};
				case 'v': return new byte[]{0x00,0x00,0x11,0x11,0x11,0x0A,0x04};
				case 'w': return new byte[]{0x00,0x00,0x11,0x11,0x15,0x15,0x0A};
				case 'x': return new byte[]{0x00,0x00,0x11,0x0A,0x04,0x0A,0x11};
				case 'y': return new byte[]{0x00,0x00,0x11,0x11,0x0F,0x01,0x0E};
				case 'z': return new byte[]{0x00,0x00,0x1F,0x02,0x04,0x08,0x1F};
				case '0': return new byte[]{0x0E,0x11,0x13,0x15,0x19,0x11,0x0E};
				case '1': return new byte[]{0x04,0x0C,0x04,0x04,0x04,0x04,0x0E};
				case '2': return new byte[]{0x0E,0x11,0x01,0x02,0x04,0x08,0x1F};
				case '3': return new byte[]{0x1F,0x02,0x04,0x02,0x01,0x11,0x0E};
				case '4': return new byte[]{0x02,0x06,0x0A,0x12,0x1F,0x02,0x02};
				case '5': return new byte[]{0x1F,0x10,0x1E,0x01,0x01,0x11,0x0E};
				case '6': return new byte[]{0x06,0x08,0x10,0x1E,0x11,0x11,0x0E};
				case '7': return new byte[]{0x1F,0x01,0x02,0x04,0x08,0x08,0x08};
				case '8': return new byte[]{0x0E,0x11,0x11,0x0E,0x11,0x11,0x0E};
				case '9': return new byte[]{0x0E,0x11,0x11,0x0F,0x01,0x02,0x0C};
				case ' ': return new byte[]{0x00,0x00,0x00,0x00,0x00,0x00,0x00};
				case '.': return new byte[]{0x00,0x00,0x00,0x00,0x00,0x06,0x06};
				case ',': return new byte[]{0x00,0x00,0x00,0x00,0x04,0x04,0x08};
				case '!': return new byte[]{0x04,0x04,0x04,0x04,0x04,0x00,0x04};
				case '?': return new byte[]{0x0E,0x11,0x01,0x02,0x04,0x00,0x04};
				case ':': return new byte[]{0x00,0x06,0x06,0x00,0x06,0x06,0x00};
				case '-': return new byte[]{0x00,0x00,0x00,0x1F,0x00,0x00,0x00};
				case '+': return new byte[]{0x00,0x04,0x04,0x1F,0x04,0x04,0x00};
				case '=': return new byte[]{0x00,0x00,0x1F,0x00,0x1F,0x00,0x00};
				case '/': return new byte[]{0x01,0x02,0x02,0x04,0x08,0x08,0x10};
				case '(': return new byte[]{0x02,0x04,0x08,0x08,0x08,0x04,0x02};
				case ')': return new byte[]{0x08,0x04,0x02,0x02,0x02,0x04,0x08};
				case '[': return new byte[]{0x0E,0x08,0x08,0x08,0x08,0x08,0x0E};
				case ']': return new byte[]{0x0E,0x02,0x02,0x02,0x02,0x02,0x0E};
				case '_': return new byte[]{0x00,0x00,0x00,0x00,0x00,0x00,0x1F};
				case '\'': return new byte[]{0x04,0x04,0x00,0x00,0x00,0x00,0x00};
				case '"': return new byte[]{0x0A,0x0A,0x00,0x00,0x00,0x00,0x00};
				default: return new byte[]{0x1F,0x11,0x11,0x11,0x11,0x11,0x1F}; // block for unknown
			}
		}

		static double Num(LuaValue[] a, int i, double d = 0)
		{
			if (i >= a.Length) return d;
			if (a[i].Type == LuaType.Number) return a[i].N;
			return d;
		}

		static Color Col(LuaValue[] a, int i, Color fallback)
		{
			if (i >= a.Length) return fallback;
			if (a[i].Type == LuaType.Table)
			{
				float R(int k)
				{
					var x = a[i].Table.Get(LuaValue.Number(k));
					return x.Type == LuaType.Number ? (float)x.N : 0f;
				}
				var aa = a[i].Table.Get(LuaValue.Number(4));
				return new Color(R(1), R(2), R(3), aa.Type == LuaType.Number ? (float)aa.N : 1f);
			}
			if (a[i].Type != LuaType.Number) return fallback;
			float r = (float)a[i].N;
			float g = i + 1 < a.Length && a[i + 1].Type == LuaType.Number ? (float)a[i + 1].N : r;
			float b = i + 2 < a.Length && a[i + 2].Type == LuaType.Number ? (float)a[i + 2].N : r;
			float al = i + 3 < a.Length && a[i + 3].Type == LuaType.Number ? (float)a[i + 3].N : 1f;
			return new Color(r, g, b, al);
		}

		static LuaValue Native(Func<LuaValue[], LuaValue> fn) { return LuaValue.FromFn(new LuaFunction { Native = fn }); }
	}
}
