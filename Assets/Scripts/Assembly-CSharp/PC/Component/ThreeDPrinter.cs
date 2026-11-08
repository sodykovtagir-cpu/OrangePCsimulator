using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using PC.Component.Cabling;
using UnityEngine;

namespace PC.Component
{
	/// <summary>
	/// Open-frame FDM printer. The bed travels forward/back, the extruder rides
	/// along the beam, and the beam/carriage is the vertical axis. Printing uses
	/// a deterministic perimeter + serpentine infill toolpath. The finished
	/// object becomes a saved PrintedModelItem with physics; while printing it
	/// has no Rigidbody/colliders. Mains power can be supplied through a cable
	/// port, and the room grid bills idle + job-dependent printing watts.
	/// </summary>
	[RequireComponent(typeof(Item))]
	public class ThreeDPrinter : Device
	{
		public enum PrinterState { Idle, Printing, Finished, Error }

		[Header("Model hooks")]
		[SerializeField] private Transform spoolMount;
		[SerializeField] private Transform printBed;
		[SerializeField] private Transform axisX;
		[SerializeField] private Transform axisY;
		[SerializeField] private Transform axisZ;
		[SerializeField] private ThreeDPrinterScreen screen;
		[SerializeField] private AudioSource loopAudio;
		[SerializeField] private AudioClip printingClip;
		[SerializeField] private AudioClip finishedClip;

		[Header("Extruder / carriage")]
		[SerializeField] private Transform extruderPivot;
		[SerializeField] private float grabClearance = 0.08f;
		[SerializeField] private float nozzleHeightOffset = 0.01f;
		[Tooltip("0 = auto: build plate moves down, print head moves up. +1/-1 forces direction.")]
		[SerializeField] private float axisYDirection = 0f;

		[Header("Toolpath")]
		[SerializeField] private int layerCount = 40;
		[SerializeField] private int perimeterPasses = 2;
		[SerializeField] private int infillLinesPerLayer = 12;
		[SerializeField] private float toolpathScale = 1f;

		[Header("Print volume")]
		[SerializeField] private BoxCollider printVolumeCollider;
		[SerializeField] private Vector3 maxPrintSize = new Vector3(0.8f, 0.8f, 0.8f);
		[SerializeField] private bool autoFitModel = true;
		[SerializeField] private bool rejectOversizedModels = false;
		[SerializeField, Range(0.01f, 1f)] private float minPrintScaleFraction = 0.05f;

		[Header("Print time / filament")]
		[SerializeField] private int referenceTriangles = 10000;
		[SerializeField] private float basePrintSeconds = 60f;
		[SerializeField] private float averagePrintSeconds = 600f;
		[SerializeField] private float minPrintSeconds = 180f;
		[SerializeField] private float maxPrintSeconds = 3600f;
		[SerializeField] private float filamentPerTriangle = 0.00008f;
		[SerializeField] private float filamentPerCubicMeter = 0f;
		[SerializeField] private float minFilamentMeters = 0.02f;

		[Header("Power")]
		[SerializeField] private CablePort powerPort;
		[SerializeField] private float idleWatts = 12f;
		[SerializeField] private float minPrintWatts = 80f;
		[SerializeField] private float maxPrintWatts = 350f;

		[Header("Printed object physics")]
		[SerializeField] private float printedDensityKgPerCubicMeter = 180f;
		[SerializeField] private float minPrintedMassKg = 0.02f;
		[SerializeField] private float maxPrintedMassKg = 5f;

		private Vector3 axisYRest;
		private Vector3 axisZRest;
		private Transform motionX;
		private Vector3 motionXRest;
		private Vector3 extruderTravelAxis = Vector3.right;
		private float extruderTravelLimit;
		private Coroutine printCoroutine;
		private GameObject printProgressRoot;
		private float totalSeconds;
		private float requiredFilament;
		private float printFitScale = 1f;
		private float modelHeight;
		private float jobVolume;
		private float toolCenterX;
		private float toolCenterZ;
		private bool lastPowerState;
		private readonly List<Vector3> toolPath = new List<Vector3>();
		private float[] toolPathCumulative;
		private float toolPathLength;

