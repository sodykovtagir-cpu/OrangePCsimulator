using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PC.Shop
{
	[CreateAssetMenu(menuName = "Shop/Item")]
	public class ShopItem : ScriptableObject
	{
		public string itemName;

		public int price;

		public float bitcoin;

		public Sprite sprite;

		public GameObject spawn;

		public bool large;

		public bool translateDescription = true;

		[TextArea(3, 5)]
		public string description;

		private static List<string> unlockedItem;

		public bool IsUnlocked()
		{
			if (bitcoin <= 0f)
				return true;

			if (unlockedItem == null)
			{
				var data = PlayerPrefs.GetString("Unlocked");
				if (!string.IsNullOrEmpty(data))
					unlockedItem = new List<string>(data.Split(','));
				else
					unlockedItem = new List<string>();
			}

			var itemName = name;
			return unlockedItem.Contains(itemName);
		}

		public void Unlock()
		{
			if (unlockedItem == null)
				unlockedItem = new List<string>();

			var itemName = name;
			unlockedItem.Add(itemName);
			PlayerPrefs.SetString("Unlocked", string.Join(",", unlockedItem));
		}
	}
}
