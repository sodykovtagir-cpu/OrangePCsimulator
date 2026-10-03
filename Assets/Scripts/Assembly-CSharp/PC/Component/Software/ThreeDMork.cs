using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class ThreeDMork : App
	{
		[Header("Panels")]
		[SerializeField]
		private GameObject testPanel;

		[SerializeField]
		private GameObject resultsPanel;

		[Header("3D Viewport")]
		[SerializeField]
		private RawImage viewportImage;

		[SerializeField]
		private Text fpsText;

		[SerializeField]
		private Text sceneInfoText;

		[SerializeField]
		private Slider testProgressBar;

		[Header("Results Screen")]
		[SerializeField]
		private Text textTotalScore;

		[SerializeField]
		private Image markCircle;

		[SerializeField]
		private Slider[] marks;

		[SerializeField]
		private Text[] text_marks;

		[SerializeField]
		private Button buttonClose;

		[SerializeField]
		private Button buttonRun;

		private Camera testCamera;
		private RenderTexture renderTexture;
		private Coroutine benchmarkCoroutine;

		protected override void Start()
		{
			base.Start();
			SetDefaultSize(new Vector2(850f, 520f));

			if (buttonClose != null)
			{
				buttonClose.onClick.RemoveAllListeners();
				buttonClose.onClick.AddListener(Close);
			}

			if (buttonRun != null)
			{
				buttonRun.onClick.RemoveAllListeners();
				buttonRun.onClick.AddListener(RunBenchmark);
			}

			RunBenchmark();
		}

		private void OnDisable()
		{
			if (testCamera != null) testCamera.enabled = false;
		}

		private void OnEnable()
		{
			if (testCamera != null) testCamera.enabled = true;
		}

		public void RunBenchmark()
		{
			if (benchmarkCoroutine != null)
			{
				StopCoroutine(benchmarkCoroutine);
				benchmarkCoroutine = null;
			}
			CleanupCamera();
			benchmarkCoroutine = StartCoroutine(BenchmarkRoutine());
		}

		private IEnumerator BenchmarkRoutine()
		{
			if (buttonClose != null) buttonClose.interactable = false;
			if (buttonRun != null) buttonRun.interactable = false;

			if (resultsPanel != null) resultsPanel.SetActive(false);
			if (testPanel != null) testPanel.SetActive(true);

			if (fpsText != null) fpsText.text = "FPS: --";
			if (sceneInfoText != null) sceneInfoText.text = "Initializing 3DMork...";
			if (testProgressBar != null)
			{
				testProgressBar.minValue = 0f;
				testProgressBar.maxValue = 1f;
				testProgressBar.value = 0f;
			}

			// Проверяем железо
			var board = system != null ? system.Board : null;
			if (board != null && board.StressGraphics())
			{
				CleanupCamera();
				yield break;
			}

			int gpuRawScore = 0;
			if (board != null)
			{
				var gpus = board.GetHardwares(HardwareType.GPU);
				if (gpus != null)
				{
					for (int i = 0; i < gpus.Count; i++)
					{
						var g = gpus[i];
						if (g != null && !g.Damaged) gpuRawScore += g.Score;
					}
				}
			}
			if (gpuRawScore == 0) gpuRawScore = 200;

			float cpuRawScore = 0f;
			if (board != null)
			{
				var cpus = board.GetHardwares(HardwareType.CPU);
				if (cpus != null)
				{
					for (int i = 0; i < cpus.Count; i++)
					{
						var cpu = cpus[i] as CPU;
						if (cpu != null && !cpu.Damaged)
							cpuRawScore += cpu.frequency * cpu.Score;
					}
				}
			}
			if (cpuRawScore == 0f) cpuRawScore = 2000f;

			int ramScore = 0;
			int ramCapacity = 0;
			if (board != null)
			{
				var rams = board.GetHardwares(HardwareType.RAM);
				if (rams != null)
				{
					for (int i = 0; i < rams.Count; i++)
					{
						var r = rams[i];
						if (r != null && !r.Damaged)
						{
							ramScore += r.Score;
							ramCapacity += r.Capacity;
						}
					}
				}
			}
			if (ramScore == 0) ramScore = 1000;
			if (ramCapacity == 0) ramCapacity = 2048;

			// Расчёт базового FPS игрового ПК
			float cpuFactor = Mathf.Clamp(cpuRawScore / 8000f, 0.45f, 1.4f);
			float ramFactor = Mathf.Clamp(ramCapacity / 8192f, 0.5f, 1.3f);
			float baseFps = Mathf.Max(9f, (gpuRawScore / 42f) * cpuFactor * ramFactor);

			// Создаём RenderTexture и тестовую камеру
			renderTexture = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
			renderTexture.name = "3DMork_RT";
			renderTexture.Create();

			if (viewportImage != null)
			{
				viewportImage.texture = renderTexture;
				viewportImage.color = Color.white;
			}

			var camObj = new GameObject("3DMork_Camera");
			testCamera = camObj.AddComponent<Camera>();
			testCamera.targetTexture = renderTexture;
			testCamera.depth = -50;
			testCamera.nearClipPlane = 0.1f;
			testCamera.farClipPlane = 200f;
			testCamera.fieldOfView = 65f;
			testCamera.cullingMask = ~((1 << 5) | (1 << 2)); // игнорируем UI и Raycast
			testCamera.clearFlags = CameraClearFlags.Skybox;

			Vector3 anchor = Vector3.zero;
			if (system != null)
			{
				anchor = system.transform.position;
			}
			else
			{
				var player = GameObject.FindGameObjectWithTag("Player");
				if (player != null) anchor = player.transform.position;
			}

			// Три сцены тестирования (общая длительность ~12 секунд)
			const float sceneDuration = 4f;
			const float totalDuration = 12f;
			float elapsed = 0f;

			for (int sceneIdx = 0; sceneIdx < 3; sceneIdx++)
			{
				string sceneName;
				float loadMultiplier;
				Vector3 startPos;
				Vector3 endPos;
				Vector3 lookTarget;

				if (sceneIdx == 0)
				{
					sceneName = "Scene 1: Workshop Flyby";
					loadMultiplier = 0.92f;
					startPos = anchor + new Vector3(-7f, 3.8f, -5f);
					endPos = anchor + new Vector3(5f, 2.8f, 4f);
					lookTarget = anchor + new Vector3(0f, -0.2f, 0f);
				}
				else if (sceneIdx == 1)
				{
					sceneName = "Scene 2: Geometry & Details Test";
					loadMultiplier = 0.82f;
					startPos = anchor + new Vector3(4.5f, 0.9f, 3f);
					endPos = anchor + new Vector3(-3.5f, 0.6f, 2.2f);
					lookTarget = anchor + new Vector3(0.3f, 0.1f, -0.2f);
				}
				else
				{
					sceneName = "Scene 3: Dynamic Lighting Test";
					loadMultiplier = 1.12f;
					startPos = anchor + new Vector3(-4.5f, 1.8f, 0f);
					endPos = anchor + new Vector3(4.5f, 1.8f, 0f);
					lookTarget = anchor + Vector3.up * 0.2f;
				}

				if (sceneInfoText != null) sceneInfoText.text = sceneName;

				float sceneTime = 0f;
				while (sceneTime < sceneDuration)
				{
					if (board != null && board.StressGraphics())
					{
						CleanupCamera();
						yield break;
					}

					float dt = Time.deltaTime;
					sceneTime += dt;
					elapsed += dt;

					float st = Mathf.Clamp01(sceneTime / sceneDuration);
					float ease = st * st * (3f - 2f * st);

					if (camObj != null)
					{
						if (sceneIdx == 2)
						{
							// Круговой облёт в третьей сцене
							float angle = (st * 140f) * Mathf.Deg2Rad;
							camObj.transform.position = anchor + new Vector3(Mathf.Cos(angle) * 4.5f, 1.8f, Mathf.Sin(angle) * 4.5f);
						}
						else
						{
							camObj.transform.position = Vector3.Lerp(startPos, endPos, ease);
						}
						camObj.transform.LookAt(lookTarget);
					}

					// Флуктуации FPS, зависящие от железа
					float jitter = Mathf.Sin(Time.time * 7f) * (baseFps * 0.04f) + UnityEngine.Random.Range(-1.5f, 1.5f);
					float liveFps = Mathf.Max(5f, baseFps * loadMultiplier + jitter);

					if (fpsText != null) fpsText.text = "FPS: " + Mathf.RoundToInt(liveFps).ToString();
					if (testProgressBar != null) testProgressBar.value = Mathf.Clamp01(elapsed / totalDuration);

					yield return null;
				}
			}

			// Завершение тестирования камеры
			CleanupCamera();

			// Расчёт результатов 3DMork
			int graphicsScore = Mathf.RoundToInt(gpuRawScore * 3.2f + (ramScore * 0.15f) + UnityEngine.Random.Range(-40, 40));
			graphicsScore = Mathf.Max(250, graphicsScore);

			int physicsScore = Mathf.RoundToInt((cpuRawScore / 1000f) * 1450f + (ramCapacity * 0.1f) + UnityEngine.Random.Range(-30, 30));
			physicsScore = Mathf.Max(300, physicsScore);

			int memoryScore = Mathf.RoundToInt((ramScore * 1.8f) + (ramCapacity * 0.25f) + UnityEngine.Random.Range(-25, 25));
			memoryScore = Mathf.Max(250, memoryScore);

			int fpsScore = Mathf.RoundToInt(baseFps * 100f);

			int totalScore = Mathf.RoundToInt((graphicsScore * 0.60f) + (physicsScore * 0.25f) + (memoryScore * 0.15f));
			totalScore = Mathf.Max(200, totalScore);

			// Переключение на экран результатов
			if (testPanel != null) testPanel.SetActive(false);
			if (resultsPanel != null) resultsPanel.SetActive(true);

			if (textTotalScore != null) textTotalScore.text = "0";
			if (markCircle != null) markCircle.fillAmount = 0f;

			var scores = new int[4] { graphicsScore, physicsScore, memoryScore, fpsScore };
			float maxSliderRange = 10000f;

			if (marks != null)
			{
				for (int i = 0; i < marks.Length; i++)
				{
					if (marks[i] == null) continue;
					marks[i].value = 0f;
					marks[i].maxValue = maxSliderRange;
					if (text_marks != null && i < text_marks.Length && text_marks[i] != null)
						text_marks[i].text = "0";
				}
			}

			yield return new WaitForSeconds(0.4f);

			// Анимация шкал категорий
			for (int i = 0; i < scores.Length && marks != null && i < marks.Length; i++)
			{
				var slider = marks[i];
				if (slider == null) continue;

				int target = scores[i];
				if (target > maxSliderRange)
				{
					maxSliderRange = target * 1.15f;
					for (int k = 0; k < marks.Length; k++)
					{
						if (marks[k] != null) marks[k].maxValue = maxSliderRange;
					}
				}

				float t = 0f;
				while (!Mathf.Approximately(slider.value, target))
				{
					t += Time.deltaTime * 1.5f;
					float f = Mathf.Clamp01(t);
					f = f * f * (3f - 2f * f);
					float v = Mathf.Lerp(0f, target, f);
					slider.value = v;

					if (text_marks != null && i < text_marks.Length && text_marks[i] != null)
					{
						if (i == 3)
							text_marks[i].text = (v / 100f).ToString("0.0") + " FPS";
						else
							text_marks[i].text = v.ToString("0");
					}

					yield return null;
				}

				yield return new WaitForSeconds(0.2f);
			}

			// Сохраняем лучший результат 3DMork
			if (PlayerPrefs.GetInt("3DMork_Score", 0) < totalScore)
			{
				PlayerPrefs.SetInt("3DMork_Score", totalScore);
				PlayerPrefs.Save();
			}

			// Анимация главного круга и общего балла
			float tScore = 0f;
			while (tScore < 1f)
			{
				tScore += Time.deltaTime * 0.8f;
				float tt = Mathf.Clamp01(tScore);
				float e = tt * tt * (3f - 2f * tt);
				if (markCircle != null) markCircle.fillAmount = e;
				if (textTotalScore != null) textTotalScore.text = Mathf.Lerp(0f, totalScore, e).ToString("0");
				yield return null;
			}

			if (buttonClose != null) buttonClose.interactable = true;
			if (buttonRun != null) buttonRun.interactable = true;

			benchmarkCoroutine = null;
		}

		private void CleanupCamera()
		{
			if (testCamera != null)
			{
				Destroy(testCamera.gameObject);
				testCamera = null;
			}
			if (renderTexture != null)
			{
				renderTexture.Release();
				Destroy(renderTexture);
				renderTexture = null;
			}
		}

		public override void OnSystemStop()
		{
			base.OnSystemStop();
			if (benchmarkCoroutine != null)
			{
				StopCoroutine(benchmarkCoroutine);
				benchmarkCoroutine = null;
			}
			CleanupCamera();
		}

		private void OnDestroy()
		{
			if (benchmarkCoroutine != null)
			{
				StopCoroutine(benchmarkCoroutine);
				benchmarkCoroutine = null;
			}
			CleanupCamera();
		}

		public override void Close()
		{
			CleanupCamera();
			base.Close();
		}
	}
}
