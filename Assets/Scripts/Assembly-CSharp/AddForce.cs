using System.Collections.Generic;
using UnityEngine;

public class AddForce : MonoBehaviour
{
	[SerializeField]
	private Vector3 velocity;

	[SerializeField]
	private bool ignore;

	private Dictionary<GameObject, int> check = new Dictionary<GameObject, int>();

	private void OnTriggerStay(Collider other)
	{
		if (other == null) return;

		Rigidbody rb = other.GetComponent<Rigidbody>();
		if (rb != null)
		{
			rb.velocity = velocity;
		}
	}

	private void OnTriggerEnter(Collider other)
	{
		if (!ignore) return;
		if (other == null) return;

		GameObject obj = other.gameObject;
		if (obj == null) return;

		if (check.ContainsKey(obj))
		{
			check[obj] = check[obj] + 1;
		}
		else
		{
			check.Add(obj, 0);
			obj.layer = LayerMask.NameToLayer("Default");
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (!ignore) return;
		if (other == null) return;

		GameObject obj = other.gameObject;
		if (obj == null) return;

		if (!check.ContainsKey(obj))
		{
			check.Add(obj, 0);
			return;
		}

		int count = check[obj];
		if (count > 0)
		{
			check[obj] = count - 1;
		}
		else
		{
			check.Remove(obj);
			obj.layer = LayerMask.NameToLayer("Default");
		}
	}
}