		public PrinterState State { get; private set; } = PrinterState.Idle;
		public bool IsPrinting => State == PrinterState.Printing;
		public FilamentSpool AttachedSpool { get; private set; }
		public PrintableModel CurrentModel { get; private set; }
		public PrintableModel PendingModel { get; private set; }
		public float Progress01 { get; private set; }
		public float RemainingSeconds { get; private set; }
		public string LastError { get; private set; }
		public float PrintScale01 { get; private set; } = 1f;
		public Vector3 MaxPrintSize => printVolumeCollider != null ? printVolumeCollider.size : maxPrintSize;
		public BoxCollider PrintVolumeCollider => printVolumeCollider;

		public event Action<PrintableModel> PrintFinished;
		public event Action<PrintableModel> ModelImported;
		public event Action StateChanged;

		public bool IsPowered
		{
			get
			{
				if (!ElectricityGrid.PowerAvailable) return false;
				if (!CableMode.Enabled) return true;
				var port = PowerPort;
				return port != null && CablePower.Energized(port.plug);
			}
		}

		public float CurrentWatts
		{
			get
			{
				if (!IsPowered) return 0f;
				if (!IsPrinting) return Mathf.Max(0f, idleWatts);
				return Mathf.Lerp(Mathf.Max(0f, minPrintWatts), Mathf.Max(minPrintWatts, maxPrintWatts), JobLoad01);
			}
		}

		private float JobLoad01
		{
			get
			{
				Vector3 max = MaxPrintSize;
				float maxVolume = Mathf.Max(0.000001f, max.x * max.y * max.z);
				float volumeRatio = Mathf.Clamp01(jobVolume / maxVolume);
				float triRatio = CurrentModel != null
					? Mathf.Clamp01(CurrentModel.TriangleCount / Mathf.Max(1f, referenceTriangles))
					: 0f;
				return Mathf.Clamp01(volumeRatio * 0.7f + triRatio * 0.3f);
			}
		}

