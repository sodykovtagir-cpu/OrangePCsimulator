#if UNITY_EDITOR
// Editor-only, bounded integration test. Nothing is polled or allocated in a player build.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using PC;
using PC.Component;
using PC.Component.Cabling;
using SaveManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CableStressMode : MonoBehaviour
{
    private const int Lanes = 8;
    private const int Reloads = 5;
    private const float Pitch = 4f;
    private static readonly Vector3 Origin = new Vector3(1000f, 15f, 1000f);
    private readonly List<Expectation> expected = new List<Expectation>();
    private readonly Dictionary<int, int> slotOwners = new Dictionary<int, int>();
    private readonly List<int> healthIds = new List<int>();
    private readonly Dictionary<int, int> monitorPowerOwners = new Dictionary<int, int>();
    private readonly List<int> boardIds = new List<int>();
    public static bool Recording { get; private set; }
    private bool busy;
    private bool geometryOnLoad;
    private bool failed;
    private bool completed;
    private int cycle;
    private int roomScene;
    private string logPath;
    private string savePath;

    private sealed class Expectation
    {
        public int cableId, end, ownerId, lane;
        public string cableName, portName;
        public PortKind kind;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindObjectOfType<CableStressMode>() == null)
        {
            var obj = new GameObject("Cable stress test (Editor only, F8)");
            DontDestroyOnLoad(obj);
            obj.AddComponent<CableStressMode>();
            Debug.Log("[CableStress] Test ready. Load a save, focus Game and press F8, " +
                "or use Tools > OrangePC > Start cable stress test.");
        }
    }

    // The Editor menu calls this even when Game has no keyboard focus. It also
    // repairs a missed bootstrap instead of leaving a silent/nonfunctional F8.
    public static void RequestFromEditor()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[CableStress] Enter Play Mode and load a save first.");
            return;
        }
        var runner = FindObjectOfType<CableStressMode>();
        if (runner == null)
        {
            Install();
            runner = FindObjectOfType<CableStressMode>();
        }
        if (runner != null) runner.RequestStart("Tools menu");
        else Debug.LogError("[CableStress] Failed to create the stress-test runner.");
    }

    private void OnEnable() { SceneManager.sceneLoaded += SceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= SceneLoaded; }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8)) RequestStart("Game view F8");
    }

    private void RequestStart(string source)
    {
        if (busy)
        {
            Debug.Log("[CableStress] Already running; see the Console for progress.");
            return;
        }
        if (SaveManager.Instance == null || SaveManager.Loader == null ||
            Player.Instance == null || Main.Instance == null || SaveManager.RestoringSave)
        {
            Debug.LogWarning("[CableStress] " + source + ": load a save into a room first " +
                "and wait for loading to finish (not the main menu).");
            return;
        }
        if (!CableMode.Enabled)
        {
            Debug.LogWarning("[CableStress] " + source + ": enable 'Physical cables' " +
                "in the main menu before loading the room.");
            return;
        }
        Debug.Log("[CableStress] " + source + ": starting isolated test room...");
        StartCoroutine(Run());
    }

    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // sceneLoaded precedes SaveManager.Start/Hook.FromData: the test wall
        // must already exist when the saved outlets re-check their mount.
        if (geometryOnLoad && scene.buildIndex == roomScene) CreateGeometry();
    }

    private void Write(string text)
    {
        string line = DateTime.Now.ToString("HH:mm:ss.fff") + " cycle=" + cycle + " " + text;
        if (!string.IsNullOrEmpty(logPath))
        {
            try { File.AppendAllText(logPath, line + Environment.NewLine); }
            catch (Exception ex) { Debug.LogError("[CableStress] Log write failed: " + ex); }
        }
        Debug.Log("[CableStress] " + line);
    }

    private void UnityLog(string message, string stack, LogType type)
    {
        if (message.StartsWith("[CableStress]", StringComparison.Ordinal)) return;
        if (type == LogType.Error || type == LogType.Exception ||
            message.Contains("Cable restore:") || message.Contains("Cable stress:") ||
            message.Contains("Save: could not reconnect") || message.Contains("сборка неполная"))
        {
            try { File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff") +
                " cycle=" + cycle + " UNITY " + type + " " + message + "\n" + stack + "\n"); }
            catch { /* the main test reports any file errors via Write */ }
        }
    }

    private IEnumerator Run()
    {
        busy = true;
        Recording = true;
        failed = false;
        completed = false;
        cycle = 0;
        roomScene = SceneManager.GetActiveScene().buildIndex;
        string suffix = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        string folder = Path.Combine(Application.persistentDataPath, "CableStress");
        Directory.CreateDirectory(folder);
        savePath = Path.Combine(folder, "CableStress-" + suffix + ".opc");
        logPath = Path.Combine(folder, "CableStress-" + suffix + ".log");
        Application.logMessageReceived += UnityLog;
        Write("START F8: 8 PCs (GTX1060 + PSU500W), 8 monitors, 8 three-socket wall outlets, 24 cables, 5 reloads. Save=" + savePath);
        try
        {
            // Clone metadata; never pass the original GameData reference to a
            // writable DataLoader, and NEVER save back into the user's .opc.
            var original = SaveManager.Loader;
            var data = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(original.GameData));
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            data.roomName = "Cable stress (temporary)";
            data.gravity = true;
            data.cableModeVersion = 1;
            data.cablesEnabled = true;
            data.otherCableMode = null;
            var seed = new ContentData { playerData = Player.Instance.SavePlayer(), itemData = new ItemData[0] };
            SaveManager.Loader = new DataLoader(savePath, data) { Content = JsonConvert.SerializeObject(seed) };
            geometryOnLoad = true;
            // Enter a fresh copy of the room so its existing live items cannot
            // accidentally be included in our test file. The original file is untouched.
            yield return SceneManager.LoadSceneAsync(roomScene);
            yield return null;
            if (!Ready()) { Fail("Room failed to initialize"); yield break; }
            foreach (var item in FindObjectsOfType<Item>())
                if (item != null) Destroy(item.gameObject);
            yield return null;
            Player.Instance.LoadPlayer(new PlayerData { x = Origin.x - 4f, y = Origin.y + 1.5f,
                z = Origin.z - 4f, ry = 30f, rx = 0f });

            var buildPrefab = Resources.Load<GameObject>("components/ready/ReadyPC_Home_Black");
            var monitorPrefab = Resources.Load<GameObject>("components/Monitor(Black)");
            var outletPrefab = Resources.Load<GameObject>("components/Wall Outlet 3");
            // Both routes need more than 5 m at the authored PSU/monitor pivots.
            // Historical asset filename: Power Cable 5m is the 10 m SKU.
            var powerPrefab = Resources.Load<GameObject>("components/Power Cable 5m");
            var hdmiPrefab = Resources.Load<GameObject>("components/HDMI Cable (6m)");
            if (buildPrefab == null || monitorPrefab == null || outletPrefab == null ||
                powerPrefab == null || hdmiPrefab == null)
            { Fail("One of the test Resources prefabs is missing"); yield break; }

            var builds = new GameObject[Lanes];
            var monitors = new PC.Component.Display[Lanes];
            var outlets = new OutletStrip[Lanes];
            for (int lane = 0; lane < Lanes; lane++)
            {
                Vector3 basePos = Origin + Vector3.right * (Pitch * lane);
                // Case_ATX has a bottom at local Y=-2 and extends 2.25 m
                // towards the rear; keep both hardware and monitor above the
                // floor, apart from the wall and from each other.
                builds[lane] = Instantiate(buildPrefab, basePos + new Vector3(0f, 2.1f, 0f), Quaternion.identity);
                // Turn the monitor's *rear* AC/HDMI ports toward the case;
                // otherwise both cables have to pass through its solid screen.
                monitors[lane] = Instantiate(monitorPrefab, basePos + new Vector3(0f, 1.7f, -4.4f),
                    Quaternion.Euler(0f, 180f, 0f)).GetComponent<PC.Component.Display>();
                // Put the wall beside the *front* of the case, not behind it:
                // cables no longer run straight through the case's solid rear
                // panel on their way to the PSU and monitor. The authored -Y
                // side of the outlet still faces the wall (here: +X).
                outlets[lane] = Instantiate(outletPrefab, basePos + new Vector3(1.2f, 1.2f, -2.7f),
                    Quaternion.FromToRotation(Vector3.down, Vector3.right)).GetComponent<OutletStrip>();
                if (monitors[lane] == null || outlets[lane] == null)
                { Fail("Prefab has no Display/OutletStrip at lane " + lane); yield break; }
                outlets[lane].GetComponent<Hook>().Hooked = true;
            }
            // ReadyBuildSpawner uses normal Slot.TryAttach asynchronously.
            float deadline = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < deadline)
            {
                bool complete = true;
                foreach (var build in builds) if (build != null) complete = false;
                if (complete) break;
                yield return null;
            }
            foreach (var build in builds) if (build != null)
            { Fail("ReadyBuildSpawner did not finish within 90 seconds"); yield break; }
            // The case/monitor are initially above the platform. Let their
            // joints and contacts settle WITHOUT cable forces, then reject
            // any damaged/disassembled assembly before wiring the fixture.
            for (int step = 0; step < 100; step++) yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();

            var gpus = FindObjectsOfType<GPU>();
            var supplies = FindObjectsOfType<Supply>();
            var boards = FindObjectsOfType<Motherboard>();
            if (gpus.Length != Lanes || supplies.Length != Lanes || boards.Length != Lanes)
            { Fail("Incomplete assemblies: GPU=" + gpus.Length + " PSU=" + supplies.Length + " board=" + boards.Length); yield break; }
            for (int lane = 0; lane < Lanes; lane++)
            {
                GPU gpu = Nearest(gpus, lane);
                Supply psu = Nearest(supplies, lane);
                Motherboard board = Nearest(boards, lane);
                if (gpu == null || psu == null || board == null ||
                    !Slotted(gpu) || !Slotted(psu) || !Slotted(board) ||
                    gpu.Damaged || psu.Damaged || monitors[lane].Damaged ||
                    !outlets[lane].IsMounted)
                {
                    Fail("Uncabled assembly failed to settle at lane " + lane +
                        " gpuSlot=" + Slotted(gpu) + " psuSlot=" + Slotted(psu) +
                        " boardSlot=" + Slotted(board) + " gpuDamaged=" + gpu?.Damaged +
                        " psuDamaged=" + psu?.Damaged + " monitorDamaged=" +
                        monitors[lane].Damaged + " outletMounted=" + outlets[lane].IsMounted);
                    yield break;
                }
                var outletPorts = OwnPorts(outlets[lane].GetComponent<Item>(), PortKind.Outlet);
                var gpuPort = Port(gpu, PortKind.GpuVideo);
                var psuPort = Port(psu, PortKind.PcPower);
                var monitorPower = Port(monitors[lane], PortKind.MonitorPower);
                var monitorVideo = Port(monitors[lane], PortKind.HdmiInput);
                if (outletPorts.Count < 2 || gpuPort == null || psuPort == null ||
                    monitorPower == null || monitorVideo == null)
                { Fail("Missing authored ports at lane " + lane); yield break; }
                // Never activate 24 fresh rope chains in one solver step.
                // Settle one real cable at a time; if even a single strand
                // tears the slotted hardware away, report the route before
                // building a misleading save or another seven PCs.
                if (!Attach(powerPrefab, outletPorts[0], psuPort, lane))
                { Fail("PSU cable insertion failed at lane " + lane); yield break; }
                yield return SettleFixtureCable(lane, "PSU");
                if (failed || !Slotted(gpu) || !Slotted(psu) || !Slotted(board))
                { Fail("PSU cable broke a slot at lane " + lane); yield break; }
                if (!Attach(powerPrefab, outletPorts[1], monitorPower, lane))
                { Fail("Monitor power insertion failed at lane " + lane); yield break; }
                yield return SettleFixtureCable(lane, "monitor power");
                if (failed || !Slotted(gpu) || !Slotted(psu) || !Slotted(board))
                { Fail("Monitor cable broke a slot at lane " + lane); yield break; }
                if (!Attach(hdmiPrefab, gpuPort, monitorVideo, lane))
                { Fail("GPU video insertion failed at lane " + lane); yield break; }
                yield return SettleFixtureCable(lane, "GPU video");
                if (failed || !Slotted(gpu) || !Slotted(psu) || !Slotted(board))
                { Fail("GPU cable broke a slot at lane " + lane); yield break; }
                healthIds.Add(gpu.Id);
                healthIds.Add(psu.Id);
                healthIds.Add(monitors[lane].Id);
                boardIds.Add(board.Id);
                monitorPowerOwners[monitors[lane].Id] = outlets[lane].GetComponent<Item>().Id;
                foreach (var hardware in new Item[] { gpu, psu, board })
                {
                    var host = hardware.GetComponent<Connector>().slot.GetComponentInParent<Item>();
                    if (host == null || host.Id == 0 || hardware.Id == 0)
                    { Fail("Missing slot host ID at lane " + lane); yield break; }
                    slotOwners[hardware.Id] = host.Id;
                }
            }
            // The fixture connects ends in one batch, like save restoration.
            // Recalculate only once after all 48 ends and slot hosts exist.
            if (expected.Count != 48) { Fail("Expected 48 plugged ends, got " + expected.Count); yield break; }
            CablePower.RefreshAll();
            if (!Observe("baseline, before settling")) yield break;
            for (int step = 0; step < 70; step++)
            {
                yield return new WaitForFixedUpdate();
                if (!Observe("baseline settle step " + step)) yield break;
            }
            if (!SaveManager.Instance.SaveData() || !File.Exists(savePath))
            { Fail("Test save could not be written"); yield break; }
            Write("BASELINE SAVED 8 PSU + 8 GPU + 8 monitors + 8 outlets; 48 ends verified. Bytes=" + new FileInfo(savePath).Length);
            for (cycle = 1; cycle <= Reloads; cycle++)
            {
                var loader = new DataLoader(savePath);
                loader.LoadFromPath();
                SaveManager.Loader = loader;
                Write("RELOAD starting");
                yield return SceneManager.LoadSceneAsync(roomScene);
                // SaveManager.Start starts LoadData after sceneLoaded/Awake;
                // do not mistake its pre-Start RestoringSave=false for done.
                yield return null;
                yield return null;
                deadline = Time.realtimeSinceStartup + 90f;
                while (Time.realtimeSinceStartup < deadline &&
                    (!Ready() || SaveManager.RestoringSave)) yield return null;
                if (!Ready() || SaveManager.RestoringSave)
                { Fail("Restore did not finish within 90 seconds"); yield break; }
                // Check each fixed step, not just the final state: catch a pin
                // or slot that temporarily detaches after the first collision.
                bool cyclePassed = true;
                for (int step = 0; step < 100; step++)
                {
                    yield return new WaitForFixedUpdate();
                    if (!Observe("reload step " + step)) { cyclePassed = false; break; }
                }
                Write((cyclePassed ? "RELOAD PASS " : "RELOAD FAIL ") + cycle + "/" + Reloads);
            }
            if (!failed) Write("PASS: no cable ends, slots, or wall mounts detached in five reloads");
            else Write("FAIL: at least one reload lost a connection; all five reloads completed");
            completed = true;
        }
        finally
        {
            if (!completed) failed = true;
            geometryOnLoad = false;
            Recording = false;
            Application.logMessageReceived -= UnityLog;
            busy = false;
            // Keep SaveManager.Loader pointing to the UNIQUE test file. Switching
            // it back here would allow Save to overwrite the user's original.
            Write((failed ? "FAIL" : "DONE") + " — log=" + logPath + " save=" + savePath +
                ". Exit Play Mode or load your ordinary save from the menu to return.");
        }
    }

    private static bool Ready() => SaveManager.Instance != null && Main.Instance != null && Player.Instance != null;
    private void Fail(string reason) { failed = true; Write("FAIL: " + reason); }
    private static bool Slotted(Item item) => item != null && item.GetComponent<Connector>() != null &&
        item.GetComponent<Connector>().slot != null;

    private static T Nearest<T>(T[] items, int lane) where T : Item
    {
        T best = null;
        float dist = 2f;
        float x = Origin.x + Pitch * lane;
        foreach (var item in items)
            if (item != null && Mathf.Abs(item.transform.position.x - x) < dist)
            { best = item; dist = Mathf.Abs(item.transform.position.x - x); }
        return best;
    }

    private static List<CablePort> OwnPorts(Item owner, PortKind kind)
    {
        var result = new List<CablePort>();
        foreach (var port in owner.GetComponentsInChildren<CablePort>(true))
            if (port.owner == owner && port.kind == kind) result.Add(port);
        result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return result;
    }
    private static CablePort Port(Item owner, PortKind kind)
    {
        var ports = OwnPorts(owner, kind);
        return ports.Count > 0 ? ports[0] : null;
    }

    private IEnumerator SettleFixtureCable(int lane, string route)
    {
        for (int step = 0; step < 35; step++)
        {
            yield return new WaitForFixedUpdate();
            if (!Observe("fixture lane " + lane + " " + route + " step " + step)) yield break;
        }
    }

    private bool Attach(GameObject prefab, CablePort a, CablePort b, int lane)
    {
        float span = Vector3.Distance(a.Snap.position, b.Snap.position);
        var cable = Instantiate(prefab, (a.Snap.position + b.Snap.position) * .5f, Quaternion.identity)
            .GetComponent<CableItem>();
        if (cable != null && span > cable.Length * .95f)
        {
            Write("INSERT FAILED lane=" + lane + " cable=" + cable.spawnId +
                " reason=span exceeds 95% insertion limit span=" + span.ToString("0.000") +
                "m length=" + cable.Length.ToString("0.000") + "m");
            Destroy(cable.gameObject);
            return false;
        }
        // Fixture setup is not a hand drag: the prefab coil starts midway
        // between sockets, not inside either authored trigger. The standard
        // TryInsert path requires an actual trigger overlap even when the
        // Rigidbody origin is placed exactly at the authored snap pivot.
        // Use the SAME path as save restoration instead. Verify mains/length
        // explicitly, so an invalid baseline can never count as a test pass.
        if (a.kind == PortKind.Outlet && !a.Powered)
        {
            Write("INSERT FAILED lane=" + lane + " outlet=" + a.owner.name +
                " reason=outlet not wall-mounted/powered");
            if (cable != null) Destroy(cable.gameObject);
            return false;
        }
        if (cable != null)
        {
            cable.End(0).GetComponent<Rigidbody>().position = a.Snap.position;
            cable.End(1).GetComponent<Rigidbody>().position = b.Snap.position;
            Physics.SyncTransforms();
        }
        if (cable == null || !cable.End(0).TryInsert(a, true) || !cable.End(1).TryInsert(b, true))
        {
            Write("INSERT FAILED lane=" + lane + " cable=" + (cable != null ? cable.name : "null") +
                " A=" + a.name + " owner=" + a.owner.name + " inserted=" +
                (cable != null && cable.End(0).Connected != null) +
                " B=" + b.name + " owner=" + b.owner.name + " inserted=" +
                (cable != null && cable.End(1).Connected != null) +
                " span=" + span.ToString("0.000") + "m length=" +
                (cable != null ? cable.Length.ToString("0.000") : "missing") + "m");
            return false;
        }
        // Try several collision-free slack arcs before a single physics step.
        // The first candidate on the old fixture touched the case at segment
        // 16 by 0.166 m even though both plugs were correctly connected.
        // Moving the *middle* of the rope away from the case leaves the
        // author-made port and both actual plug positions unchanged.
        var bends = new[] { Vector3.zero, Vector3.back * .5f,
            Vector3.back * .9f, Vector3.back * 1.3f,
            Vector3.back * 1.7f, Vector3.left * .7f,
            Vector3.back * .7f + Vector3.left * .7f,
            Vector3.right * .7f };
        float routeLength = 0f;
        string overlaps = "no viable route";
        Vector3 chosenBend = Vector3.zero;
        var rejected = new System.Text.StringBuilder();
        foreach (var bend in bends)
        {
            routeLength = cable.PrepareStressTestSpan(bend);
            overlaps = cable.StressInitialOverlaps();
            if (string.IsNullOrEmpty(overlaps))
            {
                chosenBend = bend;
                break;
            }
            rejected.Append(" {bend=").Append(bend).Append(" ")
                .Append(overlaps).Append("}");
        }
        if (!string.IsNullOrEmpty(overlaps))
        {
            Write("ROPE GEOMETRY INVALID lane=" + lane + " cable=" + cable.spawnId +
                " span=" + span.ToString("0.000") + "m length=" +
                cable.Length.ToString("0.000") + "m attempts=" + rejected);
            cable.End(0).Unplug();
            cable.End(1).Unplug();
            Destroy(cable.gameObject);
            return false;
        }
        Write("ROPE READY lane=" + lane + " cable=" + cable.spawnId +
            " A=" + a.owner.name + "/" + a.name + " B=" + b.owner.name + "/" + b.name +
            " span=" + span.ToString("0.000") + "m route=" + routeLength.ToString("0.000") +
            "m length=" + cable.Length.ToString("0.000") + "m bend=" + chosenBend);
        // Item.Start normally assigns these IDs; be explicit if the fixture
        // was created within the same frame as its first connection.
        if (a.owner.Id == 0) a.owner.Id = Main.Instance.GetNewId(a.owner);
        if (b.owner.Id == 0) b.owner.Id = Main.Instance.GetNewId(b.owner);
        if (cable.Id == 0) cable.Id = Main.Instance.GetNewId(cable);
        for (int i = 0; i < 2; i++)
        {
            var port = cable.End(i).Connected;
            expected.Add(new Expectation { cableId = cable.Id, cableName = cable.spawnId,
                end = i, ownerId = port.owner.Id, portName = port.name, kind = port.kind, lane = lane });
        }
        return true;
    }

    private bool Observe(string stage)
    {
        if (!Ready()) { Fail(stage + ": scene dependencies missing"); return false; }
        bool ok = true;
        foreach (var e in expected)
        {
            var cable = Main.Instance.GetItemById(e.cableId) as CableItem;
            var plug = cable != null ? cable.End(e.end) : null;
            var actual = plug != null ? plug.Connected : null;
            var owner = Main.Instance.GetItemById(e.ownerId);
            if (actual == null || actual.owner == null || actual.owner.Id != e.ownerId ||
                actual.name != e.portName || actual.kind != e.kind ||
                owner == null || !OwnPorts(owner, e.kind).Contains(actual))
            {
                string current = actual == null ? "UNPLUGGED" : actual.owner?.name + "(" + actual.owner?.Id +
                    ")/" + actual.name + "/" + actual.kind;
                float span = cable != null && cable.End(1 - e.end)?.Connected != null && owner != null
                    ? Vector3.Distance(cable.End(1 - e.end).Connected.Snap.position,
                        Port(owner, e.kind)?.Snap.position ?? owner.transform.position) : -1f;
                Fail(stage + " lane=" + e.lane + " cable=" + e.cableName + "(" + e.cableId +
                    ") end=" + e.end + " expected=" + owner?.name + "(" + e.ownerId +
                    ")/" + e.portName + "/" + e.kind + " actual=" + current +
                    " span=" + span.ToString("0.000") + " length=" + (cable?.Length.ToString("0.000") ?? "missing"));
                ok = false;
            }
        }
        foreach (var pair in slotOwners)
        {
            var part = Main.Instance.GetItemById(pair.Key);
            var connector = part != null ? part.GetComponent<Connector>() : null;
            var host = connector != null && connector.slot != null
                ? connector.slot.GetComponentInParent<Item>() : null;
            if (host == null || host.Id != pair.Value)
            { Fail(stage + " SLOT DETACHED part=" + part?.name + " id=" + pair.Key +
                " expectedHost=" + pair.Value + " actualHost=" + host?.Id); ok = false; }
        }
        foreach (var id in healthIds)
        {
            var hardware = Main.Instance.GetItemById(id) as Hardware;
            if (hardware == null || hardware.Damaged)
            { Fail(stage + " HARDWARE MISSING/DAMAGED id=" + id + " name=" + hardware?.name); ok = false; }
        }
        foreach (var id in boardIds)
        {
            var board = Main.Instance.GetItemById(id) as Motherboard;
            if (board == null || !CablePower.BoardHasPower(board))
            { Fail(stage + " PSU ROUTE NOT POWERED board=" + id); ok = false; }
        }
        foreach (var pair in monitorPowerOwners)
        {
            var monitor = Main.Instance.GetItemById(pair.Key) as PC.Component.Display;
            var inlet = monitor != null ? Port(monitor, PortKind.MonitorPower) : null;
            var plug = inlet != null ? inlet.plug : null;
            var source = plug != null ? plug.Cable.End(1 - plug.End).Connected : null;
            if (!CablePower.Energized(plug) || source?.owner?.Id != pair.Value)
            { Fail(stage + " MONITOR POWER LOST monitor=" + pair.Key + " outlet=" + pair.Value); ok = false; }
        }
        foreach (var outlet in FindObjectsOfType<OutletStrip>())
            if (!outlet.IsMounted)
            { Fail(stage + " OUTLET UNMOUNTED " + outlet.name + " id=" + outlet.GetComponent<Item>()?.Id); ok = false; }
        return ok;
    }

    private static void CreateGeometry()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Cable stress floor (scene-only)";
        floor.transform.position = Origin + new Vector3(13f, -.2f, -.5f);
        floor.transform.localScale = new Vector3(42f, .4f, 11f);
        for (int lane = 0; lane < Lanes; lane++)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Cable stress wall " + lane + " (scene-only)";
            // Wall normal points toward -X. The outlet's -Y back points +X;
            // its ray starts .12 m toward the wall and hits this face in .10 m.
            wall.transform.position = Origin + new Vector3(Pitch * lane + 1.52f, 1.2f, -2.7f);
            wall.transform.localScale = new Vector3(.2f, 2.4f, 1.8f);
        }
        // This isolated platform is far from the room's authored lights. Two
        // shadowless test-only lights make the black cases/cables inspectable.
        for (int i = 0; i < 2; i++)
        {
            var lamp = new GameObject("Cable stress light " + i + " (scene-only)");
            lamp.transform.position = Origin + new Vector3(6f + i * 16f, 8f, -1f);
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 26f;
            light.intensity = 2f;
            light.shadows = LightShadows.None;
        }
        Physics.SyncTransforms();
    }
}
#endif
