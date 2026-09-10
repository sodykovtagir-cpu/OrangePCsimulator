using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace UnityEngine
{
    public class DisallowMultipleComponent : Attribute { }
    public class RequireComponent : Attribute { public RequireComponent(Type type) { } }
    public class MonoBehaviour
    {
        public UI.Text BoundText = new UI.Text();
        public T GetComponent<T>() where T : class { return BoundText as T; }
    }
    public class TextAsset { public string text; public TextAsset(string value) { text = value; } }
    public static class Resources
    {
        public static TextAsset Translation;
        public static int Loads;
        public static T Load<T>(string name) where T : class
        {
            Loads++;
            if (name != "Translate") throw new Exception("Unexpected resource request");
            return Translation as T;
        }
    }
}
namespace UnityEngine.UI { public class Text { public string text; } }
namespace UnityEngine.SocialPlatforms { }

public static class LocalizationTests
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
    private static void Invoke(object target, string method)
    {
        typeof(LocalizationText).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
    public static int Main(string[] args)
    {
        try
        {
            var text = File.ReadAllText(args[0]);
            UnityEngine.Resources.Translation = new UnityEngine.TextAsset(text);
            Localization.CreateContent();
            var languages = Localization.GetAllLanguages();
            Check(languages.Length == 42, "language count");
            int lookups = 0, formats = 0;
            foreach (string line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                if (string.IsNullOrEmpty(line)) continue;
                var columns = line.Split('\t');
                if (columns[0].StartsWith("*")) continue;
                Check(columns.Length == 43, "bad TSV width: " + columns[0]);
                for (int i = 0; i < languages.Length; i++)
                {
                    string translated = Localization.GetText(columns[0], languages[i]);
                    Check(translated == columns[i + 1], "wrong language column: " + columns[0] + " / " + languages[i]);
                    if (columns[0].Contains("{0}"))
                    {
                        string formatted = string.Format(translated, "VALUE0", "VALUE1", "VALUE2", "VALUE3");
                        Check(formatted.Contains("VALUE0"), "lost format argument");
                        formats++;
                    }
                    lookups++;
                }
            }
            Check(UnityEngine.Resources.Loads == 1, "lookup reloaded the whole table");
            Check(Localization.GetText("Refresh", "ru") == "Обновить", "case-insensitive language lookup");
            Check(Localization.GetText("Wallpaper Fill", "unknown-language") == "Fill", "English fallback");
            Check(Localization.GetText("user-created-file.lua", "RU") == "user-created-file.lua", "unknown names must stay unchanged");
            Check(Localization.GetText(null, "RU") == null, "null key");
            Check(Localization.GetText("", "RU") == "", "empty key");
            languages[0] = "modified by caller";
            Check(Localization.GetAllLanguages()[0] == "EN", "GetRow exposed mutable cache");

            Localization.SetLanguage("RU");
            var label = new LocalizationText();
            label.BoundText.text = "Select Icon";
            Invoke(label, "Awake");
            Check(label.BoundText.text == "Выбор значка", "authored label not translated on Awake");
            Localization.SetLanguage("DE");
            Check(label.BoundText.text == Localization.GetText("Select Icon", "DE"), "live language change");
            label.Bind("Refresh");
            Localization.SetLanguage("RU");
            Check(label.BoundText.text == "Обновить", "Bind restored an old key");
            Invoke(label, "OnDestroy");
            Localization.SetLanguage("EN");
            Check(label.BoundText.text == "Обновить", "destroyed label still subscribed");
            var bracket = new LocalizationTextBracket();
            bracket.BoundText.text = "[Save]";
            Invoke(bracket, "Awake");
            Localization.SetLanguage("RU");
            Check(bracket.BoundText.text == "[" + Localization.GetText("Save", "RU") + "]", "bracket label");
            Invoke(bracket, "OnDestroy");

            UnityEngine.Resources.Translation = new UnityEngine.TextAsset("\uFEFF*Short Form\tEN\tRU\nKey\tEnglish fallback\t\n");
            Localization.CreateContent();
            Check(Localization.GetText("Key", "RU") == "English fallback", "empty-cell fallback");
            Check(Localization.GetAllLanguages().Length == 2, "BOM/header parsing");
            UnityEngine.Resources.Translation = null;
            Localization.CreateContent();
            Check(Localization.GetText("Safe", "RU") == "Safe", "missing resource");
            Console.WriteLine("PASS: " + lookups + " actual localized lookups, " + formats + " formatted strings, live bindings, fallbacks and cached resource loading.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
