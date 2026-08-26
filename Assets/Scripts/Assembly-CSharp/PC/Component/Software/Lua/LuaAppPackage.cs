using System;
using UnityEngine;

namespace PC.Component.Software.Lua
{
	[Serializable]
	public class LuaAppPackage
	{
		public string pcos = "lua-app";
		public string name = "App";
		public string icon = "";
		public string script = "";

		public static bool IsPackage(string content)
		{
			if (string.IsNullOrEmpty(content)) return false;
			// Быстрая проверка: должен начинаться с { и содержать "pcos"
			var trimmed = content.TrimStart();
			if (!trimmed.StartsWith("{")) return false;
			try
			{
				var pkg = JsonUtility.FromJson<LuaAppPackage>(content);
				return pkg != null && pkg.pcos == "lua-app";
			}
			catch
			{
				return false;
			}
		}

		public static bool NeedsWindow(string script)
		{
			if (string.IsNullOrEmpty(script)) return false;
			if (Has(script, "ui.")) return true;
			if (Has(script, "gfx.")) return true;
			if (Has(script, "onupdate")) return true;
			if (Has(script, "isdraggable")) return true;
			if (Has(script, "ismaximable") || Has(script, "ismaxible") || Has(script, "ismaximizable")) return true;
			return false;
		}

		static bool Has(string s, string token)
		{
			return s.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		public static LuaAppPackage Parse(string content)
		{
			if (string.IsNullOrEmpty(content)) return null;
			if (IsPackage(content))
			{
				try { return JsonUtility.FromJson<LuaAppPackage>(content); }
				catch { return null; }
			}
			return new LuaAppPackage { name = "Lua", script = content };
		}

		public string ToJson()
		{
			if (string.IsNullOrEmpty(pcos)) pcos = "lua-app";
			return JsonUtility.ToJson(this);
		}

		public Sprite MakeIcon(Sprite fallback)
		{
			if (string.IsNullOrEmpty(icon)) return fallback;
			try
			{
				var data = Convert.FromBase64String(icon);
				var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
				tex.filterMode = FilterMode.Point;
				if (!tex.LoadImage(data)) return fallback;
				return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
			}
			catch { return fallback; }
		}
	}
}
