using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PC.Component
{
	/// <summary>
	/// FDM 3D printer. Print time scales with triangle count; filament is consumed
	/// gradually from an attached spool. The spool is mounted physically through
	/// Slot/Connector (remove mode or pull force). Importing a model and starting
	/// a print are separate steps so the desktop app can show a preview first.
	///
	/// The toolpath is deterministic: for every layer the nozzle walks the
	/// perimeter of that layer's footprint and then fills it with a serpentine
	/// infill. The vertical axis moves as the print grows. If the serialized
	/// vertical axis is the build plate, the plate moves DOWN so the growing
	/// model top stays under the nozzle; otherwise the head moves UP.
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

		[Header("Axis animation ranges (local units, fallback only)")]
		[SerializeField] private Vector2 axisXRange = new Vector2(-0.1f, 0.1f);
		[SerializeField] private Vector2 axisZRange = new Vector2(-0.1f, 0.1f);
		[SerializeField] private float axisSpeed = 2f;
		[SerializeField] private int layerCount = 40;

		[Header("Toolpath")]
		[SerializeField] private int perimeterPasses = 2;
		[SerializeField] private int infillLinesPerLayer = 12;
		[SerializeField] private float nozzleHeightOffset = 0.01f;
		[Tooltip("0 = auto: build plate moves down, print head moves up. +1/-1 forces direction.")]
		[SerializeField] private float axisYDirection = 0f;
		[SerializeField] private float toolpathScale = 1f;

		[Header("Print time / filament model")]
		[SerializeField] private int referenceTriangles = 10000;
		[SerializeField] private float averagePrintSeconds = 600f;
		[SerializeField] private float minPrintSeconds = 20f;
		[SerializeField] private float maxPrintSeconds = 3600f;
		[SerializeField] private float filamentPerTriangle = 0.00008f;
		[SerializeField] private float filamentPerCubicMeter = 0f;
		[SerializeField] private float minFilamentMeters = 0.02f;

		private Vector3 axisXRest;
		private Vector3 axisYRest;
		private Vector3 axisZRest;
		private Coroutine printCoroutine;
		private GameObject printProgressRoot;
		private float totalSeconds;
		private float requiredFilament;

		private readonly List<Vector3> toolPath = new List<Vector3>();
		private float[] toolPathCumulative;
		private float toolPathLength;
		private float modelHeight;
		private float toolCenterX;
		private float toolCenterZ;

		public PrinterState State { get; private set; } = PrinterState.Idle;
		public bool IsPrinting => State == PrinterState.Printing;
		public FilamentSpool AttachedSpool { get; private set; }
		public PrintableModel CurrentModel { get; private set; }
		public PrintableModel PendingModel { get; private set; }
		public float Progress01 { get; private set; }
		public float RemainingSeconds { get; private set; }
		public string LastError { get; private set; }

		public event Action<PrintableModel> PrintFinished;
		public event Action<PrintableModel> ModelImported;
		public event Action StateChanged;

		protected override void Awake()
		{
			base.Awake();
			if (screen == null) screen = GetComponentInChildren<ThreeDPrinterScreen>(true);
			if (loopAudio == null) loopAudio = GetComponent<AudioSource>();
			if (axisX != null) axisXRest = axisX.localPosition;
			if (axisY != null) axisYRest = axisY.localPosition;
			if (axisZ != null) axisZRest = axisZ.localPosition;
			UpdateScreen();
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

		// ----- Import / print -------------------------------------------------

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

			// Wrap the imported model so we never overwrite the model's own
			// localScale (important for user-authored FBX/prefab scales).
			printProgressRoot = new GameObject("PrintProgress");
			var bed = printBed != null ? printBed : transform;
			printProgressRoot.transform.SetParent(bed, false);
			printProgressRoot.transform.localPosition = Vector3.zero;
			printProgressRoot.transform.localRotation = Quaternion.identity;
			printProgressRoot.transform.localScale = Vector3.one;
			model.Instance.SetActive(true);
			model.Instance.transform.SetParent(printProgressRoot.transform, false);

			// Seat the model bottom on the bed without touching its localScale.
			Bounds seated = GetLocalBounds(model.Instance, printProgressRoot.transform);
			if (seated.size.sqrMagnitude > 0.000001f)
				model.Instance.transform.localPosition -= Vector3.up * seated.min.y;

			BuildToolpath(model);
			printProgressRoot.transform.localScale = new Vector3(1f, 0.001f, 1f);

			if (loopAudio != null && printingClip != null)
			{
				loopAudio.clip = printingClip;
				loopAudio.loop = true;
				loopAudio.Play();
			}

			printCoroutine = StartCoroutine(PrintRoutine());
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
				printProgressRoot.transform.localScale = Vector3.one;

			// Release the finished object into the world so its Rigidbody can
			// interact with the bed/room instead of fighting the printer hierarchy.
			if (CurrentModel != null && CurrentModel.Instance != null)
			{
				CurrentModel.Instance.transform.SetParent(null, true);
				CurrentModel.AddFinishedPhysics();
			}
			CleanupPrintRoot();

			SetState(PrinterState.Finished);
			Progress01 = 1f;
			RemainingSeconds = 0f;
			ResetAxes();
			if (loopAudio != null && finishedClip != null)
				loopAudio.PlayOneShot(finishedClip);
			UpdateScreen();
			PrintFinished?.Invoke(CurrentModel);
		}

		private void Fail(string message)
		{
			LastError = message;
			if (printCoroutine != null) StopCoroutine(printCoroutine);
			printCoroutine = null;
			StopPrintAudio();
			SetState(PrinterState.Error);
			ResetAxes();
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
			return Mathf.Clamp(averagePrintSeconds * ratio, minPrintSeconds, maxPrintSeconds);
		}

		public float EstimateFilamentMeters(PrintableModel model)
		{
			if (model == null) return minFilamentMeters;
			float byTriangles = model.TriangleCount * filamentPerTriangle;
			float byVolume = model.BoundsVolume * filamentPerCubicMeter;
			return Mathf.Max(minFilamentMeters, byTriangles + byVolume);
		}

		// ----- Toolpath / visual hooks ----------------------------------------

		private float VerticalDirection
		{
			get
			{
				if (Mathf.Abs(axisYDirection) > 0.0001f) return Mathf.Sign(axisYDirection);
				// If the vertical axis is the bed, lower the bed as the model grows.
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
			modelHeight = Mathf.Max(0.001f, max.y - min.y);
			toolCenterX = (min.x + max.x) * 0.5f;
			toolCenterZ = (min.z + max.z) * 0.5f;

			int layers = Mathf.Max(1, layerCount);
			int lines = Mathf.Max(2, infillLinesPerLayer);
			int passes = Mathf.Max(1, perimeterPasses);
			float scale = Mathf.Max(0.0001f, toolpathScale);

			for (int layer = 0; layer < layers; layer++)
			{
				float y0 = min.y + modelHeight * layer / layers;
				float y1 = min.y + modelHeight * (layer + 1) / layers;
				float y = y0 + nozzleHeightOffset;

				// Footprint of this layer from vertices that lie in the slice.
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
				if (!has)
				{
					lmin = min; lmax = max;
				}
				float lx0 = (lmin.x - toolCenterX) * scale;
				float lx1 = (lmax.x - toolCenterX) * scale;
				float lz0 = (lmin.z - toolCenterZ) * scale;
				float lz1 = (lmax.z - toolCenterZ) * scale;
				if (Mathf.Abs(lx1 - lx0) < 0.0001f) { lx0 -= 0.005f; lx1 += 0.005f; }
				if (Mathf.Abs(lz1 - lz0) < 0.0001f) { lz0 -= 0.005f; lz1 += 0.005f; }

				// Perimeter passes.
				for (int p = 0; p < passes; p++)
				{
					toolPath.Add(new Vector3(lx0, y, lz0));
					toolPath.Add(new Vector3(lx1, y, lz0));
					toolPath.Add(new Vector3(lx1, y, lz1));
					toolPath.Add(new Vector3(lx0, y, lz1));
					toolPath.Add(new Vector3(lx0, y, lz0));
				}

				// Serpentine infill covering the layer area.
				for (int i = 0; i < lines; i++)
				{
					float z = Mathf.Lerp(lz0, lz1, (i + 0.5f) / lines);
					float xa = (i & 1) == 0 ? lx0 : lx1;
					float xb = (i & 1) == 0 ? lx1 : lx0;
					toolPath.Add(new Vector3(xa, y, z));
					toolPath.Add(new Vector3(xb, y, z));
				}
			}

			// Constant-speed travel along the accumulated path length.
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
			// If an axis transform is the build plate itself, moving the plate must
			// be opposite to the nozzle path so the model stays under the nozzle.
			float xSign = axisX != null && axisX == printBed ? -1f : 1f;
			float zSign = axisZ != null && axisZ == printBed ? -1f : 1f;
			if (axisX != null)
				axisX.localPosition = axisXRest + Vector3.right * (point.x * xSign);
			if (axisZ != null)
				axisZ.localPosition = axisZRest + Vector3.forward * (point.z * zSign);
			if (axisY != null)
			{
				float printedHeight = modelHeight * Mathf.Clamp01(progress);
				axisY.localPosition = axisYRest + Vector3.up * (VerticalDirection * printedHeight);
			}
		}

		private void ResetAxes()
		{
			if (axisX != null) axisX.localPosition = axisXRest;
			if (axisY != null) axisY.localPosition = axisYRest;
			if (axisZ != null) axisZ.localPosition = axisZRest;
		}

		private void GrowPrint(float progress)
		{
			if (printProgressRoot == null) return;
			printProgressRoot.transform.localScale = new Vector3(1f, Mathf.Max(0.001f, progress), 1f);
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
			if (AttachedSpool != null) jObject["tdpSpoolRemaining"] = AttachedSpool.RemainingMeters;
		}

		public override void FromData(JObject jObject)
		{
			base.FromData(jObject);
			if (AttachedSpool != null && jObject.TryGetValue("tdpSpoolRemaining", out var rem) && rem != null)
				AttachedSpool.SetRemaining(rem.Value<float>());
			// A live print cannot be resumed without the imported mesh; load idle.
			SetState(PrinterState.Idle);
			CurrentModel = null;
			Progress01 = 0f;
			RemainingSeconds = 0f;
			UpdateScreen();
		}
	}
}
