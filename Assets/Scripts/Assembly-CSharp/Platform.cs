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

	// Материал «нажатой» кнопки лифта. Если назначен — кнопки реально меняют
	// цвет, а не только keyword _EMISSION (который может быть вырезан из билда).
	public Material buttonOnMaterial;

	public bool goA;

	private AudioSource source;

	private Rigidbody rb;

	private Renderer[] buttonRenderers;

	private bool moving;

	private float speed;

	private Transform player;

	private Coroutine coroutine;

	private void Awake()
    {
		source = GetComponent<AudioSource>();
		rb = GetComponent<Rigidbody>();
		CacheButtonRenderers();
		SetButtonLit(false);
    }

	private void CacheButtonRenderers()
	{
		var list = new List<Renderer>();
		if (buttonMaterial != null)
		{
			foreach (var root in gameObject.scene.GetRootGameObjects())
			{
				foreach (var r in root.GetComponentsInChildren<Renderer>(true))
				{
					if (r != null && r.sharedMaterial == buttonMaterial)
						list.Add(r);
				}
			}
		}
		buttonRenderers = list.ToArray();
	}

	private void SetButtonLit(bool lit)
	{
		// Надёжный визуальный отклик: меняем материал кнопок на «включённый».
		if (buttonOnMaterial != null && buttonRenderers != null)
		{
			var mat = lit ? buttonOnMaterial : buttonMaterial;
			foreach (var r in buttonRenderers)
			{
				if (r != null && mat != null)
					r.sharedMaterial = mat;
			}
		}

		// Совместимость со старыми сценами, где назначен только buttonMaterial.
		if (buttonMaterial != null)
		{
			if (lit) buttonMaterial.EnableKeyword("_EMISSION");
			else buttonMaterial.DisableKeyword("_EMISSION");
		}
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
		SetButtonLit(true);
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
		SetButtonLit(false);
	}

	private void OnDestroy()
    {
		SetButtonLit(false);
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
