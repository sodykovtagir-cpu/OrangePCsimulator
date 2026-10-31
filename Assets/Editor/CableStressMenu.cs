using UnityEditor;
using UnityEngine;

// A visible, focus-independent route to the same Editor-only Play Mode test.
// Unity's Game view does not deliver Input.GetKeyDown while Scene/Console has
// keyboard focus; the menu also explains why the test cannot start yet.
public static class CableStressMenu
{
    [MenuItem("Tools/OrangePC/Start cable stress test _F8", false, 25)]
    private static void StartTest()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[CableStress] Enter Play Mode, load a save into a room, " +
                "then use Tools > OrangePC > Start cable stress test.");
            return;
        }
        CableStressMode.RequestFromEditor();
    }
}
