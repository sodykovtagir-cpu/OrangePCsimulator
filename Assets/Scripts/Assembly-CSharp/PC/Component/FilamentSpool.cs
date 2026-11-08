using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PC.Component
{
	/// <summary>
	/// A replaceable filament spool. Capacity is measured in meters of filament.
	/// The printer consumes it gradually while printing. The spool can be mounted
	/// physically through the game's Slot/Connector system (remove mode or pull
	/// force), and its remaining amount is shown by shrinking the visual Y/Z
	/// diameter around the spool center.
	/// </summary>
	public class FilamentSpool : MonoBehaviour, ISave
	{
		[SerializeField] private float capacityMeters = 1f;
		[SerializeField] private float remainingMeters = 1f;

		[Header("Optional visual hooks")]
		[SerializeField] private Transform spoolVisual;
		[SerializeField] private Renderer[] colorRenderers;
		[SerializeField] private bool keepCentered = true;

		[Header("Filament color")]
		[SerializeField] private Color filamentColor = Color.white;

		[Header("Visual amount thresholds")]
		[Tooltip("Visual Y/Z scale at empty spool (core still visible).")]
		[SerializeField, Range(0.05f, 1f)] private float minVisualScale = 0.35f;
		[Tooltip("Visual Y/Z scale at full spool.")]
		[SerializeField, Range(0.05f, 1f)] private float maxVisualScale = 1f;

		[Header("Low filament warning")]
		[SerializeField] private float lowFilamentMeters = 0.1f;

		private ThreeDPrinter attachedPrinter;
		private Item item;
		private Vector3 initialVisualScale = Vector3.one;
		private Vector3 initialVisualPosition;
		private Quaternion initialVisualRotation = Quaternion.identity;
		private Vector3 visualCenterLocal;
		private bool visualCached;
		// Unity forbids creating MaterialPropertyBlock in a field initializer /
		// constructor. Create it lazily in Awake/ApplyColor instead.
		private MaterialPropertyBlock block;

		public float CapacityMeters => Mathf.Max(0.0001f, capacityMeters);
		public float RemainingMeters => Mathf.Clamp(remainingMeters, 0f, CapacityMeters);
		public float Fill01 => RemainingMeters / CapacityMeters;
		public bool IsEmpty => RemainingMeters <= 0.0001f;
		public bool IsLowFilament => RemainingMeters <= Mathf.Max(0f, lowFilamentMeters);
		public bool IsAttached => attachedPrinter != null;
		public Color FilamentColor => filamentColor;
		public float MinVisualScale => minVisualScale;
		public float MaxVisualScale => maxVisualScale;

		private void Awake()
		{
			if (block == null) block = new MaterialPropertyBlock();
			item = GetComponent<Item>();
			if (item != null)
			{
				item.SlotConnected += OnSlotConnected;
				item.SlotDisconnected += OnSlotDisconnected;
			}
			CacheVisual();
			ApplyColor();
			UpdateVisual();
		}

		private void OnDestroy()
		{
			if (item != null)
			{
				item.SlotConnected -= OnSlotConnected;
				item.SlotDisconnected -= OnSlotDisconnected;
			}
			if (attachedPrinter != null) attachedPrinter.OnSpoolPhysicallyDetached(this);
		}

		private void OnSlotConnected()
		{
			var printer = GetComponentInParent<ThreeDPrinter>();
			if (printer != null) printer.OnSpoolPhysicallyAttached(this);
		}

		private void OnSlotDisconnected()
		{
			if (attachedPrinter != null) attachedPrinter.OnSpoolPhysicallyDetached(this);
		}

		public void SetPrinter(ThreeDPrinter printer)
		{
			attachedPrinter = printer;
		}

		public void SetColor(Color color)
		{
			filamentColor = color;
			ApplyColor();
		}

		public bool TryConsume(float meters)
		{
			if (meters <= 0f) return true;
			if (meters > RemainingMeters + 0.00001f) return false;
			Consume(meters);
			return true;
		}

		public void Consume(float meters)
		{
			remainingMeters = Mathf.Max(0f, remainingMeters - Mathf.Max(0f, meters));
			UpdateVisual();
		}

		public void Refill()
		{
			remainingMeters = CapacityMeters;
			UpdateVisual();
		}

		public void SetRemaining(float meters)
		{
			remainingMeters = Mathf.Clamp(meters, 0f, CapacityMeters);
			UpdateVisual();
		}

		private void CacheVisual()
		{
			if (visualCached || spoolVisual == null) return;
			visualCached = true;
			initialVisualScale = spoolVisual.localScale;
			initialVisualPosition = spoolVisual.localPosition;
			initialVisualRotation = spoolVisual.localRotation;

			var filter = spoolVisual.GetComponentInChildren<MeshFilter>(true);
			if (filter != null && filter.sharedMesh != null)
			{
				visualCenterLocal = filter.sharedMesh.bounds.center;
			}
			else
			{
				var renderer = spoolVisual.GetComponentInChildren<Renderer>(true);
				visualCenterLocal = renderer != null
					? spoolVisual.InverseTransformPoint(renderer.bounds.center)
					: Vector3.zero;
			}
		}

		private void UpdateVisual()
		{
			if (spoolVisual == null) return;
			CacheVisual();
			float k = Mathf.Lerp(minVisualScale, maxVisualScale, Fill01);
			var scale = new Vector3(initialVisualScale.x, initialVisualScale.y * k, initialVisualScale.z * k);
			spoolVisual.localScale = scale;
			if (keepCentered)
			{
				// Keep the mesh bounds center fixed while Y/Z shrink.
				Vector3 delta = Vector3.Scale(visualCenterLocal, initialVisualScale - scale);
				spoolVisual.localPosition = initialVisualPosition + initialVisualRotation * delta;
			}
		}

		private void ApplyColor()
		{
			if (block == null) block = new MaterialPropertyBlock();
			Renderer[] targets = colorRenderers;
			if ((targets == null || targets.Length == 0) && spoolVisual != null)
				targets = spoolVisual.GetComponentsInChildren<Renderer>(true);
			if (targets == null) return;

			block.SetColor("_Color", filamentColor);
			block.SetColor("_BaseColor", filamentColor);
			for (int i = 0; i < targets.Length; i++)
				if (targets[i] != null) targets[i].SetPropertyBlock(block);
		}

		public void ToData(JObject jObject)
		{
			jObject["filamentCapacity"] = CapacityMeters;
			jObject["filamentRemaining"] = RemainingMeters;
			jObject["filamentColor"] = ColorUtility.ToHtmlStringRGB(filamentColor);
			jObject["filamentLowWarning"] = lowFilamentMeters;
		}

		public void FromData(JObject jObject)
		{
			if (jObject.TryGetValue("filamentCapacity", out var cap) && cap != null)
				capacityMeters = Mathf.Max(0.0001f, cap.Value<float>());
			if (jObject.TryGetValue("filamentRemaining", out var rem) && rem != null)
				remainingMeters = rem.Value<float>();
			if (jObject.TryGetValue("filamentLowWarning", out var low) && low != null)
				lowFilamentMeters = Mathf.Max(0f, low.Value<float>());
			if (jObject.TryGetValue("filamentColor", out var colorToken) && colorToken != null)
			{
				string hex = colorToken.Value<string>();
				if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex, out var parsed))
					filamentColor = parsed;
			}
			remainingMeters = Mathf.Clamp(remainingMeters, 0f, CapacityMeters);
			CacheVisual();
			ApplyColor();
			UpdateVisual();
		}
	}
}
