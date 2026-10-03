using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PC.Component.Software
{
	[Serializable]
	public class FlybyPhase
	{
		[Tooltip("Название фазы, отображаемое в правом верхнем углу окна")]
		public string phaseName = "Phase";

		[Tooltip("Длительность пролёта (в секундах)")]
		public float duration = 4f;

		[Tooltip("Множитель нагрузки на железо (влияет на FPS)")]
		public float loadMultiplier = 1f;

		[Header("Трансформы в сцене (наивысший приоритет)")]
		[Tooltip("Начальная точка пролёта в сцене")]
		public Transform startTransform;

		[Tooltip("Конечная точка пролёта в сцене")]
		public Transform endTransform;

		[Tooltip("Опциональная цель взгляда камеры")]
		public Transform lookAtTransform;

		[Header("Координаты со смещением (если трансформы не заданы)")]
		public Vector3 startOffset = new Vector3(-4f, 3f, -5f);
		public Vector3 endOffset = new Vector3(3f, 2f, 2f);
		public Vector3 lookAtOffset = new Vector3(0f, 0.2f, 0f);

		[Tooltip("Считать координаты относительно ПК/стола (true) или абсолютными мировыми (false)")]
		public bool isRelative = true;
	}

	public class ThreeDMork : App
	{
		[Header("Panels")]
		[SerializeField]
		private GameObject startPanel;

		[SerializeField]
		private GameObject testPanel;

		[SerializeField]
		private GameObject resultsPanel;

		[Header("Start Screen - Buttons")]
		[SerializeField]
		private Button buttonStart;

		[SerializeField]
		private Button buttonStartClose;

		[SerializeField]
		private Button buttonMenu;

		[Header("Start Screen - Comparison Leaderboard (other PCs)")]
		[SerializeField]
		private Text[] leaderboardRank;

		[SerializeField]
		private Text[] leaderboardName;

		[SerializeField]
		private Text[] leaderboardCpu;

		[SerializeField]
		private Text[] leaderboardGpu;

		[SerializeField]
		private Text[] leaderboardScore;

		[SerializeField]
		private Text[] leaderboardFps;

		[SerializeField]
		private Text leaderboardSelfRank;

		[SerializeField]
		private Text leaderboardSelfName;

		[SerializeField]
		private Text leaderboardSelfSpec;

		[SerializeField]
		private Text leaderboardSelfScore;

		[SerializeField]
		private Text leaderboardSelfFps;

		[SerializeField]
		private Text leaderboardAverage;

		[Header("Start Screen - Local Test History (this PC)")]
		[SerializeField]
		private Text[] historyIndex;

		[SerializeField]
		private Text[] historyDate;

		[SerializeField]
		private Text[] historyScore;

		[SerializeField]
		private Text[] historyFps;

		[SerializeField]
		private Text historyBest;

		[SerializeField]
		private Text historyEmpty;

		[Header("Start Screen - Current PC Summary")]
		[SerializeField]
		private Text hardwareTitle;

		[SerializeField]
		private Text hardwareCpu;

		[SerializeField]
		private Text hardwareGpu;

		[SerializeField]
		private Text hardwareRam;

		[SerializeField]
		private Text hardwareBoard;

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

		[Header("Stage / Room Environment (Отдельная комната / сцена)")]
		[Tooltip("Префаб отдельной комнаты/сцены бенчмарка. По умолчанию загружается из Resources/3DMork_Stage.")]
		[SerializeField]
		private GameObject stagePrefab;

		[Tooltip("Имя отдельной сцены Unity (если вы хотите загружать полноценную сцену аддитивно, например '3DMork_Room').")]
		[SerializeField]
		private string stageSceneName = "";

		[Tooltip("Позиция спавна изолированной комнаты в мире (по умолчанию далеко под картой, чтобы не пересекаться с мастерской).")]
		[SerializeField]
		private Vector3 stageSpawnPosition = new Vector3(0f, -2500f, 0f);

		[Header("Flyby Configuration (Настройки пролётов)")]
		[Tooltip("Список настраиваемых фаз пролёта камеры. Можно задать прямо в Инспекторе.")]
		[SerializeField]
		private List<FlybyPhase> flybyPhases = new List<FlybyPhase>();

		[Tooltip("Опциональный родительский объект с точками в сцене (например, '3DMork_Waypoints').")]
		[SerializeField]
		private Transform waypointsRoot;

		[Tooltip("Плавность поворота камеры (чем меньше значение, тем мягче и кинематографичнее поворот; рекомендуется 3-8).")]
		[Range(1f, 20f)]
		[SerializeField]
		private float rotationSmoothing = 5.5f;

		[Tooltip("Угол обзора камеры (FOV) во время бенчмарка.")]
		[Range(30f, 100f)]
		[SerializeField]
		private float cameraFov = 65f;

		private const int LeaderboardRows = 8;
		private const int HistoryRows = 5;
		private const int MaxHistoryEntries = 12;
		private const string HistoryPrefsKey = "3DMork_History";
		private const string BestScorePrefsKey = "3DMork_Score";

		[Serializable]
		private class HistoryData
		{
			public List<HistoryEntry> entries = new List<HistoryEntry>();
		}

		[Serializable]
		private class HistoryEntry
		{
			public int score;
			public int fps;
			public int graphics;
			public int physics;
			public int memory;
			public long unixTime;
		}

		private class LeaderboardEntry
		{
			public string pcName;
			public string cpuName;
			public string gpuName;
			public int score;
			public int fps;

			public LeaderboardEntry(string pc, string cpu, string gpu, int scoreValue, int fpsValue)
			{
				pcName = pc;
				cpuName = cpu;
				gpuName = gpu;
				score = scoreValue;
				fps = fpsValue;
			}
		}

		/// <summary>
		/// Эталонные результаты других машин (сортировка по убыванию счёта).
		/// Нужны для сравнительной таблицы на стартовом экране.
		/// </summary>
		private static readonly LeaderboardEntry[] ReferenceBenchmarks =
		{
			new LeaderboardEntry("Orange Workstation", "i9-13900K", "RTX 4090", 18420, 148),
			new LeaderboardEntry("TITAN X rig", "Ryzen 9 7950X", "RTX 4080", 16980, 131),
			new LeaderboardEntry("Gaming Beast", "Ryzen 7 7800X3D", "RX 7900 XTX", 15240, 119),
			new LeaderboardEntry("Studio Pro", "i7-13700K", "RTX 4070 Ti", 12960, 97),
			new LeaderboardEntry("Creator Mini", "i7-12700H", "RTX 4060", 9130, 74),
			new LeaderboardEntry("Home Cinema PC", "Ryzen 5 3600", "GTX 1660 SUPER", 6480, 54),
			new LeaderboardEntry("Office Workstation", "i5-10400", "GTX 1650", 4380, 38),
			new LeaderboardEntry("Budget King", "i3-12100F", "GTX 1060 6GB", 3115, 27)
		};

		private struct FlybySegment
		{
			public string name;
			public float duration;
			public float loadMultiplier;
			public Vector3 startPos;
			public Vector3 endPos;
			public Quaternion startRot;
			public Quaternion endRot;
			public Transform lookAtTransform;
			public Vector3 lookAtPos;
			public bool hasLookAt;
			public bool hasRotations;
			public bool isOrbit;
			public Vector3 orbitCenter;
			public float orbitRadius;
			public float orbitHeight;
			public float orbitStartAngle;
			public float orbitSweepAngle;
		}

		private struct HardwareSnapshot
		{
			public bool hasBoard;
			public string boardName;
			public string cpuName;
			public string gpuName;
			public int ramCapacity;
			public int gpuScore;
			public int ramScore;
			public float cpuScore;
			public int gpuCount;
			public int cpuCount;
			public int ramCount;
		}

		private Camera testCamera;
		private RenderTexture renderTexture;
		private Coroutine benchmarkCoroutine;
		private GameObject currentStageInstance;
		private bool isAdditiveSceneLoaded;

		protected override void Start()
		{
			base.Start();
			SetDefaultSize(new Vector2(850f, 520f));
			WireButtons();
			ShowStartScreen();
		}

		private void WireButtons()
		{
			if (buttonStart != null)
			{
				buttonStart.onClick.RemoveAllListeners();
				buttonStart.onClick.AddListener(RunBenchmark);
			}

			if (buttonStartClose != null)
			{
				buttonStartClose.onClick.RemoveAllListeners();
				buttonStartClose.onClick.AddListener(Close);
			}

			if (buttonMenu != null)
			{
				buttonMenu.onClick.RemoveAllListeners();
				buttonMenu.onClick.AddListener(ShowStartScreen);
			}

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
		}

		/// <summary>
		/// Показывает стартовый экран: сравнительная таблица других ПК,
		/// история тестов на этой машине и сводка по текущему железу.
		/// Вся разметка заранее собрана в префабе, скрипт только заполняет текст.
		/// </summary>
		public void ShowStartScreen()
		{
			if (benchmarkCoroutine != null)
			{
				StopCoroutine(benchmarkCoroutine);
				benchmarkCoroutine = null;
			}
			CleanupCamera();
			CleanupStage();

			if (resultsPanel != null) resultsPanel.SetActive(false);
			if (testPanel != null) testPanel.SetActive(false);
			if (startPanel != null) startPanel.SetActive(true);

			if (buttonStart != null) buttonStart.interactable = true;
			if (buttonStartClose != null) buttonStartClose.interactable = true;

			var snapshot = CollectHardware();
			RefreshHardwareSummary(snapshot);
			RefreshLeaderboard(snapshot);
			RefreshHistory();
		}

		private void RefreshHardwareSummary(HardwareSnapshot snapshot)
		{
			if (hardwareTitle != null)
			{
				hardwareTitle.text = snapshot.hasBoard
					? "THIS PC - " + snapshot.boardName
					: "THIS PC - NO MOTHERBOARD";
			}

			if (hardwareCpu != null)
			{
				hardwareCpu.text = snapshot.cpuCount > 0
					? "CPU: " + snapshot.cpuName
					: "CPU: not installed";
			}

			if (hardwareGpu != null)
			{
				hardwareGpu.text = snapshot.gpuCount > 0
					? "GPU: " + snapshot.gpuName
					: "GPU: not installed";
			}

			if (hardwareRam != null)
			{
				hardwareRam.text = snapshot.ramCount > 0
					? "RAM: " + FormatRam(snapshot.ramCapacity)
					: "RAM: not installed";
			}

			if (hardwareBoard != null)
			{
				hardwareBoard.text = snapshot.hasBoard
					? "BOARD: " + snapshot.boardName
					: "BOARD: build a PC to run the benchmark";
			}
		}

		private void RefreshLeaderboard(HardwareSnapshot snapshot)
		{
			int best = GetBestScore();

			for (int i = 0; i < LeaderboardRows; i++)
			{
				bool hasRow = i < ReferenceBenchmarks.Length;
				var entry = hasRow ? ReferenceBenchmarks[i] : null;

				SetCell(leaderboardRank, i, hasRow ? "#" + (i + 1) : "--", new Color(0.75f, 0.75f, 0.8f));
				SetCell(leaderboardName, i, hasRow ? entry.pcName : "--", new Color(1f, 1f, 1f));
				SetCell(leaderboardCpu, i, hasRow ? entry.cpuName : "--", new Color(0.82f, 0.85f, 0.9f));
				SetCell(leaderboardGpu, i, hasRow ? entry.gpuName : "--", new Color(0.82f, 0.85f, 0.9f));
				SetCell(leaderboardScore, i, hasRow ? entry.score.ToString() : "--", new Color(1f, 0.72f, 0.2f));
				SetCell(leaderboardFps, i, hasRow ? entry.fps.ToString() + " fps" : "--", new Color(0.55f, 0.9f, 0.6f));
			}

			if (leaderboardSelfRank != null) leaderboardSelfRank.text = "*";

			if (leaderboardSelfName != null)
			{
				leaderboardSelfName.text = snapshot.hasBoard ? snapshot.boardName : "This PC";
			}

			if (leaderboardSelfSpec != null)
			{
				string spec = (snapshot.cpuCount > 0 ? snapshot.cpuName : "no CPU") + "  /  " +
							  (snapshot.gpuCount > 0 ? snapshot.gpuName : "no GPU");
				leaderboardSelfSpec.text = spec;
			}

			if (leaderboardSelfScore != null)
			{
				leaderboardSelfScore.text = best > 0 ? best.ToString() : "--";
			}

			if (leaderboardSelfFps != null)
			{
				var history = LoadHistory();
				int lastFps = history.Count > 0 ? history[0].fps : 0;
				leaderboardSelfFps.text = lastFps > 0 ? lastFps.ToString() + " fps" : "--";
			}

			if (leaderboardAverage != null)
			{
				int sum = 0;
				for (int i = 0; i < ReferenceBenchmarks.Length; i++) sum += ReferenceBenchmarks[i].score;
				int avg = ReferenceBenchmarks.Length > 0 ? sum / ReferenceBenchmarks.Length : 0;
				leaderboardAverage.text = "Average of " + ReferenceBenchmarks.Length + " reference PCs: " + avg +
										  "   |   Your best: " + (best > 0 ? best.ToString() : "--");
			}
		}

		private void RefreshHistory()
		{
			var history = LoadHistory();

			for (int i = 0; i < HistoryRows; i++)
			{
				bool hasRow = i < history.Count && history[i] != null;
				int number = hasRow ? history.Count - i : 0;

				SetCell(historyIndex, i, hasRow ? "#" + number : "--", new Color(0.75f, 0.75f, 0.8f));
				SetCell(historyDate, i, hasRow ? FormatDate(history[i].unixTime) : "--", new Color(0.85f, 0.87f, 0.92f));
				SetCell(historyScore, i, hasRow ? history[i].score.ToString() : "--", new Color(1f, 0.72f, 0.2f));
				SetCell(historyFps, i, hasRow ? history[i].fps.ToString() + " fps" : "--", new Color(0.55f, 0.9f, 0.6f));
			}

			if (historyEmpty != null) historyEmpty.gameObject.SetActive(history.Count == 0);

			if (historyBest != null)
			{
				int best = GetBestScore();
				historyBest.text = best > 0
					? "Best score: " + best
					: "Best score: -- (no runs yet)";
			}
		}

		private static void SetCell(Text[] cells, int index, string value, Color color)
		{
			if (cells == null || index < 0 || index >= cells.Length) return;
			var cell = cells[index];
			if (cell == null) return;
			cell.text = value;
			cell.color = color;
		}

		private static string FormatDate(long unixSeconds)
		{
			try
			{
				var date = new DateTime(1970, 1, 1).AddSeconds(unixSeconds);
				return date.ToString("dd.MM.yy HH:mm");
			}
			catch
			{
				return "--";
			}
		}

		private static string FormatRam(int megabytes)
		{
			if (megabytes >= 1024)
			{
				return (megabytes / 1024f).ToString("0.#") + " GB";
			}
			return megabytes + " MB";
		}

		private static long NowUnix()
		{
			return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
		}

		private static int GetBestScore()
		{
			return PlayerPrefs.GetInt(BestScorePrefsKey, 0);
		}

		private static List<HistoryEntry> LoadHistory()
		{
			var result = new List<HistoryEntry>();
			string json = PlayerPrefs.GetString(HistoryPrefsKey, string.Empty);
			if (string.IsNullOrEmpty(json)) return result;

			try
			{
				var data = JsonUtility.FromJson<HistoryData>(json);
				if (data != null && data.entries != null)
				{
					for (int i = 0; i < data.entries.Count; i++)
					{
						if (data.entries[i] != null) result.Add(data.entries[i]);
					}
				}
			}
			catch (Exception)
			{
				result.Clear();
			}

			return result;
		}

		private static void PushHistory(int score, int fps, int graphics, int physics, int memory)
		{
			var history = LoadHistory();

			history.Insert(0, new HistoryEntry
			{
				score = score,
				fps = fps,
				graphics = graphics,
				physics = physics,
				memory = memory,
				unixTime = NowUnix()
			});

			while (history.Count > MaxHistoryEntries) history.RemoveAt(history.Count - 1);

			PlayerPrefs.SetString(HistoryPrefsKey, JsonUtility.ToJson(new HistoryData { entries = history }));

			if (GetBestScore() < score)
			{
				PlayerPrefs.SetInt(BestScorePrefsKey, score);
			}

			PlayerPrefs.Save();
		}

		private HardwareSnapshot CollectHardware()
		{
			var snapshot = new HardwareSnapshot();

			var board = system != null ? system.Board : null;
			snapshot.hasBoard = board != null;
			if (board != null) snapshot.boardName = board.gameObject.name;

			if (board != null)
			{
				var gpus = board.GetHardwares(HardwareType.GPU);
				if (gpus != null)
				{
					for (int i = 0; i < gpus.Count; i++)
					{
						var g = gpus[i];
						if (g == null || g.Damaged) continue;
						snapshot.gpuCount++;
						snapshot.gpuScore += g.Score;
						snapshot.gpuName = g.gameObject.name;
					}
				}

				var cpus = board.GetHardwares(HardwareType.CPU);
				if (cpus != null)
				{
					for (int i = 0; i < cpus.Count; i++)
					{
						var cpu = cpus[i] as CPU;
						if (cpu == null || cpu.Damaged) continue;
						snapshot.cpuCount++;
						snapshot.cpuScore += cpu.frequency * cpu.Score;
						snapshot.cpuName = cpu.gameObject.name;
					}
				}

				var rams = board.GetHardwares(HardwareType.RAM);
				if (rams != null)
				{
					for (int i = 0; i < rams.Count; i++)
					{
						var r = rams[i];
						if (r == null || r.Damaged) continue;
						snapshot.ramCount++;
						snapshot.ramScore += r.Score;
						snapshot.ramCapacity += r.Capacity;
					}
				}
			}

			if (snapshot.gpuScore == 0) snapshot.gpuScore = 200;
			if (snapshot.cpuScore <= 0f) snapshot.cpuScore = 2000f;
			if (snapshot.ramScore == 0) snapshot.ramScore = 1000;
			if (snapshot.ramCapacity == 0) snapshot.ramCapacity = 2048;

			return snapshot;
		}

		public void RunBenchmark()
		{
			if (benchmarkCoroutine != null)
			{
				StopCoroutine(benchmarkCoroutine);
				benchmarkCoroutine = null;
			}
			CleanupCamera();
			CleanupStage();
			benchmarkCoroutine = StartCoroutine(BenchmarkRoutine());
		}

		private Vector3 GetRoomAnchor()
		{
			// 1. Позиция материнской платы собранного ПК в реальном 3D-мире
			if (system != null && system.Board != null)
			{
				var bPos = system.Board.transform.position;
				if (bPos.sqrMagnitude > 0.01f && Mathf.Abs(bPos.x) < 500f && Mathf.Abs(bPos.y) < 500f)
					return bPos;
			}

			// 2. Стол в мастерской (из Main.unity)
			var table = GameObject.Find("Table");
			if (table != null)
			{
				return table.transform.position + new Vector3(0f, 1.2f, 0f);
			}

			// 3. Игрок
			var player = GameObject.FindGameObjectWithTag("Player");
			if (player != null)
			{
				return player.transform.position + new Vector3(0f, 1.0f, 0f);
			}

			// 4. Калиброванная точка мастерской по умолчанию
			return new Vector3(-8f, -2.5f, 7f);
		}

		private List<FlybySegment> BuildSegments(Vector3 anchor, Transform stageWaypointsRoot)
		{
			var segments = new List<FlybySegment>();

			// Способ 1: Точки из отдельной комнаты / сцены (stageWaypointsRoot) или waypointsRoot в сцене
			Transform sceneRoot = stageWaypointsRoot != null ? stageWaypointsRoot : waypointsRoot;
			if (sceneRoot == null)
			{
				var foundObj = GameObject.Find("3DMork_Waypoints") ?? GameObject.Find("3DMork_Flyby") ?? GameObject.Find("FlybyWaypoints");
				if (foundObj != null) sceneRoot = foundObj.transform;
			}

			if (sceneRoot != null && sceneRoot.childCount >= 2)
			{
				for (int i = 0; i < sceneRoot.childCount - 1; i++)
				{
					var pStart = sceneRoot.GetChild(i);
					var pEnd = sceneRoot.GetChild(i + 1);

					var wp = pStart.GetComponent<FlybyWaypoint>();
					string pName = wp != null && !string.IsNullOrEmpty(wp.phaseName)
						? wp.phaseName
						: string.Format("Scene {0}: Benchmark Flyby", i + 1);
					float pDuration = wp != null ? wp.duration : 4f;
					float pLoad = wp != null ? wp.loadMultiplier : 1f;

					var seg = new FlybySegment
					{
						name = pName,
						duration = Mathf.Max(1f, pDuration),
						loadMultiplier = pLoad,
						startPos = pStart.position,
						endPos = pEnd.position,
						startRot = pStart.rotation,
						endRot = pEnd.rotation,
						hasRotations = true
					};

					if (wp != null && wp.lookAtTarget != null)
					{
						seg.lookAtTransform = wp.lookAtTarget;
						seg.hasLookAt = true;
					}

					segments.Add(seg);
				}
				if (segments.Count > 0) return segments;
			}

			// Способ 2: Настроенные фазы в инспекторе префаба (flybyPhases)
			if (flybyPhases != null && flybyPhases.Count > 0)
			{
				for (int i = 0; i < flybyPhases.Count; i++)
				{
					var phase = flybyPhases[i];
					if (phase == null) continue;

					var seg = new FlybySegment
					{
						name = string.IsNullOrEmpty(phase.phaseName) ? string.Format("Scene {0}", i + 1) : phase.phaseName,
						duration = Mathf.Max(1f, phase.duration),
						loadMultiplier = phase.loadMultiplier
					};

					if (phase.startTransform != null && phase.endTransform != null)
					{
						seg.startPos = phase.startTransform.position;
						seg.endPos = phase.endTransform.position;
						seg.startRot = phase.startTransform.rotation;
						seg.endRot = phase.endTransform.rotation;
						seg.hasRotations = true;
					}
					else
					{
						Vector3 basePos = phase.isRelative ? anchor : Vector3.zero;
						seg.startPos = basePos + phase.startOffset;
						seg.endPos = basePos + phase.endOffset;
					}

					if (phase.lookAtTransform != null)
					{
						seg.lookAtTransform = phase.lookAtTransform;
						seg.hasLookAt = true;
					}
					else if (phase.lookAtOffset != Vector3.zero)
					{
						seg.lookAtPos = (phase.isRelative ? anchor : Vector3.zero) + phase.lookAtOffset;
						seg.hasLookAt = true;
					}

					segments.Add(seg);
				}
				if (segments.Count > 0) return segments;
			}

			// Способ 3: Автоматический кинематографический облёт мастерской по умолчанию
			segments.Add(new FlybySegment
			{
				name = "Scene 1: Tech Showcase Flyby",
				duration = 4f,
				loadMultiplier = 0.95f,
				startPos = anchor + new Vector3(-5.2f, 3.2f, -5.5f),
				endPos = anchor + new Vector3(3.8f, 2.0f, 2.5f),
				lookAtPos = anchor + new Vector3(0f, 0.2f, 0f),
				hasLookAt = true
			});

			segments.Add(new FlybySegment
			{
				name = "Scene 2: Hardware & Geometry Test",
				duration = 4f,
				loadMultiplier = 0.85f,
				startPos = anchor + new Vector3(2.8f, 0.75f, 2.2f),
				endPos = anchor + new Vector3(-2.8f, 0.5f, 1.2f),
				lookAtPos = anchor + new Vector3(0.2f, -0.1f, -0.1f),
				hasLookAt = true
			});

			segments.Add(new FlybySegment
			{
				name = "Scene 3: Dynamic Lighting Test",
				duration = 4f,
				loadMultiplier = 1.15f,
				isOrbit = true,
				orbitCenter = anchor + new Vector3(0f, 0.2f, 0f),
				orbitRadius = 4.2f,
				orbitHeight = 1.4f,
				orbitStartAngle = 0f,
				orbitSweepAngle = 135f,
				lookAtPos = anchor + new Vector3(0f, 0.1f, 0f),
				hasLookAt = true
			});

			return segments;
		}

		private IEnumerator BenchmarkRoutine()
		{
			if (buttonClose != null) buttonClose.interactable = false;
			if (buttonRun != null) buttonRun.interactable = false;
			if (buttonStart != null) buttonStart.interactable = false;
			if (buttonMenu != null) buttonMenu.interactable = false;

			if (startPanel != null) startPanel.SetActive(false);
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

			// Проверка железа ПК
			var board = system != null ? system.Board : null;
			if (board != null && board.StressGraphics())
			{
				CleanupCamera();
				CleanupStage();
				ShowStartScreen();
				yield break;
			}

			var hardware = CollectHardware();
			int gpuRawScore = hardware.gpuScore;
			float cpuRawScore = hardware.cpuScore;
			int ramScore = hardware.ramScore;
			int ramCapacity = hardware.ramCapacity;

			// Расчёт базового FPS для собранного ПК
			float cpuFactor = Mathf.Clamp(cpuRawScore / 8000f, 0.45f, 1.4f);
			float ramFactor = Mathf.Clamp(ramCapacity / 8192f, 0.5f, 1.3f);
			float baseFps = Mathf.Max(9f, (gpuRawScore / 42f) * cpuFactor * ramFactor);

			// Создаём отдельную комнату/сцену для бенчмарка
			Transform stageWaypoints = null;
			Vector3 anchor = stageSpawnPosition;

			if (!string.IsNullOrEmpty(stageSceneName))
			{
				var loadOp = SceneManager.LoadSceneAsync(stageSceneName, LoadSceneMode.Additive);
				if (loadOp != null)
				{
					while (!loadOp.isDone) yield return null;
					isAdditiveSceneLoaded = true;
					var sceneWp = GameObject.Find("3DMork_Waypoints") ?? GameObject.Find("Waypoints");
					if (sceneWp != null) stageWaypoints = sceneWp.transform;
					anchor = Vector3.zero;
				}
			}
			else
			{
				var prefab = stagePrefab;
				if (prefab == null) prefab = Resources.Load<GameObject>("3DMork_Stage");
				if (prefab != null)
				{
					currentStageInstance = Instantiate(prefab, stageSpawnPosition, Quaternion.identity);
					var wpObj = currentStageInstance.transform.Find("Waypoints") ?? currentStageInstance.transform.Find("3DMork_Waypoints");
					if (wpObj != null) stageWaypoints = wpObj;
				}
				else
				{
					anchor = GetRoomAnchor();
				}
			}

			// Создаём RenderTexture для окна бенчмарка
			renderTexture = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
			renderTexture.name = "3DMork_RT";
			renderTexture.filterMode = FilterMode.Bilinear;
			renderTexture.Create();

			if (viewportImage != null)
			{
				viewportImage.texture = renderTexture;
				viewportImage.color = Color.white;
			}

			// Создаём независимую тестовую камеру
			var camObj = new GameObject("3DMork_Camera");
			testCamera = camObj.AddComponent<Camera>();
			testCamera.targetTexture = renderTexture;
			testCamera.depth = 100;
			testCamera.nearClipPlane = 0.1f;
			testCamera.farClipPlane = 150f;
			testCamera.fieldOfView = cameraFov;
			testCamera.clearFlags = CameraClearFlags.Skybox;
			testCamera.backgroundColor = new Color(0.10f, 0.10f, 0.14f, 1f);
			// Включаем геометрию мира и свет, исключаем UI (слой 5), игрока (слой 10) и будку превью (слой 11)
			testCamera.cullingMask = ~((1 << 5) | (1 << 10) | (1 << 11));
			testCamera.enabled = false; // Рендерим явно через testCamera.Render()

			List<FlybySegment> segments = BuildSegments(anchor, stageWaypoints);

			float totalDuration = 0f;
			for (int i = 0; i < segments.Count; i++) totalDuration += segments[i].duration;
			if (totalDuration <= 0f) totalDuration = 16f;

			float elapsed = 0f;
			bool isFirstFrame = true;

			for (int segIdx = 0; segIdx < segments.Count; segIdx++)
			{
				var seg = segments[segIdx];
				if (sceneInfoText != null) sceneInfoText.text = seg.name;

				float segTime = 0f;
				while (segTime < seg.duration)
				{
					if (board != null && board.StressGraphics())
					{
						CleanupCamera();
						CleanupStage();
						ShowStartScreen();
						yield break;
					}

					float dt = Time.deltaTime;
					segTime += dt;
					elapsed += dt;

					float st = Mathf.Clamp01(segTime / seg.duration);
					float ease = st * st * (3f - 2f * st); // Плавная S-кривая

					if (camObj != null)
					{
						Vector3 targetPos;
						if (seg.isOrbit)
						{
							float currentAngle = Mathf.Lerp(seg.orbitStartAngle, seg.orbitStartAngle + seg.orbitSweepAngle, ease) * Mathf.Deg2Rad;
							targetPos = seg.orbitCenter + new Vector3(Mathf.Cos(currentAngle) * seg.orbitRadius, seg.orbitHeight, Mathf.Sin(currentAngle) * seg.orbitRadius);
						}
						else
						{
							targetPos = Vector3.Lerp(seg.startPos, seg.endPos, ease);
						}
						camObj.transform.position = targetPos;

						// Расчёт плавного поворота камеры
						Quaternion targetRot;
						if (seg.hasLookAt)
						{
							Vector3 lookPoint = seg.lookAtTransform != null ? seg.lookAtTransform.position : seg.lookAtPos;
							Vector3 lookDir = lookPoint - targetPos;
							if (lookDir.sqrMagnitude > 0.001f)
							{
								targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
							}
							else
							{
								targetRot = camObj.transform.rotation;
							}
						}
						else if (seg.hasRotations)
						{
							targetRot = Quaternion.Slerp(seg.startRot, seg.endRot, ease);
						}
						else
						{
							Vector3 fwd = seg.endPos - seg.startPos;
							if (fwd.sqrMagnitude > 0.001f)
							{
								targetRot = Quaternion.LookRotation(fwd, Vector3.up);
							}
							else
							{
								targetRot = camObj.transform.rotation;
							}
						}

						// Первый кадр ориентируем сразу, далее мягко сглаживаем поворот (Slerp)
						if (isFirstFrame)
						{
							camObj.transform.rotation = targetRot;
							isFirstFrame = false;
						}
						else
						{
							camObj.transform.rotation = Quaternion.Slerp(camObj.transform.rotation, targetRot, dt * rotationSmoothing);
						}

						// Принудительный рендер кадра в RenderTexture
						if (testCamera != null && renderTexture != null)
						{
							testCamera.Render();
						}
					}

					// Расчёт FPS, зависящего от виртуального железа
					float jitter = Mathf.Sin(Time.time * 7f) * (baseFps * 0.04f) + UnityEngine.Random.Range(-1.5f, 1.5f);
					float liveFps = Mathf.Max(5f, baseFps * seg.loadMultiplier + jitter);

					if (fpsText != null) fpsText.text = "FPS: " + Mathf.RoundToInt(liveFps).ToString();
					if (testProgressBar != null) testProgressBar.value = Mathf.Clamp01(elapsed / totalDuration);

					yield return null;
				}
			}

			// Очистка камеры и отдельной сцены/комнаты после завершения облёта
			CleanupCamera();
			CleanupStage();

			// Подсчёт очков 3DMork
			int graphicsScore = Mathf.RoundToInt(gpuRawScore * 3.2f + (ramScore * 0.15f) + UnityEngine.Random.Range(-40, 40));
			graphicsScore = Mathf.Max(250, graphicsScore);

			int physicsScore = Mathf.RoundToInt((cpuRawScore / 1000f) * 1450f + (ramCapacity * 0.1f) + UnityEngine.Random.Range(-30, 30));
			physicsScore = Mathf.Max(300, physicsScore);

			int memoryScore = Mathf.RoundToInt((ramScore * 1.8f) + (ramCapacity * 0.25f) + UnityEngine.Random.Range(-25, 25));
			memoryScore = Mathf.Max(250, memoryScore);

			int fpsScore = Mathf.RoundToInt(baseFps * 100f);

			int totalScore = Mathf.RoundToInt((graphicsScore * 0.60f) + (physicsScore * 0.25f) + (memoryScore * 0.15f));
			totalScore = Mathf.Max(200, totalScore);

			// Сохраняем прогон в локальную историю этого ПК
			PushHistory(totalScore, Mathf.RoundToInt(baseFps), graphicsScore, physicsScore, memoryScore);

			// Переход на экран результатов
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

			// Анимация кольца и общего счёта
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
			if (buttonMenu != null) buttonMenu.interactable = true;

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

		private void CleanupStage()
		{
			if (currentStageInstance != null)
			{
				Destroy(currentStageInstance);
				currentStageInstance = null;
			}
			if (isAdditiveSceneLoaded && !string.IsNullOrEmpty(stageSceneName))
			{
				SceneManager.UnloadSceneAsync(stageSceneName);
				isAdditiveSceneLoaded = false;
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
			CleanupStage();
		}

		private void OnDestroy()
		{
			if (benchmarkCoroutine != null)
			{
				StopCoroutine(benchmarkCoroutine);
				benchmarkCoroutine = null;
			}
			CleanupCamera();
			CleanupStage();
		}

		public override void Close()
		{
			CleanupCamera();
			CleanupStage();
			base.Close();
		}
	}
}
