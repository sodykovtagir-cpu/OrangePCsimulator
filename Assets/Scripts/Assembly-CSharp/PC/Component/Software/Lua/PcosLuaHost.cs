using System;
using System.Collections.Generic;
using System.Globalization;
using PC.Component.Software.OS;

namespace PC.Component.Software.Lua
{
	public static class PcosLuaHost
	{
		public static void Bind(PcosLua vm, PC.Component.Software.OS.OperatingSystem os,
            PC.Component.Software.App owner = null, bool headlessAdministrator = false)
		{
			if (vm == null) return;
            int session = os != null ? os.SessionToken : -1;
            bool hasOwner = !ReferenceEquals(owner, null);
            Func<bool> authorized = () => os != null && os.Ready &&
                os.SessionToken == session &&
                (hasOwner ? owner != null && owner.IsAdministrator : headlessAdministrator);

            var admin = new LuaTable();
            admin.Set(LuaValue.String("status"), Native(a => LuaValue.Bool(authorized())));
            admin.Set(LuaValue.String("request"), Native(a =>
            {
                if (os == null || !os.Ready || os.SessionToken != session ||
                    a.Length == 0 || a[0].Type != LuaType.Function) return LuaValue.Bool(false);
                var callback = a[0];
                if (authorized())
                {
                    try { vm.Call(callback, new List<LuaValue> { LuaValue.Bool(true) }); }
                    catch (Exception ex) { os.ShowMessageBox("Lua", ex.Message); }
                    return LuaValue.Bool(true);
                }
                bool requested = os.RequestAdministrator(granted =>
                {
                    if (granted && os != null && os.Ready && os.SessionToken == session)
                    {
                        if (hasOwner)
                        {
                            if (owner != null) owner.SetAdministrator(true);
                            else granted = false;
                        }
                        else headlessAdministrator = true;
                    }
                    else granted = false;
                    try { vm.Call(callback, new List<LuaValue> { LuaValue.Bool(granted) }); }
                    catch (Exception ex) { if (os != null && os.Ready) os.ShowMessageBox("Lua", ex.Message); }
                });
                return LuaValue.Bool(requested); // result arrives in callback, not in Lua's return value
            }));
            vm.SetGlobal("admin", LuaValue.FromTable(admin));

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
                var name = a[0].AsString();
                // Native .tmn files can run remote commands. Lua must use the
                // permission-checked terminal.run API, never os.open(.tmn).
                if (name.EndsWith(".tmn", StringComparison.OrdinalIgnoreCase))
                    return LuaValue.Bool(false);
                if (PC.Component.Software.OS.OperatingSystem.IsSystemPath(name) && !authorized())
                    return LuaValue.Bool(false);
                return LuaValue.Bool(os.LuaOpen(name));
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
            osTbl.Set(LuaValue.String("is_admin"), Native(a => LuaValue.Bool(authorized())));
            osTbl.Set(LuaValue.String("request_admin"), admin.Get(LuaValue.String("request")));
			vm.SetGlobal("os", LuaValue.FromTable(osTbl));

			var fs = new LuaTable();
			fs.Set(LuaValue.String("list"), Native(a => ToArray(os != null ? os.ListUserFiles(authorized()) : null)));
			fs.Set(LuaValue.String("read"), Native(a =>
			{
				if (os == null || a.Length == 0) return LuaValue.Nil;
				string text;
                var path = a[0].AsString();
                if (PC.Component.Software.OS.OperatingSystem.IsSystemPath(path) && !authorized())
                    return LuaValue.Nil;
                return os.TryReadFile(path, out text,
                    PC.Component.Software.OS.OperatingSystem.IsSystemPath(path) ? 0 : -1) ? LuaValue.String(text) : LuaValue.Nil;
			}));
			fs.Set(LuaValue.String("write"), Native(a =>
			{
				if (os == null || a.Length < 2) return LuaValue.Bool(false);
				var path = a[0].AsString();
                bool systemPath = PC.Component.Software.OS.OperatingSystem.IsSystemPath(path);
                if (systemPath && !authorized()) return LuaValue.Bool(false);
                return LuaValue.Bool(os.TryWriteFile(path, a[1].AsString(), systemPath ? 0 : -1));
			}));
			fs.Set(LuaValue.String("delete"), Native(a =>
			{
				if (os == null || a.Length == 0) return LuaValue.Bool(false);
				var path = a[0].AsString();
                bool systemPath = PC.Component.Software.OS.OperatingSystem.IsSystemPath(path);
                if (systemPath && !authorized()) return LuaValue.Bool(false);
                return LuaValue.Bool(os.TryDeleteFile(path));
			}));
			fs.Set(LuaValue.String("exists"), Native(a =>
			{
				if (os == null || a.Length == 0) return LuaValue.Bool(false);
                var path = a[0].AsString();
                if (PC.Component.Software.OS.OperatingSystem.IsSystemPath(path) && !authorized())
                    return LuaValue.Bool(false);
                return LuaValue.Bool(os.FileExists(path));
			}));
	
			vm.SetGlobal("fs", LuaValue.FromTable(fs));

            var pc = new LuaTable();
            pc.Set(LuaValue.String("ids"), Native(a =>
            {
                var list = new LuaTable();
                if (authorized())
                {
                    var ids = os.RegisteredPcIds();
                    for (int i = 0; i < ids.Count; i++)
                        list.Set(LuaValue.Number(i + 1), LuaValue.String(ids[i].ToString("X8")));
                }
                return LuaValue.FromTable(list);
            }));
            pc.Set(LuaValue.String("scan"), Native(a =>
            {
                var list = new LuaTable();
                if (authorized())
                {
                    var ids = os.RegisteredPcIds();
                    for (int i = 0; i < ids.Count; i++)
                    {
                        var board = PC.Component.Software.DeviceAutomation.Find(ids[i]);
                        var entry = new LuaTable();
                        entry.Set(LuaValue.String("id"), LuaValue.String(ids[i].ToString("X8")));
                        entry.Set(LuaValue.String("name"), LuaValue.String(board != null ? board.name : "Offline"));
                        entry.Set(LuaValue.String("online"), LuaValue.Bool(board != null && board.Running));
                        list.Set(LuaValue.Number(i + 1), LuaValue.FromTable(entry));
                    }
                }
                return LuaValue.FromTable(list);
            }));
            pc.Set(LuaValue.String("command"), Native(a =>
            {
                int id;
                if (!authorized() || a.Length < 2 || !ParsePcId(a[0], out id))
                    return LuaValue.Bool(false);
                return LuaValue.Bool(os.RegisteredPcCommand(id, a[1].AsString(),
                    a.Length > 2 ? a[2].AsString() : ""));
            }));
            pc.Set(LuaValue.String("connect"), Native(a =>
            {
                int id;
                return LuaValue.Bool(authorized() && a.Length > 0 &&
                    ParsePcId(a[0], out id) && os.OpenRegisteredRemoteDesk(id));
            }));
            vm.SetGlobal("pc", LuaValue.FromTable(pc));

            var terminal = new LuaTable();
            terminal.Set(LuaValue.String("run"), Native(a => LuaValue.Bool(
                authorized() && a.Length > 0 && os.RunAdministratorTerminal(a[0].AsString()))));
            vm.SetGlobal("terminal", LuaValue.FromTable(terminal));

			var win = new LuaTable();
			win.Set(LuaValue.String("alert"), Native(a =>
			{
				if (os == null) return LuaValue.Nil;
				// alert(title, msg) — как os.alert, для консистентности
				var title = a.Length > 0 ? a[0].AsString() : "Lua";
				var msg = a.Length > 1 ? a[1].AsString() : (a.Length > 0 ? a[0].AsString() : "");
				if (a.Length == 1) { msg = title; title = "Lua"; }
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

        // Lua IDs are eight-digit hexadecimal strings; decimal Lua numbers also work.
        private static bool ParsePcId(LuaValue value, out int id)
        {
            id = 0;
            if (value.Type == LuaType.Number)
            {
                if (double.IsNaN(value.N) || double.IsInfinity(value.N) ||
                    value.N == 0 || value.N < int.MinValue || value.N > int.MaxValue ||
                    value.N != Math.Floor(value.N))
                    return false;
                id = (int)value.N;
                return true;
            }
            if (value.Type != LuaType.String) return false;
            var text = (value.S ?? "").Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2);
            return text.Length == 8 &&
                int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id) && id != 0;
        }
	}
}
