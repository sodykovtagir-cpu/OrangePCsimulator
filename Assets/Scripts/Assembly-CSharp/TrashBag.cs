using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class TrashBag : Item
{
    [Header("Trash Bag")]
    public int capacity = 10;

    [SerializeField]
    private int currentAmount = 0;

    private HashSet<GameObject> collectedRoots = new HashSet<GameObject>();

    public int CurrentAmount => currentAmount;
    public bool IsFull => currentAmount >= capacity;

    private void OnTriggerEnter(Collider other)
    {
        if (IsFull) return;

        GameObject root = FindBoxRoot(other);
        if (root == null) return;

        // Чистим ссылки на уничтоженные объекты, чтобы набор не рос вечно.
        if (collectedRoots.Count > 0)
            collectedRoots.RemoveWhere(r => r == null);

        if (collectedRoots.Contains(root)) return;

        collectedRoots.Add(root);
        currentAmount++;

        Destroy(root);

        Debug.Log($"Trash collected: {currentAmount}/{capacity}");
    }

    private GameObject FindBoxRoot(Collider col)
    {
        Transform t = col.transform;
        while (t != null)
        {
            if (t.CompareTag("Box")) return t.gameObject;
            t = t.parent;
        }
        return null;
    }

    public override string GetInfo()
    {
        string status;

        if (IsFull)
            status = "<color=lime>" + Localization.GetText("Full") + "</color>";
        else
            status = $"{currentAmount}/{capacity}";

        string title = Localization.GetText("Trash Bag");   // ← большая B
        string collected = Localization.GetText("Collected");

        return $"{title}\n{collected}: {status}";
    }

    public override void ToData(JObject jObject)
    {
        jObject["currentAmount"] = currentAmount;
        base.ToData(jObject);
    }

    public override void FromData(JObject jObject)
    {
        if (jObject.TryGetValue("currentAmount", out var val))
            currentAmount = val.Value<int>();

        base.FromData(jObject);
    }
}