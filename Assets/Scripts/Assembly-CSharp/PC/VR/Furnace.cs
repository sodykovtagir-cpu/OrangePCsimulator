using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PC.VR
{
	public class Furnace : MonoBehaviour
	{
		[SerializeField]
		private ParticleSystem fire;

		[SerializeField]
		private Transform wheel;

		private int fuel;

		private Vector3 direction;

		private void OnTriggerEnter(Collider other)
		{
			if (fire == null) return;
			if (!fire.isPlaying) fire.Play();

			fuel++;
			if (fuel < 3)
			{
				direction = new Vector3(0f, Time.deltaTime * (fuel * 50f), 0f);
			}
			else
			{
				VirtualWorld.Instance.EnoughFuel();
			}

			if (other == null) return;
			var rend = other.gameObject.GetComponent<Renderer>();
			if (rend == null) return;
			var mat = rend.material;
			StartCoroutine(ChangeColor(mat));
		}

		private void Update()
		{
			if (wheel != null) wheel.Rotate(direction);
		}

		private IEnumerator ChangeColor(Material mat)
		{
			if (mat == null) yield break;
			var start = mat.GetColor("_LineColor");
			var target = new Color(1f, 0f, 0f, 1f);
			float t = 0f;
			while (t < 1f)
			{
				t += Time.deltaTime;
				mat.SetColor("_LineColor", Color.Lerp(start, target, t));
				yield return null;
			}
			mat.SetColor("_LineColor", target);
		}
	}
}