		private CablePort PowerPort
		{
			get
			{
				if (powerPort == null) BindPowerPort();
				return powerPort;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			if (screen == null) screen = GetComponentInChildren<ThreeDPrinterScreen>(true);
			if (loopAudio == null) loopAudio = GetComponent<AudioSource>();
			EnsureExtruderPivot();
			motionX = extruderPivot != null ? extruderPivot : axisX;
			if (motionX != null) motionXRest = motionX.localPosition;
			if (axisY != null) axisYRest = axisY.localPosition;
			if (axisZ != null) axisZRest = axisZ.localPosition;
			CacheExtruderAxis();
			lastPowerState = IsPowered;
			UpdateScreen();
		}

		private void Update()
		{
			bool powered = IsPowered;
			if (powered != lastPowerState)
			{
				lastPowerState = powered;
				ElectricityGrid.Instance?.NotifyLoadChanged();
			}
			if (IsPrinting && !powered) Fail("No power");
		}

		private void OnDestroy()
		{
			StopPrintAudio();
			if (AttachedSpool != null) AttachedSpool.SetPrinter(null);
			if (PendingModel != null) PendingModel.DestroyInstance();
		}

		public override void OnDeviceStop()
		{
			if (IsPrinting) CancelPrint();
		}

		private void BindPowerPort()
		{
			var item = GetComponent<Item>();
			if (item == null) return;
			foreach (var candidate in GetComponentsInChildren<CablePort>(true))
			{
				if (candidate != null && candidate.kind == PortKind.PrinterPower &&
					candidate.GetComponentInParent<Item>() == item)
				{
					powerPort = candidate;
					return;
				}
			}
			if (!CableMode.Enabled) return;
			// Monitor-style placeholder inlet. Move/replace this socket in the
			// prefab when the final printer power connector is modelled.
			powerPort = CablePort.Add(item, PortKind.PrinterPower, "Printer AC input",
				new Vector3(0.32f, 0.12f, -0.45f), 0.16f);
		}

		// ----- Spool handling -------------------------------------------------

		public bool AttachSpool(FilamentSpool spool)
		{
			if (IsPrinting || spool == null) return false;
			if (AttachedSpool != null) DetachSpool();
			AttachedSpool = spool;
			spool.SetPrinter(this);
			if (spoolMount != null)
			{
				spool.transform.SetParent(spoolMount, false);
				spool.transform.localPosition = Vector3.zero;
				spool.transform.localRotation = Quaternion.identity;
			}
			UpdateScreen();
			return true;
		}

		public bool DetachSpool()
		{
			if (IsPrinting || AttachedSpool == null) return false;
			var spool = AttachedSpool;
			AttachedSpool = null;
			spool.SetPrinter(null);
			spool.transform.SetParent(null, true);
			UpdateScreen();
			return true;
		}

		public void OnSpoolPhysicallyAttached(FilamentSpool spool)
		{
			if (spool == null) return;
			AttachedSpool = spool;
			spool.SetPrinter(this);
			UpdateScreen();
		}

		public void OnSpoolPhysicallyDetached(FilamentSpool spool)
		{
			if (spool == null || AttachedSpool != spool) return;
			AttachedSpool = null;
			spool.SetPrinter(null);
			if (IsPrinting) Fail("Spool removed");
			UpdateScreen();
		}

		// ----- Import / scale -------------------------------------------------

		public void SetPrintScale01(float value)
		{
			if (IsPrinting) return;
			PrintScale01 = Mathf.Clamp01(value);
		}

		public void ImportModelDialog()
		{
			if (IsPrinting)
			{
				Fail("Printer is busy");
				return;
			}
			NativeFilePicker.RequestPermissionAsync(permission =>
			{
				if (permission != NativeFilePicker.Permission.Granted)
				{
					Fail("Storage permission denied");
					return;
				}
				NativeFilePicker.PickFile(path =>
				{
					if (!string.IsNullOrEmpty(path)) ImportModel(path);
				}, ThreeDModelImporter.SupportedExtensions);
			});
		}

		public bool ImportModel(string path)
		{
			if (IsPrinting) return false;
			try
			{
				var model = ThreeDModelImporter.Import(path, null);
				if (model.Instance != null) model.Instance.SetActive(false);
				if (PendingModel != null) PendingModel.DestroyInstance();
				PendingModel = model;
				ApplySpoolColor(model);
				ModelImported?.Invoke(model);
				UpdateScreen();
				return true;
			}
			catch (Exception e)
			{
				Fail(e.Message);
				return false;
			}
		}

		public void OpenImportDialog()
		{
			if (IsPrinting)
			{
				Fail("Printer is busy");
				return;
			}
			NativeFilePicker.RequestPermissionAsync(permission =>
			{
				if (permission != NativeFilePicker.Permission.Granted)
				{
					Fail("Storage permission denied");
					return;
				}
				NativeFilePicker.PickFile(path =>
				{
					if (!string.IsNullOrEmpty(path)) ImportAndStartPrint(path);
				}, ThreeDModelImporter.SupportedExtensions);
			});
		}

		public bool ImportAndStartPrint(string path)
		{
			if (!ImportModel(path)) return false;
			return StartPrint(PendingModel);
		}

		public bool StartPrint()
		{
			return StartPrint(PendingModel);
		}

		public bool StartPrint(PrintableModel model)
		{
			if (IsPrinting) return false;
			LastError = null;
			if (model == null) model = PendingModel;
			if (model == null || model.Instance == null)
			{
				Fail("No model");
				return false;
			}
			if (!IsPowered)
			{
				Fail("No power");
				return false;
			}
			if (AttachedSpool == null)
			{
				Fail("No filament spool");
				return false;
			}

			requiredFilament = EstimateFilamentMeters(model);
			if (requiredFilament > AttachedSpool.RemainingMeters + 0.00001f)
			{
				Fail("Not enough filament");
				return false;
			}

			CurrentModel = model;
			if (PendingModel == model) PendingModel = null;
			totalSeconds = EstimatePrintSeconds(model.TriangleCount);
			RemainingSeconds = totalSeconds;
			Progress01 = 0f;
			SetState(PrinterState.Printing);
			ApplySpoolColor(model);

			printProgressRoot = new GameObject("PrintProgress");
			var bed = printBed != null ? printBed : transform;
			printProgressRoot.transform.SetParent(bed, false);
			printProgressRoot.transform.localPosition = Vector3.zero;
			printProgressRoot.transform.localRotation = Quaternion.identity;
			printProgressRoot.transform.localScale = Vector3.one;
			model.Instance.SetActive(true);
			StripPhysicsForPrint(model.Instance);
			model.Instance.transform.SetParent(printProgressRoot.transform, false);

			Bounds seated = GetLocalBounds(model.Instance, printProgressRoot.transform);
			if (seated.size.sqrMagnitude > 0.000001f)
			{
				model.Instance.transform.localPosition -= new Vector3(seated.center.x, seated.min.y, seated.center.z);
				seated = GetLocalBounds(model.Instance, printProgressRoot.transform);
			}

			Vector3 size = seated.size;
			Vector3 max = MaxPrintSize;
			bool oversized = size.x > max.x || size.y > max.y || size.z > max.z;
			if (rejectOversizedModels && oversized)
			{
				CleanupPrintRoot();
				CurrentModel = null;
				Fail("Model too large for print volume");
				return false;
			}

			float maxFit = 1f;
			if (autoFitModel)
			{
				float sx = max.x > 0.0001f ? max.x / Mathf.Max(0.0001f, size.x) : 1f;
				float sy = max.y > 0.0001f ? max.y / Mathf.Max(0.0001f, size.y) : 1f;
				float sz = max.z > 0.0001f ? max.z / Mathf.Max(0.0001f, size.z) : 1f;
				maxFit = Mathf.Max(0.0001f, Mathf.Min(sx, Mathf.Min(sy, sz)));
			}
			float minFit = maxFit * Mathf.Clamp(minPrintScaleFraction, 0.01f, 1f);
			printFitScale = Mathf.Lerp(minFit, maxFit, PrintScale01);
			jobVolume = Mathf.Max(0f, size.x * size.y * size.z) * printFitScale * printFitScale * printFitScale;

			EnsureExtruderPivot();
			CacheExtruderAxis();
			BuildToolpath(model);
			printProgressRoot.transform.localScale = new Vector3(printFitScale, 0.001f * printFitScale, printFitScale);

			if (loopAudio != null && printingClip != null)
			{
				loopAudio.clip = printingClip;
				loopAudio.loop = true;
				loopAudio.Play();
			}

			printCoroutine = StartCoroutine(PrintRoutine());
			ElectricityGrid.Instance?.NotifyLoadChanged();
			UpdateScreen();
			return true;
		}

		public void CancelPrint()
		{
			if (!IsPrinting) return;
			if (printCoroutine != null) StopCoroutine(printCoroutine);
			printCoroutine = null;
			StopPrintAudio();
			CleanupPrintRoot();
			CurrentModel = null;
			SetState(PrinterState.Idle);
			Progress01 = 0f;
			RemainingSeconds = 0f;
			ResetAxes();
			ElectricityGrid.Instance?.NotifyLoadChanged();
			UpdateScreen();
		}

		private IEnumerator PrintRoutine()
		{
			float elapsed = 0f;
			float lastProgress = 0f;
			while (State == PrinterState.Printing)
			{
				elapsed += Time.deltaTime;
				float progress = totalSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / totalSeconds);
				float deltaFilament = (progress - lastProgress) * requiredFilament;
				lastProgress = progress;

				if (AttachedSpool == null)
				{
					Fail("Spool removed");
					yield break;
				}
				float consumed = Mathf.Min(deltaFilament, AttachedSpool.RemainingMeters);
				AttachedSpool.Consume(consumed);
				if (AttachedSpool.IsEmpty && progress < 1f)
				{
					Fail("Filament ran out");
					yield break;
				}

				Progress01 = progress;
				RemainingSeconds = Mathf.Max(0f, totalSeconds - elapsed);
				AnimateAxes(progress);
				GrowPrint(progress);
				UpdateScreen();

				if (progress >= 1f) break;
				yield return null;
			}
			if (State == PrinterState.Printing) FinishPrint();
		}

