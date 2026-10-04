using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// OrangePC / 3DMork prefab maintenance tool
// Menu: Tools -> OrangePC -> 3DMork -> Fix Leaderboard (6 rows) + Speed up (2s)
// Also available via right-click on prefab.
// This tool does the same as tools/fix_3dmork_prefab.py but inside Unity.

public static class ThreeDMorkFixTool
{
    const int TargetRows = 6;
    const float TargetDuration = 2.0f; // 2x faster than original 4s

    static readonly string[] ExtraRowNames = new string[]
    {
        "LbRow_6", "LbRank_6", "LbName_6", "LbCpu_6", "LbGpu_6", "LbScore_6", "LbFps_6",
        "LbRow_7", "LbRank_7", "LbName_7", "LbCpu_7", "LbGpu_7", "LbScore_7", "LbFps_7",
    };

    [MenuItem("Tools/OrangePC/3DMork/Fix Prefab - 6 rows + 2x speed", false, 10)]
    public static void FixAll()
    {
        FixPrefab();
        FixStagePrefab();
        FixRoomScene();
        Debug.Log("[3DMorkFix] Done. Prefab now 6 rows, durations ~2s (2x faster). Save scene/prefab if needed.");
    }

    [MenuItem("Tools/OrangePC/3DMork/Fix Prefab Only (rows)", false, 11)]
    public static void FixPrefab()
    {
        string prefabPath = "Assets/Resources/apps/3DMork.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError("[3DMorkFix] Prefab not found: " + prefabPath);
            return;
        }

