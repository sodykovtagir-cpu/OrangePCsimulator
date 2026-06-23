#if UNITY_EDITOR_WIN
using UnityEngine;
using UnityEditor;
using Microsoft.Win32;
using System.Collections.Generic;

public class PlayerPrefsEditorWin : EditorWindow
{
    private string newKey = "";
    private string newValue = "";
    private enum ValueType { Int, Float, String }
    private ValueType newType = ValueType.String;

    private Vector2 scrollPos;

    [MenuItem("Tools/PlayerPrefs Editor (Windows)")]
    public static void ShowWindow()
    {
        GetWindow<PlayerPrefsEditorWin>("PlayerPrefs Editor");
    }

    private void OnGUI()
    {
        GUILayout.Label("PlayerPrefs Editor (Windows)", EditorStyles.boldLabel);

        // Add / Modify
        GUILayout.Space(10);
        GUILayout.Label("Add / Modify PlayerPref", EditorStyles.label);
        newKey = EditorGUILayout.TextField("Key", newKey);
        newType = (ValueType)EditorGUILayout.EnumPopup("Type", newType);
        newValue = EditorGUILayout.TextField("Value", newValue);

        if (GUILayout.Button("Save"))
        {
            SavePlayerPref(newKey, newValue, newType);
        }

        GUILayout.Space(20);

        // List current PlayerPrefs
        GUILayout.Label("Current PlayerPrefs", EditorStyles.boldLabel);
        scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(300));

        foreach (var kvp in GetAllPlayerPrefs())
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(kvp.Key, GUILayout.Width(150));
            GUILayout.Label(kvp.Value, GUILayout.Width(200));

            if (GUILayout.Button("Edit", GUILayout.Width(50)))
            {
                newKey = kvp.Key;
                newValue = kvp.Value;
                newType = GuessType(kvp.Value);
            }

            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                DeletePlayerPref(kvp.Key);
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();

        GUILayout.Space(10);

        if (GUILayout.Button("Clear All PlayerPrefs"))
        {
            if (EditorUtility.DisplayDialog("Confirm Clear All", "Are you sure you want to delete all PlayerPrefs?", "Yes", "No"))
            {
                ClearAllPlayerPrefs();
            }
        }
    }

    private void SavePlayerPref(string key, string value, ValueType type)
    {
        if (string.IsNullOrEmpty(key)) return;

        switch (type)
        {
            case ValueType.Int:
                if (int.TryParse(value, out int intVal))
                    PlayerPrefs.SetInt(key, intVal);
                else
                    Debug.LogWarning("Invalid int value.");
                break;
            case ValueType.Float:
                if (float.TryParse(value, out float floatVal))
                    PlayerPrefs.SetFloat(key, floatVal);
                else
                    Debug.LogWarning("Invalid float value.");
                break;
            case ValueType.String:
                PlayerPrefs.SetString(key, value);
                break;
        }

        PlayerPrefs.Save();
    }

    private ValueType GuessType(string value)
    {
        if (int.TryParse(value, out _)) return ValueType.Int;
        if (float.TryParse(value, out _)) return ValueType.Float;
        return ValueType.String;
    }

    private void DeletePlayerPref(string key)
    {
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }

    private void ClearAllPlayerPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }

    private Dictionary<string, string> GetAllPlayerPrefs()
    {
        var result = new Dictionary<string, string>();
        string company = Application.companyName;
        string product = Application.productName;

        string path = $@"Software\{company}\{product}";
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path, false))
        {
            if (key != null)
            {
                foreach (var name in key.GetValueNames())
                {
                    var val = key.GetValue(name);
                    result[name] = val != null ? val.ToString() : "";
                }
            }
        }

        return result;
    }
}
#endif
