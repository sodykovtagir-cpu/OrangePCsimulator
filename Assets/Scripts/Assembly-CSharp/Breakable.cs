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
        if (SaveManager.RestoringSave || Time.time < SaveManager.IgnoreLoadImpactsUntil)
            return;
		if (collision != null && collision.collider != null)
		{
			float imp = collision.impulse.magnitude;
			if (imp > breakForce)
			{
                // Only rare high-impulse impacts need this hierarchy lookup.
                // A light cable joint settling must not break a monitor.
                if (collision.collider.GetComponentInParent<PC.Component.Cabling.CableItem>() != null)
                    return;
				if (collision.gameObject.CompareTag("Pillow")) return;
				if (base.GetComponent<Hardware>() && base.GetComponent<Hardware>().Damaged) return;
				source.PlayOneShot(breakSound);
				GetComponent<Hardware>().Damage();
			}
		}
	}
}