		private void FinishPrint()
		{
			printCoroutine = null;
			StopPrintAudio();
			if (printProgressRoot != null)
				printProgressRoot.transform.localScale = new Vector3(printFitScale, printFitScale, printFitScale);

			// Park the carriage just above the finished model so the player can
			// grab it, and return the bed/extruder to the center before release.
			ParkCarriage();

			var finished = CurrentModel;
			if (finished != null && finished.Instance != null)
			{
				finished.Instance.transform.SetParent(null, true);
				finished.AddFinishedPhysics(printedDensityKgPerCubicMeter, minPrintedMassKg, maxPrintedMassKg);
				AttachPrintedItem(finished);
			}
			CleanupPrintRoot();

			CurrentModel = null;
			PendingModel = null;
			Progress01 = 0f;
			RemainingSeconds = 0f;
			SetState(PrinterState.Idle);
			ElectricityGrid.Instance?.NotifyLoadChanged();
			UpdateScreen();
			if (loopAudio != null && finishedClip != null)
				loopAudio.PlayOneShot(finishedClip);
			PrintFinished?.Invoke(finished);
		}

		private void AttachPrintedItem(PrintableModel model)
		{
			if (model == null || model.Instance == null) return;
			var item = model.Instance.GetComponent<PrintedModelItem>();
			if (item == null) item = model.Instance.AddComponent<PrintedModelItem>();
			Color color = AttachedSpool != null ? AttachedSpool.FilamentColor : Color.white;
			item.Initialize(model.Name, color);
		}

