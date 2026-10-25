using UnityEditor;
using UnityEngine;
using PC.Component;
using PC.Component.Cabling;

// Makes editable trigger + pivot pairs in a prefab asset. The designer moves
// them in Prefab Mode; runtime CablePorts.Install never repositions them.
public static class PrepareCableSockets
{
    [MenuItem("Tools/Cables/Prepare sockets in selected prefab")]
    private static void Prepare()
    {
        var selected = Selection.activeObject as GameObject;
        string path = selected != null ? AssetDatabase.GetAssetPath(selected) : "";
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
        {
            EditorUtility.DisplayDialog("Cable sockets", "Select one device prefab in the Project window first.", "OK");
            return;
        }
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            int added = 0;
            foreach (var gpu in root.GetComponentsInChildren<GPU>(true))
                added += Add(gpu, PortKind.GpuVideo, "GPU video");
            foreach (var supply in root.GetComponentsInChildren<Supply>(true))
                added += Add(supply, PortKind.PcPower, "AC input");
            foreach (var board in root.GetComponentsInChildren<Motherboard>(true))
            {
                added += Add(board, PortKind.BoardVideo, "Board video");
                if (board.Integrated) added += Add(board, PortKind.PcPower, "Mini PC AC input");
            }
            foreach (var display in root.GetComponentsInChildren<PC.Component.Display>(true))
            {
                bool crt = display is Monitor && display.spawnId == "CRT_Monitor";
                added += Add(display, crt ? PortKind.VgaInput : PortKind.HdmiInput,
                    crt ? "VGA input" : "HDMI input");
                added += Add(display, PortKind.MonitorPower, "Display AC input");
            }
            foreach (var strip in root.GetComponentsInChildren<OutletStrip>(true))
            {
                var item = strip.GetComponent<Item>();
                for (int i = 0; item != null && i < strip.sockets; i++)
                    added += Add(item, PortKind.Outlet, "Outlet " + (i + 1));
            }
            if (added > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("Cable sockets: " + added + " editable ports added to " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static int Add(Item owner, PortKind kind, string label)
    {
        if (owner.transform.Find(label) != null) return 0;
        var socket = new GameObject(label);
        socket.layer = 2;
        socket.transform.SetParent(owner.transform, false);
        // An obvious placeholder in Prefab Mode; move to the *actual* model.
        socket.transform.localPosition = new Vector3(0f, 0f, -.2f);
        var trigger = socket.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = kind == PortKind.Outlet ? .28f : .42f;
        var port = socket.AddComponent<CablePort>();
        port.owner = owner;
        port.kind = kind;
        var pivot = new GameObject("Snap Pivot");
        pivot.layer = 2;
        pivot.transform.SetParent(socket.transform, false);
        port.SetPivot(pivot.transform);
        return 1;
    }
}
