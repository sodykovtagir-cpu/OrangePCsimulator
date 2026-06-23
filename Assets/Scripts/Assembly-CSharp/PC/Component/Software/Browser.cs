using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class Browser : App
	{
		[Serializable]
		private class WebsiteItem
		{
			public string url;

			public Website page;

			public bool visible;
		}

		[SerializeField]
		private GameObject home;

		[SerializeField]
		private WebsiteItem[] websites;

		[SerializeField]
		private InputField addressBar;

		[SerializeField]
		private Transform quickAccessPrefab;

		[SerializeField]
		private Transform quickAccessParent;

		[SerializeField]
		private Transform content;

		[SerializeField]
		private GameObject loading;

		private Coroutine loadingCoroutine;

		private Website web;

		private Image background;

		protected override void Start()
		{
			base.Start();

			if (content != null) background = content.GetComponent<UnityEngine.UI.Image>();

			var ws = websites;
			if (ws == null) return;

			for (int i = 0; i < ws.Length; i++)
			{
				var w = ws[i];
				if (w == null || !w.visible) continue;

				var t = Instantiate(quickAccessPrefab, quickAccessParent);
				if (t == null) continue;

				var iconT = t.GetChild(0);
				if (iconT != null)
				{
					var img = iconT.GetComponent<Image>();
					if (img != null && w.page.icon != null) img.sprite = w.page.icon;
				}

				var textT = t.GetChild(1);
				if (textT != null)
				{
					var txt = textT.GetComponent<Text>();
					if (txt != null && w.page != null) txt.text = w.page.websiteName;
				}

				var btn = t.GetComponent<Button>();
				if (btn != null)
				{
					int idx = i;
					btn.onClick.AddListener(() => QuickAccess(idx));
				}
			}
		}

		public void Home()
		{
			if (addressBar != null) addressBar.text = "";
			if (home != null) home.SetActive(true);
			if (background != null) background.enabled = true;

			var l = loading;
			if (l != null)
			{
				if (l.activeSelf)
				{
					StopCoroutine(loadingCoroutine);
					l.SetActive(false);
				}

				var w = web;
				if (w != null)
				{
					var obj = w.gameObject;
					Destroy(obj);
					web = null;
				}
			}
		}

		private void QuickAccess(int index)
		{
			var ws = websites;
			if (ws == null || index < 0 || index >= ws.Length) return;
			var i = addressBar;
			if (i == null) return;
			var item = ws[index];
			if (item == null) return;
			i.text = item.url;
			Search();
		}

		public void Search()
		{
			var l = loading;
			if (l == null) return;
			if (l.activeSelf) StopCoroutine(loadingCoroutine);
			var routine = LoadingAnimation();
			loadingCoroutine = StartCoroutine(routine);
		}

		private IEnumerator LoadingAnimation()
		{
			if (home != null) home.SetActive(false);
			if (loading != null) loading.SetActive(true);
			if (background != null) background.enabled = true;

			if (web != null)
			{
				Destroy(web.gameObject);
				web = null;
			}

			var url = addressBar != null ? addressBar.text : null;
			yield return new WaitForSeconds(2f);

			var ws = websites;
			if (ws != null)
			{
				for (int i = 0; i < ws.Length; i++)
				{
					var item = ws[i];
					if (item == null) continue;
					if (item.url == url)
					{
						var instance = Instantiate(item.page, content);
						web = instance;
						if (web != null) web.Init(system);
						if (background != null) background.enabled = false;
						break;
					}
				}
			}

			if (loading != null) loading.SetActive(false);
		}
	}
}
