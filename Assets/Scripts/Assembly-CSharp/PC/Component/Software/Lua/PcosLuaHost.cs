using System;
using System.Collections.Generic;
using PC.Component.Software.OS;

namespace PC.Component.Software.Lua
{
	public static class PcosLuaHost
	{
		public static void Bind(PcosLua vm, PC.Component.Software.OS.OperatingSystem os)
		{
			if (vm == null) return;

			var osTbl = new LuaTable();
			osTbl.Set(LuaValue.String("alert"), Native(a =>
			{
				if (os == null) return LuaValue.Nil;
				var title = a.Length > 0 ? a[0].AsString() : "Lua";
				var msg = a.Length > 1 ? a[1].AsString() : (a.Length > 0 ? a[0].AsString() : "");
				if (a.Length == 1) { msg = title; title = "Lua"; }
				os.ShowMessageBox(title, msg);
				return LuaValue.Nil;
			}));
			osTbl.Set(LuaValue.String("open"), Native(a =>
			{
				if (os == null || a.Length == 0) return LuaValue.Bool(false);
				return LuaValue.Bool(os.LuaOpen(a[0].AsString()));
			}));
			osTbl.Set(LuaValue.String("close"), Native(a =>
			{
				if (os == null || a.Length == 0) return LuaValue.Bool(false);
				return LuaValue.Bool(os.TryCloseApp(a[0].AsString()));
			}));
			osTbl.Set(LuaValue.String("apps"), Native(a => ToArray(os != null ? os.InstalledAppNames() : null)));
			osTbl.Set(LuaValue.String("windows"), Native(a => ToArray(os != null ? os.RunningAppNames() : null)));
			osTbl.Set(LuaValue.String("running"), Native(a => ToArray(os != null ? os.RunningAppNames() : null)));
			osTbl.Set(LuaValue.String("username"), Native(a => LuaValue.String(os != null ? os.UserName : "")));
			osTbl.Set(LuaValue.String("id"), Native(a =>
			{
				if (os == null) return LuaValue.String("00000000");
				return LuaValue.String(os.SystemId.ToString("X8"));
			}));
			osTbl.Set(LuaValue.String("time"), Native(a => LuaValue.Number(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds)));
			osTbl.Set(LuaValue.String("ready"), Native(a => LuaValue.Bool(os != null && os.Ready)));
			osTbl.Set(LuaValue.String("shutdown"), Native(a =>
			{
				if (os != null) os.PowerClicked();
				return LuaValue.Nil;
			}));
			osTbl.Set(LuaValue.String("installed"), Native(a =>
			{
				if (os == null || a.Length == 0) return LuaValue.Bool(false);
				return LuaValue.Bool(os.IsAppInstalled(a[0].AsString()));
			}));
			vm.SetGlobal("os", LuaValue.FromTable(osTbl));

			var fs = new LuaTable();
			fs.Set(LuaValue.String("list"), Native(a => ToArray(os != null ? os.ListUserFiles() : null)));
			fs.Set(LuaValue.String("read"), Native(a =>
			{
				if (os == null || a.Length == 0) return LuaValue.Nil;
				string text;
				return os.TryReadFile(a[0].AsString(), out text) ? LuaValue.String(text) : LuaValue.Nil;
			}));
			fs.Set(LuaValue.String("write"), Native(a =>
			{
				if (os == null || a.Length < 2) return LuaValue.Bool(false);
				return LuaValue.Bool(os.TryWriteFile(a[0].AsString(), a[1].AsString()));
			}));
			fs.Set(LuaValue.String("exists"), Native(a =>
			{
				if (os == null || a.Length == 0) return LuaValue.Bool(false);
				return LuaValue.Bool(os.FileExists(a[0].AsString()));
			}));
			fs.Set(LuaValue.String("delete"), Native(a =>
			{
				if (os == null || a.Length == 0) return LuaValue.Bool(false);
				return LuaValue.Bool(os.TryDeleteFile(a[0].AsString()));
			}));
			vm.SetGlobal("fs", LuaValue.FromTable(fs));

			var win = new LuaTable();
			win.Set(LuaValue.String("alert"), Native(a =>
			{
				if (os == null) return LuaValue.Nil;
				var msg = a.Length > 0 ? a[0].AsString() : "";
				var title = a.Length > 1 ? a[1].AsString() : "Lua";
				os.ShowMessageBox(title, msg);
				return LuaValue.Nil;
			}));
			win.Set(LuaValue.String("list"), Native(a => ToArray(os != null ? os.RunningAppNames() : null)));
			win.Set(LuaValue.String("open"), Native(a =>
			{
				if (os == null || a.Length == 0) return LuaValue.Bool(false);
				return LuaValue.Bool(os.TryLaunchApp(a[0].AsString()));
			}));
			win.Set(LuaValue.String("close"), Native(a =>
			{
				if (os == null || a.Length == 0) return LuaValue.Bool(false);
				return LuaValue.Bool(os.TryCloseApp(a[0].AsString()));
			}));
			vm.SetGlobal("win", LuaValue.FromTable(win));
		}

		static LuaValue ToArray(IList<string> items)
		{
			var t = new LuaTable();
			if (items == null) return LuaValue.FromTable(t);
			for (int i = 0; i < items.Count; i++)
				t.Set(LuaValue.Number(i + 1), LuaValue.String(items[i] ?? ""));
			return LuaValue.FromTable(t);
		}

		static LuaValue Native(Func<LuaValue[], LuaValue> fn)
		{
			return LuaValue.FromFn(new LuaFunction { Native = fn });
		}
	}
}
