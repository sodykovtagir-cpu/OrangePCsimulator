using UnityEngine;

namespace PC.Component
{
	public class Cooler : Hardware, ICooler
	{
		[SerializeField]
		private float cooling;

		[SerializeField]
		private float coolingWithoutPower;

		[SerializeField]
		private float temperature = 20f;

		[SerializeField]
		private float burnTemp = 150f;

		public float Temperature
		{
			get
			{
				return temperature;
			}
			set
            {
				temperature = value;
            }
		}

		private void FixedUpdate()
        {
			if (temperature != AirConditioner.temperature)
            {
                if (Power && !Damaged)
                {
                    temperature -= (temperature - AirConditioner.temperature) * Time.deltaTime * cooling;
                } else
                {
                    temperature -= (temperature - AirConditioner.temperature) * Time.deltaTime * coolingWithoutPower;
                }
            }

            if (temperature >= burnTemp)
            {
                Damage();
            }
        }
	}
}
