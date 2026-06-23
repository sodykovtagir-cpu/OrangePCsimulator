using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using PC.Component;
using UnityEngine;

public class Generator : MonoBehaviour
{
	private List<Hardware> hardwares = new List<Hardware>();

	private void Awake()
    {
		GetComponent<Collider>().enabled = false;
		StartCoroutine("Wait");
    }

	private IEnumerator Wait()
	{
		yield return null;
		var col = GetComponent<Collider>();
		col.enabled = true;
	}

	private void OnTriggerEnter(Collider other)
    {
		if (other == null) return;
		var hw = other.GetComponent<Hardware>();
		if (!hw) return;
		hardwares.Add(hw);
		hw.Switch(true, true);
    }

	private void OnTriggerExit(Collider other)
	{
		if (other == null) return;
		var hw = other.GetComponent<Hardware>();
		if (!hw) return;
		hardwares.Remove(hw);
		hw.Switch(false, true);
	}

	private void OnDisable()
	{
		foreach (var item in hardwares)
		{
			if (item != null)
			{
				var hw = item as Hardware;
				hw.Switch(false, true);
			}
		}
		hardwares.Clear();
	}
}
