using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PC.Component
{
	public class ContainerCooler : Hardware, ICooler
	{
		[SerializeField]
		private float cooling;

		[SerializeField]
		private float temperature;

		[SerializeField]
		private float loss = 1;

		[SerializeField]
		private float amount;

		[SerializeField]
		private ParticleSystem particle;

		public float Temperature
		{
			get
			{
				return cooling;
			}
			set
            {
                cooling = value;
            }
		}

		private void OnTriggerStay(Collider other)
		{
			if (other == null) return;
			if (other.CompareTag("LiquidNitrogenTank")) loss = 1f;
		}

		private void Update()
		{
			if (loss <= 0f)
			{
				if (particle != null && particle.isEmitting) particle.Stop();
				return;
			}

			loss -= temperature * Time.deltaTime;

			var g = Physics.gravity;
			if (g.sqrMagnitude > 1e-10f)
			{
				var up = transform.up;
				var align = Mathf.Clamp01(-Vector3.Dot(up, g.normalized));
				loss = Mathf.Min(loss, align);
			}

			if (particle != null && !particle.isEmitting) particle.Play();
		}

		private void FixedUpdate()
		{
			var dt = Time.fixedDeltaTime;
			if (loss <= 0f)
			{
				var env = AirConditioner.temperature;
				cooling -= (cooling - env) * dt;
			}
			else
			{
				cooling -= (cooling + 196f) * dt;
			}
		}

		public override string GetInfo()
		{
			var info = base.GetInfo();
			var str = string.Format("{0:0}%", loss * 100f);
			return info + str;
		}

		public override void ToData(JObject jObject)
	    {
			if (jObject == null) return;
			jObject["amount"] = loss;
			base.ToData(jObject);
		}

		public override void FromData(JObject jObject)
		{
			if (jObject == null) return;
			var t = jObject["amount"];
			if (t != null) loss = t.Value<float>();
			base.FromData(jObject);
		}
	}
}
