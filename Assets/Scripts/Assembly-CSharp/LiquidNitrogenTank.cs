using UnityEngine;

public class LiquidNitrogenTank : Item
{
	[SerializeField]
	private HingeJoint cover;

	[SerializeField]
	private ParticleSystem particle;

	[SerializeField]
	private GameObject refillRange;

	private void Update()
	{
		var h = cover;
		if (h == null) return;
		var p = particle;
		if (p == null) return;

		float angle = h.angle;
		bool emitting = p.isEmitting;

		if (angle <= 1f)
		{
			if (!emitting) return;
			p.Stop();
			if (refillRange != null) refillRange.SetActive(false);
		}
		else
		{
			if (emitting) return;
			p.Play();
			if (refillRange != null) refillRange.SetActive(true);
		}
	}
}