        // Find ThreeDMork component on root
        var mork = prefab.GetComponent<PC.Component.Software.ThreeDMork>();
        if (mork != null)
        {
            // Use SerializedObject to trim arrays
            var so = new SerializedObject(mork);
            string[] props = { "leaderboardRank", "leaderboardName", "leaderboardCpu", "leaderboardGpu", "leaderboardScore", "leaderboardFps" };
            int totalTrimmed = 0;
            foreach (var pName in props)
            {
                var prop = so.FindProperty(pName);
                if (prop != null && prop.isArray && prop.arraySize > TargetRows)
                {
                    Debug.Log($"[3DMorkFix] Trim {pName}: {prop.arraySize} -> {TargetRows}");
                    prop.arraySize = TargetRows;
                    totalTrimmed++;
                }
            }
            if (totalTrimmed > 0)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(mork);
            }
        }

        // Hide extra row GameObjects (14 objects)
        int hidden = 0;
        var allTransforms = prefab.GetComponentsInChildren<Transform>(true);
        var byName = new Dictionary<string, Transform>();
        foreach (var t in allTransforms) byName[t.name] = t;

        foreach (var name in ExtraRowNames)
        {
            if (byName.TryGetValue(name, out var tr))
            {
                if (tr.gameObject.activeSelf)
                {
                    tr.gameObject.SetActive(false);
                    hidden++;
                    Debug.Log($"[3DMorkFix] Deactivated {name}");
                    EditorUtility.SetDirty(tr.gameObject);
                }
            }
        }

        // Also try to find FlybyWaypoint components inside prefab (if any) and speed them up
        var waypoints = prefab.GetComponentsInChildren<PC.Component.Software.FlybyWaypoint>(true);
        int wpFixed = 0;
        foreach (var wp in waypoints)
        {
            if (Mathf.Abs(wp.duration - TargetDuration) > 0.01f)
            {
                var so = new SerializedObject(wp);
                var prop = so.FindProperty("duration");
                if (prop != null)
                {
                    float old = prop.floatValue;
                    // Scale proportionally: 4 -> 2, 4.2 ->2.1 etc. Simplified: set to TargetDuration
                    prop.floatValue = TargetDuration;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(wp);
                    wpFixed++;
                    Debug.Log($"[3DMorkFix] Waypoint {wp.name}: {old}s -> {prop.floatValue}s");
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[3DMorkFix] Prefab done: hidden {hidden} rows, waypoints {wpFixed}");
    }

    [MenuItem("Tools/OrangePC/3DMork/Fix Stage Prefab Speed", false, 12)]
    public static void FixStagePrefab()
    {
        string path = "Assets/Resources/3DMork_Stage.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogWarning("[3DMorkFix] Stage prefab not found: " + path); return; }
        int fixedCount = 0;
        var waypoints = prefab.GetComponentsInChildren<PC.Component.Software.FlybyWaypoint>(true);
        foreach (var wp in waypoints)
        {
            float old = wp.duration;
            float target = TargetFor(old);
            if (Mathf.Abs(old - target) > 0.01f)
            {
                var so = new SerializedObject(wp);
                so.FindProperty("duration").floatValue = target;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(wp);
                fixedCount++;
            }
        }
        if (fixedCount > 0) AssetDatabase.SaveAssets();
        Debug.Log($"[3DMorkFix] Stage prefab: fixed {fixedCount} waypoints");
    }

    [MenuItem("Tools/OrangePC/3DMork/Fix Room Scene Speed (open 3DMork_Room)", false, 13)]
    public static void FixRoomScene()
    {
        // Works on currently open scene, or loads 3DMork_Room if needed
        var waypoints = Object.FindObjectsOfType<PC.Component.Software.FlybyWaypoint>(true);
        int fixedCount = 0;
        foreach (var wp in waypoints)
        {
            float old = wp.duration;
            float target = TargetFor(old);
            if (Mathf.Abs(old - target) > 0.01f)
            {
                Undo.RecordObject(wp, "Fix 3DMork waypoint duration");
                wp.duration = target;
                EditorUtility.SetDirty(wp);
                fixedCount++;
                Debug.Log($"[3DMorkFix] Scene waypoint {wp.name}: {old}s -> {target}s");
            }
        }
        if (fixedCount > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
        Debug.Log($"[3DMorkFix] Scene: fixed {fixedCount} waypoints (target {TargetDuration}s)");
    }

    static float TargetFor(float old)
    {
        if (Mathf.Abs(old - 4.0f) < 0.01f) return TargetDuration;
        if (Mathf.Abs(old - 4.2f) < 0.01f) return Mathf.Round(TargetDuration * 1.05f * 100f) / 100f;
        if (Mathf.Abs(old - 4.5f) < 0.01f) return Mathf.Round(TargetDuration * 1.125f * 100f) / 100f;
        if (Mathf.Abs(old - 3.8f) < 0.01f) return Mathf.Round(TargetDuration * 0.95f * 100f) / 100f;
        if (old > TargetDuration + 0.1f) return Mathf.Round(old * (TargetDuration / 4f) * 100f) / 100f;
        return old;
    }

    // Add a global speed multiplier hint in inspector (optional)
    [MenuItem("Tools/OrangePC/3DMork/Help - How to make even faster", false, 30)]
    static void Help()
    {
        EditorUtility.DisplayDialog("3DMork Speed",
            "Бенчмарк теперь 2x быстрее (2с на точку вместо 4с).\n\n" +
            "• Префаб 3DMork.prefab: 6 строк в таблице (7/8 скрыты), массивы урезаны до 6\n" +
            "• Waypoints (FlybyWaypoint) в 3DMork_Stage.prefab и сцене 3DMork_Room: 4с → 2с (4.2→2.1, 4.5→2.25)\n" +
            "• Код ThreeDMork.cs / FlybyWaypoint.cs: дефолт 4f → 2f, 3 авто-сегмента 4f→2f\n\n" +
            "Чтобы сделать ещё быстрее — открой Tools/OrangePC/3DMork/Fix* и поменяй TargetDuration на 1.5f или 1.0f, " +
            "или вручную в инспекторе у каждой точки поставь 1.5.\n" +
            "Инструмент tools/fix_3dmork_prefab.py делает то же самое из командной строки: python3 tools/fix_3dmork_prefab.py --target 1.5",
            "OK");
    }
}
