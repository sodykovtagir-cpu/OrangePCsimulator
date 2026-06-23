using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class Overclock : App
	{
		[SerializeField]
		private Slider controlBar;

		[SerializeField]
		private Slider tempBar;

		[SerializeField]
		private Image warning;

		[SerializeField]
		private Text frequencyText;

		[SerializeField]
		private Text CPU_temp;

		private CPU cpu;

		private bool auto = true;

		private float delay;

		protected override void Start()
		{
			base.Start();
			var b = system != null ? system.Board : null;
			var list = b != null ? b.GetHardwares(0) : null;
			var c = list != null && list.Count > 0 ? list[0] as CPU : null;
			cpu = c;
			if (cpu == null) return;
			if (controlBar != null) controlBar.value = cpu.frequency;
			StartCoroutine(ShowInfo());
		}

		public void OnSliderChangedFrequency(float value)
		{
			if (cpu != null) cpu.frequency = value;
			if (frequencyText != null) frequencyText.text = value.ToString() + "GHz";
			delay = 0f;
		}

		public void OnToggleChangedAuto(bool value)
        {
			auto = value;
        }

		private IEnumerator ShowInfo()
		{
			float t = 0f;
			while (true)
			{
				if (auto && cpu != null)
				{
					if (cpu.temperature <= 90f)
					{
						if (warning != null) warning.enabled = false;
						if (cpu.temperature < 40f && controlBar != null)
						{
							controlBar.value = controlBar.value + UnityEngine.Time.deltaTime * 2f;
						}
					}
					else
					{
						if (controlBar != null)
						{
							controlBar.value = controlBar.value + UnityEngine.Time.deltaTime * ((cpu.temperature - 90f) / -10f);
						}
						if (warning != null) warning.enabled = true;
					}
				}

				if (delay >= 5f)
				{
					if (cpu != null && cpu.frequency >= 10f)
					{
						var ach = CloudOnceManager.Instance.GetAchievementFromId("maximum_power");
						if (ach != null) ach.Unlock(null);
					}
				}
				else
				{
					delay += UnityEngine.Time.deltaTime;
				}

				if (t >= 0.5f)
				{
					if (cpu != null && CPU_temp != null)
					{
						CPU_temp.text = cpu.temperature.ToString("0") + "°C";
					}
					t = 0f;
				}
				else
				{
					t += UnityEngine.Time.deltaTime;
				}

				if (cpu != null && tempBar != null)
				{
					tempBar.value = cpu.temperature / cpu.burnTemp;
				}

				yield return null;
			}
		}
	}
}
