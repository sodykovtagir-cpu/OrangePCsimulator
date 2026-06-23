using UnityEngine;

public class BoxRemovalBomb : Bomb
{
	[SerializeField]
	private float range = 10f;

	public override void Explode()
	{
		Vector3 transform = gameObject.transform.position;
		Collider[] items = Physics.OverlapSphere(transform, range);
		foreach (Collider it in items)
		{
			if (!it.gameObject.CompareTag("Box")) continue;
			Destroy(it.gameObject);
		}
		base.Explode();
	}
}
