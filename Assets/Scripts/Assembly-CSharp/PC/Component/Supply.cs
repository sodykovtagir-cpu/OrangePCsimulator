using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace PC.Component
{
	public class Supply : Hardware
	{
		[SerializeField]
		private GameObject spark;

		[SerializeField]
		private bool sound;

		private AudioSource source;

		private FanSpin fan;

		protected override void Start()
        {
			source = GetComponent<AudioSource>();
			fan = GetComponent<FanSpin>();
			PowerChanged += UpdatePower;
        }

		private void UpdatePower(bool on)
        {
            if (on)
			{
				if (!sound || source == null)
        			return;
                if (!source.isPlaying)
                {
					StartCoroutine(nameof(FanSound));
                }
            }
        }

		private IEnumerator FanSound()
		{
			if (source == null || fan == null)
				yield break;

			source.Play();

			while (true)
			{
				if (fan == null || source == null)
					yield break;

				if (fan.Velocity <= 0f)
				{
					source.volume = 0f;
					source.Stop();
					yield break;
				}

				float value = fan.Velocity / fan.MaxSpeed;
				source.volume = value;
				source.pitch = value;

				yield return null;
			}
		}

		public void Overload()
		{
			base.Damage();

			var t = transform;
			if (t == null || spark == null)
				return;

			var rotation = spark.transform != null ? spark.transform.rotation : Quaternion.identity;
			var obj = Instantiate(spark, t.position, rotation, t);
			Destroy(obj, 4f);
		}
	}
}
