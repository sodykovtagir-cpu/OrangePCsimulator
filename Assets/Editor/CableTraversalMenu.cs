using UnityEditor;
using UnityEngine;

// Menu only: the user previously started F8 by accident.
public static class CableTraversalMenu
{
    [MenuItem("Tools/OrangePC/Start cable traversal test", false, 26)]
    private static void StartTest()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[CableTraversal] Enter Play Mode and load a room first.");
            return;
        }
        CableTraversalMode.RequestFromEditor();
    }
}