		private void Fail(string message)
		{
			LastError = message;
			if (printCoroutine != null) StopCoroutine(printCoroutine);
			printCoroutine = null;
			StopPrintAudio();
			SetState(PrinterState.Error);
			ResetAxes();
			ElectricityGrid.Instance?.NotifyLoadChanged();
			UpdateScreen();
		}

		private void CleanupPrintRoot()
		{
			if (printProgressRoot != null) Destroy(printProgressRoot);
			printProgressRoot = null;
		}

		private void StopPrintAudio()
		{
			if (loopAudio != null) loopAudio.Stop();
		}

		private void SetState(PrinterState state)
		{
			if (State == state) return;
			State = state;
			StateChanged?.Invoke();
		}

		private void ApplySpoolColor(PrintableModel model)
		{
			if (model == null) return;
			Color color = AttachedSpool != null ? AttachedSpool.FilamentColor : Color.white;
			model.ApplyColor(color);
		}

		// ----- Estimation -----------------------------------------------------

		public float EstimatePrintSeconds(int triangleCount)
		{
			float ratio = triangleCount / Mathf.Max(1f, referenceTriangles);
			return Mathf.Clamp(basePrintSeconds + averagePrintSeconds * ratio, minPrintSeconds, maxPrintSeconds);
		}

		public float EstimateFilamentMeters(PrintableModel model)
		{
			if (model == null) return minFilamentMeters;
			float byTriangles = model.TriangleCount * filamentPerTriangle;
			float byVolume = model.BoundsVolume * filamentPerCubicMeter;
			return Mathf.Max(minFilamentMeters, byTriangles + byVolume);
		}

		// ----- Extruder pivot / toolpath --------------------------------------

		private void EnsureExtruderPivot()
		{
			if (extruderPivot != null || axisX == null) return;
			// Move the extruder under a centered pivot so travel is relative to
			// the carriage center, not the mesh corner.
			Bounds bounds = GetWorldBounds(axisX.gameObject);
			if (bounds.size.sqrMagnitude <= 0.000001f) return;
			var pivot = new GameObject("Extruder Pivot");
			pivot.transform.SetParent(axisX.parent, true);
			pivot.transform.position = bounds.center;
			pivot.transform.rotation = axisX.rotation;
			pivot.transform.localScale = Vector3.one;
			axisX.SetParent(pivot.transform, true);
			extruderPivot = pivot.transform;
			motionX = extruderPivot;
			motionXRest = motionX.localPosition;
		}

		private float VerticalDirection
		{
			get
			{
				if (Mathf.Abs(axisYDirection) > 0.0001f) return Mathf.Sign(axisYDirection);
				return axisY != null && axisY == printBed ? -1f : 1f;
			}
		}

