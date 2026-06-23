using System;
using System.Collections.Generic;
using PC.Shop;
using UnityEngine;

namespace PC.Component.Software
{
	public class Market : App
	{
		[Serializable]
		private struct RandomData
		{
			public long lastTime;

			public List<int> randoms;
		}

		[SerializeField]
		private Transform page;

		[SerializeField]
		private ShopUI selectionPrefab;

		[SerializeField]
		private ShopUI randomSelectionPrefab;

		[SerializeField]
		private ShopItem[] items;

		[SerializeField]
		private ShopItem[] randomItems;

		[SerializeField]
		private float randomRate = 0.2f;

		private RandomData random;

		protected override void Start()
		{
			base.Start();

			var json = PlayerPrefs.GetString("DailyRandom");
			if (string.IsNullOrEmpty(json))
			{
				NewRandom();
			}
			else
			{
				random = JsonUtility.FromJson<RandomData>(json);
				var now = DateTime.Now;
				var next = DateTime.FromBinary(random.lastTime).AddDays(1);
				if (now >= next) NewRandom();
			}

			var list = randomItems;
			if (list != null && random.randoms != null)
			{
				for (int i = 0; i < list.Length; i++)
				{
					if (!random.randoms.Contains(i)) continue;
					var ui = Instantiate(randomSelectionPrefab, page);
					ui.Init(list[i]);
					ui.OnBuy += Main.Instance.Buy;
				}
			}

			var all = items;
			if (all != null)
			{
				for (int i = 0; i < all.Length; i++)
				{
					var ui = Instantiate(selectionPrefab, page);
					ui.Init(all[i]);
					ui.OnBuy += Main.Instance.Buy;
				}
			}
		}

		private void NewRandom()
		{
			var list = new List<int>();
			var items = randomItems;
			if (items != null)
			{
				for (int i = 0; i < items.Length; i++)
				{
					if (UnityEngine.Random.value < randomRate) list.Add(i);
				}
			}

			random.lastTime = DateTime.Now.ToBinary();
			random.randoms = list;

			var json = JsonUtility.ToJson(random);
			PlayerPrefs.SetString("DailyRandom", json);
		}
	}
}
