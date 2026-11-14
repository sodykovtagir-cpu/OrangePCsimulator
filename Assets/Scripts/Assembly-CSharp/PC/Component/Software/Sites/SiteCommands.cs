using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PC.Component.Software.Sites
{
    [Serializable]
    public sealed class SiteAction
    {
        public string command = "Alert";
        public List<string> arguments = new List<string>();
    }
    public sealed class SiteCommand
    {
        public readonly string Name;
        public readonly string[] Arguments, Types;
        public SiteCommand(string name, params string[] parameters)
        {
            Name = name; Arguments = new string[parameters.Length]; Types = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++) { var pair = parameters[i].Split(':'); Arguments[i] = pair[0]; Types[i] = pair[1]; }
        }
        public string Hint => Arguments.Length == 0 ? "No arguments" : string.Join("\n", Arguments);
    }
    /// <summary>Single allowlist shared by the action editor, validation and Lua host.
    /// Arguments are data, never concatenated as executable user expressions.</summary>
    public static class SiteCommands
    {
        public static readonly SiteCommand[] All = {
            new SiteCommand("RedirectURL", "url:string"), new SiteCommand("OpenFile", "file:string"),
            new SiteCommand("StartDownload", "file:string"), new SiteCommand("Alert", "text:string"),
            new SiteCommand("SetText", "id:string", "text:string"), new SiteCommand("AppendText", "id:string", "text:string"),
            new SiteCommand("ClearText", "id:string"), new SiteCommand("GetText", "id:string"),
            new SiteCommand("SetTextColor", "id:string", "color:string"), new SiteCommand("SetFontSize", "id:string", "size:number"),
            new SiteCommand("SetFont", "id:string", "font:string"),
            new SiteCommand("SetPosition", "id:string", "x:number", "y:number"), new SiteCommand("MoveBy", "id:string", "dx:number", "dy:number"),
            new SiteCommand("SetSize", "id:string", "width:number", "height:number"), new SiteCommand("SetPivot", "id:string", "x:number", "y:number"),
            new SiteCommand("GetX", "id:string"), new SiteCommand("GetY", "id:string"), new SiteCommand("GetWidth", "id:string"), new SiteCommand("GetHeight", "id:string"),
            new SiteCommand("SetFillColor", "id:string", "color:string"), new SiteCommand("SetImage", "id:string", "file:string"),
            new SiteCommand("ClearImage", "id:string"), new SiteCommand("SetBackground", "color:string"),
            new SiteCommand("Show", "id:string"), new SiteCommand("Hide", "id:string"), new SiteCommand("ToggleVisible", "id:string"),
            new SiteCommand("SetVisible", "id:string", "visible:bool"), new SiteCommand("IsVisible", "id:string"),
            new SiteCommand("SetEnabled", "id:string", "enabled:bool"), new SiteCommand("IsEnabled", "id:string"),
            new SiteCommand("GoToPage", "page:string"),
            new SiteCommand("GetTitle"), new SiteCommand("GetDescription"), new SiteCommand("GetURL"), new SiteCommand("FileExists", "file:string")
        };
        public static SiteCommand Find(string name) { return Array.Find(All, c => c.Name == name); }
        public static string RootFile(string name)
        {
            if (name != null && name.StartsWith("site/", StringComparison.Ordinal)) name = name.Substring(5);
            if (!SiteDocument.SafeName(name)) throw new ArgumentException("File must be in the site root");
            return name;
        }
        public static void Validate(SiteAction action)
        {
            var command = action == null ? null : Find(action.command);
            if (command == null || action.arguments == null || action.arguments.Count != command.Arguments.Length)
                throw new ArgumentException("Unknown command or incorrect argument count");
            if(action.command=="GoToPage"&&!SitePages.SafeRoute(action.arguments[0]))throw new ArgumentException("Choose a page path such as /about (same site)");
            for (int i = 0; i < action.arguments.Count; i++)
            {
                string value = action.arguments[i];
                if (value == null || value.Length > 4096) throw new ArgumentException("Argument too long or null");
                if (command.Types[i] == "number") Number(value);
                if (command.Types[i] == "bool" && value != "true" && value != "false") throw new ArgumentException("Use true or false");
            }
        }
        public static double Number(string text)
        {
            double n;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out n) || double.IsNaN(n) || double.IsInfinity(n))
                throw new ArgumentException("Finite number expected (use a decimal point)");
            return n;
        }
        public static string Compile(SiteAction action)
        {
            Validate(action); var command = Find(action.command); var code = new StringBuilder(action.command).Append('(');
            for (int i = 0; i < action.arguments.Count; i++)
            {
                if (i > 0) code.Append(", ");
                code.Append(command.Types[i] == "string" ? Quote(action.arguments[i]) : command.Types[i] == "number" ? Number(action.arguments[i]).ToString("R", CultureInfo.InvariantCulture) : action.arguments[i]);
            }
            return code.Append(')').ToString();
        }
        public static string Quote(string text)
        {
            return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
        }
        public static List<string> ReadArgumentLines(string value, int count)
        {
            if (count == 0) { if (!string.IsNullOrWhiteSpace(value)) throw new ArgumentException("This command has no arguments"); return new List<string>(); }
            return new List<string>((value ?? "").Replace("\r\n", "\n").Split('\n'));
        }
    }
}
