#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using PC.Component;
using UnityEngine;

// Play-mode diagnostic: uses the actual shop spawner, game raycast and PhysX.
// Run in a loaded room; the isolated test objects are deleted afterwards.
public sealed class ServerPhysicsDiagnostic : MonoBehaviour
{
    private static bool running;
    private static readonly Vector3 Origin = new Vector3(2000f, 50f, 2000f);
    private const float MaxRelativeDrift = 0.08f;
    private const float MaxRelativeAngle = 4f;
    private readonly List<GameObject> cleanup = new List<GameObject>();

    public static void RequestRun()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[ServerPhysics] Enter Play Mode and load a room first."); return; }
        if (running) { Debug.LogWarning("[ServerPhysics] Already running."); return; }
        var go = new GameObject("Server physics diagnostic");
        var runner = go.AddComponent<ServerPhysicsDiagnostic>();
        if (runner == null)
        {
            Debug.LogError("[ServerPhysics] Failed to create the runtime diagnostic component.");
            Destroy(go);
            return;
        }
        runner.StartCoroutine(runner.Run());
    }

    private IEnumerator Run()
    {
        running = true;
        var grid = PlacementGrid.Instance;
        bool oldGrid = grid != null && grid.SnapEnabled;
        if (grid != null) { grid.OnDragEnded(); grid.SetEnabled(false); }
        Debug.Log("[ServerPhysics] START: ReadyServer_Entry, full assembly then board removed. " +
                  "This creates objects far from the room and simulates a game raycast.");
        yield return Scenario(false);
        foreach (var obj in cleanup) if (obj != null) Destroy(obj);
        cleanup.Clear();
        yield return null;
        yield return Scenario(true);
        if (grid != null) { grid.OnDragEnded(); grid.SetEnabled(oldGrid); }
        foreach (var obj in cleanup) if (obj != null) Destroy(obj);
        running = false;
        Debug.Log("[ServerPhysics] DONE. Compare full vs without-board and inspect penetration log; " +
                  "FAIL means measured instability or setup failure, not proof of a particular cause.");
        Destroy(gameObject);
    }

    private IEnumerator Scenario(bool removeBoard)
    {
        string label = removeBoard ? "WITHOUT BOARD" : "FULL";
        var prefab = Resources.Load<GameObject>("components/servers/ReadyServer_Entry");
        if (prefab == null) { Debug.LogError("[ServerPhysics] FAIL: missing ReadyServer_Entry"); yield break; }
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cleanup.Add(floor); floor.name = "Server diagnostic floor";
        floor.transform.position = Origin + new Vector3(0, -0.5f, 0);
        floor.transform.localScale = new Vector3(20, 1, 20);
        var spawn = Instantiate(prefab, Origin + new Vector3(0, 0.35f, 0), Quaternion.identity);
        cleanup.Add(spawn);
        // Let ServerChassis.Start and ReadyBuildSpawner build through real slots.
        yield return new WaitForSeconds(3f);
        ServerChassis chassis = null;
        foreach (var c in FindObjectsOfType<ServerChassis>())
            if (Vector3.Distance(c.transform.position, Origin) < 10f) { chassis = c; break; }
        if (chassis == null) { Debug.LogError("[ServerPhysics] FAIL " + label + ": server not spawned"); yield break; }
        cleanup.Add(chassis.gameObject);
        var board = chassis.GetComponentInChildren<Motherboard>();
        if (board == null) { Debug.LogError("[ServerPhysics] FAIL " + label + ": board missing from ready build"); yield break; }
        var boardBody = board.GetComponent<Rigidbody>();
        var chassisBody = chassis.GetComponent<Rigidbody>();
        var bodies = chassis.GetComponentsInChildren<Rigidbody>();
        if (bodies.Length < 8) { Debug.LogError("[ServerPhysics] FAIL " + label + ": incomplete assembly, bodies=" + bodies.Length); yield break; }
        Physics.SyncTransforms();
        LogBoardOverlaps(board, chassis);
        if (removeBoard)
        {
            var connector = board.GetComponent<Connector>();
            if (connector == null || connector.slot == null) { Debug.LogError("[ServerPhysics] FAIL: board not connected"); yield break; }
            connector.Break();
            board.gameObject.SetActive(false);
            cleanup.Add(board.gameObject); Destroy(board.gameObject);
            yield return null;
            yield return new WaitForSeconds(1f);
        }
        var cameraGo = new GameObject("Server diagnostic camera"); cleanup.Add(cameraGo);
        var camera = cameraGo.AddComponent<Camera>();
        camera.enabled = false;
        var raycaster = cameraGo.AddComponent<Raycast>();
        // Do not run Start/Update: they bind the room HUD and input. The test
        // calls ShootRaycast directly but still exercises its real coroutine.
        raycaster.enabled = false;
        typeof(Raycast).GetField("cam", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(raycaster, camera);
        typeof(Raycast).GetField("layer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(raycaster, (LayerMask)((1 << 0) | (1 << 8)));
        // Aim at the right chassis wall, not the lid or a slot trigger.
        Collider wall = null;
        foreach (var c in chassis.GetComponentsInChildren<BoxCollider>())
            if (!c.isTrigger && c.attachedRigidbody == chassisBody && c.transform.name == "Chassis Mesh" &&
                (wall == null || c.bounds.center.x > wall.bounds.center.x)) wall = c;
        if (wall == null) { Debug.LogError("[ServerPhysics] FAIL " + label + ": chassis wall missing"); yield break; }
        var point = wall.bounds.center;
        cameraGo.transform.position = point + Vector3.right * 7f;
        cameraGo.transform.LookAt(point);
        Physics.SyncTransforms();
        var ray = camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, 20f, (1 << 0) | (1 << 8), QueryTriggerInteraction.UseGlobal) || hit.rigidbody != chassisBody)
        {
            Debug.LogError("[ServerPhysics] FAIL " + label + ": ray missed chassis; hit=" + (hit.collider ? hit.collider.name : "none"));
            yield break;
        }
        raycaster.ShootRaycast(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));
        var spring = (SpringJoint)typeof(Raycast).GetField("spring", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(raycaster);
        if (spring != null) cleanup.Add(spring.gameObject);
        if (spring == null || spring.connectedBody != chassisBody)
        { Debug.LogError("[ServerPhysics] FAIL " + label + ": game raycast did not grab chassis"); raycaster.End(); yield break; }
        yield return new WaitForFixedUpdate();
        // Measure each attached body's pose relative to chassis: ordinary
        // translation of the entire held server is not counted as shaking.
        bodies = chassis.GetComponentsInChildren<Rigidbody>();
        var positions = new Dictionary<Rigidbody, Vector3>();
        var rotations = new Dictionary<Rigidbody, Quaternion>();
        foreach (var body in bodies) if (body != chassisBody)
        {
            positions[body] = chassis.transform.InverseTransformPoint(body.position);
            rotations[body] = Quaternion.Inverse(chassisBody.rotation) * body.rotation;
        }
        float drift = 0f, angle = 0f, peakSpeed = 0f, chassisSpeed = 0f, chassisSpin = 0f;
        bool lostBody = false;
        string worst = "none";
        for (int i = 0; i < 150; i++)
        {
            yield return new WaitForFixedUpdate();
            if (!chassisBody) break;
            // Ignore the initial spring transient, then measure absolute chassis
            // motion too: relative poses alone miss a rigid assembly shaking.
            if (i >= 50)
            {
                chassisSpeed = Mathf.Max(chassisSpeed, chassisBody.velocity.magnitude);
                chassisSpin = Mathf.Max(chassisSpin, chassisBody.angularVelocity.magnitude);
            }
            foreach (var entry in positions)
            {
                var body = entry.Key; if (!body) { lostBody = true; continue; }
                float d = Vector3.Distance(entry.Value, chassis.transform.InverseTransformPoint(body.position));
                if (d > drift) { drift = d; worst = body.name; }
                angle = Mathf.Max(angle, Quaternion.Angle(rotations[body], Quaternion.Inverse(chassisBody.rotation) * body.rotation));
                peakSpeed = Mathf.Max(peakSpeed, body.velocity.magnitude);
            }
        }
        raycaster.End();
        bool pass = chassisBody && !lostBody && drift < MaxRelativeDrift && angle < MaxRelativeAngle &&
                    chassisSpeed < 0.3f && chassisSpin < 1f;
        var message = "[ServerPhysics] " + (pass ? "PASS " : "FAIL ") + label +
            " relative drift=" + drift.ToString("F4") + "m angle=" + angle.ToString("F2") +
            "deg max body speed=" + peakSpeed.ToString("F3") +
            " chassis speed=" + chassisSpeed.ToString("F3") + "m/s spin=" + chassisSpin.ToString("F3") + "rad/s" +
            " lostBody=" + lostBody + " worst=" + worst +
            " bodies=" + bodies.Length + " board=" + (boardBody ? boardBody.name : "removed");
        if (pass) Debug.Log(message); else Debug.LogError(message);
        Destroy(chassis.gameObject); Destroy(cameraGo); Destroy(floor);
        yield return null;
    }

    private static void LogBoardOverlaps(Motherboard board, ServerChassis chassis)
    {
        var boardBody = board.GetComponent<Rigidbody>();
        var all = chassis.GetComponentsInChildren<Collider>();
        int overlaps = 0;
        foreach (var a in board.GetComponentsInChildren<Collider>())
        {
            if (!a.enabled || a.isTrigger || a.attachedRigidbody != boardBody) continue;
            foreach (var b in all)
            {
                if (!b.enabled || b.isTrigger || b.attachedRigidbody == null || b.attachedRigidbody == boardBody ||
                    Physics.GetIgnoreLayerCollision(a.gameObject.layer, b.gameObject.layer) || Physics.GetIgnoreCollision(a, b)) continue;
                Vector3 direction; float distance;
                if (Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                                               b, b.transform.position, b.transform.rotation, out direction, out distance))
                {
                    overlaps++;
                    Debug.LogWarning("[ServerPhysics] BOARD OVERLAP " + a.name + " (layer " + a.gameObject.layer + ") / " +
                        b.name + " body=" + b.attachedRigidbody.name + " layer=" + b.gameObject.layer + " depth=" + distance.ToString("F5") +
                        "m (joint collision settings may suppress contact)");
                }
            }
        }
        Debug.Log("[ServerPhysics] Board overlapping collider pairs=" + overlaps + "; ignores triggers and disabled layer pairs.");
    }
}
#endif
