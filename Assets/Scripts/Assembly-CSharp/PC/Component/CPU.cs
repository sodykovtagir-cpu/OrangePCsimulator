using System.Collections;
using UnityEngine;

namespace PC.Component
{
	public class CPU : Hardware
	{
		[SerializeField]
		private LayerMask layer;

		[SerializeField]
		private Color burnedColor = new Color(50, 50, 50);

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
		private void Update()
        {
			if (delay >= 0.5f)
            {
				delay = 0f;
				bool cool = HasCooling(out cooler);
				cooling = cool;
            } else
            {
				delay += Time.deltaTime;
            }
        }

		private void FixedUpdate()
		{
			if (Power && !Damaged)
			{
				if (burnTemp <= temperature)
				{
					OverHeat();
				}
				else
				{
					temperature += Time.fixedDeltaTime * frequency * heat;
				}
			}

			if (!cooling && temperature != AirConditioner.temperature)
			{
				temperature -= (temperature - AirConditioner.temperature) / 10f * Time.fixedDeltaTime;
				return;
			}

			if (temperature != cooler.Temperature)
            {
                float dtemp = (temperature - cooler.Temperature) * 5f * Time.fixedDeltaTime;
				temperature -= dtemp;
				cooler.Temperature += dtemp;
            }
		}

		private bool HasCooling(out ICooler cooler)
		{
			var t = transform;

			var origin = t.position;
			var direction = t.up;
			var ray = new Ray(origin, direction);
			int mask = layer;

			if (Physics.Raycast(ray, out RaycastHit hit, 0.1f, mask))
			{
				if (hit.transform != null &&
					hit.transform.TryGetComponent(out cooler))
				{
					return true;
				}
			}

			cooler = null;
			return false;
		}

		public void OverHeat()
		{
			Damage();
		    var achievement = CloudOnceManager.Instance.GetAchievementFromId("too_hot");
		    achievement?.Unlock(null);
			var t = transform;
			var original = particle;

			var obj = Instantiate(original, t);
			Destroy(obj, 6f);
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
