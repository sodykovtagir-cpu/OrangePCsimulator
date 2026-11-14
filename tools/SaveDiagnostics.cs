// Read-only local diagnostic front end. Compile with the ACTUAL SaveUtility.cs.
// It deliberately never prints world data, paths, names, screenshots or secrets.
using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using SaveManagement;

class SaveDiagnostics
{
    static readonly StringBuilder report = new StringBuilder();
    static void Line(string s) { report.AppendLine(s); }
    static IDictionary<string, object> Json(string text, string label)
    {
        try
        {
            var parser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 2048 };
            var data = parser.DeserializeObject(text) as IDictionary<string, object>;
            Line(label + ": " + (data == null ? "not a JSON object" : "JSON object OK"));
            return data;
        }
        catch (Exception e) { Line(label + ": parse failed (" + e.GetType().Name + "). Parser differs from Unity/Newtonsoft."); return null; }
    }
    static void Count(IDictionary<string, object> data, string key)
    {
        object value;
        if (data != null && data.TryGetValue(key, out value))
        {
            var items = value as ICollection;
            Line(key + " count: " + (items == null ? "not a collection" : items.Count.ToString()));
        }
        else Line(key + ": missing");
    }
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length != 1) { Console.WriteLine("Drag one .opc/.pc file onto Diagnose-save.cmd. No file is changed or uploaded."); return; }
        try
        {
            string path = args[0];
            Line("Orange PC Sim - read-only save diagnostics v1");
            Line(SaveUtility.InspectFile(path));
            string raw = File.ReadAllText(path), payload;
            Line("Format: " + (raw.StartsWith(SaveUtility.CompressedTag, StringComparison.Ordinal) ? "OPC3" : "XOR legacy/OPC2"));
            var status = SaveUtility.Decode(raw, out payload);
            raw = null;
            Line("Full decode status: " + status);
            if (status != SaveUtility.SaveIntegrity.Tampered)
            {
                Line("Decoded characters: " + payload.Length);
                int split = payload.IndexOf('\n');
                string head = split < 0 ? payload : payload.Substring(0, split);
                Line("Header exceeded former 8MiB preview cap: " + (head.Length > 8 * 1024 * 1024));
                var data = Json(head, "GameData");
                object value;
                if (data != null && data.TryGetValue("version", out value))
                {
                    string v = value as string;
                    Line("Game version: " + (v != null && System.Text.RegularExpressions.Regex.IsMatch(v, @"\A[0-9A-Za-z.+_-]{1,40}\z") ? v : "nonstandard/missing"));
                }
                if (data != null && data.TryGetValue("otherCableMode", out value))
                {
                    var other = value as IDictionary<string, object>;
                    if (other != null && other.TryGetValue("content", out value) && value is string)
                    { Line("Alternate world characters: " + ((string)value).Length); if (((string)value).Length > 0) Json((string)value, "Alternate world"); }
                }
                if (split >= 0 && split < payload.Length - 1)
                {
                    var world = Json(payload.Substring(split + 1), "World");
                    Count(world, "itemData");
                    Line("playerData present: " + (world != null && world.ContainsKey("playerData")));
                }
                else Line("World: empty (new/preset world, or missing world data)");
            }
            Line("This checks envelope and JSON only; it does NOT reconstruct Unity objects, prefabs or Lua state.");
            Line("No input file was changed. No network requests were made.");
        }
        catch (Exception e) { Line("Diagnostic error: " + e.GetType().Name); }
        string output = report.ToString();
        Console.WriteLine(output);
        // Only report goes to stdout. Launcher chooses its own output location,
        // never the save directory. No backup/recovery/rewrite code is called.
    }
}

// Stubs for unrelated Unity path/network helpers in SaveUtility. The diagnostic
// never invokes these helpers; attempting to use networking fails immediately.
namespace UnityEngine
{
    public static class Application { public static string persistentDataPath = "", streamingAssetsPath = ""; }
}
namespace UnityEngine.Networking
{
    public class DownloadHandler { public string text; }
    public class UnityWebRequestAsyncOperation { public event Action<UnityWebRequestAsyncOperation> completed; }
    public class UnityWebRequest
    {
        public enum Result { ConnectionError, ProtocolError, Success }
        public Result result; public string error; public DownloadHandler downloadHandler;
        public static UnityWebRequest Get(string path) { throw new NotSupportedException(); }
        public UnityWebRequestAsyncOperation SendWebRequest() { throw new NotSupportedException(); }
    }
}
