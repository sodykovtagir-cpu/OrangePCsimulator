using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

public class AssemblyLine : MonoBehaviour
{	
	public float interval;

	public float moveDuration =2.0f;

	public AnimationCurve speedCurve = AnimationCurve.Linear(0, 1, 1, 1);

	public GameObject product;

	public Transform productParent;

	public Vector3 moveDir;

	public ScrollingUV_Layers conveyor;

	public ConveyorBelt conveyor2;

	private IEnumerator Start()
	{
		while (true)
		{
			if (conveyor != null) conveyor.enabled = true;
			if (conveyor2 != null) conveyor2.enabled = true;

			yield return new WaitForSeconds(1f);

			float time = 0f;
			while (time < 1f)
			{
				time += Time.deltaTime / moveDuration;
				float s = speedCurve != null ? speedCurve.Evaluate(time) : 0f;

				if (conveyor != null) conveyor.uvAnimationRate = new Vector2(s * 0.3f, 0f);
				if (conveyor2 != null) conveyor2.moveDir = new Vector3(-3f * s, 0f, 0f);

				yield return null;
			}

			if (conveyor != null) conveyor.enabled = false;
			if (conveyor2 != null) conveyor2.enabled = false;

			if (product != null) Instantiate(product, productParent);
		}
	}

	private void Update()
    {
		return;
    }

	public void OnTriggerEnterConveyor(Collider other)
	{
		if (other == null) return;
		if (!other.CompareTag("Case")) return;
		var c = other.GetComponent<PC.Component.Case>();
		if (c == null) return;
		c.UpdatePower(true);
		Destroy(other.gameObject, 1f);
	}
}
