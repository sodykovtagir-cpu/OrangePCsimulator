#if UNITY_EDITOR
// Deliberately menu-only: unlike F8 this cannot start by an accidental key press.
// The test loads a disposable room and NEVER writes the user's original .opc.
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

public sealed class CableTraversalMode : MonoBehaviour
{
    private static readonly Vector3 Origin = new Vector3(1500f, 15f, 1500f);
    private const float Pitch = 12f;
    private const int SettleSteps = 65;
    private bool busy;
    private bool geometryOnLoad;
    private bool failed;
    private bool monitoringPins;
    private bool pcRouteNegative;
    private int roomScene;
    private string logPath;
    private string savePath;
    private readonly List<GameObject> hands = new List<GameObject>();
    private readonly List<CableItem> cables = new List<CableItem>();
    private readonly List<string> firstViolations = new List<string>();
    private readonly List<Collider> obstacles = new List<Collider>();

    public static void RequestFromEditor()
    {
        if (!Application.isPlaying || SaveManager.Instance == null ||
            SaveManager.Loader == null || Main.Instance == null ||
            Player.Instance == null || SaveManager.RestoringSave)
        {
            Debug.LogWarning("[CableTraversal] Enter Play Mode and load a room first.");
            return;
        }
        if (!CableMode.Enabled)
        {
            Debug.LogWarning("[CableTraversal] Enable Physical cables before loading the room.");
            return;
        }
        var runner = FindObjectOfType<CableTraversalMode>();
        if (runner == null)
        {
            var obj = new GameObject("Cable traversal test (Editor only)");
            DontDestroyOnLoad(obj);
            runner = obj.AddComponent<CableTraversalMode>();
        }
        if (runner.busy)
        {
            Debug.LogWarning("[CableTraversal] Already running.");
            return;
        }
        runner.StartCoroutine(runner.Run());
    }