		private void BuildToolpath(PrintableModel model)
		{
			toolPath.Clear();
			toolPathCumulative = null;
			toolPathLength = 0f;
			modelHeight = 0.1f;
			toolCenterX = 0f;
			toolCenterZ = 0f;
			if (model == null || model.Instance == null) return;

			Transform bed = printBed != null ? printBed : transform;
			var verts = new List<Vector3>();
			CollectVerticesInSpace(model.Instance, bed, verts);
			if (verts.Count == 0) return;

			Vector3 min = verts[0], max = verts[0];
			for (int i = 1; i < verts.Count; i++)
			{
				min = Vector3.Min(min, verts[i]);
				max = Vector3.Max(max, verts[i]);
			}
			float fullHeight = Mathf.Max(0.001f, max.y - min.y);
			modelHeight = fullHeight * printFitScale;
			toolCenterX = (min.x + max.x) * 0.5f;
			toolCenterZ = (min.z + max.z) * 0.5f;

			int layers = Mathf.Max(1, layerCount);
			int lines = Mathf.Max(2, infillLinesPerLayer);
			int passes = Mathf.Max(1, perimeterPasses);
			float scale = Mathf.Max(0.0001f, toolpathScale * printFitScale);

			for (int layer = 0; layer < layers; layer++)
			{
				float y0 = min.y + fullHeight * layer / layers;
				float y1 = min.y + fullHeight * (layer + 1) / layers;
				float y = (y0 + nozzleHeightOffset) * printFitScale;
				Vector3 lmin = new Vector3(float.MaxValue, 0f, float.MaxValue);
				Vector3 lmax = new Vector3(float.MinValue, 0f, float.MinValue);
				bool has = false;
				for (int i = 0; i < verts.Count; i++)
				{
					Vector3 v = verts[i];
					if (v.y < y0 || v.y > y1) continue;
					has = true;
					lmin.x = Mathf.Min(lmin.x, v.x); lmax.x = Mathf.Max(lmax.x, v.x);
					lmin.z = Mathf.Min(lmin.z, v.z); lmax.z = Mathf.Max(lmax.z, v.z);
				}
				if (!has) { lmin = min; lmax = max; }
				float lx0 = (lmin.x - toolCenterX) * scale;
				float lx1 = (lmax.x - toolCenterX) * scale;
				float lz0 = (lmin.z - toolCenterZ) * scale;
				float lz1 = (lmax.z - toolCenterZ) * scale;
				if (Mathf.Abs(lx1 - lx0) < 0.0001f) { lx0 -= 0.005f; lx1 += 0.005f; }
				if (Mathf.Abs(lz1 - lz0) < 0.0001f) { lz0 -= 0.005f; lz1 += 0.005f; }

				for (int p = 0; p < passes; p++)
				{
					toolPath.Add(new Vector3(lx0, y, lz0));
					toolPath.Add(new Vector3(lx1, y, lz0));
					toolPath.Add(new Vector3(lx1, y, lz1));
					toolPath.Add(new Vector3(lx0, y, lz1));
					toolPath.Add(new Vector3(lx0, y, lz0));
				}
				for (int i = 0; i < lines; i++)
				{
					float z = Mathf.Lerp(lz0, lz1, (i + 0.5f) / lines);
					float xa = (i & 1) == 0 ? lx0 : lx1;
					float xb = (i & 1) == 0 ? lx1 : lx0;
					toolPath.Add(new Vector3(xa, y, z));
					toolPath.Add(new Vector3(xb, y, z));
				}
			}

			toolPathCumulative = new float[toolPath.Count];
			float acc = 0f;
			for (int i = 0; i < toolPath.Count; i++)
			{
				if (i > 0) acc += Vector3.Distance(toolPath[i - 1], toolPath[i]);
				toolPathCumulative[i] = acc;
			}
			toolPathLength = acc;
		}

		private Vector3 SampleToolpath(float progress)
		{
			if (toolPath.Count == 0) return Vector3.zero;
			if (toolPath.Count == 1 || toolPathLength <= 0.00001f) return toolPath[0];
			float target = Mathf.Clamp01(progress) * toolPathLength;
			int lo = 0, hi = toolPathCumulative.Length - 1;
			while (lo < hi)
			{
				int mid = (lo + hi) / 2;
				if (toolPathCumulative[mid] < target) lo = mid + 1;
				else hi = mid;
			}
			int i = Mathf.Clamp(lo, 1, toolPath.Count - 1);
			float prev = toolPathCumulative[i - 1];
			float next = toolPathCumulative[i];
			float t = next > prev ? Mathf.Clamp01((target - prev) / (next - prev)) : 0f;
			return Vector3.Lerp(toolPath[i - 1], toolPath[i], t);
		}

