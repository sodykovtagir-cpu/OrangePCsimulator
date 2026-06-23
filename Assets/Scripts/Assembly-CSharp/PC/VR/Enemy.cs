using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;

namespace PC.VR
{
	public class Enemy : MonoBehaviour
	{
		[SerializeField]
		private float damageMultiplier = 200;	

		[SerializeField]
		private GameObject deadPrefab;

		[SerializeField]
		private GameObject explodePrefab;

		[SerializeField]
		private AudioClip hitSound;

		public static int count;

		private float health = 100;

		private Transform target;

		private NavMeshAgent agent;

		private Material mat;

		private AudioSource source;

		private bool inRange;

		private bool countDown;

		private bool isDie;

		private void Start()
		{
			var go = GameObject.FindGameObjectWithTag("Player");
			target = go.transform;
			agent = GetComponent<NavMeshAgent>();
			var rend = GetComponent<Renderer>();
			mat = rend.material;
			source = GetComponent<AudioSource>();
			count++;
		}

		private void Update()
		{
			if (target != null && agent != null)
			{
				agent.SetDestination(target.position);
			}
		}

		public void Hurt()
		{
			int dmg = UnityEngine.Random.Range(10, 20);
			health -= dmg;
			if (health <= 0f)
			{
				if (isDie) return;
				Die();
				var go = Instantiate(deadPrefab, transform.position, transform.rotation);
				Destroy(go, 2f);
				return;
			}
			if (source != null)
			{
				source.PlayOneShot(hitSound);
				StartCoroutine("HurtAnimation");
			}
		}

		private IEnumerator HurtAnimation()
		{
			int i = 0;
			var wait = new WaitForSeconds(0.1f);
			while (i <= 2)
			{
				if (mat == null) yield break;
				mat.SetColor("_LineColor", new Color(1f, 0f, 0f, 1f));
				yield return wait;
				mat.SetColor("_LineColor", Color.white);
				yield return wait;
				i++;
			}
		}

		public void ExplodeRange(bool enter)
		{
			inRange = enter;
			if (enter && !countDown)
			{
				StartCoroutine("CountDown");
				countDown = true;
			}
		}

		private IEnumerator CountDown()
		{
			if (source != null) source.Play();
			int i = 0;
			var wait = new WaitForSeconds(0.2f);
			while (i <= 4)
			{
				if (mat != null) mat.SetColor("_LineColor", new Color(0f, 0f, 0f, 1f));
				yield return wait;
				if (mat != null) mat.SetColor("_LineColor", Color.white);
				yield return wait;
				i++;
			}
			if (isDie) yield break;
			if (inRange && target != null)
			{
				var d = Vector3.Distance(transform.position, target.position);
				var f = Mathf.Clamp01(1f - d / 10f);
				VirtualWorld.Instance.Hurt(f * damageMultiplier);
			}
			var go = Instantiate(explodePrefab, transform.position, transform.rotation);
			Destroy(go, 2f);
			Die();
		}

		private void Die()
		{
			isDie = true;
			Destroy(gameObject);
			count--;
		}
	}
}