    private void OnEnable() { SceneManager.sceneLoaded += SceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= SceneLoaded; }
    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (geometryOnLoad && scene.buildIndex == roomScene) CreateGeometry();
    }
    private void Write(string message)
    {
        string line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + message;
        Debug.Log("[CableTraversal] " + line);
        if (string.IsNullOrEmpty(logPath)) return;
        try { File.AppendAllText(logPath, line + Environment.NewLine); }
        catch (Exception ex) { Debug.LogError("[CableTraversal] Log file: " + ex); }
    }
    private void Fail(string message)
    {
        failed = true;
        if (firstViolations.Count < 12) firstViolations.Add(message);
        Write("FAIL " + message);
    }
    private void UnityLog(string message, string stack, LogType type)
    {
        if (monitoringPins && message.StartsWith("Cable HDMI: PIN BREAK", StringComparison.Ordinal))
            Fail("PIN BREAK while isolated traversal fixture is active: " + message);
    }

    private IEnumerator Run()
    {
        busy = true;
        failed = false;
        roomScene = SceneManager.GetActiveScene().buildIndex;
        string suffix = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" +
            Guid.NewGuid().ToString("N").Substring(0, 6);
        string folder = Path.Combine(Application.persistentDataPath, "CableTraversal");
        Directory.CreateDirectory(folder);
        savePath = Path.Combine(folder, "Traversal-" + suffix + ".opc");
        logPath = Path.Combine(folder, "Traversal-" + suffix + ".log");
        Write("START: human-paced spring drag, thin pole, thick wall, assembled PC, save/reload. " +
            "Disposable save=" + savePath);
        try
        {
            // Follow CableStressMode's isolated-loader pattern: even the
            // user's Save button can write only the unique test path here.
            var original = SaveManager.Loader;
            var data = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(original.GameData));
            Physics.gravity = new Vector3(0f, -9.81f);
            data.roomName = "Cable traversal (temporary)";
            data.gravity = true;
            data.cableModeVersion = 1;
            data.cablesEnabled = true;
            data.otherCableMode = null;
            var seed = new ContentData {
                playerData = Player.Instance.SavePlayer(), itemData = new ItemData[0]
            };
            SaveManager.Loader = new DataLoader(savePath, data) {
                Content = JsonConvert.SerializeObject(seed)
            };
            geometryOnLoad = true;
            yield return SceneManager.LoadSceneAsync(roomScene);
            yield return null;
            if (SaveManager.Instance == null || Main.Instance == null || Player.Instance == null)
            { Fail("Temporary room did not initialize"); yield break; }
            foreach (var item in FindObjectsOfType<Item>())
                if (item != null) Destroy(item.gameObject);
            yield return null;
            // Only monitor the disposable room, never the user's own objects
            // removed during the scene switch or later normal gameplay.
            Application.logMessageReceived += UnityLog;
            monitoringPins = true;
            Player.Instance.LoadPlayer(new PlayerData { x = Origin.x - 5f,
                y = Origin.y + 1.5f, z = Origin.z - 5f, ry = 30f, rx = 0f });
            var lan = Resources.Load<GameObject>("components/LAN Cable (5m)");
            var power = Resources.Load<GameObject>("components/Power Cable (15m)");
            var hdmi = Resources.Load<GameObject>("components/HDMI Cable 6m");
            var ready = Resources.Load<GameObject>("components/ready/ReadyPC_Home_Black");
            if (lan == null || power == null || hdmi == null || ready == null)
            { Fail("Missing LAN/Power/HDMI or assembled-PC prefab"); yield break; }

            // The real ready-build spawner assembles motherboard/GPU/PSU in
            // authored slots, rather than substituting a box for a computer.
            Vector3 pcCentre = Origin + new Vector3(Pitch * 2f, 2.1f, 0f);
            var build = Instantiate(ready, pcCentre, Quaternion.identity);
            float deadline = Time.realtimeSinceStartup + 90f;
            while (build != null && Time.realtimeSinceStartup < deadline) yield return null;
            if (build != null) { Fail("Ready PC did not assemble in 90 seconds"); yield break; }
            for (int i = 0; i < 100; i++) yield return new WaitForFixedUpdate();
            var pcCase = FindNearestCase(pcCentre);
            if (pcCase == null) { Fail("Assembled PC has no Case"); yield break; }
            var caseItem = pcCase.GetComponentInParent<Item>();
            if (caseItem != null)
                foreach (var solid in caseItem.GetComponentsInChildren<Collider>())
                    if (solid != null && solid.enabled && !solid.isTrigger &&
                        solid.GetComponentInParent<Item>() == caseItem)
                        obstacles.Add(solid);
            if (obstacles.Count == 0) { Fail("PC case has no solid colliders"); yield break; }
            Physics.SyncTransforms();
            Bounds pcBounds = obstacles[0].bounds;
            for (int i = 1; i < obstacles.Count; i++)
                pcBounds.Encapsulate(obstacles[i].bounds);
            // The HDMI prefab labelled "6m" actually has length=12 and a
            // roughly 1m delivery coil. Its Plug B can project more than 1.1m
            // from the cable ROOT. Reserve space for the entire coil and
            // authored plug collider, not just the root, outside the case.
            // Route the HDMI probe around the side OPPOSITE the assembled
            // PC's video sockets. Real port triggers can otherwise auto-insert
            // the dragged plug, turning a collision test into a pin-break test.
            int negativePorts = 0, positivePorts = 0;
            foreach (var port in FindObjectsOfType<CablePort>())
            {
                if (port == null ||
                    (port.kind != PortKind.GpuVideo && port.kind != PortKind.BoardVideo &&
                     port.kind != PortKind.HdmiInput) ||
                    (port.TriggerCenter - pcBounds.center).sqrMagnitude > 36f) continue;
                if (port.TriggerCenter.z < pcBounds.center.z) negativePorts++;
                else positivePorts++;
            }
            pcRouteNegative = negativePorts <= positivePorts;
            Vector3 pcCableSpawn = new Vector3(pcBounds.min.x - 2.1f,
                Origin.y + 1.4f, pcRouteNegative ? pcBounds.min.z + .4f : pcBounds.max.z - .4f);
            Write("FIXTURE PC bounds=" + pcBounds + " cable-spawn=" + pcCableSpawn +
                " video-ports-z(-/+): " + negativePorts + "/" + positivePorts +
                " detour=" + (pcRouteNegative ? "negative Z" : "positive Z"));

            var pole = GameObject.Find("Cable traversal thin pole (scene-only)");
            var wall = GameObject.Find("Cable traversal thick wall (scene-only)");
            if (pole == null || wall == null)
            { Fail("Test pole or wall missing after room load"); yield break; }
            var thin = pole.GetComponent<Collider>();
            var thick = wall.GetComponent<Collider>();
            if (thin == null || thick == null) { Fail("Test wall has no collider"); yield break; }

            var props = new[] { lan, power, hdmi };
            for (int lane = 0; lane < props.Length; lane++)
            {
                var position = lane == 2 ? pcCableSpawn :
                    Origin + new Vector3(Pitch * lane - 1.6f, 1.4f, -.7f);
                var cable = Instantiate(props[lane], position, Quaternion.identity)
                    .GetComponent<CableItem>();
                if (cable == null) { Fail("Cable prefab has no CableItem lane=" + lane); yield break; }
                cables.Add(cable);
            }
            for (int step = 0; step < SettleSteps; step++) yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            // Check all three baselines independently: one bad lane must not
            // hide the other two or prevent their full save/reload check.
            bool poleReady = Observe("preflight pole", cables[0], new[] { thin });
            bool wallReady = Observe("preflight wall", cables[1], new[] { thick });
            bool pcReady = Observe("preflight PC", cables[2], obstacles);
            if (poleReady)
                yield return RunStage("spiral thin pole", cables[0], new[] { thin },
                    Origin + new Vector3(0f, 1.35f, 0f), .42f, false);
            else Write("SKIP spiral: invalid initial coil");
            if (wallReady)
                yield return RunStage("thick wall / opposing ends", cables[1], new[] { thick },
                    Origin + new Vector3(Pitch, 1.35f, 0f), 1.05f, true);
            else Write("SKIP wall: invalid initial coil");
            if (pcReady)
                yield return RunStage("assembled PC housing", cables[2], obstacles,
                    pcCentre, 1.7f, true);
            else Write("SKIP PC: invalid initial coil");
            // Destroy(hand) is deferred until the end of this frame. Saving
            // immediately after a stage FAIL would serialize a rope while its
            // test springs are still physically attached. Release and settle
            // before the baseline comparison and full save/reload.
            for (int step = 0; step < 25; step++) yield return new WaitForFixedUpdate();
            Observe("before save pole", cables[0], new[] { thin });
            Observe("before save wall", cables[1], new[] { thick });
            Observe("before save PC", cables[2], obstacles);
            // Save and reload even after a geometry FAIL: the second phase
            // checks whether the already-wrong rope gets worse on restore.
            // Full SaveManager save + room reload, not a synthetic cable-only
            // roundtrip. SceneLoaded recreates only the test's static solids.
            var ids = new int[cables.Count];
            for (int i = 0; i < cables.Count; i++) ids[i] = cables[i].Id;
            if (!SaveManager.Instance.SaveData() || !File.Exists(savePath))
            { Fail("SaveManager could not write isolated test save"); yield break; }
            Write("SAVED three moved cables + assembled PC, bytes=" + new FileInfo(savePath).Length);
            var loader = new DataLoader(savePath);
            loader.LoadFromPath();
            SaveManager.Loader = loader;
            yield return SceneManager.LoadSceneAsync(roomScene);
            yield return null;
            yield return null;
            deadline = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < deadline &&
                (SaveManager.Instance == null || Main.Instance == null || SaveManager.RestoringSave))
                yield return null;
            if (SaveManager.Instance == null || Main.Instance == null || SaveManager.RestoringSave)
            { Fail("Isolated room restore timed out"); yield break; }
            for (int step = 0; step < 70; step++) yield return new WaitForFixedUpdate();
            var restoredPole = GameObject.Find("Cable traversal thin pole (scene-only)")?.GetComponent<Collider>();
            var restoredWall = GameObject.Find("Cable traversal thick wall (scene-only)")?.GetComponent<Collider>();
            var restoredCase = FindNearestCase(pcCentre);
            if (restoredPole == null || restoredWall == null || restoredCase == null)
            { Fail("An obstacle or PC disappeared on reload"); yield break; }
            var restoredCaseSolids = new List<Collider>();
            var restoredItem = restoredCase.GetComponentInParent<Item>();
            if (restoredItem != null)
                foreach (var solid in restoredItem.GetComponentsInChildren<Collider>())
                    if (solid != null && solid.enabled && !solid.isTrigger &&
                        solid.GetComponentInParent<Item>() == restoredItem)
                        restoredCaseSolids.Add(solid);
            var savedObstacles = new List<Collider>[] {
                new List<Collider> { restoredPole }, new List<Collider> { restoredWall },
                restoredCaseSolids
            };
            var restoredCables = new CableItem[ids.Length];
            var reloadValid = new bool[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                restoredCables[i] = Main.Instance.GetItemById(ids[i]) as CableItem;
                if (restoredCables[i] == null)
                { Fail("Missing cable after reload id=" + ids[i]); continue; }
                reloadValid[i] = Observe("after save/reload lane=" + i,
                    restoredCables[i], savedObstacles[i]);
            }
            // A reloaded plug can snap or its pin can break several physics
            // ticks after the initial check. Watch for two more seconds instead
            // of reporting PASS immediately before a delayed PIN BREAK.
            for (int step = 0; step < 100; step++)
            {
                yield return new WaitForFixedUpdate();
                if (step % 10 != 0) continue;
                for (int i = 0; i < ids.Length; i++)
                    if (reloadValid[i])
                        reloadValid[i] = Observe("reload watch step=" + step + " lane=" + i,
                            restoredCables[i], savedObstacles[i]);
            }
            Write(failed ? "FAIL: see first violation and isolated log" :
                "PASS: spiral, thick wall, assembled PC and one full save/reload");
        }
        finally
        {
            monitoringPins = false;
            Application.logMessageReceived -= UnityLog;
            for (int i = 0; i < hands.Count; i++) if (hands[i] != null) Destroy(hands[i]);
            hands.Clear();
            geometryOnLoad = false;
            busy = false;
            Write((failed ? "FAIL" : "DONE") + " log=" + logPath +
                " save=" + savePath + ". Exit Play Mode or load your own save to return.");
        }
    }

    private static Case FindNearestCase(Vector3 at)
    {
        Case nearest = null;
        float best = 6f * 6f;
        foreach (var pc in FindObjectsOfType<Case>())
        {
            float d = (pc.transform.position - at).sqrMagnitude;
            if (d < best) { best = d; nearest = pc; }
        }
        return nearest;
    }

    private GameObject Hand(Rigidbody target, float spring, float damper)
    {
        var hand = new GameObject("Cable traversal hand (scene-only)");
        // Place the Transform BEFORE adding the joint. In a fresh GameObject
        // its default world origin is (0,0,0); at this test's distant room an
        // auto-configured connectedAnchor can generate a huge first impulse.
        hand.transform.position = target.position;
        var rb = hand.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        var joint = hand.AddComponent<SpringJoint>();
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector3.zero;
        joint.connectedAnchor = Vector3.zero;
        joint.spring = spring;
        joint.damper = damper;
        joint.connectedBody = target;
        hands.Add(hand);
        return hand;
    }

    private IEnumerator RunStage(string stage, CableItem cable,
        IList<Collider> solids, Vector3 centre, float radius, bool cross)
    {
        bool priorFailure = failed;
        failed = false;
        yield return Exercise(stage, cable, solids, centre, radius, cross);
        bool stageFailed = failed;
        failed |= priorFailure;
        if (stageFailed) Write("STAGE FAIL " + stage + "; proceeding to the other independent lanes");
    }

    private IEnumerator Exercise(string stage, CableItem cable,
        IList<Collider> solids, Vector3 centre, float radius, bool cross)
    {
        if (failed) { Write("SKIP " + stage + " after earlier failure"); yield break; }
        if (cable == null) { Fail(stage + " cable missing"); yield break; }
        var fixedEnd = cable.End(0).GetComponent<Rigidbody>();
        var movingPlug = cable.End(1);
        var movingEnd = movingPlug.GetComponent<Rigidbody>();
        var stationary = Hand(fixedEnd, 120f, 9f);
        var moving = Hand(movingEnd, 100f, 5f);
        float oldDrag = movingEnd.drag, oldAngularDrag = movingEnd.angularDrag;
        var oldCcd = movingEnd.collisionDetectionMode;
        // Raycast does the same temporary CCD switch during a real plug grab;
        // this menu fixture does not run through Raycast's input pipeline.
        movingEnd.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        movingEnd.drag = 10f;
        movingEnd.angularDrag = 5f;
        movingPlug.SetHeld(true); // same held state as the player's plug drag
        // Isolate a physics jump caused by merely attaching the springs from
        // a real traversal failure. Measure it before issuing ANY hand move.
        Write(stage + " GRASP anchors fixed=" +
            Vector3.Distance(stationary.transform.position, fixedEnd.position).ToString("0.000") +
            "m moving=" + Vector3.Distance(moving.transform.position, movingEnd.position).ToString("0.000") + "m");
        yield return new WaitForFixedUpdate();
        Observe(stage + " stationary grasp before movement", cable, solids);
        // Ease in/out at every waypoint; deterministic little overcorrections
        // emulate a hand moving the mouse rather than robotic linear steps.
        var waypoints = new List<Vector3> { movingEnd.position };
        if (!cross)
        {
            for (int k = 0; k <= 12; k++)
            {
                float angle = -Mathf.PI * .75f + k * Mathf.PI * 2f / 12f;
                waypoints.Add(centre + new Vector3(Mathf.Cos(angle) * radius,
                    .06f * Mathf.Sin(k * .8f), Mathf.Sin(angle) * radius));
            }
        }
        else
        {
            // First lead the plug *around* the wall/case edge to put both
            // real ends on opposite sides. Only then drag directly across the
            // middle, checking that the real jacket remains around the edge.
            var bounds = solids[0].bounds;
            for (int solid = 1; solid < solids.Count; solid++)
                if (solids[solid] != null) bounds.Encapsulate(solids[solid].bounds);
            float left = bounds.min.x - .22f, right = bounds.max.x + .22f;
            // Keep the case route away from whichever Z side has the actual
            // video sockets; no assumption about the prefab's orientation.
            // A 15m Power section is ~0.58m long, with ~0.36m reach from
            // centre to tip. The old 0.22m waypoint clipped a wall corner
            // even when the hand passed outside it. Give the capsule itself
            // room to turn, then still check every physical contact.
            float around = stage == "assembled PC housing" ?
                (pcRouteNegative ? bounds.min.z - .45f : bounds.max.z + .45f) :
                bounds.max.z + .45f;
            waypoints.Add(new Vector3(left, centre.y, around));
            waypoints.Add(new Vector3(right, centre.y, around));
            waypoints.Add(new Vector3(right, centre.y, centre.z));
            waypoints.Add(new Vector3(left, centre.y, centre.z));
            waypoints.Add(new Vector3(right, centre.y, centre.z));
        }
        int checks = 0;
        bool oppositeSeen = false;
        float maxPoleTurns = 0f;
        Bounds barrier = solids[0].bounds;
        for (int solid = 1; solid < solids.Count; solid++)
            if (solids[solid] != null) barrier.Encapsulate(solids[solid].bounds);
        for (int i = 1; i < waypoints.Count && !failed; i++)
        {
            Vector3 a = waypoints[i - 1], b = waypoints[i];
            // About twice the previous hand speed while retaining smooth
            // acceleration, uneven timing and small human-like corrections.
            int frames = i % 3 == 0 ? 24 : i % 3 == 1 ? 17 : 29;
            for (int frame = 0; frame < frames; frame++)
            {
                float t = (frame + 1f) / frames;
                float ease = Mathf.SmoothStep(0f, 1f, t);
                float wobble = Mathf.Sin(Mathf.PI * t) * .025f;
                float noise = Mathf.PerlinNoise((checks + frame) * .045f, i * .71f) - .5f;
                Vector3 target = Vector3.Lerp(a, b, ease) +
                    new Vector3(wobble * noise, wobble * .2f, wobble * Mathf.Sin(t * 13f));
                moving.GetComponent<Rigidbody>().MovePosition(target);
                yield return new WaitForFixedUpdate();
                checks++;
                if (cross && fixedEnd.position.x < barrier.min.x - .05f &&
                    movingEnd.position.x > barrier.max.x + .05f)
                    oppositeSeen = true;
                if (!Observe(stage + " move=" + i + " frame=" + frame,
                    cable, solids)) break;
                if (!cross)
                    maxPoleTurns = Mathf.Max(maxPoleTurns, PoleWindings(cable, centre));
            }
            for (int pause = 0; pause < 4 && !failed; pause++)
            {
                yield return new WaitForFixedUpdate();
                if (pause == 3) Observe(stage + " pause=" + i, cable, solids);
            }
        }
        movingPlug.SetHeld(false);
        movingEnd.collisionDetectionMode = oldCcd;
        movingEnd.drag = oldDrag;
        movingEnd.angularDrag = oldAngularDrag;
        if (moving != null) { hands.Remove(moving); Destroy(moving); }
        if (stationary != null) { hands.Remove(stationary); Destroy(stationary); }
        for (int step = 0; step < 30 && !failed; step++)
        {
            yield return new WaitForFixedUpdate();
            if (step % 5 == 0) Observe(stage + " settle=" + step, cable, solids);
        }
        if (!failed && cross && !oppositeSeen)
            Fail(stage + ": INCONCLUSIVE — plug never reached the opposite side while the other end stayed behind the obstacle");
        // Moving the hand in a circle is NOT proof that the actual rope wound
        // around the pole: the other end can slide, or coarse links may trace
        // a short chord. Require substantial winding of the physics chain.
        if (!failed && !cross && maxPoleTurns < .85f)
            Fail(stage + ": INCONCLUSIVE — hand circled pole, but physical cable wound only " +
                maxPoleTurns.ToString("0.00") + " turns (need >=0.85)");
        if (!failed) Write("PASS " + stage + ": " + checks + " human-paced fixed steps; " +
            "opposite=" + oppositeSeen + " max-winding=" + maxPoleTurns.ToString("0.00") + " turns");
    }

    private static float PoleWindings(CableItem cable, Vector3 pole)
    {
        var sections = new List<Rigidbody>();
        foreach (var body in cable.GetComponentsInChildren<Rigidbody>())
            if (body != null && body.name.StartsWith("Cable segment ")) sections.Add(body);
        sections.Sort((left, right) => SectionIndex(left).CompareTo(SectionIndex(right)));
        Vector3 start = cable.End(0).CableExitPhysicsPosition - pole;
        float previous = Mathf.Atan2(start.z, start.x) * Mathf.Rad2Deg;
        float total = 0f;
        for (int i = 0; i <= sections.Count; i++)
        {
            Vector3 offset = (i == sections.Count ? cable.End(1).CableExitPhysicsPosition :
                sections[i].position) - pole;
            float next = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
            total += Mathf.DeltaAngle(previous, next);
            previous = next;
        }
        return Mathf.Abs(total) / 360f;
    }

    private bool Observe(string stage, CableItem cable, IList<Collider> solids)
    {
        if (cable == null) { Fail(stage + ": missing cable"); return false; }
        var rope = cable.GetComponentsInChildren<Rigidbody>();
        var line = cable.GetComponent<LineRenderer>();
        if (line == null) { Fail(stage + ": no cable jacket"); return false; }
        var ordered = new List<Rigidbody>();
        foreach (var body in rope)
            if (body != null && body.name.StartsWith("Cable segment ")) ordered.Add(body);
        ordered.Sort((left, right) => SectionIndex(left).CompareTo(SectionIndex(right)));
        if (ordered.Count == 0 || line.positionCount != ordered.Count + 1)
        { Fail(stage + ": physical/visual section count mismatch"); return false; }
        float maxGap = 0f;
        string worst = "none";
        for (int i = 0; i < rope.Length; i++)
        {
            var rb = rope[i];
            if (rb == null || !rb.name.StartsWith("Cable segment ")) continue;
            var capsule = rb.GetComponent<CapsuleCollider>();
            var joint = rb.GetComponent<CharacterJoint>();
            if (capsule == null || joint == null || joint.connectedBody == null)
            { Fail(stage + ": section missing capsule/joint " + rb.name); return false; }
            if (!capsule.enabled || !rb.detectCollisions)
            { Fail(stage + ": disabled collisions " + rb.name); return false; }
            float gap = Vector3.Distance(rb.position + rb.rotation * joint.anchor,
                joint.connectedBody.position + joint.connectedBody.rotation * joint.connectedAnchor);
            if (gap > maxGap) { maxGap = gap; worst = rb.name; }
            for (int j = 0; j < solids.Count; j++)
            {
                var solid = solids[j];
                if (solid == null || !solid.enabled || solid.isTrigger) continue;
                Vector3 direction;
                float depth;
                if (Physics.ComputePenetration(capsule, rb.position, rb.rotation,
                    solid, solid.transform.position, solid.transform.rotation,
                    out direction, out depth) && depth > .025f)
                {
                    Fail(stage + ": CAPSULE through " + solid.name + " " + rb.name +
                        " depth=" + depth.ToString("0.000") + "m ignored=" +
                        Physics.GetIgnoreCollision(capsule, solid));
                    return false;
                }
            }
        }
        // A connector may collide even when all cable capsules and the
        // jacket are clear. This caught the old fixture's oversized HDMI
        // delivery coil: Plug B was born inside the case before any drag.
        for (int end = 0; end < 2; end++)
        {
            var plug = cable.End(end);
            if (plug == null) { Fail(stage + ": missing plug " + end); return false; }
            var plugBody = plug.GetComponent<Rigidbody>();
            foreach (var plugSolid in plug.GetComponentsInChildren<Collider>())
            {
                if (plugSolid == null || !plugSolid.enabled || plugSolid.isTrigger ||
                    plugSolid.attachedRigidbody != plugBody) continue;
                for (int k = 0; k < solids.Count; k++)
                {
                    var solid = solids[k];
                    if (solid == null || !solid.enabled || solid.isTrigger) continue;
                    Vector3 direction;
                    float depth;
                    if (Physics.ComputePenetration(plugSolid, plugSolid.transform.position,
                        plugSolid.transform.rotation, solid, solid.transform.position,
                        solid.transform.rotation, out direction, out depth) && depth > .025f)
                    {
                        Fail(stage + ": PLUG through " + solid.name + " end=" + end +
                            " depth=" + depth.ToString("0.000") + "m ignored=" +
                            Physics.GetIgnoreCollision(plugSolid, solid));
                        return false;
                    }
                }
            }
        }
        // Projection starts at 0.02 m. A value printed as 0.020 m can
        // exceed 0.020000 by floating-point noise during normal settling;
        // report actual deviations above a small solver margin. Total rope
        // path and endpoint span are checked separately below.
        if (maxGap > .03f)
        { Fail(stage + ": STRETCHED " + worst + " gap=" + maxGap.ToString("0.0000") + "m"); return false; }
        var a = cable.End(0);
        var b = cable.End(1);
        if (a == null || b == null) { Fail(stage + ": missing cable end"); return false; }
        if (a.Connected != null || b.Connected != null)
        { Fail(stage + ": plug unexpectedly inserted into fixture hardware"); return false; }
        if (Vector3.Distance(a.CableExitPhysicsPosition, b.CableExitPhysicsPosition) > cable.Length + .08f)
        { Fail(stage + ": endpoint span exceeds cable length"); return false; }
        float path = Vector3.Distance(a.CableExitPhysicsPosition, ordered[0].position);
        for (int i = 1; i < ordered.Count; i++)
            path += Vector3.Distance(ordered[i - 1].position, ordered[i].position);
        path += Vector3.Distance(ordered[ordered.Count - 1].position, b.CableExitPhysicsPosition);
        if (path > cable.Length + .1f)
        { Fail(stage + ": rope path stretched to " + path.ToString("0.000") + "m"); return false; }
        // Rebuild the jacket's joint polyline using actual Rigidbody poses.
        // LineRenderer's interpolated Transform poses can lag a physics tick
        // while the human-like hand is moving; a stale visual frame must not
        // create a false PASS or FAIL for the physical barrier.
        var physicalPath = new Vector3[ordered.Count + 1];
        physicalPath[0] = a.CableExitPhysicsPosition;
        for (int i = 1; i < ordered.Count; i++)
        {
            var joint = ordered[i].GetComponent<CharacterJoint>();
            if (joint == null) { Fail(stage + ": missing path joint"); return false; }
            physicalPath[i] = (ordered[i].position + ordered[i].rotation * joint.anchor +
                ordered[i - 1].position + ordered[i - 1].rotation * joint.connectedAnchor) * .5f;
        }
        physicalPath[ordered.Count] = b.CableExitPhysicsPosition;
        var rayHits = new RaycastHit[48];
        for (int i = 1; i < physicalPath.Length; i++)
        {
            Vector3 start = physicalPath[i - 1], delta = physicalPath[i] - start;
            if (delta.sqrMagnitude < .0001f) continue;
            int hits = Physics.RaycastNonAlloc(start, delta.normalized, rayHits,
                delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (hits == rayHits.Length) { Fail(stage + ": ray query overflow"); return false; }
            for (int h = 0; h < hits; h++)
            {
                var solid = rayHits[h].collider;
                for (int k = 0; k < solids.Count; k++)
                    if (solid != null && solid == solids[k])
                    {
                        // A connector whose authored solid does not cover its
                        // cable-exit pivot can leave a physical gap at a case
                        // wall. Record whether this is an ignored contact, a
                        // real penetration, or a collider that misses the exit.
                        string plugContact = "none";
                        var plugBody = b.GetComponent<Rigidbody>();
                        foreach (var plugSolid in b.GetComponentsInChildren<Collider>())
                        {
                            if (plugSolid == null || !plugSolid.enabled || plugSolid.isTrigger ||
                                plugSolid.attachedRigidbody != plugBody) continue;
                            Vector3 direction;
                            float depth;
                            bool overlaps = Physics.ComputePenetration(plugSolid,
                                plugSolid.transform.position, plugSolid.transform.rotation,
                                solid, solid.transform.position, solid.transform.rotation,
                                out direction, out depth);
                            plugContact = plugSolid.name + " overlap=" +
                                (overlaps ? depth.ToString("0.000") : "none") +
                                " ignored=" + Physics.GetIgnoreCollision(plugSolid, solid);
                            break;
                        }
                        Fail(stage + ": JACKET through " + solid.name +
                            " span=" + (i - 1) + "-" + i +
                            " from=" + start.ToString("F3") +
                            " to=" + physicalPath[i].ToString("F3") +
                            " solid-bounds=" + solid.bounds +
                            " plugB=" + plugBody.position.ToString("F3") +
                            " connected=" + (b.Connected != null ? b.Connected.name : "none") +
                            " plug-contact=" + plugContact);
                        return false;
                    }
            }
        }
        return true;
    }

    private static int SectionIndex(Rigidbody rb)
    {
        int index;
        return int.TryParse(rb.name.Substring("Cable segment ".Length), out index) ? index : int.MaxValue;
    }

    private static void CreateGeometry()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Cable traversal floor (scene-only)";
        floor.transform.position = Origin + new Vector3(Pitch, -.2f, 0f);
        floor.transform.localScale = new Vector3(36f, .4f, 12f);
        var thin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        thin.name = "Cable traversal thin pole (scene-only)";
        thin.transform.position = Origin + new Vector3(0f, 1.35f, 0f);
        thin.transform.localScale = new Vector3(.045f, 2.7f, .045f);
        var thick = GameObject.CreatePrimitive(PrimitiveType.Cube);
        thick.name = "Cable traversal thick wall (scene-only)";
        thick.transform.position = Origin + new Vector3(Pitch, 1.35f, 0f);
        thick.transform.localScale = new Vector3(.38f, 2.7f, 3f);
        var light = new GameObject("Cable traversal light (scene-only)");
        light.transform.position = Origin + new Vector3(Pitch, 7f, -1f);
        var lamp = light.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.range = 40f;
        lamp.intensity = 2f;
        lamp.shadows = LightShadows.None;
        Physics.SyncTransforms();
    }
}
#endif
