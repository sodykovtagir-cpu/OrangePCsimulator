using PC.Component;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Breakable : MonoBehaviour
{
	[SerializeField]
	private float breakForce = 20f;

	[SerializeField]
	private AudioClip breakSound;

	private AudioSource source;

	private void Start()
	{
		source = GetComponent<AudioSource>();
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (collision != null && collision.collider != null)
		{
			float imp = collision.impulse.magnitude;
			if (imp > breakForce)
			{
				if (collision.gameObject.CompareTag("Pillow")) return;
				if (base.GetComponent<Hardware>() && base.GetComponent<Hardware>().Damaged) return;
				source.PlayOneShot(breakSound);
				GetComponent<Hardware>().Damage();
			}
		}
	}
}
