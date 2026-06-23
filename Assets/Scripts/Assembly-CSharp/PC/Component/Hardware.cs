using System;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PC.Component
{
	public class Hardware : Item, ISave
	{
		[SerializeField]
		private int capacity;

		[SerializeField]
		private int score;

		[SerializeField]
		private float wattage;

		private byte powerCount;

		private bool mainPower;

		private bool power;

		public bool Power
		{
			get
			{
				return power;
			}
			private set
            {
				power = value;
            }
		}

		public bool Damaged { get; private set; }

		public int Capacity
		{
			get
			{
				return capacity;
			}
			private set
            {
				capacity = value;
            }
		}

		public int Score
		{
			get
			{
				return score;
			}
			private set
            {
				score = value;
            }
		}

		public float Wattage
		{
			get
			{
				return wattage;
			}
			private set
            {
				wattage = value;
            }
		}

		public event Action<bool> PowerChanged;

		public event Action OnDamaged;

		public void Switch(bool on, bool counting = false)
		{
			if (counting)
			{
				if (on)
					powerCount += 1;
				else
					powerCount -= 1;
			}
			else
			{
				mainPower = on;
			}

			bool flag = (!mainPower && powerCount == 0) ? false : !Damaged;

			if (power != flag)
			{
				PowerChanged?.Invoke(flag);
				power = flag;
			}
		}

		public virtual void Damage()
        {
			OnDamaged?.Invoke();
			Damaged = true;
        }

		public override string GetInfo()
		{
			string baseStr = base.GetInfo();
			if (capacity != 0)
			{
				string tConv = Conversion.Size(capacity);
				baseStr = string.Concat(baseStr, "\n", tConv);
			}
			if (Damaged)
			{
				string brok = Localization.GetText("Broken");
				baseStr += "\n<color=red>" + brok + "</color>";
			}
			return baseStr;
		}

		public override void ToData(JObject jObject)
        {
			jObject["damaged"] = Damaged;
			base.ToData(jObject);
        }

		public override void FromData(JObject jObject)
        {
			Damaged = jObject["damaged"].ToObject<bool>();
			base.FromData(jObject);
        }
	}
}
