using System.Collections;
using UnityEngine;

namespace PC.Component
{
	public class CPU : Hardware
	{
		[SerializeField]
		private LayerMask layer;

		[SerializeField]
		private Color burnedColor = new Color(50f / 255f, 50f / 255f, 50f / 255f, 1f);

		public float defaultFrequency;

		public float frequency;

		[SerializeField]
		private float heat = 10;

		public float temperature = 20;

		public float burnTemp = 150;

		[SerializeField]
		private GameObject particle;

		private ICooler cooler;

		private bool cooling;

		private float delay;

		private const float CoolerSearchRadius = 0.28f;

		private void Update()
		{
			if (delay >= 0.25f)
			{
				delay = 0f;
				cooling = HasCooling(out cooler);
			}
			else
			{
				delay += Time.deltaTime;
			}
		}

		private void FixedUpdate()
		{
			float dt = Time.fixedDeltaTime;
			float ambient = AirConditioner.temperature;

			if (Power && !Damaged)
			{
				// Heat scales with clock vs stock, not raw GHz * 10 (that cooked even a Celeron).
				float stock = Mathf.Max(defaultFrequency, 0.5f);
				float load = Mathf.Clamp(frequency / stock, 0.25f, 4f);
				float heatPerSecond = heat * load * 1.35f;
				temperature += dt * heatPerSecond;
			}

			if (cooling && cooler != null)
			{
				float dtemp = (temperature - cooler.Temperature) * 8f * dt;
				temperature -= dtemp;
				cooler.Temperature += dtemp;
			}
			else
			{
				temperature -= (temperature - ambient) * 2.5f * dt;
			}

			if (Power && !Damaged && temperature >= burnTemp)
			{
				OverHeat();
			}
		}

		private bool HasCooling(out ICooler found)
		{
			found = null;
			var t = transform;
			var origin = t.position;

			if (TryGetCooler(t, out found))
				return true;

			var ray = new Ray(origin, t.up);
			int mask = layer.value != 0 ? layer.value : Physics.DefaultRaycastLayers;
			if (Physics.Raycast(ray, out RaycastHit hit, 0.35f, mask, QueryTriggerInteraction.Collide))
			{
				if (TryGetCooler(hit.transform, out found))
					return true;
			}

			var hits = Physics.OverlapSphere(origin, CoolerSearchRadius, mask, QueryTriggerInteraction.Collide);
			for (int i = 0; i < hits.Length; i++)
			{
				if (hits[i] == null) continue;
				if (hits[i].transform.IsChildOf(t)) continue;
				if (TryGetCooler(hits[i].transform, out found))
					return true;
			}

			return false;
		}

		private static bool TryGetCooler(Transform tr, out ICooler found)
		{
			found = null;
			if (tr == null) return false;
			if (tr.TryGetComponent(out found) && found != null)
				return true;
			found = tr.GetComponentInParent<ICooler>();
			if (found != null) return true;
			found = tr.GetComponentInChildren<ICooler>();
			return found != null;
		}

		public void OverHeat()
		{
			Damage();
			var achievement = CloudOnceManager.Instance.GetAchievementFromId("too_hot");
			achievement?.Unlock(null);
			var t = transform;
			var original = particle;
			if (original != null)
			{
				var obj = Instantiate(original, t);
				Destroy(obj, 6f);
			}
		}

		public override string GetInfo()
		{
			return frequency.ToString() + "GHZ\n" + base.GetInfo();
		}

		public override void Damage()
		{
			base.Damage();
			StartCoroutine(nameof(Render));
		}

		private IEnumerator Render()
		{
			var t = transform;
			var renderer = t.GetComponent<Renderer>();
			if (renderer == null) yield break;
			var mat = renderer.material;

			var from = mat.color;
			float f = 0f;

			while (f < 1f)
			{
				f += Time.deltaTime;
				float k = Mathf.Clamp01(f);

				var c = new Color(
					Mathf.Lerp(from.r, burnedColor.r, k),
					Mathf.Lerp(from.g, burnedColor.g, k),
					Mathf.Lerp(from.b, burnedColor.b, k),
					Mathf.Lerp(from.a, burnedColor.a, k)
				);

				mat.color = c;
				yield return null;
			}
		}
	}
}
