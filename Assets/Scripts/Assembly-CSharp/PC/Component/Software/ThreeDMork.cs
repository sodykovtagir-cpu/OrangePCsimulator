using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
		// 3DMork strings are localized, see tools/localize_3dmork_source.py
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
		private Text leaderboardSelfCpu;
		private Text leaderboardSelfGpu;

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
		private Text hardwareDrive;

		[Header("3D Viewport")]
		[SerializeField]
		private RawImage viewportImage;

		[SerializeField]
		private Text fpsText;

		[SerializeField]
		private Text resolutionText;

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
		private const string FormulaPrefsKey = "3DMork_Formula";
		/// <summary>Версия формулы очков. Меняется вместе с пересчётом результатов.</summary>
		private const int FormulaVersion = 2;

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
			public string pcKey;
			public string cpuName;
			public string gpuName;
			public int gpuScore;
			public float cpuScore;
			public int ramScore;
			public int driveScore;

			public LeaderboardEntry(string pcKey, string cpu, string gpuLabel,
				int gpuRaw, float cpuRaw, int ramRaw, int driveRaw)
			{
				this.pcKey = pcKey;
				cpuName = cpu;
				gpuName = gpuLabel;
				gpuScore = gpuRaw;
				cpuScore = cpuRaw;
				ramScore = ramRaw;
				driveScore = driveRaw;
			}

			/// <summary>
			/// Счёт эталонной машины считается той же формулой, что и для игрового ПК,
			/// поэтому таблица сравнения не может разойтись с реальными прогонами.
			/// </summary>
			public BenchmarkScores ScoresAt(float fpsCap)
			{
				return ComputeScores(gpuScore, cpuScore, ramScore, driveScore, fpsCap);
			}
		}

		/// <summary>
		/// Референсные максимумы игрового железа (значения из ассетов):
		/// это детали сборки мечты - первой строки таблицы сравнения:
		/// 2 x RTX 5090, RMD Ryzen 9 7950X (3 ГГц), 2 x 64 ГБ (RGB),
		/// SSD 16 ТБ + M.2 8 ТБ. Сборка мечты набирает потолок во всех
		/// категориях, а ПК игрока не может превзойти эти потолки.
		/// </summary>
		private const float RefGpuScore = 27000f;
		private const float RefCpuScore = 3645f;
		private const float RefRamScore = 24000f;
		private const float RefDriveScore = 18000f;

		/// <summary>Потолки категорий: максимум, который набирает эталонная сборка.</summary>
		private const float GraphicsCeiling = 160000f;
		private const float PhysicsCeiling = 50000f;
		private const float MemoryCeiling = 32000f;
		private const float FpsScoreFactor = 130f;
		private const float TotalGraphicsWeight = 0.60f;
		private const float TotalPhysicsWeight = 0.24f;
		private const float TotalMemoryWeight = 0.10f;
		private const float TotalFpsWeight = 0.06f;

		/// <summary>Условия эталонного замера - без ограничения настройками игры.</summary>
		private const float ReferenceFpsCap = 240f;

		/// <summary>Опорное разрешение, для которого считается пропускная способность железа.</summary>
		private const float ReferencePixels = 1280f * 720f;

		/// <summary>Разрешения рендера теста, от большего к меньшему.</summary>
		private static readonly Vector2Int[] BenchmarkResolutions =
		{
			new Vector2Int(1920, 1080),
			new Vector2Int(1600, 900),
			new Vector2Int(1280, 720),
			new Vector2Int(960, 540),
			new Vector2Int(640, 360)
		};

		private struct BenchmarkScores
		{
			public int graphics;
			public int physics;
			public int memory;
			public int fps;
			public int total;
			public float machineFps;
			public int achievedFps;
		}

		/// <summary>
		/// Эталонные результаты других машин (сортировка по убыванию счёта).
		/// Каждая строка - настоящая сборка из деталей игры: видеокарты,
		/// процессор, память и накопители подобраны друг к другу, а видеокарты
		/// в паре всегда одинаковые - "RTX 5090 + RTX 3090" никто не собирает.
		/// Сверху стоит сборка
		/// мечты - в каждой категории там самая мощная деталь.
		/// Название - ключ перевода, чтобы таблица читалась на любом языке.
		/// Список сборок продублирован в tools/3dmork_builds.py вместе с
		/// проверкой баланса, тесты сверяют оба источника.
		/// </summary>
		private static readonly LeaderboardEntry[] ReferenceBenchmarks =
		{
			//                       ключ перевода              процессор       видеокарты                    CPU сырой  GPU  RAM  накопители
			new LeaderboardEntry("3DMork build 1", "RMD Ryzen 9 7950X", "2x RTX 5090", 27000, 3645f, 24000, 18000),
			new LeaderboardEntry("3DMork build 2", "i9-9900K", "2x RTX 4080 Ti", 22000, 3214f, 20000, 17000),
			new LeaderboardEntry("3DMork build 3", "i9-7900X", "2x RTX 4080", 20000, 2974f, 12000, 14000),
			new LeaderboardEntry("3DMork build 4", "Xeon E5-2689", "2x RTX 3080 Ti", 14000, 2776f, 10000, 13000),
			new LeaderboardEntry("3DMork build 5", "i7-8700K", "2x RTX 3080", 12000, 2622f, 10000, 11000),
			new LeaderboardEntry("3DMork build 6", "i5-8400", "2x RTX 2080 Ti", 11000, 2328f, 9000, 10000),
			new LeaderboardEntry("3DMork build 7", "i3-8300", "2x GTX 1080 Ti", 9000, 2219f, 6000, 8500),
			new LeaderboardEntry("3DMork build 8", "Celeron G3920", "2x GTX 1060", 6000, 1949f, 4000, 1700)
		};

		/// <summary>Мощность процессора для бенчмарка.</summary>
		/// <remarks>
		/// Раньше здесь была произведённая на частоту величина (frequency * Score),
		/// из-за чего разогнанный i3-8300 (3.7 ГГц) считался мощнее нового
		/// i5-8400 (2.8 ГГц) - гонка частоты ломала порядок поколений.
		/// Теперь основной вес даёт Score компонента, а частота добавляет
		/// не более 15% поправки.
		/// </remarks>
		private static float CpuPower(CPU cpu)
		{
			float frequencyFactor = 0.85f + 0.15f * Mathf.Clamp(cpu.frequency / 3.5f, 0.5f, 1.2f);
			return cpu.Score * frequencyFactor;
		}

		/// <summary>Потолок кадров в секунду, выбранный игроком в настройках игры.</summary>
		private static float GetFpsCap()
		{
			int limit = Application.targetFrameRate;
			if (limit <= 0) limit = GraphicsBootstrap.TargetFps;
			return Mathf.Clamp(limit, 30, 240);
		}

		/// <summary>Сколько кадров в секунду выдаёт собранный ПК сам по себе (на 1280x720).</summary>
		private static float ComputeMachineFps(int gpuScore, float cpuScore, int ramScore)
		{
			float gpuPower = Mathf.Clamp01(gpuScore / RefGpuScore);
			float cpuPower = Mathf.Clamp01(cpuScore / RefCpuScore);
			float ramPower = Mathf.Clamp01(ramScore / RefRamScore);
			float throughput = 18f + 780f * gpuPower;
			return throughput * (0.55f + 0.45f * cpuPower) * (0.70f + 0.30f * ramPower);
		}

		/// <summary>
		/// Очки по категориям и общий счёт. Формула общая для прогона игрока
		/// и для эталонной таблицы, поэтому числа всегда сопоставимы.
		/// </summary>
		private static BenchmarkScores ComputeScores(int gpuScore, float cpuScore, int ramScore,
			int driveScore, float fpsCap)
		{
			float gpuPower = Mathf.Clamp01(gpuScore / RefGpuScore);
			float cpuPower = Mathf.Clamp01(cpuScore / RefCpuScore);
			float ramPower = Mathf.Clamp01(ramScore / RefRamScore);
			float drivePower = Mathf.Clamp01(driveScore / RefDriveScore);

			var result = new BenchmarkScores();
			result.graphics = Mathf.RoundToInt(GraphicsCeiling * Mathf.Pow(gpuPower, 1.10f) * (0.82f + 0.18f * cpuPower));
			result.physics = Mathf.RoundToInt(PhysicsCeiling * Mathf.Pow(cpuPower, 1.05f) * (0.85f + 0.15f * ramPower));
			result.memory = Mathf.RoundToInt(MemoryCeiling * Mathf.Pow(ramPower, 1.05f) * (0.85f + 0.15f * drivePower));

			result.machineFps = ComputeMachineFps(gpuScore, cpuScore, ramScore);
			result.achievedFps = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(result.machineFps, fpsCap)));
			result.fps = Mathf.RoundToInt(result.achievedFps * FpsScoreFactor);
			result.total = Mathf.RoundToInt(
				result.graphics * TotalGraphicsWeight +
				result.physics * TotalPhysicsWeight +
				result.memory * TotalMemoryWeight +
				result.fps * TotalFpsWeight);
			result.total = Mathf.Max(1, result.total);
			return result;
		}

		/// <summary>
		/// Разрешение рендера теста: самое большое, которое железо тянет
		/// с потолком кадров из настроек игры.
		/// </summary>
		private static Vector2Int PickRenderResolution(float machineFps, float fpsCap)
		{
			for (int i = 0; i < BenchmarkResolutions.Length; i++)
			{
				var size = BenchmarkResolutions[i];
				float fps = machineFps * (ReferencePixels / (size.x * (float)size.y));
				if (fps >= fpsCap) return size;
			}
			return BenchmarkResolutions[BenchmarkResolutions.Length - 1];
		}

		/// <summary>Потолок шкалы категории - эталонный максимум по этой категории.</summary>
		private static float GetCategoryCeiling(int index)
		{
			switch (index)
			{
				case 0: return GraphicsCeiling;
				case 1: return PhysicsCeiling;
				case 2: return MemoryCeiling;
				default: return ReferenceFpsCap * FpsScoreFactor;
			}
		}

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
			public string gpuName2;
			public string driveName;
			public string driveName2;
			public int ramCapacity;
			public int gpuScore;
			public int ramScore;
			public float cpuScore;
			public int driveScore;
			public int gpuCount;
			public int cpuCount;
			public int ramCount;
			public int driveCount;
		}

		private Camera testCamera;
		private RenderTexture renderTexture;
		private Coroutine benchmarkCoroutine;
		private GameObject currentStageInstance;
		private bool isAdditiveSceneLoaded;
		private int liveFps = -1;
		private string liveResolution = string.Empty;
		private string currentSceneName = string.Empty;

		protected override void Start()
		{
			base.Start();
			SetDefaultSize(new Vector2(722.5f, 408.5f));
			ResetStaleResults();
			WireButtons();
			Localization.LanguageChanged += OnLanguageChanged;
			ShowStartScreen();
		}

		/// <summary>
		/// Прогоны, сделанные по прошлой формуле, показывались рядом с новыми
		/// и выглядели как ошибка: тот же ПК получал 1059 FPS и 117669 очков.
		/// При смене формулы старая история и рекорд удаляются.
		/// </summary>
		private void ResetStaleResults()
		{
			if (PlayerPrefs.GetInt(FormulaPrefsKey, 0) == FormulaVersion) return;

			PlayerPrefs.DeleteKey(HistoryPrefsKey);
			PlayerPrefs.DeleteKey(BestScorePrefsKey);
			PlayerPrefs.SetInt(FormulaPrefsKey, FormulaVersion);
			PlayerPrefs.Save();
		}

		/// <summary>Перевод строки приложения, ключ берётся из Resources/Translate.txt.</summary>
		private static string Tr(string key)
		{
			return Localization.GetText(key);
		}

		/// <summary>Перевод строки с одним подставляемым значением.</summary>
		private static string Format(string key, object value)
		{
			string pattern = Localization.GetText(key);
			return pattern.Contains("{0}") ? string.Format(pattern, value) : pattern;
		}

		/// <summary>Перевод строки с тремя подставляемыми значениями.</summary>
		private static string Format(string key, object a, object b, object c)
		{
			string pattern = Localization.GetText(key);
			return pattern.Contains("{0}") ? string.Format(pattern, a, b, c) : pattern;
		}

		private void RefreshTestTexts()
		{
			if (fpsText != null)
			{
				fpsText.text = Format("3DMork fps", liveFps >= 0 ? liveFps.ToString() : "--");
			}

			if (sceneInfoText != null)
			{
				sceneInfoText.text = string.IsNullOrEmpty(currentSceneName)
					? Tr("3DMork initialising")
					: currentSceneName;
			}

			if (resolutionText != null)
			{
				resolutionText.text = string.IsNullOrEmpty(liveResolution)
					? Format("3DMork resolution", "--")
					: Format("3DMork resolution", liveResolution);
			}
		}

		/// <summary>Смена языка перерисовывает подписи, не прерывая тест.</summary>
		private void OnLanguageChanged()
		{
			if (startPanel != null && startPanel.activeSelf)
			{
				var snapshot = CollectHardware();
				RefreshHardwareSummary(snapshot);
				RefreshLeaderboard(snapshot);
				RefreshHistory();
			}

			RefreshTestTexts();
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
					? Format("3DMork hardware title", snapshot.boardName)
					: Tr("3DMork hardware no board");
			}

			if (hardwareCpu != null)
			{
				hardwareCpu.text = Format("3DMork hardware cpu", snapshot.cpuCount > 0
					? snapshot.cpuName
					: Tr("3DMork hardware missing"));
			}

			if (hardwareGpu != null)
			{
				hardwareGpu.text = Format("3DMork hardware gpu", snapshot.gpuCount > 0
					? JoinPair(snapshot.gpuName, snapshot.gpuName2)
					: Tr("3DMork hardware missing"));
			}

			if (hardwareRam != null)
			{
				hardwareRam.text = Format("3DMork hardware ram", snapshot.ramCount > 0
					? FormatRam(snapshot.ramCapacity)
					: Tr("3DMork hardware missing"));
			}

			if (hardwareDrive != null)
			{
				hardwareDrive.text = Format("3DMork hardware drive", snapshot.driveCount > 0
					? JoinPair(snapshot.driveName, snapshot.driveName2)
					: Tr("3DMork hardware missing"));
			}
		}

		private void RefreshLeaderboard(HardwareSnapshot snapshot)
		{
			int best = GetBestScore();

			// Эталонные машины считаются по той же формуле и с тем же потолком
			// кадров, что и прогон игрока, поэтому таблица всегда сопоставима.
			float fpsCap = GetFpsCap();
			int sum = 0;

			for (int i = 0; i < LeaderboardRows; i++)
			{
				bool hasRow = i < ReferenceBenchmarks.Length;
				var entry = hasRow ? ReferenceBenchmarks[i] : null;
				var scores = hasRow ? entry.ScoresAt(fpsCap) : new BenchmarkScores();

				SetCell(leaderboardRank, i, hasRow ? "#" + (i + 1) : "--", new Color(0.75f, 0.75f, 0.8f));
				SetCell(leaderboardName, i, hasRow ? Tr(entry.pcKey) : "--", new Color(1f, 1f, 1f));
				SetCell(leaderboardCpu, i, hasRow ? entry.cpuName : "--", new Color(0.82f, 0.85f, 0.9f));
				SetCell(leaderboardGpu, i, hasRow ? entry.gpuName : "--", new Color(0.82f, 0.85f, 0.9f));
				SetCell(leaderboardScore, i, hasRow ? scores.total.ToString() : "--", new Color(1f, 0.72f, 0.2f));
				SetCell(leaderboardFps, i, hasRow ? scores.achievedFps.ToString() : "--", new Color(0.55f, 0.9f, 0.6f));

				if (hasRow) sum += scores.total;
			}

			if (leaderboardSelfRank != null) leaderboardSelfRank.text = "*";

			if (leaderboardSelfName != null)
			{
				leaderboardSelfName.text = snapshot.hasBoard ? snapshot.boardName : Tr("3DMork self pc");
			}

			// Своя строка повторяет колонки таблицы: процессор в колонке ЦП,
			// видеокарты в колонке ВИДЕО. Раньше обе детали писались одной
			// строкой через " / ", и длинная сборка налезала на счёт.
			if (leaderboardSelfCpu != null)
			{
				leaderboardSelfCpu.text = snapshot.cpuCount > 0 ? snapshot.cpuName : Tr("3DMork no cpu");
			}

			if (leaderboardSelfGpu != null)
			{
				leaderboardSelfGpu.text = snapshot.gpuCount > 0
					? JoinPair(snapshot.gpuName, snapshot.gpuName2)
					: Tr("3DMork no gpu");
			}

			if (leaderboardSelfScore != null)
			{
				leaderboardSelfScore.text = best > 0 ? best.ToString() : "--";
			}

			if (leaderboardSelfFps != null)
			{
				var history = LoadHistory();
				int lastFps = history.Count > 0 ? history[0].fps : 0;
				leaderboardSelfFps.text = lastFps > 0 ? lastFps.ToString() : "--";
			}

			if (leaderboardAverage != null)
			{
				int avg = ReferenceBenchmarks.Length > 0 ? sum / ReferenceBenchmarks.Length : 0;
				leaderboardAverage.text = Format("3DMork average", ReferenceBenchmarks.Length, avg,
												  best > 0 ? best.ToString() : "--");
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
				SetCell(historyFps, i, hasRow ? history[i].fps.ToString() : "--", new Color(0.55f, 0.9f, 0.6f));
			}

			if (historyEmpty != null) historyEmpty.gameObject.SetActive(history.Count == 0);

			if (historyBest != null)
			{
				int best = GetBestScore();
				historyBest.text = Format("3DMork best score", best > 0
					? best.ToString()
					: Tr("3DMork no runs yet"));
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

		/// <summary>
		/// Приводит название детали к виду, как оно записано в таблице:
		/// убирает служебный сужефикс Unity и префикс категории "CPU ",
		/// а в названиях видеокарт ставит пробелы ("RTX5090Ti" -> "RTX 5090 Ti").
		/// </summary>
		private static string CleanName(string value)
		{
			if (string.IsNullOrEmpty(value)) return string.Empty;
			string clean = value.Replace("(Clone)", string.Empty).Replace("(clone)", string.Empty).Trim();
			if (clean.StartsWith("CPU ", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(4).Trim();
			return PrettyGpuName(clean);
		}

		/// <summary>"RTX5090" -> "RTX 5090", "GTX1080Ti" -> "GTX 1080 Ti", "RX570" -> "RX 570".</summary>
		private static string PrettyGpuName(string value)
		{
			if (string.IsNullOrEmpty(value)) return value;
			var match = Regex.Match(value, @"^([A-Za-z]{2,4})(\d{2,4})([A-Za-z]{0,3})$");
			if (!match.Success) return value;
			string tail = match.Groups[3].Value;
			if (tail.Length == 0) return match.Groups[1].Value + " " + match.Groups[2].Value;
			return match.Groups[1].Value + " " + match.Groups[2].Value + " " + tail;
		}

		/// <summary>Соединяет названия двух деталей в одну строку, например "RTX 5090 + RTX 4080".</summary>
		private static string JoinPair(string first, string second)
		{
			if (string.IsNullOrEmpty(first)) return second ?? string.Empty;
			if (string.IsNullOrEmpty(second)) return first;
			return first + " + " + second;
		}

		private HardwareSnapshot CollectHardware()
		{
			var snapshot = new HardwareSnapshot();

			var board = system != null ? system.Board : null;
			snapshot.hasBoard = board != null;
			if (board != null) snapshot.boardName = CleanName(board.gameObject.name);

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
						string name = CleanName(g.gameObject.name);
						if (string.IsNullOrEmpty(snapshot.gpuName)) snapshot.gpuName = name;
						else if (string.IsNullOrEmpty(snapshot.gpuName2)) snapshot.gpuName2 = name;
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
						snapshot.cpuScore += CpuPower(cpu);
						snapshot.cpuName = CleanName(cpu.gameObject.name);
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

				var drives = board.GetHardwares(HardwareType.Drive);
				if (drives != null)
				{
					for (int i = 0; i < drives.Count; i++)
					{
						var d = drives[i];
						if (d == null || d.Damaged) continue;
						snapshot.driveCount++;
						snapshot.driveScore += d.Score;
						string name = CleanName(d.gameObject.name);
						if (string.IsNullOrEmpty(snapshot.driveName)) snapshot.driveName = name;
						else if (string.IsNullOrEmpty(snapshot.driveName2)) snapshot.driveName2 = name;
					}
				}
			}

			if (snapshot.gpuScore == 0) snapshot.gpuScore = 200;
			if (snapshot.cpuScore <= 0f) snapshot.cpuScore = 2000f;
			if (snapshot.ramScore == 0) snapshot.ramScore = 1000;
			if (snapshot.ramCapacity == 0) snapshot.ramCapacity = 2048;
			if (snapshot.driveScore == 0) snapshot.driveScore = 500;

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
						? Localization.GetText(wp.phaseName)
						: Format("3DMork scene generic", (i + 1).ToString());
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
						name = string.IsNullOrEmpty(phase.phaseName)
							? Format("3DMork scene generic", (i + 1).ToString())
							: Localization.GetText(phase.phaseName),
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
				name = Tr("3DMork scene 1"),
				duration = 4f,
				loadMultiplier = 0.95f,
				startPos = anchor + new Vector3(-5.2f, 3.2f, -5.5f),
				endPos = anchor + new Vector3(3.8f, 2.0f, 2.5f),
				lookAtPos = anchor + new Vector3(0f, 0.2f, 0f),
				hasLookAt = true
			});

			segments.Add(new FlybySegment
			{
				name = Tr("3DMork scene 2"),
				duration = 4f,
				loadMultiplier = 0.85f,
				startPos = anchor + new Vector3(2.8f, 0.75f, 2.2f),
				endPos = anchor + new Vector3(-2.8f, 0.5f, 1.2f),
				lookAtPos = anchor + new Vector3(0.2f, -0.1f, -0.1f),
				hasLookAt = true
			});

			segments.Add(new FlybySegment
			{
				name = Tr("3DMork scene 3"),
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

			liveFps = -1;
			liveResolution = "";
			currentSceneName = "";
			RefreshTestTexts();
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
			int driveScore = hardware.driveScore;

			// Потолок кадров из настроек игры: тест не может рендерить чаще,
			// чем игрок выбрал в настройках, даже если железо быстрее.
			float fpsCap = GetFpsCap();
			BenchmarkScores plan = ComputeScores(gpuRawScore, cpuRawScore, ramScore, driveScore, fpsCap);
			float machineFps = plan.machineFps;
			float baseFps = Mathf.Min(machineFps, fpsCap);
			Vector2Int renderSize = PickRenderResolution(machineFps, fpsCap);
			liveResolution = renderSize.x + "x" + renderSize.y;

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

			// Создаём RenderTexture для окна бенчмарка.
			// Разрешение подбирается по мощности железа: чем слабее ПК,
			// тем меньше кадров он держит и тем ниже разрешение рендера.
			renderTexture = new RenderTexture(renderSize.x, renderSize.y, 24, RenderTextureFormat.ARGB32);
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
				currentSceneName = seg.name;
				RefreshTestTexts();

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

					// Расчёт FPS, зависящего от виртуального железа,
					// но не выше потолка, выбранного в настройках игры
					float jitter = Mathf.Sin(Time.time * 7f) * (baseFps * 0.04f) + UnityEngine.Random.Range(-1.5f, 1.5f);
					float currentFps = Mathf.Clamp(baseFps * seg.loadMultiplier + jitter, 5f, fpsCap);

					liveFps = Mathf.RoundToInt(currentFps);
					RefreshTestTexts();
					if (testProgressBar != null) testProgressBar.value = Mathf.Clamp01(elapsed / totalDuration);

					yield return null;
				}
			}

			// Очистка камеры и отдельной сцены/комнаты после завершения облёта
			CleanupCamera();
			CleanupStage();

			// Подсчёт очков 3DMork по общей формуле (та же, что у эталонных машин)
			int graphicsScore = plan.graphics;
			int physicsScore = plan.physics;
			int memoryScore = plan.memory;
			int fpsScore = plan.fps;
			int totalScore = plan.total;

			// Сохраняем прогон в локальную историю этого ПК
			PushHistory(totalScore, Mathf.RoundToInt(baseFps), graphicsScore, physicsScore, memoryScore);

			// Переход на экран результатов
			if (testPanel != null) testPanel.SetActive(false);
			if (resultsPanel != null) resultsPanel.SetActive(true);

			if (textTotalScore != null) textTotalScore.text = "0";
			if (markCircle != null) markCircle.fillAmount = 0f;

			var scores = new int[4] { graphicsScore, physicsScore, memoryScore, fpsScore };

			if (marks != null)
			{
				for (int i = 0; i < marks.Length; i++)
				{
					if (marks[i] == null) continue;
					marks[i].value = 0f;
					// Шкала каждой категории растянута на эталонный максимум,
					// поэтому полоса всегда читается как доля от лучшей сборки.
					marks[i].maxValue = GetCategoryCeiling(i);
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
				if (slider.maxValue < target) slider.maxValue = target;

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
							text_marks[i].text = Format("3DMork fps", (v / FpsScoreFactor).ToString("0"));
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
			Localization.LanguageChanged -= OnLanguageChanged;
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
