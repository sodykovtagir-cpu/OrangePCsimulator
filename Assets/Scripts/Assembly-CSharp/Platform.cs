using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Rigidbody))]
public class Platform : SceneObject
{
	public Transform pointA;

	public Transform pointB;

	public float acceleration = 10f;

	public AudioClip elevatorStartSound;

	public AudioClip elevatorMovingSound;

	public AudioClip elevatorStopSound;

	public Material buttonMaterial;

	public bool goA;

	private AudioSource source;

	private Rigidbody rb;

	private bool moving;

	private float speed;

	private Transform player;

	private Coroutine coroutine;

	private void Awake()
    {
		source = GetComponent<AudioSource>();
		rb = GetComponent<Rigidbody>();
    }

	public void Move()
	{
		if (moving)
		{
			StopCoroutine(coroutine);
			Stop();
			return;
		}

		var targetTransform = goA ? pointA : pointB;
		if (targetTransform != null)
		{
			var routine = MoveTo(targetTransform.position);
			coroutine = StartCoroutine(routine);
			return;
		}
	}

	public void PlayerTriggerEnter(Collider other)
    {
		player = other.transform;
    }

	public void PlayerTriggerExit(Collider other)
    {
		player = null;
    }

	private IEnumerator MoveTo(Vector3 target)
	{
		source.clip = elevatorMovingSound;
		source.Play();
		source.PlayOneShot(elevatorStartSound);
		moving = true;
		rb.isKinematic = false;
		speed = 0f;
		buttonMaterial.EnableKeyword("_EMISSION");
		var lastPos = transform.position;
		while (true)
		{
			if (player != null)
			{
				var delta = transform.position - lastPos;
				player.Translate(delta);
			}
			Physics.SyncTransforms();
			lastPos = transform.position;
			var current = transform.position;
			if (Vector3.Distance(current, target) < 0.1f)
			{
				Stop();
				yield break;
			}
			var toTarget = target - current;
			var dist = toTarget.magnitude;
			var vMax = Mathf.Sqrt(2f * acceleration * dist);
			if (vMax < speed) speed = vMax;
			else if (vMax > speed) speed += acceleration * Time.fixedDeltaTime;
			var dir = dist > 1e-5f ? toTarget / dist : Vector3.zero;
			rb.velocity = dir * speed;
			yield return new WaitForFixedUpdate();
		}
	}

	private void Stop()
	{
		goA = !goA;
		moving = false;
		if (source != null)
		{
			source.Stop();
			source.PlayOneShot(elevatorStopSound);
		}
		if (rb != null) rb.isKinematic = true;
		if (buttonMaterial != null) buttonMaterial.DisableKeyword("_EMISSION");
	}

	private void OnDestroy()
    {
		buttonMaterial.DisableKeyword("_EMISSION");
    }

	public override void ToData(JObject jObject)
	{
		jObject["elv"] = JToken.FromObject(new { pos = new { transform.position.x, transform.position.y, transform.position.z}, goA });
	}

	public override void FromData(JObject jObject)
	{
		if (jObject == null) return;
		if (!jObject.TryGetValue("elv", out JToken elv)) return;
		var t = transform;
		var pos = elv["pos"];
		if (pos != null) t.position = pos.ToObject<Vector3>();
		goA = elv.Value<bool>("goA");
	}
}
