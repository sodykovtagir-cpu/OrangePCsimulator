using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using PC.Component;
using UnityEngine;

public class Dustbin : MonoBehaviour
{
	[SerializeField]
	private Transform cover;

	private Dictionary<Rigidbody, int> rubbish = new Dictionary<Rigidbody, int>();

	private bool detecting;


	private void OnTriggerEnter(Collider other)
	{
		if (other == null) return;
		if (other.CompareTag("Player"))
		{
			var main = Main.Instance;
			if (main != null)
			{
				if (!main.hardcore)
				{
					var t = other.transform;
					if (t != null)
						StartCoroutine(ResetItem(t));
				}

				CloudOnceManager.Instance.GetAchievementFromId("forget_yourself")?.Unlock(null);
			}
			return;
		}

		if (other.transform == cover) return;
		if (rubbish == null) return;

		var rb = other.attachedRigidbody;
		if (rb == null) return;

		if (rubbish.TryGetValue(rb, out var count))
			rubbish[rb] = count + 1;
		else
			rubbish.Add(rb, 1);
	}

	private void OnTriggerExit(Collider other)
	{
		if (other == null) return;

		if (other.CompareTag("Player"))
		{
			var main = Main.Instance;
			if (main == null) return;

			if (main.hardcore)
				CloudOnceManager.Instance.GetAchievementFromId("you_cannot_catch_me")?.Unlock(null);

			return;
		}

		if (rubbish == null) return;

		var rb = other.attachedRigidbody;
		if (rb == null) return;

		if (rubbish.TryGetValue(rb, out var count))
		{
			if (count < 2)
				rubbish.Remove(rb);
			else
				rubbish[rb] = count - 1;
		}
	}

	private void Update()
	{
		if (cover == null) return;

		if (cover.localRotation.x < -0.01f)
		{
			detecting = true;
			return;
		}

		if (!detecting) return;

		if (rubbish != null && rubbish.Count > 0)
		{
			var keys = new List<Rigidbody>(rubbish.Keys);

			foreach (var rb in keys)
			{
				if (rb == null) continue;

				if (rb.CompareTag("Motherboard"))
				{
					var display = rb.GetComponent<Motherboard>().monitor;
					display?.DisconnectBoard();
					Destroy(rb.gameObject);
					continue;
				}

				if (rb.CompareTag("Monitor"))
				{
					var display = rb.GetComponent<PC.Component.Display>();
					display?.DisconnectBoard();
					Destroy(rb.gameObject);
					continue;
				}

				if (!rb.TryGetComponent<Seat>(out var seat) || !seat.enabled)
				{
					Destroy(rb.gameObject);
				}
			}

			rubbish.Clear();
		}

		detecting = false;
	}

	private IEnumerator ResetItem(Transform t) { 
		yield return new WaitForSeconds(5f); 
		if (t == null) yield break; 
		t.position = new Vector3(-5.26f, -2.75f, 2.91f); 
		Physics.SyncTransforms(); 
	}
}
