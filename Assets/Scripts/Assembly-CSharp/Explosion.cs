using System.Collections.Generic;
using UnityEngine;

public class Explosion : MonoBehaviour
{
	[SerializeField]
	private float explosionForce;

	[SerializeField]
	private float range;

	[SerializeField]
	private float damageRange;

	private void Start()
	{
		ApplyDamage();
		AddForce();
		Player.Instance.StartCoroutine("ShakeCamera");
		Destroy(this, 2);
	}

	private void AddForce()
	{
		var transform = GetComponent<Transform>();
		if (transform == null)
			return;

		Vector3 explosionPosition = transform.position;
		Collider[] colliders = Physics.OverlapSphere(explosionPosition, range);
		var rigidbodies = new List<Rigidbody>();

		if (colliders != null)
		{
			foreach (var collider in colliders)
			{
				if (collider == null)
					continue;

				Rigidbody rb = collider.attachedRigidbody;
				if (rb != null && !rigidbodies.Contains(rb))
				{
					rigidbodies.Add(rb);
				}
			}

			foreach (var rb in rigidbodies)
			{
				if (rb == null)
					continue;

				rb.AddExplosionForce(explosionForce, explosionPosition, range, 1.0f, ForceMode.Impulse);
			}
		}
	}

	private void ApplyDamage()
	{
		var transform = GetComponent<Transform>();
		if (transform == null)
			return;

		Vector3 position = transform.position;
		Collider[] colliders = Physics.OverlapSphere(position, damageRange);
		var handlers = new List<IExplosionDamageHandler>();

		if (colliders != null)
		{
			foreach (var collider in colliders)
			{
				if (collider == null)
					continue;

				var handler = collider.GetComponent<IExplosionDamageHandler>();
				if (handler != null && !handlers.Contains(handler))
				{
					handlers.Add(handler);
				}
			}

			foreach (var handler in handlers)
			{
				handler?.OnExplosionDamage();
			}
		}
	}
}
