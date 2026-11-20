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

        customItems.Enqueue(() => New(itemPrefab));
        customConfigures.Enqueue(configure);
        Box.CustomDelivery = TakeCustomItem;
        EnsureSpawnHook();

        DeliverPackage(boxPrefab);
        return true;
    }

    private readonly System.Collections.Generic.Queue<Func<GameObject>> customItems =
        new System.Collections.Generic.Queue<Func<GameObject>>();
    private readonly System.Collections.Generic.Queue<Action<GameObject>> customConfigures =
        new System.Collections.Generic.Queue<Action<GameObject>>();
    private bool spawnHookInstalled;

    private GameObject TakeCustomItem()
    {
        if (customItems.Count == 0) { Box.CustomDelivery = null; return null; }
        var make = customItems.Dequeue();
        Box.CustomDelivery = customItems.Count > 0 ? (Func<GameObject>)TakeCustomItem : null;
        return make();
    }

    private void EnsureSpawnHook()
    {
        if (spawnHookInstalled) return;
        spawnHookInstalled = true;
        Spawned += OnSpawned;
    }

    private void OnSpawned(GameObject spawned)
    {
        if (Box.Restoring) return;
        var box = spawned != null ? spawned.GetComponent<Box>() : null;
        if (box == null || !box.NeedsContent) return;
        if (customConfigures.Count == 0) return;
        box.ReleaseFrom(customConfigures.Dequeue());
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
