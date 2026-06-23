using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class Benchmark : App
	{
		[SerializeField]
		private Slider[] marks;

		[SerializeField]
		private Image mark;

		[SerializeField]
		private Button button_close;

		[SerializeField]
		private Text[] text_marks;

		[SerializeField]
		private Text text_mark;

		protected override void Start()
		{
			base.Start();
			StartCoroutine(ShowScore());
		}

		private IEnumerator ShowScore()
		{
			if (button_close != null) button_close.interactable = false;
			if (text_mark != null) text_mark.text = "0";
			if (mark != null) mark.fillAmount = 0f;
			if (marks != null)
			{
				for (int i = 0; i < marks.Length; i++)
				{
					var s = marks[i];
					if (s == null) continue;
					s.value = 0f;
					s.maxValue = 5000f;
					if (text_marks != null && i < text_marks.Length && text_marks[i] != null) text_marks[i].text = "";
				}
			}

			yield return new WaitForSeconds(2f);

			int score = 0;
			float range = 5000f;
			var scores = new int[4];
			scores[1] = 500;

			var b = system != null ? system.Board : null;
			if (b != null)
			{
				var cpus = b.GetHardwares(HardwareType.CPU);
				if (cpus != null)
				{
					for (int i = 0; i < cpus.Count; i++)
					{
						var h = cpus[i];
						var cpu = h as CPU;
						if (cpu != null) scores[0] += Mathf.FloorToInt(cpu.frequency * cpu.Score);
					}
				}

				var gpus = b.GetHardwares(HardwareType.GPU);
				if (gpus != null)
				{
					for (int i = 0; i < gpus.Count; i++)
					{
						var h = gpus[i];
						if (h != null) scores[1] += h.Score;
					}
				}

				var rams = b.GetHardwares(HardwareType.RAM);
				if (rams != null)
				{
					for (int i = 0; i < rams.Count; i++)
					{
						var h = rams[i];
						if (h != null) scores[2] += h.Score;
					}
				}
			}

			var storages = system != null ? system.AllStorage : null;
			if (storages != null)
			{
				for (int i = 0; i < storages.Count; i++)
				{
					var st = storages[i];
					if (st != null) scores[3] += st.Score;
				}
			}

			for (int i = 0; i < scores.Length && marks != null && i < marks.Length; i++)
			{
				var slider = marks[i];
				if (slider == null) continue;

				int target = scores[i] - UnityEngine.Random.Range(0, 400);
				score += target;

				float t = 0f;
				while (!UnityEngine.Mathf.Approximately(slider.value, target))
				{
					t += UnityEngine.Time.deltaTime;
					if (t > 1f) t = 1f;
					if (t < 0f) t = 0f;
					float f = t * t * (3f - 2f * t);
					float v = UnityEngine.Mathf.Lerp(0f, target, f);

					if (v > range)
					{
						range = v;
						for (int k = 0; k < marks.Length; k++)
						{
							var m = marks[k];
							if (m != null) m.maxValue = range;
						}
					}

					slider.value = v;
					if (text_marks != null && i < text_marks.Length && text_marks[i] != null) text_marks[i].text = v.ToString("0");
					yield return null;
				}

				yield return new WaitForSeconds(1f);
			}

			var main = Main.Instance;
//			if (main != null && !main.example)
//			{
//				var lb = main.hardcore ? CloudOnce.Leaderboards.s_leaderboard_highscore_hardcore : CloudOnce.Leaderboards.s_leaderboard_highscore;
//				if (lb != null) lb.SubmitScore(score, null);
//			}

			if (PlayerPrefs.GetInt("Score") < score) PlayerPrefs.SetInt("Score", score);

			float t2 = 0f;
			while (t2 < 1f)
			{
				t2 += Time.deltaTime * 0.5f;
				float tt = t2;
				if (tt > 1f) tt = 1f;
				if (tt < 0f) tt = 0f;
				float e = tt * tt * (3f - 2f * tt);
				if (mark != null) mark.fillAmount = e;
				if (text_mark != null) text_mark.text = Mathf.Lerp(0f, score, e).ToString("0");
				yield return null;
			}

			if (button_close != null) button_close.interactable = true;
		}
	}
}
