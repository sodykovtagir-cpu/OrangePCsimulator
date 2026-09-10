using System;
using System.Collections.Generic;
using UnityEngine;

public static class Localization
{
    private static Dictionary<string, string[]> content;
    private static Dictionary<string, int> languageIndices;
    private static string language;

    public static event Action LanguageChanged;

    public static string GetText(string key)
    {
        return GetText(key, language);
    }

    public static string GetText(string key, string lang)
    {
        if (string.IsNullOrEmpty(key)) return key;
        if (content == null) CreateContent();
        if (!content.TryGetValue(key, out var values)) return key;

        // Cache the table/header instead of loading and splitting the entire TSV
        // for every label. New UI and all 42 language columns use the same lookup.
        string selected = string.IsNullOrEmpty(lang) ? "EN" : lang;
        if (languageIndices.TryGetValue(selected, out int index) && index < values.Length &&
            !string.IsNullOrEmpty(values[index])) return values[index];
        if (languageIndices.TryGetValue("EN", out int fallback) && fallback < values.Length &&
            !string.IsNullOrEmpty(values[fallback])) return values[fallback];
        return key;
    }

    public static string[] GetRow(string key)
    {
        if (content == null) CreateContent();
        if (key != null && content.TryGetValue(key, out var values)) return (string[])values.Clone();
        return new string[0];
    }

    public static string GetLanguage()
    {
        return language;
    }

    public static void SetLanguage(string language)
    {
        Localization.language = language;
        LanguageChanged?.Invoke();
    }

    public static string[] GetAllLanguages()
    {
        return GetRow("*Short Form");
    }

    public static void CreateContent()
    {
        content = new Dictionary<string, string[]>(StringComparer.Ordinal);
        languageIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var text = GetLocalFile();
        if (string.IsNullOrEmpty(text)) return;
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;
            var columns = line.Split('\t');
            string key = columns[0].TrimStart('\uFEFF');
            if (columns.Length < 2 || string.IsNullOrEmpty(key)) continue;
            var values = new string[columns.Length - 1];
            Array.Copy(columns, 1, values, 0, values.Length);
            content[key] = values;
        }
        if (content.TryGetValue("*Short Form", out var languages))
            for (int i = 0; i < languages.Length; i++)
                if (!string.IsNullOrEmpty(languages[i])) languageIndices[languages[i]] = i;
    }

    public static string GetLocalFile()
    {
        var textAsset = Resources.Load<TextAsset>("Translate");
        return textAsset != null ? textAsset.text : null;
    }
}
