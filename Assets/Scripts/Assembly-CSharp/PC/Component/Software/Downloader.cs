using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
	public class Downloader : App
	{
		private struct AppBlock
		{
			public Text text;

			public Image installed;

			public App app;
		}

		[SerializeField]
		private GameObject appPrefab;

		[SerializeField]
		private Transform appParent;

		[SerializeField]
		private Button installButton;

		[SerializeField]
		private Button uninstallButton;

		[SerializeField]
		private App[] apps;

		private int selected = -1;

		private AppBlock[] appBlocks;

		protected override void Start()
		{
			base.Start();

			if (apps == null) return;
			appBlocks = new AppBlock[apps.Length];

			for (int i = 0; i < apps.Length; i++)
			{
				var go = Instantiate(appPrefab, appParent);
				if (go == null) continue;

				var textComp = go.GetComponentInChildren<Text>();
				var child0 = go.transform != null && go.transform.childCount > 0 ? go.transform.GetChild(0) : null;
				var img = child0 != null ? child0.GetComponent<Image>() : null;

				var app = apps[i];
				var name = app != null ? app.AppName : null;
				if (textComp != null) textComp.text = Localization.GetText(name);

				int idx = i;
				var btn = textComp != null ? textComp.GetComponent<Button>() : null;
				if (btn != null) btn.onClick.AddListener(() => SelectText(idx));

				appBlocks[i] = new AppBlock { text = textComp, installed = img, app = app };
			}

			RefreshInstalled();
			SelectText(-1);
		}

		private void SelectText(int index)
		{
			var blocks = appBlocks;

			if (selected == index) index = -1;

			if (blocks != null)
			{
				for (int i = 0; i < blocks.Length; i++)
				{
					var t = blocks[i].text as UnityEngine.UI.Text;
					if (t != null) t.color = UnityEngine.Color.black;
				}
			}

			if (index == -1)
			{
				if (installButton != null) installButton.interactable = false;
				if (uninstallButton != null) uninstallButton.interactable = false;
				selected = index;
				return;
			}

			if (blocks == null || index < 0 || index >= blocks.Length) return;

			var selText = blocks[index].text as UnityEngine.UI.Text;
			if (selText != null) selText.color = UnityEngine.Color.red;

			var app = blocks[index].app;
			var os = system;
			bool installed = app != null && os != null && os.IsAppInstalled(app.AppName);

			if (installButton != null) installButton.interactable = !installed;
			if (uninstallButton != null) uninstallButton.interactable = installed;

			selected = index;
		}

		private void RefreshInstalled()
		{
			var blocks = appBlocks;
			var os = system;
			if (blocks == null || os == null) return;

			for (int i = 0; i < blocks.Length; i++)
			{
				var app = blocks[i].app;
				var beh = blocks[i].installed as UnityEngine.Behaviour;
				if (beh == null || app == null) continue;
				beh.enabled = os.IsAppInstalled(app.AppName);
			}
		}

		public void Install()
		{
			var blocks = appBlocks;
			if (blocks == null) return;
			var idx = selected;
			if (idx >= 0 && idx < blocks.Length) StartCoroutine(InstallProgress(blocks[idx].app));
		}

		private IEnumerator InstallProgress(App app)
		{
			var os = system;
			var pb = os != null ? os.ProgressBar : null;
			if (pb == null) yield break;

			var title = Localization.GetText("Downloading") + "...";
			var color = new Color(0.3f, 1f, 0.3f, 1f);
			pb.CallProgressBar(title, color, null);

			float t = 0.1f;
			while (t < 1f)
			{
				pb.SetProgress(t);
				yield return new WaitForSeconds(0.5f);
				t += 0.1f;
			}

			pb.SetProgress(1f);
			pb.CloseProgressBar();

			if (os != null) os.InstallApp(app);
			RefreshInstalled();
			SelectText(-1);
		}

		public void Uninstall()
		{
			var blocks = appBlocks;
			var idx = selected;
			if (blocks == null || idx < 0 || idx >= blocks.Length) return;
			var app = blocks[idx].app;
			if (app == null) return;
			StartCoroutine(UninstallProgress(app.AppName));
		}

		private IEnumerator UninstallProgress(string id)
		{
			var os = system;
			var pb = os != null ? os.ProgressBar : null;
			if (pb == null) yield break;

			var title = Localization.GetText("Uninstalling") + "...";
			var color = new Color(1f, 0.3f, 0.3f, 1f);
			pb.CallProgressBar(title, color, null);

			float t = 0f;
			while (t < 1f)
			{
				t += Time.deltaTime;
				pb.SetProgress(t);
				yield return null;
			}

			pb.SetProgress(1f);
			pb.CloseProgressBar();

			if (os != null) os.UninstallApp(id);
			RefreshInstalled();
			SelectText(-1);
		}

		public override void Close()
		{
			var os = system;
			var pb = os != null ? os.ProgressBar : null;
			if (pb != null) pb.CloseProgressBar();
			base.Close();
		}
	}
}