		private void AnimateAxes(float progress)
		{
			Vector3 point = SampleToolpath(progress);
			float xSign = motionX != null && motionX == printBed ? -1f : 1f;
			float zSign = axisZ != null && axisZ == printBed ? -1f : 1f;
			if (motionX != null)
			{
				float x = point.x * xSign;
				if (extruderTravelLimit > 0.0001f)
					x = Mathf.Clamp(x, -extruderTravelLimit, extruderTravelLimit);
				motionX.localPosition = motionXRest + extruderTravelAxis * x;
			}
			if (axisZ != null)
				axisZ.localPosition = axisZRest + Vector3.forward * (point.z * zSign);
			if (axisY != null)
			{
				float printedHeight = modelHeight * Mathf.Clamp01(progress);
				axisY.localPosition = axisYRest + Vector3.up * (VerticalDirection * (printedHeight + nozzleHeightOffset));
			}
		}

		private void ParkCarriage()
		{
			if (motionX != null) motionX.localPosition = motionXRest;
			if (axisZ != null) axisZ.localPosition = axisZRest;
			if (axisY != null)
				axisY.localPosition = axisYRest + Vector3.up * (VerticalDirection * (modelHeight + grabClearance));
		}

		private void ResetAxes()
		{
			if (motionX != null) motionX.localPosition = motionXRest;
			if (axisY != null) axisY.localPosition = axisYRest;
			if (axisZ != null) axisZ.localPosition = axisZRest;
		}

		private void CacheExtruderAxis()
		{
			extruderTravelAxis = Vector3.right;
			extruderTravelLimit = 0f;
			Transform mover = motionX != null ? motionX : axisX;
			if (mover == null || mover.parent == null) return;
			Transform beam = mover.parent;
			var mf = beam.GetComponent<MeshFilter>();
			if (mf == null) mf = beam.GetComponentInChildren<MeshFilter>(true);
			if (mf == null || mf.sharedMesh == null) return;
			Vector3 s = mf.sharedMesh.bounds.size;
			int dom = s.x >= s.y && s.x >= s.z ? 0 : (s.y >= s.z ? 1 : 2);
			extruderTravelAxis = dom == 0 ? Vector3.right : (dom == 1 ? Vector3.up : Vector3.forward);
			extruderTravelLimit = Mathf.Max(0.01f, s[dom] * 0.5f - 0.02f);
		}

		private void GrowPrint(float progress)
		{
			if (printProgressRoot == null) return;
			float y = Mathf.Max(0.001f, progress) * printFitScale;
			printProgressRoot.transform.localScale = new Vector3(printFitScale, y, printFitScale);
		}

		private static void StripPhysicsForPrint(GameObject root)
		{
			if (root == null) return;
			var colliders = root.GetComponentsInChildren<Collider>(true);
			for (int i = 0; i < colliders.Length; i++)
			{
				if (colliders[i] == null) continue;
				colliders[i].enabled = false;
				Destroy(colliders[i]);
			}
			var bodies = root.GetComponentsInChildren<Rigidbody>(true);
			for (int i = 0; i < bodies.Length; i++)
			{
				if (bodies[i] == null) continue;
				bodies[i].isKinematic = true;
				bodies[i].detectCollisions = false;
				Destroy(bodies[i]);
			}
		}

		private static void CollectVerticesInSpace(GameObject root, Transform space, List<Vector3> output)
		{
			var filters = root.GetComponentsInChildren<MeshFilter>(true);
			for (int f = 0; f < filters.Length; f++)
			{
				var mesh = filters[f] != null ? filters[f].sharedMesh : null;
				if (mesh == null) continue;
				Matrix4x4 toSpace = space.worldToLocalMatrix * filters[f].transform.localToWorldMatrix;
				var vertices = mesh.vertices;
				for (int i = 0; i < vertices.Length; i++)
					output.Add(toSpace.MultiplyPoint3x4(vertices[i]));
			}
		}

