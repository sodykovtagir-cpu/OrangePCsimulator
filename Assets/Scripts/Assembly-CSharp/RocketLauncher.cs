using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RocketLauncher : MonoBehaviour
{
	public ParticleSystem muzzle;

	public Rigidbody prefab;

	public Transform pivot;

	public int times = 16;

	public float interval = 1f;

	public float force = 1f;

	public float reactionForce = 1f;

	public AudioClip shootSound;

	private AudioSource source;

	private Rigidbody rb;

	private void Start()
    {
		source = GetComponent<AudioSource>();
		rb = GetComponent<Rigidbody>();
    }

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.O))
			StartCoroutine(Shoot());
	}

	public IEnumerator Shoot()
	{
		for (int i = 0; i < times; i++)
		{
			if (source != null) source.PlayOneShot(shootSound);
			if (rb != null) rb.AddForce(transform.up * -reactionForce, ForceMode.Impulse);
			if (muzzle != null) muzzle.Play();
			var t = pivot != null ? pivot : transform;
			var proj = Instantiate(prefab, t.position, t.rotation);
			if (proj != null) proj.AddForce(transform.up * force, ForceMode.Impulse);
			yield return new WaitForSeconds(interval);
		}
	}
}
