using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	/// <summary>
	/// Desktop app for the physical 3D printer: select a nearby printer, import a
	/// model, preview it, and start the print. Buttons/text/preview are wired in
	/// the 3DPrinter app prefab.
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

		[Header("Preview")]
		[SerializeField] private RawImage previewImage;
		[SerializeField] private int printerDeviceType = 4;
		[SerializeField] private Vector2Int previewResolution = new Vector2Int(256, 256);
		[SerializeField] private float previewRotationSpeed = 30f;
		[SerializeField] private Color previewBackground = new Color(0.10f, 0.10f, 0.12f, 1f);

		private ThreeDPrinter selectedPrinter;
		private GameObject previewRoot;
		private Camera previewCamera;
		private RenderTexture previewTexture;
		private GameObject previewClone;
		private PrintableModel previewModel;

		protected override void Start()
		{
			base.Start();
			Bind(selectPrinterButton, SelectPrinter);
			Bind(importModelButton, ImportModel);
			Bind(startPrintButton, StartPrint);
			if (selectPrinterText != null) selectPrinterText.text = Localization.GetText("Select Printer");
			if (importModelText != null) importModelText.text = Localization.GetText("Import Model");
			if (startPrintText != null) startPrintText.text = Localization.GetText("Start Print");
			RefreshUI();
		}

		private void Bind(Button button, UnityEngine.Events.UnityAction action)
		{
			if (button == null) return;
			// The prefab already wires persistent onClick calls. Only add a runtime
			// listener as a fallback, otherwise Import/Start would fire twice.
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
				picker.PickDevice(os, device =>
				{
					selectedPrinter = device as ThreeDPrinter;
					ClearPreviewClone();
					RefreshUI();
				}, printerDeviceType, true);
				return;
			}

			var printers = FindObjectsOfType<ThreeDPrinter>();
			if (printers != null && printers.Length > 0)
			{
				selectedPrinter = printers[0];
				ClearPreviewClone();
				RefreshUI();
			}
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

		public void StartPrint()
		{
			if (selectedPrinter == null)
			{
				ShowHint("No printer selected");
				return;
			}
			selectedPrinter.StartPrint();
			RefreshUI();
		}

		private void RefreshUI()
		{
			bool hasPrinter = selectedPrinter != null;
			bool printing = hasPrinter && selectedPrinter.IsPrinting;
			bool canImport = hasPrinter && !printing;
			bool canStart = hasPrinter && !printing && selectedPrinter.PendingModel != null && selectedPrinter.AttachedSpool != null;

			if (importModelButton != null) importModelButton.interactable = canImport;
			if (startPrintButton != null) startPrintButton.interactable = canStart;

			if (infoText == null) return;
			if (!hasPrinter)
			{
				infoText.text = Localization.GetText("No printer selected");
				return;
			}

			var sb = new System.Text.StringBuilder();
			sb.AppendLine(selectedPrinter.DeviceName);
			var model = selectedPrinter.CurrentModel != null ? selectedPrinter.CurrentModel : selectedPrinter.PendingModel;
			if (model != null)
			{
				sb.AppendLine(Localization.GetText("Model") + ": " + model.Name);
				sb.AppendLine(Localization.GetText("Triangles") + ": " + model.TriangleCount);
				float seconds = selectedPrinter.EstimatePrintSeconds(model.TriangleCount);
				sb.AppendLine(Localization.GetText("Time") + ": " + FormatTime(seconds));
				float needed = selectedPrinter.EstimateFilamentMeters(model);
				sb.AppendLine(Localization.GetText("Required filament") + ": " + needed.ToString("0.00") + " m");
			}
			else
			{
				sb.AppendLine(Localization.GetText("No model imported"));
			}

			var spool = selectedPrinter.AttachedSpool;
			if (spool != null)
			{
				sb.AppendLine(Localization.GetText("Filament") + ": " + spool.RemainingMeters.ToString("0.00") +
					" / " + spool.CapacityMeters.ToString("0.00") + " m");
				if (spool.IsLowFilament) sb.AppendLine(Localization.GetText("Low filament"));
			}
			else
			{
				sb.AppendLine(Localization.GetText("No filament spool"));
			}

			if (printing)
				sb.AppendLine(Localization.GetText("Printing") + " " + Mathf.RoundToInt(selectedPrinter.Progress01 * 100f) + "%");
			else if (selectedPrinter.State == ThreeDPrinter.PrinterState.Finished)
				sb.AppendLine(Localization.GetText("Finished"));
			else if (selectedPrinter.State == ThreeDPrinter.PrinterState.Error)
				sb.AppendLine(Localization.GetText("Error") + ": " + selectedPrinter.LastError);

			infoText.text = sb.ToString();
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

		// ----- Preview --------------------------------------------------------

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
			if (previewRoot != null)
				previewRoot.transform.Rotate(Vector3.up, previewRotationSpeed * Time.deltaTime, Space.World);
			if (previewCamera != null) previewCamera.Render();
		}

		private void EnsurePreviewRig()
		{
			if (previewCamera != null) return;
			previewRoot = new GameObject("3DPrinterPreviewRoot");
			previewRoot.transform.position = new Vector3(0f, -1000f, 0f);

			var camGo = new GameObject("3DPrinterPreviewCamera");
			camGo.transform.position = previewRoot.transform.position + new Vector3(0f, 0f, -5f);
			camGo.transform.rotation = Quaternion.identity;
			previewCamera = camGo.AddComponent<Camera>();
			previewCamera.enabled = false;
			previewCamera.cullingMask = 1 << 11; // Preview layer
			previewCamera.clearFlags = CameraClearFlags.SolidColor;
			previewCamera.backgroundColor = previewBackground;
			previewCamera.orthographic = true;
			previewCamera.orthographicSize = 1.5f;
			previewCamera.nearClipPlane = 0.01f;
			previewCamera.farClipPlane = 100f;

			previewTexture = new RenderTexture(Mathf.Max(32, previewResolution.x), Mathf.Max(32, previewResolution.y), 24, RenderTextureFormat.ARGB32);
			previewCamera.targetTexture = previewTexture;
			previewImage.texture = previewTexture;
			previewImage.color = Color.white;
		}

		private void RebuildPreview(PrintableModel model)
		{
			ClearPreviewClone();
			if (model == null || model.Instance == null) return;
			EnsurePreviewRig();

			previewClone = Instantiate(model.Instance, previewRoot.transform);
			previewClone.name = "PreviewModel";
			previewClone.SetActive(true);
			SetLayerRecursive(previewClone, 11);
			foreach (var mb in previewClone.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
			foreach (var col in previewClone.GetComponentsInChildren<Collider>(true)) Destroy(col);
			foreach (var rb in previewClone.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);

			Color color = selectedPrinter != null && selectedPrinter.AttachedSpool != null
				? selectedPrinter.AttachedSpool.FilamentColor : Color.white;
			var block = new MaterialPropertyBlock();
			block.SetColor("_Color", color);
			block.SetColor("_BaseColor", color);
			foreach (var r in previewClone.GetComponentsInChildren<Renderer>(true))
				if (r != null) r.SetPropertyBlock(block);

			Bounds bounds = GetBounds(previewClone);
			if (bounds.size.sqrMagnitude <= 0.000001f) return;
			previewClone.transform.position += previewRoot.transform.position - bounds.center;
			float max = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
			float scale = 2f / Mathf.Max(0.0001f, max);
			previewClone.transform.localScale *= scale;
			Bounds bounds2 = GetBounds(previewClone);
			previewClone.transform.position += previewRoot.transform.position - bounds2.center;
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
		}

		private void ReleasePreview()
		{
			ClearPreviewClone();
			if (previewCamera != null) Destroy(previewCamera.gameObject);
			previewCamera = null;
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

		public override void Close()
		{
			ReleasePreview();
			base.Close();
		}

		private void OnDestroy()
		{
			ReleasePreview();
		}
	}
}
