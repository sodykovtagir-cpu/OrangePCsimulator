using System;
using UnityEngine;

// Doubles of the game types the custom-cover delivery path touches.
// Box.cs is the REAL shipped file; only the engine and Main are faked.
public class GameMain
{
    public static GameMain Instance { get; private set; }
    public static GameMain Create() { Instance = new GameMain(); return Instance; }

    public int Money = 10000;
    public int SpendCalls;
    public readonly System.Collections.Generic.List<GameObject> Queue = new System.Collections.Generic.List<GameObject>();
    public event Action<GameObject> Spawned;

    public void Spend(int amount) { Money -= amount; SpendCalls++; }
    public void AddMoney(int value) { Money += value; }

    private readonly System.Collections.Generic.Queue<Func<GameObject>> customOrders =
        new System.Collections.Generic.Queue<Func<GameObject>>();

    private GameObject NotifySpawned(GameObject go)
    {
        if (go != null && Spawned != null) Spawned(go);
        return go;
    }

    // Mirrors Main.DeliverCustom: deferred content + order-preserving queue.
    public bool DeliverCustom(GameObject itemPrefab, GameObject boxPrefab, Action<GameObject> configure)
    {
        if (itemPrefab == null) return false;
        if (boxPrefab == null)
        {
            var direct = NotifySpawned(New(itemPrefab));
            if (direct == null) return false;
            if (configure != null) configure(direct);
            return true;
        }

        customItems.Enqueue(itemPrefab);
        customConfigures.Enqueue(configure);
        customBoxes.Enqueue(boxPrefab);
        EnsureSpawnHook();

        DeliverPackage(boxPrefab);
        return true;
    }

    private readonly System.Collections.Generic.Queue<GameObject> customItems =
        new System.Collections.Generic.Queue<GameObject>();
    private readonly System.Collections.Generic.Queue<Action<GameObject>> customConfigures =
        new System.Collections.Generic.Queue<Action<GameObject>>();
    private readonly System.Collections.Generic.Queue<GameObject> customBoxes =
        new System.Collections.Generic.Queue<GameObject>();
    private bool spawnHookInstalled;

    private void EnsureSpawnHook()
    {
        if (spawnHookInstalled) return;
        spawnHookInstalled = true;
        Spawned += OnSpawned;
    }

    private void OnSpawned(GameObject spawned)
    {
        if (spawned == null || customConfigures.Count == 0) return;
        if (Box.Restoring) return;
        // Mirrors Main.OnSpawned: the glass crate keeps its Box on the inactive
        // BrokenCrate child, not on the crate root.
        var box = spawned.GetComponent<Box>();
        if (box == null) box = spawned.GetComponentInChildren<Box>(true);
        if (box == null) return;

        GameObject expected = customBoxes.Count > 0 ? customBoxes.Peek() : null;
        if (expected == null || !SamePrefab(spawned, expected)) return;
        customBoxes.Dequeue();
        Action<GameObject> configure = customConfigures.Dequeue();

        GameObject itemPrefab = customItems.Count > 0 ? customItems.Dequeue() : null;
        if (itemPrefab == null) return;
        box.ExpectContent();
        // Mirrors Main: position is read from the Box when the crate is broken.
        box.FillWith(() => UnityEngine.Object.Instantiate(itemPrefab, box.transform.position, box.transform.rotation), configure);
        // Mirrors Main: the crate arrives whole and is broken with the hammer.
    }

    public bool CustomQueuesEmpty
    {
        get { return customItems.Count == 0 && customConfigures.Count == 0 && customBoxes.Count == 0; }
    }

    private static bool SamePrefab(GameObject instance, GameObject prefab)
    {
        return instance != null && prefab != null && instance.name.StartsWith(prefab.name);
    }

    public GameObject DeliverPackage(GameObject prefab)
    {
        if (prefab == null) return null;
        Queue.Add(prefab);
        return null;
    }

    public GameObject SpawnFromQueue(GameObject prefab)
    {
        return NotifySpawned(UnityEngine.Object.Instantiate(prefab, new Vector3(), new Quaternion()));
    }

    private GameObject New(GameObject prefab)
    {
        return UnityEngine.Object.Instantiate(prefab, new Vector3(), new Quaternion());
    }
}
