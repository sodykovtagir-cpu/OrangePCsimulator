using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PC.Component
{
	public class CameraDevice : Device
	{

		[SerializeField]
		private float fps;

		[SerializeField]
		private Camera cam;

		public Camera Cam
		{
			get
			{
				return cam;
			}
			private set
            {
				cam = value;
            }
		}

		private IEnumerator Capture()
		{
			yield return new WaitForSeconds(2f);
			var delay = new WaitForSeconds(1f / fps);
			while (true)
			{
				cam.Render();
				yield return delay;
			}
		}

		public override void OnDeviceStart()
        {
			StartCoroutine("Capture");
        }

		public override void OnDeviceStop()
        {
			StopCoroutine("Capture");
        }
	}
}
