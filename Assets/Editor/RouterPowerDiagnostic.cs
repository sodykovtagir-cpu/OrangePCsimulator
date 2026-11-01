#if UNITY_EDITOR
using PC.Component.Cabling;
using PC.Component.Networking;
using UnityEditor;
using UnityEngine;

// Explicit one-shot Play Mode probe: no polling, no runtime cost in builds.
public static class RouterPowerDiagnostic
{
    [MenuItem("Tools/Typo-Link/Diagnose router power")]
    private static void Diagnose()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Typo-Link power: start Play Mode, load the room, plug in both ends, then run this menu item.");
            return;
        }
        foreach (var router in Object.FindObjectsOfType<RouterDevice>())
        {
            var item = router.GetComponent<Item>();
            Debug.Log($"Typo-Link power: router={router.name} id={item?.Id} " +
                $"wiredMode={CableMode.Enabled} grid={ElectricityGrid.PowerAvailable} " +
                $"powered={router.IsPowered} mains={router.HasMainsCable}", router);
            int found = 0;
            foreach (var port in router.GetComponentsInChildren<CablePort>(true))
            {
                if (port.kind != PortKind.RouterPower) continue;
                found++;
                var plug = port.plug;
                var cable = plug != null ? plug.Cable : null;
                var source = cable != null ? cable.End(1 - plug.End) : null;
                var outlet = source != null ? source.Connected : null;
                Debug.Log($"Typo-Link power: port={port.name} ownerCorrect={port.owner == item} " +
                    $"plug={plug != null} plugBackLink={plug != null && plug.Connected == port} " +
                    $"cable={cable?.spawnId} sourcePlug={source != null} " +
                    $"sourceConnected={outlet != null} outletKind={outlet?.kind} " +
                    $"outletPowered={outlet != null && outlet.Powered} " +
                    $"energized={CablePower.Energized(plug)}", port);
            }
            if (found == 0) Debug.LogError("Typo-Link power: no RouterPower CablePort exists under this router", router);
            var led = router.transform.Find("Router/Indicators/Power");
            if (led == null)
                foreach (var child in router.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Indicators" && child.GetComponentInParent<Item>() == item)
                    { led = child.Find("Power"); break; }
            Debug.Log($"Typo-Link power: indicatorFound={led != null} " +
                $"indicatorRenderer={led != null && led.GetComponent<Renderer>() != null}", router);
        }
    }
}
#endif
