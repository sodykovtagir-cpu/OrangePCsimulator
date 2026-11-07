using UnityEngine;

public class Impact : MonoBehaviour
{
	[SerializeField]
	private float impactThreshold;

	[SerializeField]
	private float volumeMultiplier;

	[SerializeField]
	private AudioClip impactSound;

	[SerializeField]
	private ParticleSystem particle;

	private AudioSource source;
    private float nextSoundTime;

	private void Awake()
	{
		source = GetComponent<AudioSource>();

		// Impact FX — только по столкновению. Если на префабе/инстансе случайно
		// оставлен Play On Awake, перья/искры не должны сыпаться сразу при
		// загрузке комнаты или спавне предмета.
		if (particle != null)
		{
			var main = particle.main;
			if (main.playOnAwake)
			{
				main.playOnAwake = false;
				particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			}
		}
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (collision == null)
			return;

		Vector3 relativeVelocity = collision.relativeVelocity;
		float velocityMagnitude = relativeVelocity.magnitude;

		if (velocityMagnitude < impactThreshold)
			return;

        // Do not stack dozens of collision clips when a heavy case rattles
        // against cable joints/furniture. The collision and its physical
        // response are untouched; only the one-shot audio is rate-limited.
        if (source != null && impactSound != null && Time.time >= nextSoundTime)
        {
            nextSoundTime = Time.time + .12f;
            source.PlayOneShot(impactSound,
                Mathf.Clamp01(velocityMagnitude * volumeMultiplier));
        }

		if (particle != null)
		{
			Transform particleTransform = particle.transform;
			ContactPoint[] contacts = collision.contacts;
			if (contacts != null && contacts.Length > 0)
			{
				Vector3 normal = contacts[0].normal;
				Quaternion rotation = Quaternion.LookRotation(normal);

				if (particleTransform != null)
					particleTransform.rotation = rotation;

				particle.Play();
			}
		}
	}
}
