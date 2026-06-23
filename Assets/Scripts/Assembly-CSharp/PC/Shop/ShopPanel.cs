using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Shop
{
	public class ShopPanel : MonoBehaviour
	{
		private struct Page
		{
			public GameObject page;

			public ShopUI[] graphics;
		}

		[SerializeField]
		private Shop component;

		[SerializeField]
		private GameObject pagePrefab;

		[SerializeField]
		private GameObject menuPrefab;

		[SerializeField]
		private Transform pageParent;

		[SerializeField]
		private Transform menuParent;

		[SerializeField]
		private ShopUI selectionPrefab;

		[SerializeField]
		private ScrollRect scroll;

		private Page[] contents;

		private int selectedPage;

		private void Awake()
		{
			var m = Main.Instance;
			if (m != null) m.MoneyChanged += UpdatePage;

			if (component == null || component.pages == null) return;

			contents = new Page[component.pages.Length];

			for (int i = 0; i < component.pages.Length; i++)
			{
				var pageGo = Instantiate(pagePrefab, pageParent);
				contents[i].page = pageGo;

				var menuGo = Instantiate(menuPrefab, menuParent);
				var menuText = menuGo != null ? menuGo.GetComponentInChildren<Text>() : null;
				if (menuText != null) menuText.text = component.pages[i].pageName;

				var menuBtn = menuGo != null ? menuGo.GetComponent<Button>() : null;
				if (menuBtn != null)
				{
					int pageIndex = i;
					menuBtn.onClick.AddListener(() => DisplayPage(pageIndex));
				}

				var items = component.pages[i].item;
				if (items != null)
				{
					contents[i].graphics = new ShopUI[items.Length];
					var parent = pageGo != null ? pageGo.transform : null;
					for (int j = 0; j < items.Length; j++)
					{
						var ui = Instantiate(selectionPrefab, parent);
						ui.Init(items[j]);
						if (m != null) ui.OnBuy += m.Buy;
						contents[i].graphics[j] = ui;
					}
				}

				if (pageGo != null) pageGo.SetActive(false);
			}
		}

		private void OnEnable()
		{
			var m = Main.Instance;
			if (m == null) return;
			m.StopAllControl();
			UpdatePage();
			var ads = AdManager.Instance;
			if (ads != null) ads.HideBanner(true);
		}

		private void OnDisable()
		{
			var m = Main.Instance;
			if (m == null) return;
			m.ResumeAllControl();
			var ads = AdManager.Instance;
			if (ads != null) ads.HideBanner(false);
		}

		private void OnDestroy()
		{
			var m = Main.Instance;
			if (m == null) return;
			m.MoneyChanged -= UpdatePage;
		}

		private void DisplayPage(int index)
		{
			selectedPage = index;
			if (contents == null || contents.Length == 0) return;

			for (int i = 0; i < contents.Length; i++)
			{
				var go = contents[i].page;
				if (go == null) continue;

				bool active = i == index;
				go.SetActive(active);

				if (active && scroll != null)
				{
					var rt = go.transform as RectTransform;
					if (rt != null) scroll.content = rt;
					StartCoroutine(ScrollToTop(rt, scroll));
				}
			}

			UpdatePage();
		}

		private IEnumerator ScrollToTop(RectTransform content, ScrollRect scroll)
		{
			yield return new WaitForEndOfFrame();
			if (scroll != null) scroll.verticalNormalizedPosition = 1f;
		}

		private void UpdatePage()
		{
			if (contents == null) return;
			var page = selectedPage;
			if (page < 0 || page >= contents.Length) return;

			var uis = contents[page].graphics;
			if (uis == null) return;

			if (component == null || component.pages == null || page >= component.pages.Length) return;
			var items = component.pages[page].item;
			if (items == null) return;

			var m = Main.Instance;
			for (int i = 0; i < uis.Length && i < items.Length; i++)
			{
				var it = items[i];
				if (it == null) continue;
				if (!it.IsUnlocked()) continue;

				var ui = uis[i];
				if (ui == null || m == null) continue;
				ui.CanBuy(it.price <= m.Money);
			}
		}
	}
}
