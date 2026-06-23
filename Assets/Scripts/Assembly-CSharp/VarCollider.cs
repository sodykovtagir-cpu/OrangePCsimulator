using UnityEngine;

[RequireComponent(typeof(Item))]
public class VarCollider : MonoBehaviour
{
	public Vector3 attachedCenter;

	public Vector3 attachedSize;

	private BoxCollider col;

	private Vector3 originalCenter;

	private Vector3 originalSize;

	private void Awake()
	{
		col = GetComponent<BoxCollider>();
		if (col == null) return;
		originalCenter = col.center;
		originalSize = col.size;
		GetComponent<Item>().SlotConnected += OnSlotConnected;
		GetComponent<Item>().SlotDisconnected += OnSlotDisconnected;
	}

	private void OnSlotConnected()
	{
		if (col == null) return;
		col.center = attachedCenter;
		col.size = attachedSize;
	}

	private void OnSlotDisconnected()
	{
		if (col == null) return;
		col.center = originalCenter;
		col.size = originalSize;
	}
}
