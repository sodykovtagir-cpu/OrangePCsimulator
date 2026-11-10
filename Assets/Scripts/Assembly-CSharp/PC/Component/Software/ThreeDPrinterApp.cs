using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	/// <summary>
	/// Desktop app for the physical 3D printer. The preview is a real 3D scene:
	/// the model stands on a small build plate, is lit from several sides and
	/// slowly rotates. A slider scales the pending print from a tiny fraction up
	/// to the maximum volume (optionally defined by a BoxCollider). The app
	/// clears its selection/preview when closed, when the OS stops, and after a
	/// print finishes.
	/// </summary>
	public class ThreeDPrinterApp : App
	{
		[Header("Buttons")]
		[SerializeField] private Button selectPrinterButton;
		[SerializeField] private Button importModelButton;
		[SerializeField] private Button startPrintButton;

		[Header("Text")]
		[SerializeField] private Text infoText;
		[SerializeField] private Text selectPrinterText;
		[SerializeField] private Text importModelText;
		[SerializeField] private Text startPrintText;
		[SerializeField] private Text scaleText;

		[Header("Preview")]
		[SerializeField] private RawImage previewImage;
		[SerializeField] private Slider scaleSlider;
		[SerializeField] private BoxCollider previewMaxBounds;
		[SerializeField] private int printerDeviceType = 4;
		[SerializeField] private Vector2Int previewResolution = new Vector2Int(384, 384);
		[SerializeField] private float previewRotationSpeed = 24f;
		[SerializeField, Range(0.01f, 1f)] private float minPreviewScaleFraction = 0.05f;
		[SerializeField] private Color previewBackground = new Color(0.10f, 0.10f, 0.12f, 1f);

		private ThreeDPrinter selectedPrinter;
		private ThreeDPrinter subscribedPrinter;
		private GameObject previewRoot;
		private GameObject previewPlate;
		private Transform previewModelPivot;
		private Camera previewCamera;
		private Light previewLight;
		private RenderTexture previewTexture;
		private GameObject previewClone;
		private PrintableModel previewModel;
		private Vector3 previewBaseScale = Vector3.one;
		private bool previewBaseValid;

		protected override void Start()
		{
			base.Start();
			Bind(selectPrinterButton, SelectPrinter);
			Bind(importModelButton, ImportModel);
			Bind(startPrintButton, StartPrint);
			if (selectPrinterText != null) selectPrinterText.text = Localization.GetText("Select Printer");
			if (importModelText != null) importModelText.text = Localization.GetText("Import Model");
			if (startPrintText != null) startPrintText.text = Localization.GetText("Start Print");
			if (scaleSlider != null)
			{
				scaleSlider.minValue = 0f;
				scaleSlider.maxValue = 1f;
				scaleSlider.wholeNumbers = false;
				scaleSlider.onValueChanged.AddListener(OnScaleSlider);
			}
			ResetAppState();
		}

		private void Bind(Button button, UnityEngine.Events.UnityAction action)
		{
			if (button == null) return;
			if (button.onClick.GetPersistentEventCount() == 0)
				button.onClick.AddListener(action);
		}

		private void Update()
		{
			RefreshUI();
			UpdatePreview();
		}

		public void SelectPrinter()
		{
			var os = system;
			if (os == null) return;
			var picker = os.DevicePicker;
			if (picker != null)
			{
				picker.PickDevice(os, device => SetSelectedPrinter(device as ThreeDPrinter), printerDeviceType, true);
				return;
			}
			var printers = FindObjectsOfType<ThreeDPrinter>();
			if (printers != null && printers.Length > 0) SetSelectedPrinter(printers[0]);
		}

		private void SetSelectedPrinter(ThreeDPrinter printer)
		{
			UnsubscribePrinter();
			selectedPrinter = printer;
			subscribedPrinter = printer;
			if (subscribedPrinter != null)
			{
				subscribedPrinter.PrintFinished += OnPrintFinished;
				subscribedPrinter.PrintCancelled += OnPrintFinished;
				subscribedPrinter.ModelImported += OnModelImported;
				subscribedPrinter.StateChanged += OnStateChanged;
				if (scaleSlider != null) scaleSlider.SetValueWithoutNotify(subscribedPrinter.PrintScale01);
			}
			previewModel = null;
			ClearPreviewClone();
			RefreshUI();
		}

		private void UnsubscribePrinter()
		{
			if (subscribedPrinter == null) return;
			subscribedPrinter.PrintFinished -= OnPrintFinished;
			subscribedPrinter.PrintCancelled -= OnPrintFinished;
			subscribedPrinter.ModelImported -= OnModelImported;
			subscribedPrinter.StateChanged -= OnStateChanged;
			subscribedPrinter = null;
		}

		private void OnPrintFinished(PrintableModel model)
		{
			ResetAppState();
		}

		private void OnModelImported(PrintableModel model)
		{
			previewModel = null;
			ClearPreviewClone();
			RefreshUI();
		}

		private void OnStateChanged()
		{
			RefreshUI();
		}

		public void ImportModel()
		{
			if (selectedPrinter == null)
			{
				ShowHint("No printer selected");
				return;
			}
			selectedPrinter.ImportModelDialog();
		}

		/// <summary>
		/// Start / cancel toggle. The prefab button keeps calling this method, so
		/// the same button cancels a running job and hands over the partial print.
		/// </summary>
		public void StartPrint()
		{
			if (selectedPrinter == null)
			{
				ShowHint("No printer selected");
				return;
			}
			if (selectedPrinter.IsPrinting)
			{
				CancelPrint();
				return;
			}
			if (scaleSlider != null) selectedPrinter.SetPrintScale01(scaleSlider.value);
			selectedPrinter.StartPrint();
			RefreshUI();
		}

		public void CancelPrint()
		{
			if (selectedPrinter == null) return;
			selectedPrinter.CancelPrint();
			RefreshUI();
		}

		private void OnScaleSlider(float value)
		{
			if (selectedPrinter != null) selectedPrinter.SetPrintScale01(value);
			ApplyPreviewScale();
			RefreshUI();
		}

		private void RefreshUI()
		{
			bool hasPrinter = selectedPrinter != null;
			bool printing = hasPrinter && selectedPrinter.IsPrinting;
			var model = hasPrinter ? (selectedPrinter.CurrentModel != null ? selectedPrinter.CurrentModel : selectedPrinter.PendingModel) : null;
			bool canImport = hasPrinter && !printing;
			bool canStart = hasPrinter && !printing && model != null && selectedPrinter.AttachedSpool != null && selectedPrinter.IsPowered;

			if (importModelButton != null) importModelButton.interactable = canImport;
			// While printing the same button cancels the job, so it stays active.
			if (startPrintButton != null) startPrintButton.interactable = printing || canStart;
			if (startPrintText != null)
				startPrintText.text = Localization.GetText(printing ? "Cancel Print" : "Start Print");
			if (scaleSlider != null) scaleSlider.interactable = hasPrinter && !printing && model != null;

			float scale01 = scaleSlider != null ? scaleSlider.value : (hasPrinter ? selectedPrinter.PrintScale01 : 1f);
			if (scaleText != null) scaleText.text = Localization.GetText("Scale") + ": " + Mathf.RoundToInt(scale01 * 100f) + "%";

			if (infoText == null) return;
			if (!hasPrinter)
			{
				infoText.text = Localization.GetText("No printer selected");
				return;
			}

			var sb = new System.Text.StringBuilder();
			sb.AppendLine(selectedPrinter.DeviceName);
			sb.AppendLine(Localization.GetText(selectedPrinter.IsPowered ? "Powered" : "No power"));
			if (model != null)
			{
				sb.AppendLine(Localization.GetText("Model") + ": " + model.Name);
				sb.AppendLine(Localization.GetText("Triangles") + ": " + model.TriangleCount);
				sb.AppendLine(Localization.GetText("Time") + ": " + FormatTime(selectedPrinter.EstimatePrintSeconds(model)));
				sb.AppendLine(Localization.GetText("Weight") + ": " + FormatMass(selectedPrinter.EstimatePrintedMassKg(model)));
			}
			else sb.AppendLine(Localization.GetText("No model imported"));

			var spool = selectedPrinter.AttachedSpool;
			if (spool != null)
			{
				if (spool.IsLowFilament) sb.AppendLine(Localization.GetText("Low filament"));
			}
			else sb.AppendLine(Localization.GetText("No filament spool"));

			if (hasPrinter && selectedPrinter.WaitingForFilament)
				sb.AppendLine(Localization.GetText("Replace filament spool"));
			if (printing)
				sb.AppendLine(Localization.GetText("Printing") + " " + Mathf.RoundToInt(selectedPrinter.Progress01 * 100f) + "%");
			else if (selectedPrinter.State == ThreeDPrinter.PrinterState.Error)
				sb.AppendLine(Localization.GetText("Error") + ": " + selectedPrinter.LastError);

			infoText.text = sb.ToString();
		}

		private static string FormatMass(float kilograms)
		{
			return kilograms < 1f
				? Mathf.RoundToInt(kilograms * 1000f) + " g"
				: kilograms.ToString("0.00") + " kg";
		}

		private static string FormatTime(float seconds)
		{
			int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
			return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
		}

		private void ShowHint(string key)
		{
			var os = system;
			if (os != null) os.ShowMessageBox(Localization.GetText("3D Printer"), Localization.GetText(key));
		}

		public void ResetAppState()
		{
			UnsubscribePrinter();
			selectedPrinter = null;
			previewModel = null;
			previewBaseValid = false;
			ClearPreviewClone();
			ReleasePreview();
			if (scaleSlider != null) scaleSlider.SetValueWithoutNotify(1f);
			if (infoText != null) infoText.text = Localization.GetText("No printer selected");
			if (importModelButton != null) importModelButton.interactable = false;
			if (startPrintButton != null) startPrintButton.interactable = false;
			if (scaleSlider != null) scaleSlider.interactable = false;
		}

		// ----- 3D preview -----------------------------------------------------

		private void UpdatePreview()
		{
			if (previewImage == null) return;
			var model = selectedPrinter != null
				? (selectedPrinter.CurrentModel != null ? selectedPrinter.CurrentModel : selectedPrinter.PendingModel)
				: null;
			if (model != previewModel)
			{
				previewModel = model;
				RebuildPreview(model);
			}
			if (previewModelPivot != null)
				previewModelPivot.Rotate(Vector3.up, previewRotationSpeed * Time.deltaTime, Space.World);
			if (previewCamera != null) previewCamera.Render();
		}

		private void EnsurePreviewRig()
		{
			if (previewCamera != null) return;
			previewRoot = new GameObject("3DPrinterPreviewRoot");
			previewRoot.transform.position = new Vector3(0f, -1000f, 0f);

			var pivot = new GameObject("PreviewModelPivot");
			pivot.transform.SetParent(previewRoot.transform, false);
			previewModelPivot = pivot.transform;

			previewPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
			previewPlate.name = "PreviewBuildPlate";
			Destroy(previewPlate.GetComponent<Collider>());
			previewPlate.transform.SetParent(previewRoot.transform, false);
			previewPlate.transform.localPosition = new Vector3(0f, -0.03f, 0f);
			previewPlate.transform.localScale = new Vector3(2.2f, 0.06f, 2.2f);
			previewPlate.layer = 11;
			var plateRenderer = previewPlate.GetComponent<Renderer>();
			if (plateRenderer != null)
			{
				var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") != null
					? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard"));
				mat.color = new Color(0.16f, 0.16f, 0.18f, 1f);
				plateRenderer.sharedMaterial = mat;
			}

			var camGo = new GameObject("3DPrinterPreviewCamera");
			camGo.transform.SetParent(previewRoot.transform, false);
			camGo.transform.localPosition = new Vector3(2.35f, 1.85f, -2.75f);
			camGo.transform.LookAt(previewRoot.transform.position + new Vector3(0f, 0.55f, 0f));
			previewCamera = camGo.AddComponent<Camera>();
			previewCamera.enabled = false;
			previewCamera.cullingMask = 1 << 11;
			previewCamera.clearFlags = CameraClearFlags.SolidColor;
			previewCamera.backgroundColor = previewBackground;
			previewCamera.fieldOfView = 38f;
			previewCamera.nearClipPlane = 0.01f;
			previewCamera.farClipPlane = 100f;

			var lightGo = new GameObject("3DPrinterPreviewLight");
			lightGo.transform.SetParent(previewRoot.transform, false);
			lightGo.transform.localRotation = Quaternion.Euler(50f, -35f, 0f);
			previewLight = lightGo.AddComponent<Light>();
			previewLight.type = LightType.Directional;
			previewLight.intensity = 1.15f;
			previewLight.cullingMask = 1 << 11;

			previewTexture = new RenderTexture(Mathf.Max(32, previewResolution.x), Mathf.Max(32, previewResolution.y), 24, RenderTextureFormat.ARGB32);
			previewCamera.targetTexture = previewTexture;
			previewImage.texture = previewTexture;
			previewImage.color = Color.white;
		}

		private void RebuildPreview(PrintableModel model)
		{
			ClearPreviewClone();
			previewBaseValid = false;
			if (model == null || model.Instance == null) return;
			EnsurePreviewRig();

			previewClone = Instantiate(model.Instance, previewModelPivot);
			previewClone.name = "PreviewModel";
			previewClone.SetActive(true);
			SetLayerRecursive(previewClone, 11);
			foreach (var mb in previewClone.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
			foreach (var col in previewClone.GetComponentsInChildren<Collider>(true)) Destroy(col);
			foreach (var rb in previewClone.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);

			var spool = selectedPrinter != null ? selectedPrinter.AttachedSpool : null;
			Color color = spool != null ? spool.FilamentColor : Color.white;
			float smoothness = spool != null ? spool.Smoothness : 0.6f;
			Color emission = spool != null ? spool.EmissionColor : Color.black;
			FilamentLook.Apply(previewClone.GetComponentsInChildren<Renderer>(true), color, smoothness, emission);

			previewBaseScale = previewClone.transform.localScale;
			previewBaseValid = GetBounds(previewClone).size.sqrMagnitude > 0.000001f;
			ApplyPreviewScale();
		}

		/// <summary>
		/// The printer's own build-volume BoxCollider defines the slider maximum.
		/// The app prefab does not need any collider reference: it is enough to
		/// assign Print Volume Collider on the printer prefab.
		/// </summary>
		private Vector3 GetPreviewMaxSize()
		{
			if (selectedPrinter != null)
			{
				Vector3 printerVolume = selectedPrinter.PrintVolumeSize;
				if (printerVolume.sqrMagnitude > 0.000001f) return printerVolume;
			}
			if (previewMaxBounds != null) return previewMaxBounds.size;
			return new Vector3(1.6f, 1.6f, 1.6f);
		}

		private void ApplyPreviewScale()
		{
			if (previewClone == null || !previewBaseValid || previewModelPivot == null) return;
			previewClone.transform.localScale = previewBaseScale;
			Bounds baseBounds = GetBounds(previewClone);
			if (baseBounds.size.sqrMagnitude <= 0.000001f) return;
			Vector3 maxSize = GetPreviewMaxSize();
			float sx = maxSize.x > 0.0001f ? maxSize.x / Mathf.Max(0.0001f, baseBounds.size.x) : 1f;
			float sy = maxSize.y > 0.0001f ? maxSize.y / Mathf.Max(0.0001f, baseBounds.size.y) : 1f;
			float sz = maxSize.z > 0.0001f ? maxSize.z / Mathf.Max(0.0001f, baseBounds.size.z) : 1f;
			float maxFit = Mathf.Max(0.0001f, Mathf.Min(sx, Mathf.Min(sy, sz)));
			float minFit = maxFit * Mathf.Clamp(minPreviewScaleFraction, 0.01f, 1f);
			float t = scaleSlider != null ? scaleSlider.value : 1f;
			float scale = Mathf.Lerp(minFit, maxFit, Mathf.Clamp01(t));
			previewClone.transform.localScale = previewBaseScale * scale;

			Bounds bounds = GetBounds(previewClone);
			Vector3 target = previewModelPivot.position;
			previewClone.transform.position += target - bounds.center;
			bounds = GetBounds(previewClone);
			previewClone.transform.position += Vector3.up * (target.y - bounds.min.y);
		}

		private static Bounds GetBounds(GameObject go)
		{
			var renderers = go.GetComponentsInChildren<Renderer>(true);
			Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
			bool has = false;
			for (int i = 0; i < renderers.Length; i++)
			{
				if (renderers[i] == null) continue;
				if (!has) { bounds = renderers[i].bounds; has = true; }
				else bounds.Encapsulate(renderers[i].bounds);
			}
			return bounds;
		}

		private static void SetLayerRecursive(GameObject go, int layer)
		{
			go.layer = layer;
			foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
		}

		private void ClearPreviewClone()
		{
			if (previewClone != null) Destroy(previewClone);
			previewClone = null;
			previewBaseValid = false;
		}

		private void ReleasePreview()
		{
			ClearPreviewClone();
			if (previewCamera != null) Destroy(previewCamera.gameObject);
			previewCamera = null;
			if (previewLight != null) Destroy(previewLight.gameObject);
			previewLight = null;
			if (previewPlate != null) Destroy(previewPlate);
			previewPlate = null;
			if (previewModelPivot != null) Destroy(previewModelPivot.gameObject);
			previewModelPivot = null;
			if (previewRoot != null) Destroy(previewRoot);
			previewRoot = null;
			if (previewTexture != null)
			{
				previewTexture.Release();
				Destroy(previewTexture);
			}
			previewTexture = null;
			if (previewImage != null) previewImage.texture = null;
		}

		public override void OnSystemStop()
		{
			ResetAppState();
		}

		public override void Close()
		{
			ResetAppState();
			base.Close();
		}

		private void OnDestroy()
		{
			UnsubscribePrinter();
			ReleasePreview();
		}
	}
}
