using System;
using System.Collections.Generic;
using PC.Component.Software.Lua;

namespace PC.Component.Software.Sites
{
    /// <summary>Website-only capabilities. Never binds PcosLuaHost, LuaUi or LuaGfx.
    /// File commands address the package root, never the visitor's PCOS disks.</summary>
    public sealed class SiteLua
    {
        readonly SiteDocument document;
        readonly PcosLua vm;
        readonly Dictionary<string, bool> visible = new Dictionary<string, bool>(), enabled = new Dictionary<string, bool>();
        int actions;
        public string URL = "";
        public Action<string> Redirect, Message;
        public Action<SiteAsset> Download, OpenFile;
        public Action<string, string> TextChanged;
        public Action<string, bool> VisibilityChanged, EnabledChanged;
        public Action<string> ElementChanged, BackgroundChanged;
        public SiteLua(SiteDocument doc)
        {
            document = doc ?? throw new ArgumentNullException("doc");
            vm = new PcosLua { OperationLimit = 5000, StringLimit = 8192, CallDepthLimit = 32 };
            foreach (string key in new[] { "string", "table", "pcall" }) vm.SetGlobal(key, LuaValue.Nil);
            vm.Printer = s => { Budget(); Message?.Invoke(Short(s, 512)); };
            foreach (var descriptor in SiteCommands.All)
            {
                string command = descriptor.Name;
                vm.SetNative(command, args => Execute(command, args));
            }
        }
        SiteElement Element(string id)
        {
            var element = document.Find(id);
            if (element == null) throw new ArgumentException("Unknown element ID: " + id);
            return element;
        }
        static bool State(Dictionary<string, bool> states, string id) { bool value; return !states.TryGetValue(id, out value) || value; }
        LuaValue Execute(string command, LuaValue[] args)
        {
            var spec = SiteCommands.Find(command);
            if (spec == null || args.Length != spec.Types.Length) throw new ArgumentException("Incorrect command/argument count");
            for (int i = 0; i < args.Length; i++)
            {
                if ((spec.Types[i] == "string" && args[i].Type != LuaType.String) || (spec.Types[i] == "number" && args[i].Type != LuaType.Number) || (spec.Types[i] == "bool" && args[i].Type != LuaType.Bool))
                    throw new ArgumentException("Argument type mismatch: " + spec.Arguments[i]);
            }
            Budget();
            string first = args.Length > 0 && args[0].Type == LuaType.String ? args[0].S : "";
            switch (command)
            {
                case "RedirectURL": Redirect?.Invoke(SiteDocument.NormalizeUrl(first)); return LuaValue.Nil;
                case "OpenFile": case "StartDownload":
                    var asset = document.Asset(SiteCommands.RootFile(first));
                    if (asset == null) throw new ArgumentException("File not found in site root");
                    if (command == "OpenFile") OpenFile?.Invoke(asset); else Download?.Invoke(asset);
                    return LuaValue.Nil;
                case "FileExists": return LuaValue.Bool(document.Asset(SiteCommands.RootFile(first)) != null);
                case "Alert": Message?.Invoke(Short(first, 512)); return LuaValue.Nil;
                case "GetTitle": return LuaValue.String(document.title);
                case "GetDescription": return LuaValue.String(document.description);
                case "GetURL": return LuaValue.String(URL);
                case "SetBackground": SiteDocument.ValidateColor(first); document.background = first; BackgroundChanged?.Invoke(first); return LuaValue.Nil;
            }
            var e = Element(first);
            switch (command)
            {
                case "GetText": return LuaValue.String(e.text);
                case "SetText": case "AppendText": case "ClearText":
                    e.text = Short(command == "SetText" ? args[1].S : command == "ClearText" ? "" : e.text + args[1].S, 4096);
                    TextChanged?.Invoke(e.id, e.text); break;
                case "SetTextColor": SiteDocument.ValidateColor(args[1].S); e.textColor = args[1].S; break;
                case "SetFontSize": e.fontSize = (int)Number(args, 1, 6, 256); break;
                case "SetFont":
                    if (args[1].S != "default" && args[1].S != "mono" && args[1].S != "medium") throw new ArgumentException("Unknown font");
                    e.font = args[1].S; break;
                case "SetPosition": case "MoveBy":
                    float x = Number(args, 1, -16384, 16384), y = Number(args, 2, -16384, 16384);
                    if (command == "MoveBy") { x += e.x; y += e.y; }
                    CheckRange(x, -16384, 16384); CheckRange(y, -16384, 16384); e.x = x; e.y = y; break;
                case "SetSize":
                    float w = Number(args, 1, 1, 4096), h = Number(args, 2, 1, 16384); e.width = w; e.height = h; break;
                case "SetPivot":
                    float px = Number(args, 1, 0, 1), py = Number(args, 2, 0, 1); e.pivotX = px; e.pivotY = py; break;
                case "GetX": return LuaValue.Number(e.x);
                case "GetY": return LuaValue.Number(e.y);
                case "GetWidth": return LuaValue.Number(e.width);
                case "GetHeight": return LuaValue.Number(e.height);
                case "SetFillColor": SiteDocument.ValidateColor(args[1].S); e.fill = args[1].S; break;
                case "SetImage":
                    var image = document.Asset(SiteCommands.RootFile(args[1].S));
                    if (image == null || !image.media.StartsWith("image/", StringComparison.Ordinal)) throw new ArgumentException("Image not found in site root");
                    e.image = image.name; break;
                case "ClearImage": e.image = ""; break;
                case "SetVisible": case "Show": case "Hide": case "ToggleVisible":
                    bool show = command == "SetVisible" ? args[1].IsTruthy() : command == "Show" || (command == "ToggleVisible" && !State(visible, e.id));
                    visible[e.id] = show; VisibilityChanged?.Invoke(e.id, show); break;
                case "IsVisible": return LuaValue.Bool(State(visible, e.id));
                case "SetEnabled": enabled[e.id] = args[1].IsTruthy(); EnabledChanged?.Invoke(e.id, enabled[e.id]); break;
                case "IsEnabled": return LuaValue.Bool(State(enabled, e.id));
                default: throw new ArgumentException("Unsupported website command");
            }
            ElementChanged?.Invoke(e.id); return LuaValue.Nil;
        }
        public void Click(string id)
        {
            var element = document.Find(id);
            if (element == null || element.kind != "button" || !State(enabled, id) || !State(visible, id)) return;
            string source = element.onClick ?? "";
            if (source.Length > SiteDocument.MaxScriptLength || new Lexer(source).Run().Count > 256) throw new ArgumentException("Handler too large or too complex (256 tokens maximum)");
            actions = 0;
            if (element.actions != null)
                foreach (var action in element.actions)
                {
                    SiteCommands.Validate(action); var spec = SiteCommands.Find(action.command); var values = new LuaValue[action.arguments.Count];
                    for (int i = 0; i < values.Length; i++) values[i] = spec.Types[i] == "string" ? LuaValue.String(action.arguments[i]) : spec.Types[i] == "bool" ? LuaValue.Bool(action.arguments[i] == "true") : LuaValue.Number(SiteCommands.Number(action.arguments[i]));
                    var result = Execute(action.command, values);
                    if (result.Type != LuaType.Nil) { string text = result.AsString(); Message?.Invoke(text.Length > 512 ? text.Substring(0, 512) : text); }
                }
            if (!string.IsNullOrWhiteSpace(source)) vm.DoString(source);
        }
        void Budget() { if (++actions > 32) throw new ArgumentException("Too many website actions (32 per click)"); }
        static float Number(LuaValue[] args, int i, float min, float max) { float n = (float)args[i].AsNumber(); CheckRange(n, min, max); return n; }
        static void CheckRange(float n, float min, float max) { if (float.IsNaN(n) || float.IsInfinity(n) || n < min || n > max) throw new ArgumentException("Website value outside limits"); }
        static string Short(string value, int limit) { if (value == null || value.Length > limit) throw new ArgumentException("Website text exceeds limit"); return value; }
    }
}
