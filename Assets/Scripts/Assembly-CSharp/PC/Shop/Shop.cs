using System;
using UnityEngine;

namespace PC.Shop
{
	[CreateAssetMenu(menuName = "Shop/Page")]
	public class Shop : ScriptableObject
	{
		[Serializable]
		public class Page
		{
			public string pageName;

			public ShopItem[] item;
		}

		public Page[] pages;
	}
}