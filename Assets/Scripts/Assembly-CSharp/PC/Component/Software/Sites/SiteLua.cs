using System;
using PC.Component.Software.Lua;

namespace PC.Component.Software.Sites
{
    /// <summary>Website-only capabilities. Never binds PcosLuaHost, LuaUi or LuaGfx.
    /// Redirects are in-game browser events, downloads only come from the site's
    /// virtual root, and saving a download must be confirmed by the visitor.
    /// </summary>
    public sealed class SiteLua
    {
        readonly SiteDocument document;
        readonly PcosLua vm;
        int actions;
        public Action<string> Redirect;
        public Action<SiteAsset> Download;
        public Action<string> Message;
        public Action<string, string> TextChanged;
        public Action<string, bool> VisibilityChanged;
        public SiteLua(SiteDocument doc)
        {
            document = doc ?? throw new ArgumentNullException("doc");
            vm = new PcosLua { OperationLimit = 5000, StringLimit = 8192, CallDepthLimit = 32 };
            // Untrusted remote pages do not get expensive/unbounded format,
            // pattern, repetition, table-concat or protected-call helpers.
            foreach (string key in new[] { "string", "table", "pcall" }) vm.SetGlobal(key, LuaValue.Nil);
            vm.Printer = s => { ActionBudget(); Message?.Invoke(Short(s, 512)); };
            vm.SetNative("RedirectURL", args =>
            {
                ActionBudget(); Redirect?.Invoke(SiteDocument.NormalizeUrl(Arg(args, 0))); return LuaValue.Nil;
            });
            vm.SetNative("StartDownload", args =>
            {
                ActionBudget(); string name = Arg(args, 0);
                if (name.StartsWith("site/", StringComparison.Ordinal)) name = name.Substring(5);
                if (!SiteDocument.SafeName(name)) throw new ArgumentException("Download must be in the site root");
                var asset = document.Asset(name);
                if (asset == null) throw new ArgumentException("File not found in site root");
                Download?.Invoke(asset); return LuaValue.Nil;
            });
            vm.SetNative("Alert", args => { ActionBudget(); Message?.Invoke(Short(Arg(args, 0), 512)); return LuaValue.Nil; });
            vm.SetNative("SetText", args =>
            {
                ActionBudget(); string id = Arg(args, 0), text = Short(Arg(args, 1), 4096);
                if (document.Find(id) == null) throw new ArgumentException("Unknown element ID");
                document.Find(id).text = text;
                TextChanged?.Invoke(id, text); return LuaValue.Nil;
            });
            vm.SetNative("GetText", args =>
            {
                var element = document.Find(Arg(args, 0));
                return LuaValue.String(element != null ? element.text : "");
            });
            vm.SetNative("SetVisible", args =>
            {
                ActionBudget(); string id = Arg(args, 0);
                if (document.Find(id) == null || args.Length < 2) throw new ArgumentException("Unknown element ID or missing visibility");
                VisibilityChanged?.Invoke(id, args[1].IsTruthy()); return LuaValue.Nil;
            });
        }
        public void Click(string id)
        {
            var element = document.Find(id);
            if (element == null || element.kind != "button" || string.IsNullOrWhiteSpace(element.onClick)) return;
            string source = element.onClick;
            if (source.Length > SiteDocument.MaxScriptLength) throw new ArgumentException("Handler too large");
            // Bound parser/AST recursion before parsing arbitrary remote Lua.
            // A small handler can call a named function retained from an earlier click.
            var tokens = new Lexer(source).Run();
            if (tokens.Count > 256) throw new ArgumentException("Handler is too complex (256 tokens maximum)");
            actions = 0;
            vm.DoString(source);
        }
        void ActionBudget() { if (++actions > 32) throw new ArgumentException("Too many website actions"); }
        static string Arg(LuaValue[] args, int index)
        {
            if (index >= args.Length || args[index].Type != LuaType.String) throw new ArgumentException("String argument expected");
            return args[index].S;
        }
        static string Short(string value, int limit)
        {
            if (value == null || value.Length > limit) throw new ArgumentException("Website text exceeds limit");
            return value;
        }
    }
}
