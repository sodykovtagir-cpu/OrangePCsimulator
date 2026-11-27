#if UNITY_EDITOR
using UnityEditor;

public static class ServerPhysicsDiagnosticMenu
{
    [MenuItem("Tools/OrangePC/Server physics diagnostic")]
    private static void RunFromMenu()
    {
        ServerPhysicsDiagnostic.RequestRun();
    }
}
#endif
