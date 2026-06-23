using UnityEngine;

public class Destruction : MonoBehaviour, IExplosionDamageHandler
{
	[SerializeField]
	private GameObject spawn;

	[SerializeField]
	private AudioClip clip;

	[SerializeField]
	private float force = 10;

	[SerializeField]
	private int destroyTime = 10;

	private bool played;

	private void OnCollisionEnter(Collision collision)
	{
		if (collision.impulse.magnitude < force) return;
		Break();
	}

	private void Break()
	{
		if (played) return;played = true;
		Vector3 pos = transform.position;
		Quaternion rot = transform.rotation;
		Destroy(gameObject);
		var origi = spawn;
		GameObject ins = Instantiate(origi, pos, rot);
		if (destroyTime > 0)
		{
			Destroy(ins, destroyTime);
		}
		if (clip != null)
		{
			AudioSource src = PlayClipAtPoint(clip, pos);
			src.minDistance = 4f;
		}
		OnBreak();
	}

	protected virtual void OnBreak() {}

	private AudioSource PlayClipAtPoint(AudioClip clip, Vector3 pos)
	{
		GameObject audioobj = new GameObject("One shot audio");
		Transform abtrans = audioobj.transform;
		abtrans.position = pos;
		AudioSource sr = audioobj.AddComponent<AudioSource>();
		sr.clip = clip;
		sr.spatialBlend = 1;
		sr.Play();
		Destroy(audioobj, clip.length);
		return sr;
	}

	public void OnExplosionDamage()
	{
		Break();
	}
}
