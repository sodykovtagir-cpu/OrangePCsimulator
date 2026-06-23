using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

[InitializeOnLoad]
public static class PreventPlayWithMissingScripts
{
    static PreventPlayWithMissingScripts()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            if (HasMissingScriptsInProject())
            {
                Debug.LogError("Cannot enter Play mode: Missing scripts detected in scenes or prefabs.");
                EditorApplication.isPlaying = false;
            }
        }
    }

    private static bool HasMissingScriptsInProject()
    {
        bool foundMissing = false;

        // Check all open scenes
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            Scene scene = EditorSceneManager.GetSceneAt(i);
            foreach (var go in scene.GetRootGameObjects())
            {
                if (CheckMissingComponentsRecursive(go, $"Scene: {scene.path}"))
                    foundMissing = true;
            }
        }

        // Check all prefabs in the project
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && CheckMissingComponentsRecursive(prefab, $"Prefab: {path}"))
                foundMissing = true;
        }

        return foundMissing;
    }

    private static bool CheckMissingComponentsRecursive(GameObject go, string parentPath)
    {
        bool missingFound = false;

        var components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                Debug.LogError($"Missing script on GameObject '{go.name}' in {parentPath}");
                missingFound = true;
            }
        }

        foreach (Transform child in go.transform)
        {
            string childPath = $"{parentPath}/{child.name}";
            if (CheckMissingComponentsRecursive(child.gameObject, childPath))
                missingFound = true;
        }

        return missingFound;
    }
}
