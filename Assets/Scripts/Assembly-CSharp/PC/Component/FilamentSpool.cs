using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PC.Component
{
	/// <summary>
	/// A replaceable filament spool. Capacity is measured in meters of filament.
	/// The printer consumes it gradually while printing; swapping is only allowed
	/// when the printer is idle (enforced by ThreeDPrinter).
	/// </summary>
	public class FilamentSpool : MonoBehaviour, ISave
	{
		[SerializeField] private float capacityMeters = 1f;
		[SerializeField] private float remainingMeters = 1f;

		[Header("Optional visual hooks")]
		[SerializeField] private Transform spoolVisual;
		[SerializeField] private float visualRotationPerMeter = 360f;

		private ThreeDPrinter attachedPrinter;

		public float CapacityMeters => Mathf.Max(0.0001f, capacityMeters);
		public float RemainingMeters => Mathf.Clamp(remainingMeters, 0f, CapacityMeters);
		public float Fill01 => RemainingMeters / CapacityMeters;
		public bool IsEmpty => RemainingMeters <= 0.0001f;
		public bool IsAttached => attachedPrinter != null;

		public void SetPrinter(ThreeDPrinter printer)
		{
			attachedPrinter = printer;
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

		private void UpdateVisual()
		{
			if (spoolVisual == null) return;
			float used = CapacityMeters - RemainingMeters;
			spoolVisual.localRotation = Quaternion.Euler(0f, 0f, -used * visualRotationPerMeter);
		}

		public void ToData(JObject jObject)
		{
			jObject["filamentCapacity"] = CapacityMeters;
			jObject["filamentRemaining"] = RemainingMeters;
		}

		public void FromData(JObject jObject)
		{
			if (jObject.TryGetValue("filamentCapacity", out var cap) && cap != null)
				capacityMeters = Mathf.Max(0.0001f, cap.Value<float>());
			if (jObject.TryGetValue("filamentRemaining", out var rem) && rem != null)
				remainingMeters = rem.Value<float>();
			remainingMeters = Mathf.Clamp(remainingMeters, 0f, CapacityMeters);
			UpdateVisual();
		}
	}
}
