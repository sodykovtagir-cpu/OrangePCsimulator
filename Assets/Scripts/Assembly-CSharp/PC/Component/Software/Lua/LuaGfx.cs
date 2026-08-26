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
			var pix = new Color[tw * th];
			for (int i = 0; i < pix.Length; i++) pix[i] = c;
			tex.SetPixels(pix);
			dirty = true;
			Apply();
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
