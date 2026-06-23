using UnityEngine;

public class TrashBag : Item
{
    [Header("Trash Bag")]
    public int capacity = 10;

    private int currentAmount = 0;

    protected override void Start()
    {
        base.Start();
        UpdateName();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentAmount >= capacity)
            return;

        if (other.CompareTag("Box"))
        {
            currentAmount++;

            Destroy(other.gameObject);

            UpdateName();

            Debug.Log($"Trash collected: {currentAmount}/{capacity}");
        }
    }

    private void UpdateName()
    {
        if (currentAmount >= capacity)
            gameObject.name = "Trash Bag (FULL)";
        else
            gameObject.name = $"Trash Bag ({currentAmount}/{capacity})";
    }

    public override string GetInfo()
    {
        string status;

        if (currentAmount >= capacity)
            status = "<color=lime>FULL</color>";
        else
            status = $"{currentAmount}/{capacity}";

        return
            "<size=24><b>Trash Bag</b></size>\n\n" +
            $"<b>Stored Trash:</b> {status}\n" +
            $"<b>Capacity:</b> {capacity}\n\n" +
            "<color=grey>Used to collect boxes and debris.</color>";
    }
}