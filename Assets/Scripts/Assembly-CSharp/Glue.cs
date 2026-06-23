using UnityEngine;

public class Glue : MonoBehaviour
{
	private void OnCollisionEnter(Collision collision)
	{
		if (collision == null) return;
		var col = collision.collider;
		if (col == null) return;
		if (col.CompareTag("Glue")) return;
		if (!col.TryGetComponent<Item>(out var _a)) return;
		if (!col.TryGetComponent<FixedJoint>(out var joint)) return;
		joint.breakForce = float.PositiveInfinity;
		Destroy(gameObject);
	}
}
