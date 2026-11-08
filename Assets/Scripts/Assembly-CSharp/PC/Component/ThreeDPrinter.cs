using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PC.Component
{
	/// <summary>
	/// FDM 3D printer. Print time scales with triangle count; filament is consumed
	/// gradually from an attached spool. The spool is mounted physically through
	/// Slot/Connector (remove mode or pull force). Importing a model and starting
	/// a print are separate steps so the desktop app can show a preview first.
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

		[Header("Axis animation ranges (local units)")]
		[SerializeField] private Vector2 axisXRange = new Vector2(-0.1f, 0.1f);
		[SerializeField] private Vector2 axisZRange = new Vector2(-0.1f, 0.1f);
		[SerializeField] private float axisSpeed = 2f;
		[SerializeField] private int layerCount = 40;

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

		// Called by FilamentSpool when the physical Slot/Connector attaches it.
		public void OnSpoolPhysicallyAttached(FilamentSpool spool)
		{
			if (spool == null) return;
			AttachedSpool = spool;
			spool.SetPrinter(this);
			UpdateScreen();
		}

		// Called by FilamentSpool when remove mode / pull force / destruction detaches it.
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

		// Backwards-compatible helper: import and immediately start.
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
			printProgressRoot.transform.localScale = new Vector3(1f, 0.001f, 1f);
			model.Instance.SetActive(true);
			model.Instance.transform.SetParent(printProgressRoot.transform, false);

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
				AnimateAxes(progress, elapsed);
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

		// ----- Visual hooks ---------------------------------------------------

		private void AnimateAxes(float progress, float elapsed)
		{
			if (axisX != null)
			{
				float k = Mathf.PingPong(elapsed * axisSpeed, 1f);
				axisX.localPosition = axisXRest + Vector3.right * Mathf.Lerp(axisXRange.x, axisXRange.y, k);
			}
			if (axisZ != null)
			{
				float k = Mathf.PingPong(elapsed * axisSpeed * 0.7f, 1f);
				axisZ.localPosition = axisZRest + Vector3.forward * Mathf.Lerp(axisZRange.x, axisZRange.y, k);
			}
			if (axisY != null)
			{
				float height = CurrentModel != null ? Mathf.Max(0.001f, CurrentModel.Bounds.size.y) : 0.1f;
				int layer = Mathf.Clamp(Mathf.FloorToInt(progress * layerCount), 0, layerCount);
				axisY.localPosition = axisYRest + Vector3.up * (height * layer / Mathf.Max(1, layerCount));
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