		private static Bounds GetLocalBounds(GameObject root, Transform space)
		{
			Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
			bool has = false;
			var filters = root.GetComponentsInChildren<MeshFilter>(true);
			for (int f = 0; f < filters.Length; f++)
			{
				var mesh = filters[f] != null ? filters[f].sharedMesh : null;
				if (mesh == null) continue;
				Matrix4x4 toSpace = space.worldToLocalMatrix * filters[f].transform.localToWorldMatrix;
				Bounds mb = mesh.bounds;
				for (int c = 0; c < 8; c++)
				{
					Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3(
						(c & 1) == 0 ? -1f : 1f,
						(c & 2) == 0 ? -1f : 1f,
						(c & 4) == 0 ? -1f : 1f));
					Vector3 p = toSpace.MultiplyPoint3x4(corner);
					if (!has) { bounds = new Bounds(p, Vector3.zero); has = true; }
					else bounds.Encapsulate(p);
				}
			}
			if (!has)
			{
				var renderers = root.GetComponentsInChildren<Renderer>(true);
				for (int r = 0; r < renderers.Length; r++)
				{
					if (renderers[r] == null) continue;
					Bounds wb = renderers[r].bounds;
					for (int c = 0; c < 8; c++)
					{
						Vector3 corner = wb.center + Vector3.Scale(wb.extents, new Vector3(
							(c & 1) == 0 ? -1f : 1f,
							(c & 2) == 0 ? -1f : 1f,
							(c & 4) == 0 ? -1f : 1f));
						Vector3 p = space.InverseTransformPoint(corner);
						if (!has) { bounds = new Bounds(p, Vector3.zero); has = true; }
						else bounds.Encapsulate(p);
					}
				}
			}
			return bounds;
		}

		private static Bounds GetWorldBounds(GameObject root)
		{
			Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
			bool has = false;
			var renderers = root.GetComponentsInChildren<Renderer>(true);
			for (int i = 0; i < renderers.Length; i++)
			{
				if (renderers[i] == null) continue;
				if (!has) { bounds = renderers[i].bounds; has = true; }
				else bounds.Encapsulate(renderers[i].bounds);
			}
			return bounds;
		}

		private void UpdateScreen()
		{
			if (screen == null) return;
			float filament = AttachedSpool != null ? AttachedSpool.RemainingMeters : 0f;
			string modelName = CurrentModel != null ? CurrentModel.Name :
				PendingModel != null ? PendingModel.Name : "Model";
			switch (State)
			{
				case PrinterState.Printing:
					screen.ShowPrinting(modelName, RemainingSeconds, filament, Progress01);
					break;
				case PrinterState.Finished:
					screen.ShowFinished(modelName, filament);
					break;
				case PrinterState.Error:
					screen.ShowError(string.IsNullOrEmpty(LastError) ? "Error" : LastError, filament);
					break;
				default:
					screen.ShowIdle(filament);
					break;
			}
		}

		// ----- Save -----------------------------------------------------------

		public override void ToData(JObject jObject)
		{
			base.ToData(jObject);
			jObject["tdpState"] = (int)State;
			jObject["tdpModel"] = CurrentModel != null ? CurrentModel.Name : null;
			jObject["tdpProgress"] = Progress01;
			jObject["tdpScale"] = PrintScale01;
			if (AttachedSpool != null) jObject["tdpSpoolRemaining"] = AttachedSpool.RemainingMeters;
		}

		public override void FromData(JObject jObject)
		{
			base.FromData(jObject);
			if (AttachedSpool != null && jObject.TryGetValue("tdpSpoolRemaining", out var rem) && rem != null)
				AttachedSpool.SetRemaining(rem.Value<float>());
			if (jObject.TryGetValue("tdpScale", out var scale) && scale != null)
				PrintScale01 = Mathf.Clamp01(scale.Value<float>());
			SetState(PrinterState.Idle);
			CurrentModel = null;
			Progress01 = 0f;
			RemainingSeconds = 0f;
			UpdateScreen();
		}
	}
}
